using System;
using System.IO;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Hardware.Circuits.Ata;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Dependency;
using ZXMAK2.Mvvm;
using ZXMAK2.Resources;


namespace ZXMAK2.Hardware.Evo
{
    public class IdePentEvo : BusDeviceBase, IMediaStatusDevice
    {
        #region Fields

        private bool m_sandbox = false;
        private CpuUnit m_cpu;
        private IconDescriptor m_iconHdd = new IconDescriptor("HDD", ResourceImages.OsdHddRd);
        private AtaPort m_ata = new AtaPort();
        private string m_ideFileName;
        private bool m_hasConfiguredImage;
        private int m_ide_write;
        private int m_ide_hi_byte_w;
        private int m_ide_hi_byte_w1;
        private int m_ide_hi_byte_r;
        private int m_ide_read;
        private IdeMediaCommand m_openImageCommand;
        private IdeMediaCommand m_ejectImageCommand;

        #endregion Fields


        public IdePentEvo()
        {
            Category = BusDeviceCategory.Disk;
            Name = "IDE PentEvo";
            Description = "PentEvo IDE controller";
        }


        #region IBusDevice Members

        public override void BusInit(IBusManager bmgr)
        {
            m_sandbox = bmgr.IsSandbox;
            m_cpu = bmgr.CPU;

            m_ideFileName = bmgr.GetSatelliteFileName("vmide");

            m_openImageCommand = new IdeMediaCommand(
                OpenImageCommand_OnExecute,
                MediaCommand_OnCanExecute,
                "Open HDD image...",
                MediaCommandAction.Load);
            m_ejectImageCommand = new IdeMediaCommand(
                EjectImageCommand_OnExecute,
                MediaCommand_OnCanExecute,
                "Eject HDD",
                MediaCommandAction.Eject);
            bmgr.AddCommandUi(m_openImageCommand);
            bmgr.AddCommandUi(m_ejectImageCommand);

            bmgr.RegisterIcon(m_iconHdd);
            bmgr.Events.SubscribeBeginFrame(BusBeginFrame);
            bmgr.Events.SubscribeEndFrame(BusEndFrame);

            bmgr.Events.SubscribeReset(BusReset);

            // BaseConf r1364: IS_NIDE_REGS = low[2:0]==0 and low[3]!=low[4].
            // C8 is part of that set, but selects the ATA alternate-status block.
            // Register it first so the generic x08 family leaves it handled.
            bmgr.Events.SubscribeRdIo(0xFF, 0xC8, ReadIdeAltStatus);
            bmgr.Events.SubscribeWrIo(0xFF, 0xC8, WriteIdeAltStatus);

            bmgr.Events.SubscribeRdIo(0xFF, 0x11, ReadIde);
            bmgr.Events.SubscribeWrIo(0xFF, 0x11, WriteIde);
            bmgr.Events.SubscribeRdIo(0x1F, 0x10, ReadIde);
            bmgr.Events.SubscribeWrIo(0x1F, 0x10, WriteIde);
            bmgr.Events.SubscribeRdIo(0x1F, 0x08, ReadIde);
            bmgr.Events.SubscribeWrIo(0x1F, 0x08, WriteIde);
        }

        public override void BusConnect()
        {
            if (!m_sandbox)
            {
                Load();
                m_ata.Open();
            }
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(m_ideFileName))
            {
                return;
            }
            if (!m_hasConfiguredImage && File.Exists(m_ideFileName))
            {
                m_ata.Devices[0].DeviceInfo.Load(m_ideFileName);
            }
            // Keep the legacy sidecar as an automatically maintained internal
            // descriptor.  The user never has to edit it by hand.
            m_ata.Devices[0].DeviceInfo.Save(m_ideFileName);
        }

        public override void BusDisconnect()
        {
            if (!m_sandbox)
                m_ata.Close();
        }

        protected override void OnConfigLoad(XmlNode itemNode)
        {
            base.OnConfigLoad(itemNode);
            LogIo = Utils.GetXmlAttributeAsBool(itemNode, "logIo", false);
            m_hasConfiguredImage = Utils.GetXmlAttributeAsBool(itemNode, "ideConfigured", false);
            if (m_hasConfiguredImage)
            {
                var image = Utils.GetXmlAttributeAsString(itemNode, "ideImage", string.Empty);
                var readOnly = Utils.GetXmlAttributeAsBool(itemNode, "ideReadOnly", false);
                var cylinders = Utils.GetXmlAttributeAsUInt32(itemNode, "ideCylinders", 20);
                var heads = Utils.GetXmlAttributeAsUInt32(itemNode, "ideHeads", 16);
                var sectors = Utils.GetXmlAttributeAsUInt32(itemNode, "ideSectors", 63);
                var lba = Utils.GetXmlAttributeAsUInt32(itemNode, "ideLba", 20160);
                m_ata.Devices[0].DeviceInfo.Configure(
                    image, readOnly, cylinders, heads, sectors, lba);
            }
        }

