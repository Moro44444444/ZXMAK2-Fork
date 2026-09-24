using System;

using ZXMAK2.Host.Interfaces;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Hardware.Evo;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    public partial class CtlSettingsGenericSound : ConfigScreenControl
    {
        private BusManager m_bmgr;
        private ISoundRenderer m_device;
        private BusDeviceBase m_busDevice;
        private bool m_isPentEvoSelector;


        public CtlSettingsGenericSound()
        {
            InitializeComponent();
        }

        public void Init(BusManager bmgr, IHostService host, ISoundRenderer device)
        {
            m_bmgr = bmgr;
            m_device = device;
            m_busDevice = (BusDeviceBase)device;
            txtDevice.Text = m_busDevice.Name;
            txtDescription.Text = m_busDevice.Description.Replace("\n", Environment.NewLine);

            int value = m_device.Volume;
            if (value < 0)
                value = 0;
            if (value > 100)
                value = 100;
            trkVolume.Value = value;
        }

        public void InitPentEvo(
            BusManager bmgr,
            IHostService host,
            BusDeviceBase device)
        {
            m_bmgr = bmgr;
            m_busDevice = device;
            m_device = device as ISoundRenderer;
            txtDevice.Text = device.Name;
            txtDescription.Text = device.Description.Replace(
                "\n",
                Environment.NewLine);
            var value = GetPentEvoVolume(device);
            if (value < trkVolume.Minimum)
                value = trkVolume.Minimum;
            if (value > trkVolume.Maximum)
                value = trkVolume.Maximum;
            trkVolume.Value = value;
            m_isPentEvoSelector = true;
            txtDevice.Visible = false;
            cbxPentEvoDevice.Items.Clear();
            cbxPentEvoDevice.Items.Add("AY/YM (AY8910-CHRV)");
            cbxPentEvoDevice.Items.Add("TurboSound FM Pro Rev. C");
            cbxPentEvoDevice.SelectedIndex =
                device is TurboSoundFmPro ? 1 : 0;
            cbxPentEvoDevice.Visible = true;
        }

        public override void Apply()
        {
            if (!m_isPentEvoSelector)
            {
                m_device.Volume = trkVolume.Value;
                return;
            }

            var useTurboSound = cbxPentEvoDevice.SelectedIndex == 1;
            var oldDevice = m_busDevice;
            if (useTurboSound == (oldDevice is TurboSoundFmPro))
            {
                SetPentEvoVolume(oldDevice, trkVolume.Value);
                SynchronizePentEvoSetting(useTurboSound);
                return;
            }

            BusDeviceBase newBusDevice = useTurboSound
                ? (BusDeviceBase)new TurboSoundFmPro()
                : new AYCHRV();
            var oldBusOrder = oldDevice.BusOrder;
            SetPentEvoVolume(newBusDevice, trkVolume.Value);
            m_bmgr.Remove(oldDevice);
            m_bmgr.Add(newBusDevice);
            // BusManager.Add assigns a temporary tail position. Restore the
            // replaced socket's position before normalizing the device list.
            newBusDevice.BusOrder = oldBusOrder;
            m_bmgr.Sort();
            m_busDevice = newBusDevice;
            m_device = newBusDevice as ISoundRenderer;
            SynchronizePentEvoSetting(useTurboSound);
        }

        private static int GetPentEvoVolume(BusDeviceBase device)
        {
            var turboSound = device as TurboSoundFmPro;
            if (turboSound != null)
                return turboSound.Volume;
            var renderer = device as ISoundRenderer;
            return renderer == null ? 100 : renderer.Volume;
        }

        private static void SetPentEvoVolume(
            BusDeviceBase device,
            int volume)
        {
            var turboSound = device as TurboSoundFmPro;
            if (turboSound != null)
            {
                turboSound.Volume = volume;
                return;
            }
            var renderer = device as ISoundRenderer;
            if (renderer != null)
                renderer.Volume = volume;
        }

        private void SynchronizePentEvoSetting(bool useTurboSound)
        {
            var ula = m_bmgr.FindDevice<UlaPentEvo>();
            if (ula != null)
            {
                ula.InternalSound = useTurboSound
                    ? PentEvoInternalSound.TurboSoundFmPro
                    : PentEvoInternalSound.AY8910CHRV;
            }
        }

        private void cbxPentEvoDevice_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (!m_isPentEvoSelector)
                return;
            txtDescription.Text = cbxPentEvoDevice.SelectedIndex == 1
                ? "NedoPC TurboSound FM Pro Rev. C" + Environment.NewLine +
                    "2 x YM2203 and SAA1099."
                : "AY8910 with #FE value on IRB input " +
                    "(required for PentEvo)";
        }
    }
}
