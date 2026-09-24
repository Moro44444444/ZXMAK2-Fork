using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    /// <summary>
    /// Two equivalent physical ZXBUS connectors and the real MultiSound A2
    /// switch block. This page intentionally lives apart from ULA and Music.
    /// </summary>
    public sealed class CtlSettingsPentEvoZxBus : ConfigScreenControl
    {
        private readonly CheckBox m_slot1Enabled;
        private readonly ComboBox m_slot1Device;
        private readonly CheckBox m_slot2Enabled;
        private readonly ComboBox m_slot2Device;
        private readonly CheckBox m_automatic;
        private readonly CheckBox m_ym;
        private readonly CheckBox m_saa;
        private readonly CheckBox m_gs;
        private readonly CheckBox m_soundDrive;
        private readonly Label m_effective;
        private BusManager m_bmgr;
        private UlaPentEvo m_device;
        private bool m_updatingSlots;

        public event EventHandler ConfigurationChanged;

        public bool IsNeoGsBoardEnabled
        {
            get
            {
                return IsSelected(PentEvoZxBusDevice.NeoGS,
                        m_slot1Enabled, m_slot1Device) ||
                    IsSelected(PentEvoZxBusDevice.NeoGS,
                        m_slot2Enabled, m_slot2Device);
            }
        }

        public bool IsMultiSoundBoardEnabled
        {
            get
            {
                return IsSelected(PentEvoZxBusDevice.MultiSound,
                        m_slot1Enabled, m_slot1Device) ||
                    IsSelected(PentEvoZxBusDevice.MultiSound,
                        m_slot2Enabled, m_slot2Device);
            }
        }

        public bool IsAutomaticMultiSoundYmActive
        {
            get
            {
                return IsMultiSoundBoardEnabled &&
                    m_automatic.Checked && m_ym.Checked;
            }
        }

        public CtlSettingsPentEvoZxBus()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            Size = new Size(300, 410);

            var slots = new GroupBox();
            slots.Text = "ZXBUS expansion slots:";
            slots.Location = new Point(4, 4);
            slots.Size = new Size(292, 146);
            slots.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(slots);

            m_slot1Enabled = CreateSlotCheckBox(slots, "Slot 1 active", 28);
            m_slot1Device = CreateSlotComboBox(slots, 112, 24);
            m_slot2Enabled = CreateSlotCheckBox(slots, "Slot 2 active", 70);
            m_slot2Device = CreateSlotComboBox(slots, 112, 66);
            var slotHint = new Label();
            slotHint.AutoSize = false;
            slotHint.Location = new Point(10, 103);
            slotHint.Size = new Size(272, 34);
            slotHint.Text = "Both connectors are equivalent. One board of " +
                "each type may be installed.";
            slots.Controls.Add(slotHint);

            var switches = new GroupBox();
            switches.Text = "ZX-MultiSound Rev.A2 switches:";
            switches.Location = new Point(4, 156);
            switches.Size = new Size(292, 244);
            switches.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(switches);

            m_automatic = CreateSwitch(switches,
                "Automatic conflict protection", 24);
            m_ym = CreateSwitch(switches, "YM / TurboSound FM", 52);
            m_saa = CreateSwitch(switches, "SAA1099", 78);
            m_gs = CreateSwitch(switches, "General Sound 1.05b", 104);
            m_soundDrive = CreateSwitch(switches, "SounDrive", 130);
            m_effective = new Label();
            m_effective.AutoSize = false;
            m_effective.Location = new Point(10, 160);
            m_effective.Size = new Size(272, 72);
            switches.Controls.Add(m_effective);

            m_slot1Enabled.CheckedChanged += Slot1SelectionChanged;
            m_slot1Device.SelectedIndexChanged += Slot1SelectionChanged;
            m_slot2Enabled.CheckedChanged += Slot2SelectionChanged;
            m_slot2Device.SelectedIndexChanged += Slot2SelectionChanged;
            m_automatic.CheckedChanged += SwitchChanged;
            m_ym.CheckedChanged += SwitchChanged;
            m_saa.CheckedChanged += SwitchChanged;
            m_gs.CheckedChanged += SwitchChanged;
            m_soundDrive.CheckedChanged += SwitchChanged;
        }

        public void Initialize(BusManager bmgr, IHostService host,
            UlaPentEvo device)
        {
            m_bmgr = bmgr;
            m_device = device;
            m_updatingSlots = true;
            try
            {
                m_slot1Enabled.Checked = m_device.ZxBusSlot1Enabled;
                SelectSlotDevice(m_slot1Device, m_device.ZxBusSlot1Device);
                m_slot2Enabled.Checked = m_device.ZxBusSlot2Enabled;
                SelectSlotDevice(m_slot2Device, m_device.ZxBusSlot2Device);

                if (m_bmgr.FindDevice<NeoGsDevice>() != null &&
                    !IsNeoGsBoardEnabled)
                {
                    m_slot1Enabled.Checked = true;
                    SelectSlotDevice(m_slot1Device,
                        PentEvoZxBusDevice.NeoGS);
                }
                var multiSound = m_bmgr.FindDevice<ZxMultiSoundDevice>();
                if (multiSound != null && !IsMultiSoundBoardEnabled)
                {
                    if (!m_slot1Enabled.Checked)
                    {
                        m_slot1Enabled.Checked = true;
                        SelectSlotDevice(m_slot1Device,
                            PentEvoZxBusDevice.MultiSound);
                    }
                    else
                    {
                        m_slot2Enabled.Checked = true;
                        SelectSlotDevice(m_slot2Device,
                            PentEvoZxBusDevice.MultiSound);
                    }
                }
                m_automatic.Checked = multiSound == null ||
                    multiSound.AutomaticConfiguration;
                m_ym.Checked = multiSound == null || multiSound.YmEnabled;
                m_saa.Checked = multiSound == null || multiSound.SaaEnabled;
                m_gs.Checked = multiSound == null ||
                    multiSound.GeneralSoundEnabled;
                m_soundDrive.Checked = multiSound == null ||
                    multiSound.SoundDriveEnabled;
            }
            finally
            {
                m_updatingSlots = false;
            }
            UpdateControls();
        }

        public override void Apply()
        {
            m_device = m_bmgr.FindDevice<UlaPentEvo>();
            if (m_device == null)
            {
                RemoveBoard<NeoGsDevice>();
                RemoveBoard<ZxMultiSoundDevice>();
                return;
            }

            NormalizeDuplicate(0);
            var slot1Device = GetSlotValue(m_slot1Device);
            var slot2Device = GetSlotValue(m_slot2Device);
            var neoGsSelected = IsNeoGsBoardEnabled;
            var multiSoundSelected = IsMultiSoundBoardEnabled;

            // Validate manual switch wiring before changing the bus.  Apply
            // can be rejected safely without adding/removing a board or
            // altering the already accepted internal Music device.
            if (multiSoundSelected && !m_automatic.Checked)
            {
                if (m_gs.Checked && neoGsSelected)
                    throw new InvalidOperationException(
                        "ZX-MultiSound GS and NeoGS both decode ports #B3/#BB. " +
                        "Disable the MultiSound GS switch or use Automatic mode.");
                if (m_ym.Checked &&
                    (m_bmgr.FindDevice<AYCHRV>() != null ||
                     m_bmgr.FindDevice<TurboSoundFmPro>() != null))
                    throw new InvalidOperationException(
                        "ZX-MultiSound YM/TSFM conflicts with internal AY/TSFM. " +
                        "Select Music: None or use Automatic mode.");
            }

            SetBoardPresence<NeoGsDevice>(neoGsSelected);
            var multiSound = m_bmgr.FindDevice<ZxMultiSoundDevice>();
            if (multiSoundSelected && multiSound == null)
            {
                multiSound = new ZxMultiSoundDevice();
                m_bmgr.Add(multiSound);
            }
            else if (!multiSoundSelected && multiSound != null)
            {
                m_bmgr.Remove(multiSound);
                multiSound = null;
            }

            if (multiSound != null)
            {
                multiSound.AutomaticConfiguration = m_automatic.Checked;
                multiSound.YmEnabled = m_ym.Checked;
                multiSound.SaaEnabled = m_saa.Checked;
                multiSound.GeneralSoundEnabled = m_gs.Checked;
                multiSound.SoundDriveEnabled = m_soundDrive.Checked;

                if (multiSound.AutomaticConfiguration && multiSound.YmEnabled)
                {
                    foreach (var ay in m_bmgr.FindDevices<AYCHRV>().ToArray())
                        m_bmgr.Remove(ay);
                    foreach (var tsfm in m_bmgr
                        .FindDevices<TurboSoundFmPro>().ToArray())
                        m_bmgr.Remove(tsfm);
                    m_device.InternalSound = PentEvoInternalSound.None;
                }

                multiSound.ResolveConfiguration(neoGsSelected,
                    m_bmgr.FindDevice<AYCHRV>() != null ||
                    m_bmgr.FindDevice<TurboSoundFmPro>() != null);
            }

            m_device.ZxBusSlot1Enabled = m_slot1Enabled.Checked;
            m_device.ZxBusSlot1Device = slot1Device;
            m_device.ZxBusSlot2Enabled = m_slot2Enabled.Checked;
            m_device.ZxBusSlot2Device = slot2Device;
        }

        private static CheckBox CreateSlotCheckBox(Control parent,
            string text, int top)
        {
            var checkBox = new CheckBox();
            checkBox.AutoSize = true;
            checkBox.Location = new Point(10, top);
            checkBox.Text = text;
            parent.Controls.Add(checkBox);
            return checkBox;
        }

        private static CheckBox CreateSwitch(Control parent,
            string text, int top)
        {
            var checkBox = new CheckBox();
            checkBox.AutoSize = true;
            checkBox.Location = new Point(10, top);
            checkBox.Text = text;
            parent.Controls.Add(checkBox);
            return checkBox;
        }

        private static ComboBox CreateSlotComboBox(Control parent,
            int left, int top)
        {
            var comboBox = new ComboBox();
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Location = new Point(left, top);
            comboBox.Size = new Size(168, 24);
            comboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.Empty, "Empty"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.NeoGS, "NeoGS Rev. C-VS"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.MultiSound, "ZX-MultiSound Rev.A2"));
            comboBox.SelectedIndex = 0;
            parent.Controls.Add(comboBox);
            return comboBox;
        }

        private void Slot1SelectionChanged(object sender, EventArgs e)
        {
            NormalizeDuplicate(1);
        }

        private void Slot2SelectionChanged(object sender, EventArgs e)
        {
            NormalizeDuplicate(2);
        }

        private void SwitchChanged(object sender, EventArgs e)
        {
            UpdateControls();
        }

        private void NormalizeDuplicate(int changedSlot)
        {
            if (m_updatingSlots)
                return;
            m_updatingSlots = true;
            try
            {
                var first = m_slot1Enabled.Checked
                    ? GetSlotValue(m_slot1Device)
                    : PentEvoZxBusDevice.Empty;
                var second = m_slot2Enabled.Checked
                    ? GetSlotValue(m_slot2Device)
                    : PentEvoZxBusDevice.Empty;
                if (first != PentEvoZxBusDevice.Empty && first == second)
                {
                    if (changedSlot == 1)
                    {
                        m_slot2Enabled.Checked = false;
                        SelectSlotDevice(m_slot2Device,
                            PentEvoZxBusDevice.Empty);
                    }
                    else
                    {
                        m_slot1Enabled.Checked = false;
                        SelectSlotDevice(m_slot1Device,
                            PentEvoZxBusDevice.Empty);
                    }
                }
            }
            finally
            {
                m_updatingSlots = false;
            }
            UpdateControls();
        }

        private void UpdateControls()
        {
            m_slot1Device.Enabled = m_slot1Enabled.Checked;
            m_slot2Device.Enabled = m_slot2Enabled.Checked;
            var multiSound = IsMultiSoundBoardEnabled;
            m_automatic.Enabled = multiSound;
            m_ym.Enabled = multiSound;
            m_saa.Enabled = multiSound;
            m_gs.Enabled = multiSound;
            m_soundDrive.Enabled = multiSound;

            if (!multiSound)
            {
                m_effective.Text = "MultiSound is not installed.";
            }
            else if (m_automatic.Checked)
            {
                var gs = m_gs.Checked && !IsNeoGsBoardEnabled
                    ? "on"
                    : m_gs.Checked ? "off (NeoGS owns #B3/#BB)" : "off";
                m_effective.Text = "Effective: YM " + OnOff(m_ym.Checked) +
                    ", SAA " + OnOff(m_saa.Checked) + ", GS " + gs +
                    ", SounDrive " + OnOff(m_soundDrive.Checked) + ".";
            }
            else
            {
                m_effective.Text = "Manual: switch positions are applied " +
                    "literally. Conflicting ports are rejected on Apply.";
            }

            var handler = ConfigurationChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private static string OnOff(bool value)
        {
            return value ? "on" : "off";
        }

        private void SetBoardPresence<T>(bool selected)
            where T : ZXMAK2.Engine.Entities.BusDeviceBase, new()
        {
            var board = m_bmgr.FindDevice<T>();
            if (selected && board == null)
                m_bmgr.Add(new T());
            else if (!selected && board != null)
                m_bmgr.Remove(board);
        }

        private void RemoveBoard<T>()
            where T : ZXMAK2.Engine.Entities.BusDeviceBase
        {
            var board = m_bmgr.FindDevice<T>();
            if (board != null)
                m_bmgr.Remove(board);
        }

        private static bool IsSelected(PentEvoZxBusDevice value,
            CheckBox enabled, ComboBox device)
        {
            return enabled.Checked && GetSlotValue(device) == value;
        }

        private static PentEvoZxBusDevice GetSlotValue(ComboBox comboBox)
        {
            var choice = comboBox.SelectedItem as SlotChoice;
            return choice != null ? choice.Value : PentEvoZxBusDevice.Empty;
        }

        private static void SelectSlotDevice(ComboBox comboBox,
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
