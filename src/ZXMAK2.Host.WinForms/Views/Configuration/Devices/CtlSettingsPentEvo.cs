using System;
using System.Drawing;
using System.IO;
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
        private readonly GroupBox m_neoGsMedia;
        private readonly CheckBox m_neoGsSdConnected;
        private readonly TextBox m_neoGsSdPath;
        private readonly Button m_neoGsSdBrowse;
        private readonly Button m_neoGsSdEject;
        private readonly Label m_neoGsSdStatus;

        private BusManager m_bmgr;
        private UlaPentEvo m_device;
        private bool m_updatingSlots;

        public CtlSettingsPentEvo()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            Size = new Size(284, 470);

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
            m_internalSound.SelectedIndexChanged += InternalSound_SelectedIndexChanged;
            motherboard.Controls.Add(m_internalSound);

            var volumeLabel = new Label();
            volumeLabel.AutoSize = true;
            volumeLabel.Location = new Point(10, 62);
            volumeLabel.Text = "AY/YM volume:";
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

            m_neoGsMedia = new GroupBox();
            m_neoGsMedia.Text = "NeoGS Rev. C-VS microSD:";
            m_neoGsMedia.Location = new Point(4, 334);
            m_neoGsMedia.Size = new Size(276, 128);
            m_neoGsMedia.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(m_neoGsMedia);

            m_neoGsSdConnected = new CheckBox();
            m_neoGsSdConnected.Text = "Card connected";
            m_neoGsSdConnected.AutoSize = true;
            m_neoGsSdConnected.Location = new Point(10, 23);
            m_neoGsSdConnected.CheckedChanged += NeoGsSdConnectedChanged;
            m_neoGsMedia.Controls.Add(m_neoGsSdConnected);

            m_neoGsSdPath = new TextBox();
            m_neoGsSdPath.Location = new Point(10, 49);
            m_neoGsSdPath.Size = new Size(205, 20);
            m_neoGsSdPath.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            m_neoGsSdPath.TextChanged += NeoGsSdPathChanged;
            m_neoGsMedia.Controls.Add(m_neoGsSdPath);

            m_neoGsSdBrowse = new Button();
            m_neoGsSdBrowse.Text = "...";
            m_neoGsSdBrowse.Location = new Point(221, 47);
            m_neoGsSdBrowse.Size = new Size(45, 23);
            m_neoGsSdBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_neoGsSdBrowse.Click += NeoGsSdBrowseClick;
            m_neoGsMedia.Controls.Add(m_neoGsSdBrowse);

            m_neoGsSdStatus = new Label();
            m_neoGsSdStatus.AutoSize = false;
            m_neoGsSdStatus.Location = new Point(10, 82);
            m_neoGsSdStatus.Size = new Size(177, 34);
            m_neoGsSdStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            m_neoGsMedia.Controls.Add(m_neoGsSdStatus);

            m_neoGsSdEject = new Button();
            m_neoGsSdEject.Text = "Eject";
            m_neoGsSdEject.Location = new Point(191, 82);
            m_neoGsSdEject.Size = new Size(75, 23);
            m_neoGsSdEject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_neoGsSdEject.Click += NeoGsSdEjectClick;
            m_neoGsMedia.Controls.Add(m_neoGsSdEject);
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
            if (m_bmgr.FindDevice<NeoGsDevice>() != null &&
                !IsNeoGsSelected(m_slot1Enabled, m_slot1Device) &&
                !IsNeoGsSelected(m_slot2Enabled, m_slot2Device))
            {
                m_slot1Enabled.Checked = true;
                SelectSlotDevice(m_slot1Device, PentEvoZxBusDevice.NeoGS);
            }
            var neoGs = m_bmgr.FindDevice<NeoGsDevice>();
            SetNeoGsSdPath(
                neoGs == null ? string.Empty : neoGs.ConfiguredSdImageFileName);
            UpdateSoundControls();
            UpdateSlotControls();
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
            if (neoGsSelected && neoGs != null)
            {
                if (m_neoGsSdConnected.Checked &&
                    string.IsNullOrWhiteSpace(m_neoGsSdPath.Text))
                    throw new InvalidOperationException(
                        "Select a NeoGS microSD image or eject the card");
                neoGs.ConfigureSdCard(m_neoGsSdConnected.Checked
                    ? m_neoGsSdPath.Text.Trim()
                    : string.Empty);
            }

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
            var enabled = IsNeoGsSelected(m_slot1Enabled, m_slot1Device) ||
                IsNeoGsSelected(m_slot2Enabled, m_slot2Device);
            m_neoGsMedia.Enabled = enabled;
            UpdateNeoGsSdControls();
        }

        private void SetNeoGsSdPath(string fileName)
        {
            m_neoGsSdPath.Text = fileName ?? string.Empty;
            m_neoGsSdPath.SelectionStart = m_neoGsSdPath.Text.Length;
            m_neoGsSdConnected.Checked =
                !string.IsNullOrEmpty(m_neoGsSdPath.Text);
            UpdateNeoGsSdControls();
        }

        private void NeoGsSdBrowseClick(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select NeoGS microSD image";
                dialog.Filter =
                    "Disk image file (*.img, *.ima, *.vhd)|*.img;*.ima;*.vhd";
                dialog.DefaultExt = "img";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(m_neoGsSdPath.Text))
                    dialog.FileName = m_neoGsSdPath.Text;
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                SetNeoGsSdPath(dialog.FileName);
            }
        }

        private void NeoGsSdEjectClick(object sender, EventArgs e)
        {
            SetNeoGsSdPath(string.Empty);
        }

        private void NeoGsSdConnectedChanged(object sender, EventArgs e)
        {
            UpdateNeoGsSdControls();
        }

        private void NeoGsSdPathChanged(object sender, EventArgs e)
        {
            UpdateNeoGsSdControls();
        }

        private void UpdateNeoGsSdControls()
        {
            var connected = m_neoGsMedia.Enabled &&
                m_neoGsSdConnected.Checked;
            m_neoGsSdPath.Enabled = connected;
            m_neoGsSdBrowse.Enabled = m_neoGsMedia.Enabled;
            m_neoGsSdEject.Enabled = m_neoGsMedia.Enabled &&
                (m_neoGsSdConnected.Checked ||
                    !string.IsNullOrEmpty(m_neoGsSdPath.Text));
            if (!m_neoGsSdConnected.Checked ||
                string.IsNullOrWhiteSpace(m_neoGsSdPath.Text))
            {
                m_neoGsSdStatus.Text = "No card image selected";
                return;
            }
            try
            {
                var fullPath = Path.GetFullPath(m_neoGsSdPath.Text.Trim());
                m_neoGsSdStatus.Text = File.Exists(fullPath)
                    ? "Selected: " + Path.GetFileName(fullPath)
                    : "Selected image is not available";
            }
            catch
            {
                m_neoGsSdStatus.Text = "Selected image path is invalid";
            }
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
