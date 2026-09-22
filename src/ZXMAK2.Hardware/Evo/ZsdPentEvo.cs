using System;

using System.IO;
using System.Xml;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Engine;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Mvvm;


namespace ZXMAK2.Hardware.Evo
{
    public class ZsdPentEvo : BusDeviceBase, IMediaStatusDevice
    {
        #region Fields

        private IMemoryDevice mem;
        private bool m_sandbox;
        private SdCard card;
        private byte buf;
        private bool card_cs;
        private readonly object cardSync = new object();
        private SdImageOpenCommand openImageCommand;
        private SdImageOpenCommand ejectImageCommand;
        private string configuredImageFileName = string.Empty;

        #endregion


        public ZsdPentEvo()
        {
            Category = BusDeviceCategory.Disk;
            Name = "SD PentEvo";
            Description = "PentEvo SD Card\r\nWritten by ZEK";
        }


        public bool SHADOW
        {
            get { return (mem == null) ? false : mem.SYSEN; }
        }


        #region IBusDevice

        public override void BusInit(IBusManager bmgr)
        {
            m_sandbox = bmgr.IsSandbox;
            mem = bmgr.FindDevice<IMemoryDevice>();
            openImageCommand = new SdImageOpenCommand(
                CommandUi_OnExecute,
                CommandUi_OnCanExecute,
                "Open SD Card image...",
                MediaCommandAction.Load);
            ejectImageCommand = new SdImageOpenCommand(
                EjectCommandUi_OnExecute,
                CommandUi_OnCanExecute,
                "Eject SD Card",
                MediaCommandAction.Eject);
            bmgr.AddCommandUi(openImageCommand);
            bmgr.AddCommandUi(ejectImageCommand);

            bmgr.Events.SubscribeReset(Reset);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0057, WrXX57);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x0057, RdXX57);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0077, WrXX77);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x0077, RdXX77);
        }

        public override void BusConnect()
        {
            lock (cardSync)
            {
                if (card != null)
                {
                    card.Close();
                }
                card = new SdCard();
                card_cs = false;
                buf = 0xFF;
            }
            // Machine Settings builds its editable profile in a sandbox bus.
            // Like IDE, that bus must retain only the media descriptor: opening
            // the same writable image there would contend with the live card.
            if (!m_sandbox)
            {
                RestoreConfiguredCard();
            }
        }

        public override void BusDisconnect()
        {
            lock (cardSync)
            {
                if (card != null)
                {
                    card.Close();
                    card = null;
                }
                card_cs = false;
                buf = 0xFF;
            }
        }

        #endregion


        protected override void OnConfigLoad(XmlNode itemNode)
        {
            base.OnConfigLoad(itemNode);
            configuredImageFileName = Utils.GetXmlAttributeAsString(
                itemNode, "sdImage", string.Empty);
        }

        protected override void OnConfigSave(XmlNode itemNode)
        {
            base.OnConfigSave(itemNode);
            Utils.SetXmlAttribute(
                itemNode, "sdImage", configuredImageFileName ?? string.Empty);
        }


        #region Bus handlers

        protected virtual void Reset()
        {
            lock (cardSync)
            {
                card_cs = false;
                if (card != null)
                {
                    card.Reset();
                }
                buf = 0xFF;
            }
        }

        protected virtual void WrXX57(ushort addr, byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            if (SHADOW)
            {
                if ((addr & 0x8000) != 0)
                {
                    lock (cardSync)
                    {
                        card_cs = ((val & 0x02) != 0);
                    }
                }
                else
                    CardWr(val);
            }
            else
                CardWr(val);
        }

        protected virtual void RdXX57(ushort addr, ref byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            // BaseConf zports.v: all xx57 reads return SPI data and clock FF.
            // In Shadow, A15 distinguishes configuration/data writes only.
            val = CardRd();
        }

        protected virtual void WrXX77(ushort addr, byte val, ref bool handled)
        {
            if (!handled && !SHADOW)
            {
                handled = true;
                lock (cardSync)
                {
                    card_cs = ((val & 0x02) != 0);
                }
            }
        }

        protected virtual void RdXX77(ushort addr, ref byte val, ref bool handled)
        {
            if (!handled && !SHADOW)
            {
                handled = true;
                val = 0;
            }
        }

        #endregion


        #region SD Card Emu

        protected void CardWr(byte val)
        {
            lock (cardSync)
            {
                if (card == null)
                {
                    return;
                }
                buf = card.Rd();
                card.Wr(val);
            }
        }

        protected byte CardRd()
        {
            lock (cardSync)
            {
                if (card == null)
                {
                    return 0xFF;
                }
                var tmp = buf;
                buf = card.Rd();
                card.Wr(0xff);
                return tmp;
            }
        }

        #endregion


        #region CommandUi

        private bool CommandUi_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            return viewResolver.CheckAvailable<IOpenFileDialog>();
        }

        private void CommandUi_OnExecute(Object arg)
        {
            if (!CommandUi_OnCanExecute(arg))
            {
                return;
            }
            try
            {
                var viewResolver = Locator.Resolve<IResolver>("View");
                var dlg = viewResolver.TryResolve<IOpenFileDialog>();
                if (dlg == null)
                {
                    return;
                }
                dlg.CheckFileExists = true;
                //dlg.CheckPathExists = true;
                //dlg.DefaultExt = "img|ima|vhd";
                dlg.Filter = "Disk image file (*.img, *.ima, *.vhd)|*.img;*.ima;*.vhd";
                dlg.Multiselect = false;
                if (dlg.ShowDialog(arg) != DlgResult.OK)
                {
                    return;
                }

                MountCard(dlg.FileName);
                openImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot open SD Card image!\n\n{0}", ex.Message);
            }
        }

        private void EjectCommandUi_OnExecute(Object arg)
        {
            try
            {
                EjectCard();
                ejectImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot eject SD Card!\n\n{0}", ex.Message);
            }
        }

        public MediaStatusKind MediaStatusKind
        {
            get { return MediaStatusKind.SecureDigital; }
        }

        public bool IsMediaMounted
        {
            get
            {
                lock (cardSync)
                {
                    return card != null &&
                        !string.IsNullOrEmpty(card.MountedFileName);
                }
            }
        }

        public string ConfiguredImageFileName
        {
            get { return configuredImageFileName ?? string.Empty; }
        }

        /// <summary>
        /// Updates the saved configuration on a detached settings bus. A live
        /// replacement must go through MountCard so the previous handle is
        /// closed before the new image is opened.
        /// </summary>
        public void ConfigureCard(string fileName)
        {
            configuredImageFileName = NormalizeImageFileName(fileName);
        }

        public void MountCard(string fileName)
        {
            var fullPath = NormalizeImageFileName(fileName);
            if (string.IsNullOrEmpty(fullPath))
            {
                EjectCard();
                return;
            }

            // Reproduce physical hot swap order: remove and close the old
            // card before opening the new image. Opening the replacement
            // first can collide with an image handle retained by an earlier
            // mount when cycling A -> B -> A.
            SdCard previous;
            string previousFileName;
            lock (cardSync)
            {
                previous = card;
                previousFileName = previous != null ? previous.MountedFileName : null;
                card = null;
                card_cs = false;
                buf = 0xFF;
            }
            if (previous != null)
            {
                previous.Close();
            }

            SdCard replacement = null;
            try
            {
                Logger.Info("SD hot swap: close '{0}', open '{1}'",
                    previousFileName ?? "<none>", fullPath);
                replacement = new SdCard();
                replacement.Open(fullPath);
                lock (cardSync)
                {
                    card = replacement;
                    replacement = null;
                    card_cs = false;
                    buf = 0xFF;
                    configuredImageFileName = fullPath;
                }
                Logger.Info("SD hot swap completed: '{0}'", fullPath);
            }
            catch
            {
                RestorePreviousCard(previousFileName);
                throw;
            }
            finally
            {
                if (replacement != null)
                {
                    replacement.Close();
                }
            }
        }

        public void EjectCard()
        {
            SdCard previous;
            lock (cardSync)
            {
                previous = card;
                card = new SdCard();
                card_cs = false;
                buf = 0xFF;
                configuredImageFileName = string.Empty;
            }
            if (previous != null)
            {
                previous.Close();
            }
            Logger.Info("SD card ejected");
        }

        private void RestoreConfiguredCard()
        {
            var configured = configuredImageFileName;
            if (string.IsNullOrEmpty(configured))
            {
                return;
            }
            try
            {
                MountCard(configured);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Cannot restore configured SD card: {0}", configured);
                EjectCard();
            }
        }

        private static string NormalizeImageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }
            var fullPath = Path.GetFullPath(fileName.Trim());
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("SD Card image not found", fullPath);
            }
            return fullPath;
        }

        private void RestorePreviousCard(string fileName)
        {
            SdCard restored = null;
            try
            {
                restored = new SdCard();
                if (!string.IsNullOrEmpty(fileName))
                {
                    restored.Open(fileName);
                }
                lock (cardSync)
                {
                    card = restored;
                    restored = null;
                    card_cs = false;
                    buf = 0xFF;
                }
                Logger.Info(
                    "SD hot swap rollback completed: '{0}'",
                    fileName ?? "<none>");
            }
            catch (Exception restoreError)
            {
                Logger.Error(restoreError);
                lock (cardSync)
                {
                    card = new SdCard();
                    card_cs = false;
                    buf = 0xFF;
                }
            }
            finally
            {
                if (restored != null)
                {
                    restored.Close();
                }
            }
        }

        #endregion CommandUi

        private sealed class SdImageOpenCommand : CommandDelegate, IMediaCommand
        {
            public SdImageOpenCommand(
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
                get { return MediaCommandKind.SecureDigital; }
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
    }
}