        protected override void OnConfigSave(XmlNode itemNode)
        {
            base.OnConfigSave(itemNode);
            Utils.SetXmlAttribute(itemNode, "logIo", LogIo);
            var info = HardDisk;
            Utils.SetXmlAttribute(itemNode, "ideConfigured", true);
            Utils.SetXmlAttribute(itemNode, "ideImage", info.FileName ?? string.Empty);
            Utils.SetXmlAttribute(itemNode, "ideReadOnly", info.ReadOnly);
            Utils.SetXmlAttribute(itemNode, "ideCylinders", info.Cylinders);
            Utils.SetXmlAttribute(itemNode, "ideHeads", info.Heads);
            Utils.SetXmlAttribute(itemNode, "ideSectors", info.Sectors);
            Utils.SetXmlAttribute(itemNode, "ideLba", info.Lba);
        }

        #endregion


        #region Properties

        public bool LogIo
        {
            get { return m_ata.LogIo; }
            set { m_ata.LogIo = value; }
        }

        public AtaDeviceInfo HardDisk
        {
            get { return m_ata.Devices[0].DeviceInfo; }
        }

        public void ConfigureHardDisk(string fileName, bool readOnly)
        {
            HardDisk.ConfigureImage(fileName, readOnly);
            m_hasConfiguredImage = true;
        }

        public void DisconnectHardDisk()
        {
            HardDisk.Disconnect();
            m_hasConfiguredImage = true;
        }

        public MediaStatusKind MediaStatusKind
        {
            get { return MediaStatusKind.HardDisk; }
        }

        public bool IsMediaMounted
        {
            get { return !string.IsNullOrEmpty(HardDisk.FileName); }
        }

        #endregion


        #region Private

