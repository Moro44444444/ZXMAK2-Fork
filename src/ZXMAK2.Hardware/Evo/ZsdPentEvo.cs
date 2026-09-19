using System;

using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Mvvm;


namespace ZXMAK2.Hardware.Evo
{
    public class ZsdPentEvo : BusDeviceBase
    {
        #region Fields

        private IMemoryDevice mem;
        private SdCard card;
        private byte buf;
        private bool card_cs;
        private readonly object cardSync = new object();
        private SdImageOpenCommand openImageCommand;

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
            mem = bmgr.FindDevice<IMemoryDevice>();
            openImageCommand = new SdImageOpenCommand(
                CommandUi_OnExecute,
                CommandUi_OnCanExecute,
                "Open SD Card image...");
            bmgr.AddCommandUi(openImageCommand);

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
            SdCard replacement = null;
            string previousFileName = null;
            bool previousDetached = false;
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

                // Reproduce physical hot swap order: remove and close the old
                // card before opening the new image.  Opening the replacement
                // first can collide with an image handle retained by an
                // earlier mount when cycling A -> B -> A.
                SdCard previous;
                lock (cardSync)
                {
                    previous = card;
                    previousFileName = previous != null ?
                        previous.MountedFileName : null;
                    card = null;
                    card_cs = false;
                    buf = 0xFF;
                    previousDetached = true;
                }
                Logger.Info(
                    "SD hot swap: close '{0}', open '{1}'",
                    previousFileName ?? "<none>",
                    dlg.FileName);
                if (previous != null)
                {
                    previous.Close();
                }

                replacement = new SdCard();
                replacement.Open(dlg.FileName);

                lock (cardSync)
                {
                    card = replacement;
                    replacement = null;
                    card_cs = false;
                    buf = 0xFF;
                }
                Logger.Info("SD hot swap completed: '{0}'", dlg.FileName);
                openImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                if (previousDetached)
                {
                    RestorePreviousCard(previousFileName);
                }
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot open SD Card image!\n\n{0}", ex.Message);
            }
            finally
            {
                if (replacement != null)
                {
                    replacement.Close();
                }
            }
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

        private sealed class SdImageOpenCommand : CommandDelegate, ISuccessCommand
        {
            public SdImageOpenCommand(
                Action<object> action,
                Func<object, bool> canExecute,
                string text)
                : base(action, canExecute, text)
            {
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
