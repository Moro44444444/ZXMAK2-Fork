using System;
using System.IO;
using System.Linq;
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
    public class IdePentEvo : BusDeviceBase, IMediaStatusDevice,
        IOpticalMediaStatus
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
        private IdeMediaCommand m_connectCdRomCommand;
        private IdeMediaCommand m_disconnectCdRomCommand;

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
                MediaCommandKind.HardDisk,
                MediaCommandAction.Load);
            m_ejectImageCommand = new IdeMediaCommand(
                EjectImageCommand_OnExecute,
                MediaCommand_OnCanExecute,
                "Eject HDD",
                MediaCommandKind.HardDisk,
                MediaCommandAction.Eject);
            m_connectCdRomCommand = new IdeMediaCommand(
                ConnectCdRomCommand_OnExecute,
                ConnectCdRomCommand_OnCanExecute,
                "Connect CD-ROM...",
                MediaCommandKind.OpticalDisc,
                MediaCommandAction.Load);
            m_disconnectCdRomCommand = new IdeMediaCommand(
                DisconnectCdRomCommand_OnExecute,
                DisconnectCdRomCommand_OnCanExecute,
                "Eject / Disconnect CD-ROM",
                MediaCommandKind.OpticalDisc,
                MediaCommandAction.Eject);
            bmgr.AddCommandUi(m_openImageCommand);
            bmgr.AddCommandUi(m_ejectImageCommand);
            bmgr.AddCommandUi(m_connectCdRomCommand);
            bmgr.AddCommandUi(m_disconnectCdRomCommand);

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

            // BusManager may reuse this device instance while applying a
            // changed machine profile.  Always clear the optional slave
            // first; otherwise an ejected CD/DVD from the previous profile
            // survives a reset because there is nothing below to overwrite it
            // when ideCdConfigured is false.
            m_ata.Devices[1].DeviceInfo.Disconnect();

            var hasConfiguredCdRom = Utils.GetXmlAttributeAsBool(itemNode, "ideCdConfigured", false);
            if (hasConfiguredCdRom)
            {
                var drive = Utils.GetXmlAttributeAsString(itemNode, "ideCdDrive", string.Empty);
                if (!string.IsNullOrEmpty(drive))
                {
                    try
                    {
                        m_ata.Devices[1].DeviceInfo.ConfigureCdromDrive(drive);
                    }
                    catch (Exception ex)
                    {
                        // A portable profile can legitimately be opened on a
                        // computer without the previously selected host drive.
                        // Leave the optional slave disconnected rather than
                        // aborting startup of the complete virtual machine.
                        Logger.Warn("IDE CD/DVD drive {0} is unavailable: {1}", drive, ex.Message);
                        m_ata.Devices[1].DeviceInfo.Disconnect();
                    }
                }
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
            var cdrom = CdRom;
            Utils.SetXmlAttribute(itemNode, "ideCdConfigured",
                cdrom.IsCdrom && !string.IsNullOrEmpty(cdrom.FileName));
            Utils.SetXmlAttribute(itemNode, "ideCdDrive", cdrom.FileName ?? string.Empty);
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

        /// <summary>
        /// The documented second device on BaseConf's single Nemo IDE channel.
        /// HDD remains the master; an optional Windows CD/DVD drive is slave.
        /// </summary>
        public AtaDeviceInfo CdRom
        {
            get { return m_ata.Devices[1].DeviceInfo; }
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

        public void ConfigureCdRom(string driveName)
        {
            CdRom.ConfigureCdromDrive(driveName);
        }

        public void DisconnectCdRom()
        {
            CdRom.Disconnect();
        }

        public bool IsOpticalDeviceConnected
        {
            get
            {
                return CdRom.IsCdrom &&
                    !string.IsNullOrEmpty(CdRom.FileName);
            }
        }

        public bool IsOpticalMediaReady
        {
            get
            {
                if (!IsOpticalDeviceConnected)
                    return false;
                try
                {
                    var drive = new DriveInfo(CdRom.FileName);
                    return drive.DriveType == DriveType.CDRom && drive.IsReady;
                }
                catch
                {
                    return false;
                }
            }
        }

        public string OpticalDriveName
        {
            get { return CdRom.FileName ?? string.Empty; }
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

        private bool ConnectCdRomCommand_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            return viewResolver.CheckAvailable<IUserQuery>() &&
                AtapiPasser.GetOpticalDrives().Any();
        }

        private bool DisconnectCdRomCommand_OnCanExecute(Object arg)
        {
            return IsOpticalDeviceConnected;
        }

        private void ConnectCdRomCommand_OnExecute(Object arg)
        {
            try
            {
                var drives = AtapiPasser.GetOpticalDrives().Cast<object>().ToArray();
                if (drives.Length == 0)
                {
                    Locator.Resolve<IUserMessage>().Warning(
                        "No Windows CD/DVD drive is available.");
                    return;
                }
                var query = Locator.Resolve<IResolver>("View").TryResolve<IUserQuery>();
                if (query == null)
                    return;
                var selected = drives.Length == 1
                    ? drives[0]
                    : query.ObjectSelector(drives, "Connect CD/DVD-ROM (IDE Slave)");
                var driveName = selected as string;
                if (string.IsNullOrEmpty(driveName))
                    return;

                m_ata.Close();
                try
                {
                    ConfigureCdRom(driveName);
                    m_ata.Open();
                }
                catch
                {
                    DisconnectCdRom();
                    m_ata.Open();
                    throw;
                }
                m_connectCdRomCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>().Error(
                    "Cannot connect CD/DVD-ROM!\n\n{0}", ex.Message);
            }
        }

        private void DisconnectCdRomCommand_OnExecute(Object arg)
        {
            try
            {
                m_ata.Close();
                DisconnectCdRom();
                m_ata.Open();
                m_disconnectCdRomCommand.NotifyExecutedSuccessfully();
                Logger.Info("CD/DVD-ROM ejected and IDE Slave disconnected");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>().Error(
                    "Cannot disconnect CD/DVD-ROM!\n\n{0}", ex.Message);
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
                MediaCommandKind mediaKind,
                MediaCommandAction mediaAction)
                : base(action, canExecute, text)
            {
                MediaKind = mediaKind;
                MediaAction = mediaAction;
            }

            public MediaCommandKind MediaKind
            {
                get;
                private set;
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
