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
            var ula = m_bmgr.FindDevice<UlaPentEvo>();
            txtDevice.Text = "Internal music";
            var value = device != null
                ? GetPentEvoVolume(device)
                : ula != null ? ula.InternalSoundVolume : 100;
            if (value < trkVolume.Minimum)
                value = trkVolume.Minimum;
            if (value > trkVolume.Maximum)
                value = trkVolume.Maximum;
            trkVolume.Value = value;
            m_isPentEvoSelector = true;
            txtDevice.Visible = false;
            cbxPentEvoDevice.Items.Clear();
            cbxPentEvoDevice.Items.Add("None (ZXBUS music device)");
            cbxPentEvoDevice.Items.Add("AY/YM (AY8910-CHRV)");
            cbxPentEvoDevice.Items.Add("TurboSound FM Pro Rev. C");
            cbxPentEvoDevice.SelectedIndex = device == null
                ? 0
                : device is TurboSoundFmPro ? 2 : 1;
            cbxPentEvoDevice.Visible = true;
            UpdatePentEvoDescription();
        }

        public void SetMultiSoundYmOverride(bool enabled)
        {
            if (!m_isPentEvoSelector)
                return;
            if (enabled)
                cbxPentEvoDevice.SelectedIndex = 0;
            cbxPentEvoDevice.Enabled = !enabled;
            trkVolume.Enabled = !enabled;
            UpdatePentEvoDescription();
        }

        public override void Apply()
        {
            if (!m_isPentEvoSelector)
            {
                m_device.Volume = trkVolume.Value;
                return;
            }

            var selection = cbxPentEvoDevice.SelectedIndex;
            var oldDevice = m_busDevice;
            var selectedType = selection == 2
                ? typeof(TurboSoundFmPro)
                : selection == 1 ? typeof(AYCHRV) : null;
            if ((selectedType == null && oldDevice == null) ||
                (oldDevice != null && selectedType == oldDevice.GetType()))
            {
                if (oldDevice != null)
                    SetPentEvoVolume(oldDevice, trkVolume.Value);
                SynchronizePentEvoSetting(selection);
                return;
            }

            var oldBusOrder = oldDevice != null ? oldDevice.BusOrder : -1;
            if (oldDevice != null)
                m_bmgr.Remove(oldDevice);
            BusDeviceBase newBusDevice = selection == 2
                ? (BusDeviceBase)new TurboSoundFmPro()
                : selection == 1 ? (BusDeviceBase)new AYCHRV() : null;
            if (newBusDevice != null)
            {
                SetPentEvoVolume(newBusDevice, trkVolume.Value);
                m_bmgr.Add(newBusDevice);
                if (oldBusOrder >= 0)
                    newBusDevice.BusOrder = oldBusOrder;
                m_bmgr.Sort();
            }
            m_busDevice = newBusDevice;
            m_device = newBusDevice as ISoundRenderer;
            SynchronizePentEvoSetting(selection);
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

        private void SynchronizePentEvoSetting(int selection)
        {
            var ula = m_bmgr.FindDevice<UlaPentEvo>();
            if (ula != null)
            {
                ula.InternalSound = selection == 2
                    ? PentEvoInternalSound.TurboSoundFmPro
                    : selection == 1
                        ? PentEvoInternalSound.AY8910CHRV
                        : PentEvoInternalSound.None;
                ula.InternalSoundVolume = trkVolume.Value;
            }
        }

        private void cbxPentEvoDevice_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            if (!m_isPentEvoSelector)
                return;
            UpdatePentEvoDescription();
        }

        private void UpdatePentEvoDescription()
        {
            if (!m_isPentEvoSelector)
                return;
            if (!cbxPentEvoDevice.Enabled)
            {
                txtDescription.Text =
                    "Internal music is disabled automatically because " +
                    "ZX-MultiSound YM/TSFM is active on ZXBUS.";
            }
            else if (cbxPentEvoDevice.SelectedIndex == 2)
            {
                txtDescription.Text = "NedoPC TurboSound FM Pro Rev. C" +
                    Environment.NewLine + "2 x YM2203 and SAA1099.";
            }
            else if (cbxPentEvoDevice.SelectedIndex == 1)
            {
                txtDescription.Text = "AY8910 with #FE value on IRB input " +
                    "(required for PentEvo)";
            }
            else
            {
                txtDescription.Text =
                    "No internal music chip. Use this when a ZXBUS card " +
                    "provides AY/YM-compatible ports.";
            }
        }
    }
}
