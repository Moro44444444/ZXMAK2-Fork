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

        public CtlSettingsPentEvo()
        {
            AutoScaleMode = AutoScaleMode.Font;
            Size = new Size(584, 420);

            var motherboard = new GroupBox();
            motherboard.Text = "ZX Evolution BaseConf:";
            motherboard.Location = new Point(12, 10);
            motherboard.Size = new Size(548, 150);
            motherboard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(motherboard);

            var soundLabel = new Label();
            soundLabel.AutoSize = true;
            soundLabel.Location = new Point(18, 31);
            soundLabel.Text = "Internal sound:";
            motherboard.Controls.Add(soundLabel);

            m_internalSound = new ComboBox();
            m_internalSound.DropDownStyle = ComboBoxStyle.DropDownList;
            m_internalSound.Location = new Point(139, 27);
            m_internalSound.Size = new Size(265, 24);
            m_internalSound.Items.Add(new SoundChoice(
                PentEvoInternalSound.AY8910CHRV,
                "AY/YM (AY8910-CHRV)"));
            m_internalSound.Items.Add(new SoundChoice(
                PentEvoInternalSound.None,
                "None"));
            m_internalSound.SelectedIndexChanged += InternalSound_SelectedIndexChanged;
            motherboard.Controls.Add(m_internalSound);

            var volumeLabel = new Label();
            volumeLabel.AutoSize = true;
            volumeLabel.Location = new Point(18, 73);
            volumeLabel.Text = "AY/YM volume:";
            motherboard.Controls.Add(volumeLabel);

            m_ayVolume = new TrackBar();
            m_ayVolume.AutoSize = false;
            m_ayVolume.Location = new Point(139, 65);
            m_ayVolume.Maximum = 100;
            m_ayVolume.TickFrequency = 10;
            m_ayVolume.Size = new Size(265, 35);
            m_ayVolume.ValueChanged += AyVolume_ValueChanged;
            motherboard.Controls.Add(m_ayVolume);

            m_ayVolumeValue = new Label();
            m_ayVolumeValue.AutoSize = true;
            m_ayVolumeValue.Location = new Point(416, 73);
            motherboard.Controls.Add(m_ayVolumeValue);

            var soundHint = new Label();
            soundHint.AutoSize = false;
            soundHint.Location = new Point(18, 111);
            soundHint.Size = new Size(512, 28);
            soundHint.Text = "One internal music device is active at a time. " +
                "Beeper and Covox are separate onboard outputs.";
            motherboard.Controls.Add(soundHint);

            var zxBus = new GroupBox();
            zxBus.Text = "ZXBUS expansion slots:";
            zxBus.Location = new Point(12, 172);
            zxBus.Size = new Size(548, 184);
            zxBus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(zxBus);

            m_slot1Enabled = CreateSlotCheckBox(zxBus, "Slot 1 active", 24);
            m_slot1Device = CreateSlotComboBox(zxBus, 139, 20);
            m_slot2Enabled = CreateSlotCheckBox(zxBus, "Slot 2 active", 64);
            m_slot2Device = CreateSlotComboBox(zxBus, 139, 60);

            var busHint = new Label();
            busHint.AutoSize = false;
            busHint.Location = new Point(18, 106);
            busHint.Size = new Size(512, 62);
            busHint.Text = "The real ZX Evolution has two physical slots on the same " +
                "ZXBUS. Device choices will appear here as their emulation is " +
                "completed; currently both slots are empty.";
            zxBus.Controls.Add(busHint);
        }

        public void Init(BusManager bmgr, IHostService host, UlaPentEvo device)
        {
            m_bmgr = bmgr;
            m_device = device;

            var ay = m_bmgr.FindDevice<AYCHRV>();
            var actualSound = ay != null
                ? PentEvoInternalSound.AY8910CHRV
                : PentEvoInternalSound.None;
            SelectSound(actualSound);

            m_ayVolume.Value = Math.Max(
                0,
                Math.Min(100, ay != null ? ay.Volume : 100));
            m_slot1Enabled.Checked = m_device.ZxBusSlot1Enabled;
            SelectSlotDevice(m_slot1Device, m_device.ZxBusSlot1Device);
            m_slot2Enabled.Checked = m_device.ZxBusSlot2Enabled;
            SelectSlotDevice(m_slot2Device, m_device.ZxBusSlot2Device);
            UpdateSoundControls();
        }

        public override void Apply()
        {
            var sound = ((SoundChoice)m_internalSound.SelectedItem).Value;
            var ay = m_bmgr.FindDevice<AYCHRV>();
            if (sound == PentEvoInternalSound.AY8910CHRV)
            {
                if (ay == null)
                {
                    ay = new AYCHRV();
                    m_bmgr.Add(ay);
                }
                ay.Volume = m_ayVolume.Value;
            }
            else if (ay != null)
            {
                m_bmgr.Remove(ay);
            }

            m_device.InternalSound = sound;
            m_device.ZxBusSlot1Enabled = m_slot1Enabled.Checked;
            m_device.ZxBusSlot1Device =
                ((SlotChoice)m_slot1Device.SelectedItem).Value;
            m_device.ZxBusSlot2Enabled = m_slot2Enabled.Checked;
            m_device.ZxBusSlot2Device =
                ((SlotChoice)m_slot2Device.SelectedItem).Value;
        }

        private static CheckBox CreateSlotCheckBox(
            Control parent,
            string text,
            int top)
        {
            var checkBox = new CheckBox();
            checkBox.AutoSize = true;
            checkBox.Location = new Point(18, top);
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
            comboBox.Size = new Size(265, 24);
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.Empty,
                "Empty"));
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

        private void UpdateSoundControls()
        {
            var choice = m_internalSound.SelectedItem as SoundChoice;
            var enabled = choice != null &&
                choice.Value == PentEvoInternalSound.AY8910CHRV;
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