        private bool MediaCommand_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            return viewResolver.CheckAvailable<IOpenFileDialog>();
        }

        private void OpenImageCommand_OnExecute(Object arg)
        {
            if (!MediaCommand_OnCanExecute(arg))
            {
                return;
            }
            try
            {
                var viewResolver = Locator.Resolve<IResolver>("View");
                var dialog = viewResolver.TryResolve<IOpenFileDialog>();
                if (dialog == null)
                {
                    return;
                }
                dialog.CheckFileExists = true;
                dialog.Filter = "HDD images (*.hdd, *.img, *.ima, *.vhd)|*.hdd;*.img;*.ima;*.vhd";
                dialog.Multiselect = false;
                if (dialog.ShowDialog(arg) != DlgResult.OK)
                {
                    return;
                }

                MountHardDisk(dialog.FileName, false);
                m_openImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot open HDD image!\n\n{0}", ex.Message);
            }
        }

        private void EjectImageCommand_OnExecute(Object arg)
        {
            try
            {
                EjectHardDisk();
                m_ejectImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot eject HDD!\n\n{0}", ex.Message);
            }
        }

        private void MountHardDisk(string fileName, bool readOnly)
        {
            var previous = HardDisk;
            var previousFileName = previous.FileName;
            var previousReadOnly = previous.ReadOnly;
            var previousCylinders = previous.Cylinders;
            var previousHeads = previous.Heads;
            var previousSectors = previous.Sectors;
            var previousLba = previous.Lba;

            m_ata.Close();
            try
            {
                ConfigureHardDisk(fileName, readOnly);
                m_ata.Open();
            }
            catch
            {
                HardDisk.Configure(
                    previousFileName,
                    previousReadOnly,
                    previousCylinders,
                    previousHeads,
                    previousSectors,
                    previousLba);
                m_ata.Open();
                throw;
            }
        }

        private void EjectHardDisk()
        {
            m_ata.Close();
            DisconnectHardDisk();
            m_ata.Open();
            Logger.Info("HDD ejected");
        }

        protected virtual void BusBeginFrame()
        {
            m_ata.LedIo = false;
        }

        protected virtual void BusEndFrame()
        {
            m_iconHdd.Visible = m_ata.LedIo;
        }

        protected virtual void BusReset()
        {
            // A hardware reset also resets the 8/16-bit Nemo IDE adapter
            // latches. Keeping a half-word phase across an image replacement
            // corrupts the first ATA command issued to the new disk.
            m_ide_write = 0;
            m_ide_hi_byte_w = 0;
            m_ide_hi_byte_w1 = 0;
            m_ide_hi_byte_r = 0;
            m_ide_read = 0;
            m_ata.Reset();
        }

        protected virtual void WriteIde(ushort addr, byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            if ((addr & 0xFF) == 0x11)
            {
                m_ide_write = value;
                m_ide_hi_byte_w = 0;
                m_ide_hi_byte_w1 = 1;
                return;
            }
            var data = value;
            if ((addr & 0xFE) == 0x10)
            {
                m_ide_hi_byte_w ^= 1;

                if (m_ide_hi_byte_w1 != 0) // Была запись в порт 0x11 (старший байт уже запомнен)
                {
                    m_ide_hi_byte_w1 = 0;
                }
                else
                {
                    if (m_ide_hi_byte_w != 0) // Запоминаем младший байт
                    {
                        m_ide_write = data;
                        return;
                    }
                    else // Меняем старший и младший байты местами (как этого ожидает write_hdd_5)
                    {
                        var tmp = (byte)m_ide_write;
                        m_ide_write = data;
                        data = tmp;
                    }
                }
            }
            else
            {
                m_ide_hi_byte_w = 0;
            }
            addr >>= 5;
            addr &= 7;
            if (addr != 0)
            {
                AtaWrite((AtaReg)addr, data);
            }
            else
            {
                var dataWord = (ushort)(data | (m_ide_write << 8));
                if (LogIo)
                {
                    Logger.Info("IDE WR DATA: #{0:X4} @ PC=#{1:X4}", dataWord, m_cpu.regs.PC);
                }
                m_ata.WriteData(dataWord);
            }
        }

        protected virtual void ReadIde(ushort addr, ref byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;
            
            if ((addr & 0xFF) == 0x11)
            {
                m_ide_hi_byte_r = 0;
                value = (byte)m_ide_read;
                return;
            }

            if ((addr & 0xFE) == 0x10)
            {
                m_ide_hi_byte_r ^= 1;
                if (m_ide_hi_byte_r==0)
                {
                    value = (byte)m_ide_read;
                    return;
                }
            }
            else
            {
                m_ide_hi_byte_r = 0;
            }
            addr >>= 5;
            addr &= 7;
            if (addr != 0)
            {
                value = AtaRead((AtaReg)addr);
                return;
            }
            var dataWord = m_ata.ReadData();
            if (LogIo)
            {
                Logger.Info("IDE RD DATA: #{0:X4} @ PC=#{1:X4}", dataWord, m_cpu.regs.PC);
            }
            m_ide_read = (dataWord >> 8) & 0xFF;
            value = (byte)dataWord;
        }

        protected virtual void WriteIdeAltStatus(ushort addr, byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            AtaWrite(AtaReg.ControlAltStatus, value);
        }

        protected virtual void ReadIdeAltStatus(ushort addr, ref byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            value = AtaRead(AtaReg.ControlAltStatus);
        }


        private void AtaWrite(AtaReg ataReg, byte value)
        {
            if (LogIo)
            {
                Logger.Info("IDE WR {0,-13}: #{1:X2} @ PC=#{2:X4}", ataReg, value, m_cpu.regs.PC);
            }
            m_ata.Write(ataReg, value);
        }

        private byte AtaRead(AtaReg ataReg)
        {
            var value = m_ata.Read(ataReg);
            if (LogIo)
            {
                Logger.Info("IDE RD {0,-13}: #{1:X2} @ PC=#{2:X4}", ataReg, value, m_cpu.regs.PC);
            }
            return value;
        }

        private sealed class IdeMediaCommand : CommandDelegate, IMediaCommand
        {
            public IdeMediaCommand(
                Action<object> action,
                Func<object, bool> canExecute,
                string text,
                MediaCommandAction mediaAction)
                : base(action, canExecute, text)
            {
                MediaAction = mediaAction;
            }

            public MediaCommandKind MediaKind
            {
                get { return MediaCommandKind.HardDisk; }
            }

            public MediaCommandAction MediaAction { get; private set; }

            public int DriveIndex
            {
                get { return -1; }
            }

            public event EventHandler ExecutedSuccessfully;

            public void NotifyExecutedSuccessfully()
            {
                var handler = ExecutedSuccessfully;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        //--
        // ??m_ata.reset();
        // ??value = m_ata.read_intrq() & 0x80
        #endregion Private
    }
}
