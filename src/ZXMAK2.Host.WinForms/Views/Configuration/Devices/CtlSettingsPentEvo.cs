using System;
using System.Drawing;
using System.Windows.Forms;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    /// <summary>
    /// BaseConf motherboard settings. Internal devices are selected here so
    /// they cannot be accidentally combined through the generic device list.
    /// ZXBUS slots are physical positions on one shared ZXBUS, not independent
    /// buses; the slot number is retained for deterministic device order.
    /// </summary>
    public sealed class CtlSettingsPentEvo : ConfigScreenControl
    {
        private readonly ComboBox m_internalSound;
        private readonly TrackBar m_ayVolume;
        private readonly Label m_ayVolumeValue;
        private readonly CheckBox m_slot1Enabled;
        private readonly ComboBox m_slot1Device;
        private readonly CheckBox m_slot2Enabled;
        private readonly ComboBox m_slot2Device;

        private BusManager m_bmgr;
        private UlaPentEvo m_device;
        private bool m_updatingSlots;

        public event EventHandler NeoGsAvailabilityChanged;

        public bool IsNeoGsBoardEnabled
        {
            get
            {
                return IsNeoGsSelected(m_slot1Enabled, m_slot1Device) ||
                    IsNeoGsSelected(m_slot2Enabled, m_slot2Device);
            }
        }

        public CtlSettingsPentEvo()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            Size = new Size(284, 334);

            var motherboard = new GroupBox();
            motherboard.Text = "ZX Evolution BaseConf:";
            motherboard.Location = new Point(4, 4);
            motherboard.Size = new Size(276, 130);
            motherboard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(motherboard);

            var soundLabel = new Label();
            soundLabel.AutoSize = true;
            soundLabel.Location = new Point(10, 27);
            soundLabel.Text = "Internal sound:";
            motherboard.Controls.Add(soundLabel);

            m_internalSound = new ComboBox();
            m_internalSound.DropDownStyle = ComboBoxStyle.DropDownList;
            m_internalSound.Location = new Point(108, 22);
            m_internalSound.Size = new Size(158, 24);
            m_internalSound.Items.Add(new SoundChoice(
                PentEvoInternalSound.AY8910CHRV,
                "AY/YM (AY8910-CHRV)"));
            m_internalSound.Items.Add(new SoundChoice(
                PentEvoInternalSound.None,
                "None"));
            m_internalSound.Items.Add(new SoundChoice(
                PentEvoInternalSound.TurboSoundFmPro,
                "TSFM Pro Rev. C"));
            m_internalSound.SelectedIndexChanged += InternalSound_SelectedIndexChanged;
            motherboard.Controls.Add(m_internalSound);

            var volumeLabel = new Label();
            volumeLabel.AutoSize = true;
            volumeLabel.Location = new Point(10, 62);
            volumeLabel.Text = "Music volume:";
            motherboard.Controls.Add(volumeLabel);

            m_ayVolume = new TrackBar();
            m_ayVolume.AutoSize = false;
            m_ayVolume.Location = new Point(108, 54);
            m_ayVolume.Maximum = 100;
            m_ayVolume.TickFrequency = 10;
            m_ayVolume.Size = new Size(115, 35);
            m_ayVolume.ValueChanged += AyVolume_ValueChanged;
            motherboard.Controls.Add(m_ayVolume);

            m_ayVolumeValue = new Label();
            m_ayVolumeValue.AutoSize = true;
            m_ayVolumeValue.Location = new Point(228, 62);
            motherboard.Controls.Add(m_ayVolumeValue);

            var soundHint = new Label();
            soundHint.AutoSize = false;
            soundHint.Location = new Point(10, 92);
            soundHint.Size = new Size(256, 30);
            soundHint.Text = "One internal music device is active at a time. " +
                "Beeper and Covox are separate onboard outputs.";
            motherboard.Controls.Add(soundHint);

            var zxBus = new GroupBox();
            zxBus.Text = "ZXBUS expansion slots:";
            zxBus.Location = new Point(4, 140);
            zxBus.Size = new Size(276, 188);
            zxBus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(zxBus);

            m_slot1Enabled = CreateSlotCheckBox(zxBus, "Slot 1", 24);
            m_slot1Device = CreateSlotComboBox(zxBus, 86, 20);
            m_slot2Enabled = CreateSlotCheckBox(zxBus, "Slot 2", 62);
            m_slot2Device = CreateSlotComboBox(zxBus, 86, 58);
            m_slot1Enabled.CheckedChanged += Slot1SelectionChanged;
            m_slot1Device.SelectedIndexChanged += Slot1SelectionChanged;
            m_slot2Enabled.CheckedChanged += Slot2SelectionChanged;
            m_slot2Device.SelectedIndexChanged += Slot2SelectionChanged;

            var busHint = new Label();
            busHint.AutoSize = false;
            busHint.Location = new Point(10, 101);
            busHint.Size = new Size(256, 72);
            busHint.Text = "Both connectors share one ZXBUS. NeoGS Rev. C-VS " +
                "uses official firmware 1.11 and can occupy either slot.";
            zxBus.Controls.Add(busHint);

        }

        public void Init(BusManager bmgr, IHostService host, UlaPentEvo device)
        {
            m_bmgr = bmgr;
            m_device = device;

            var ay = m_bmgr.FindDevice<AYCHRV>();
            var tsfm = m_bmgr.FindDevice<TurboSoundFmPro>();
            var actualSound = tsfm != null
                ? PentEvoInternalSound.TurboSoundFmPro
                : ay != null
                    ? PentEvoInternalSound.AY8910CHRV
                    : PentEvoInternalSound.None;
            SelectSound(actualSound);

            m_ayVolume.Value = Math.Max(
                0,
                Math.Min(100,
                    tsfm != null ? tsfm.Volume : ay != null ? ay.Volume : 100));
            m_slot1Enabled.Checked = m_device.ZxBusSlot1Enabled;
            SelectSlotDevice(m_slot1Device, m_device.ZxBusSlot1Device);
            m_slot2Enabled.Checked = m_device.ZxBusSlot2Enabled;
            SelectSlotDevice(m_slot2Device, m_device.ZxBusSlot2Device);
            if (m_bmgr.FindDevice<NeoGsDevice>() != null &&
                !IsNeoGsSelected(m_slot1Enabled, m_slot1Device) &&
                !IsNeoGsSelected(m_slot2Enabled, m_slot2Device))
            {
                m_slot1Enabled.Checked = true;
                SelectSlotDevice(m_slot1Device, PentEvoZxBusDevice.NeoGS);
            }
            UpdateSoundControls();
            UpdateSlotControls();
        }

        public override void Apply()
        {
            var sound = ((SoundChoice)m_internalSound.SelectedItem).Value;
            var ay = m_bmgr.FindDevice<AYCHRV>();
            var tsfm = m_bmgr.FindDevice<TurboSoundFmPro>();
            if (sound == PentEvoInternalSound.AY8910CHRV)
            {
                if (tsfm != null)
                {
                    m_bmgr.Remove(tsfm);
                    tsfm = null;
                }
                if (ay == null)
                {
                    ay = new AYCHRV();
                    m_bmgr.Add(ay);
                }
                ay.Volume = m_ayVolume.Value;
            }
            else if (sound == PentEvoInternalSound.TurboSoundFmPro)
            {
                if (ay != null)
                {
                    m_bmgr.Remove(ay);
                    ay = null;
                }
                if (tsfm == null)
                {
                    tsfm = new TurboSoundFmPro();
                    m_bmgr.Add(tsfm);
                }
                tsfm.Volume = m_ayVolume.Value;
            }
            else
            {
                if (ay != null)
                    m_bmgr.Remove(ay);
                if (tsfm != null)
                    m_bmgr.Remove(tsfm);
            }

            m_device.InternalSound = sound;
            var slot1Device = GetSlotValue(m_slot1Device);
            var slot2Device = GetSlotValue(m_slot2Device);
            if (m_slot1Enabled.Checked && m_slot2Enabled.Checked &&
                slot1Device == PentEvoZxBusDevice.NeoGS &&
                slot2Device == PentEvoZxBusDevice.NeoGS)
            {
                m_slot2Enabled.Checked = false;
                slot2Device = PentEvoZxBusDevice.Empty;
                SelectSlotDevice(m_slot2Device, slot2Device);
            }

            var neoGsSelected =
                (m_slot1Enabled.Checked &&
                    slot1Device == PentEvoZxBusDevice.NeoGS) ||
                (m_slot2Enabled.Checked &&
                    slot2Device == PentEvoZxBusDevice.NeoGS);
            var neoGs = m_bmgr.FindDevice<NeoGsDevice>();
            if (neoGsSelected && neoGs == null)
            {
                neoGs = new NeoGsDevice();
                m_bmgr.Add(neoGs);
            }
            else if (!neoGsSelected && neoGs != null)
                m_bmgr.Remove(neoGs);
            m_device.ZxBusSlot1Enabled = m_slot1Enabled.Checked;
            m_device.ZxBusSlot1Device = slot1Device;
            m_device.ZxBusSlot2Enabled = m_slot2Enabled.Checked;
            m_device.ZxBusSlot2Device = slot2Device;
        }

        private static CheckBox CreateSlotCheckBox(
            Control parent,
            string text,
            int top)
        {
            var checkBox = new CheckBox();
            checkBox.AutoSize = true;
            checkBox.Location = new Point(10, top);
            checkBox.Text = text;
            parent.Controls.Add(checkBox);
            return checkBox;
        }

        private static ComboBox CreateSlotComboBox(
            Control parent,
            int left,
            int top)
        {
            var comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Location = new Point(left, top);
            comboBox.Size = new Size(180, 24);
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.Empty,
                "Empty"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.NeoGS,
                "NeoGS Rev. C-VS (ROM 1.11)"));
            comboBox.SelectedIndex = 0;
            parent.Controls.Add(comboBox);
            return comboBox;
        }

        private void SelectSound(PentEvoInternalSound value)
        {
            for (var i = 0; i < m_internalSound.Items.Count; i++)
            {
                if (((SoundChoice)m_internalSound.Items[i]).Value == value)
                {
                    m_internalSound.SelectedIndex = i;
                    return;
                }
            }
            m_internalSound.SelectedIndex = 0;
        }

        private static void SelectSlotDevice(
            ComboBox comboBox,
            PentEvoZxBusDevice value)
        {
            for (var i = 0; i < comboBox.Items.Count; i++)
            {
                if (((SlotChoice)comboBox.Items[i]).Value == value)
                {
                    comboBox.SelectedIndex = i;
                    return;
                }
            }
            comboBox.SelectedIndex = 0;
        }

        private void InternalSound_SelectedIndexChanged(
            object sender,
            EventArgs e)
        {
            UpdateSoundControls();
        }

        private void AyVolume_ValueChanged(object sender, EventArgs e)
        {
            m_ayVolumeValue.Text = m_ayVolume.Value + "%";
        }

        private void Slot1SelectionChanged(object sender, EventArgs e)
        {
            NormalizeSlots(1);
        }

        private void Slot2SelectionChanged(object sender, EventArgs e)
        {
            NormalizeSlots(2);
        }

        private void NormalizeSlots(int changedSlot)
        {
            if (m_updatingSlots)
                return;
            m_updatingSlots = true;
            try
            {
                if (IsNeoGsSelected(m_slot1Enabled, m_slot1Device) &&
                    IsNeoGsSelected(m_slot2Enabled, m_slot2Device))
                {
                    if (changedSlot == 1)
                    {
                        m_slot2Enabled.Checked = false;
                        SelectSlotDevice(
                            m_slot2Device,
                            PentEvoZxBusDevice.Empty);
                    }
                    else
                    {
                        m_slot1Enabled.Checked = false;
                        SelectSlotDevice(
                            m_slot1Device,
                            PentEvoZxBusDevice.Empty);
                    }
                }
                UpdateSlotControls();
            }
            finally
            {
                m_updatingSlots = false;
            }
        }

        private void UpdateSlotControls()
        {
            m_slot1Device.Enabled = m_slot1Enabled.Checked;
            m_slot2Device.Enabled = m_slot2Enabled.Checked;
            var handler = NeoGsAvailabilityChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private static bool IsNeoGsSelected(
            CheckBox enabled,
            ComboBox device)
        {
            return enabled.Checked &&
                GetSlotValue(device) == PentEvoZxBusDevice.NeoGS;
        }

        private static PentEvoZxBusDevice GetSlotValue(ComboBox comboBox)
        {
            var choice = comboBox.SelectedItem as SlotChoice;
            return choice != null
                ? choice.Value
                : PentEvoZxBusDevice.Empty;
        }

        private void UpdateSoundControls()
        {
            var choice = m_internalSound.SelectedItem as SoundChoice;
            var enabled = choice != null &&
                choice.Value != PentEvoInternalSound.None;
            m_ayVolume.Enabled = enabled;
            m_ayVolumeValue.Enabled = enabled;
            AyVolume_ValueChanged(this, EventArgs.Empty);
        }

        private sealed class SoundChoice
        {
            public SoundChoice(PentEvoInternalSound value, string text)
            {
                Value = value;
                Text = text;
            }

            public PentEvoInternalSound Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString() { return Text; }
        }

        private sealed class SlotChoice
        {
            public SlotChoice(PentEvoZxBusDevice value, string text)
            {
                Value = value;
                Text = text;
            }

            public PentEvoZxBusDevice Value { get; private set; }
            public string Text { get; private set; }
            public override string ToString() { return Text; }
        }
    }
}
