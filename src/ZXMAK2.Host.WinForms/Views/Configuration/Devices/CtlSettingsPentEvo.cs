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
        private readonly CheckBox m_tsFmSaaCompatibility;
        private readonly Label m_effective;
        private readonly GroupBox m_multiSoundSwitches;
        private readonly GroupBox m_maxSwitches;
        private readonly CheckBox m_maxAutomatic;
        private readonly CheckBox m_maxYm;
        private readonly CheckBox m_maxSaa;
        private readonly CheckBox m_maxGs;
        private readonly CheckBox m_maxSoundDrive;
        private readonly CheckBox m_maxTsFmSaaCompatibility;
        private readonly Label m_maxEffective;
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

        public bool IsMoonSoundBoardEnabled
        {
            get
            {
                return IsSelected(PentEvoZxBusDevice.MoonSound,
                        m_slot1Enabled, m_slot1Device) ||
                    IsSelected(PentEvoZxBusDevice.MoonSound,
                        m_slot2Enabled, m_slot2Device);
            }
        }

        public bool IsMultiSoundMaxBoardEnabled
        {
            get
            {
                return IsSelected(PentEvoZxBusDevice.MultiSoundMax,
                        m_slot1Enabled, m_slot1Device) ||
                    IsSelected(PentEvoZxBusDevice.MultiSoundMax,
                        m_slot2Enabled, m_slot2Device);
            }
        }

        public bool IsZxNetUsbBoardEnabled
        {
            get
            {
                return IsSelected(PentEvoZxBusDevice.ZXNetUSB,
                        m_slot1Enabled, m_slot1Device) ||
                    IsSelected(PentEvoZxBusDevice.ZXNetUSB,
                        m_slot2Enabled, m_slot2Device);
            }
        }

        public bool IsAutomaticMultiSoundYmActive
        {
            get
            {
                return (IsMultiSoundBoardEnabled &&
                        m_automatic.Checked && m_ym.Checked) ||
                    (IsMultiSoundMaxBoardEnabled &&
                        m_maxAutomatic.Checked && m_maxYm.Checked);
            }
        }

        public CtlSettingsPentEvoZxBus()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            Size = new Size(300, 430);

            var slots = new GroupBox();
            slots.Text = "ZXBUS expansion slots:";
            slots.Location = new Point(4, 4);
            slots.Size = new Size(292, 160);
            slots.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(slots);

            m_slot1Enabled = CreateSlotCheckBox(slots, "Slot 1 active", 28);
            m_slot1Device = CreateSlotComboBox(slots, 112, 24);
            m_slot2Enabled = CreateSlotCheckBox(slots, "Slot 2 active", 70);
            m_slot2Device = CreateSlotComboBox(slots, 112, 66);
            var slotHint = new Label();
            slotHint.AutoSize = false;
            slotHint.Location = new Point(10, 119);
            slotHint.Size = new Size(272, 34);
            slotHint.Text = "Both connectors are equivalent. One board of " +
                "each type may be installed.";
            slots.Controls.Add(slotHint);

            m_multiSoundSwitches = new GroupBox();
            m_multiSoundSwitches.Text = "ZX-MultiSound Rev.A2 switches:";
            m_multiSoundSwitches.Location = new Point(4, 170);
            m_multiSoundSwitches.Size = new Size(292, 260);
            m_multiSoundSwitches.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(m_multiSoundSwitches);

            m_automatic = CreateSwitch(m_multiSoundSwitches,
                "Automatic conflict protection", 24);
            m_ym = CreateSwitch(m_multiSoundSwitches,
                "YM / TurboSound FM", 52);
            m_saa = CreateSwitch(m_multiSoundSwitches, "SAA1099", 78);
            m_gs = CreateSwitch(m_multiSoundSwitches,
                "General Sound 1.05b", 104);
            m_soundDrive = CreateSwitch(m_multiSoundSwitches,
                "SounDrive", 130);
            m_tsFmSaaCompatibility = CreateSwitch(m_multiSoundSwitches,
                "TSFM SAA port compatibility", 156);
            m_effective = new Label();
            m_effective.AutoSize = false;
            m_effective.Location = new Point(10, 184);
            m_effective.Size = new Size(272, 66);
            m_multiSoundSwitches.Controls.Add(m_effective);

            m_maxSwitches = new GroupBox();
            m_maxSwitches.Text = "ZX-MultiSound Max switches:";
            m_maxSwitches.Location = new Point(4, 438);
            m_maxSwitches.Size = new Size(292, 260);
            m_maxSwitches.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(m_maxSwitches);
            m_maxAutomatic = CreateSwitch(m_maxSwitches,
                "Automatic conflict protection", 24);
            m_maxYm = CreateSwitch(m_maxSwitches,
                "YM / TurboSound FM", 52);
            m_maxSaa = CreateSwitch(m_maxSwitches, "SAA1099", 78);
            m_maxGs = CreateSwitch(m_maxSwitches,
                "General Sound 2 MB", 104);
            m_maxSoundDrive = CreateSwitch(m_maxSwitches,
                "SounDrive", 130);
            m_maxTsFmSaaCompatibility = CreateSwitch(m_maxSwitches,
                "TSFM SAA port compatibility", 156);
            m_maxEffective = new Label();
            m_maxEffective.AutoSize = false;
            m_maxEffective.Location = new Point(10, 184);
            m_maxEffective.Size = new Size(272, 66);
            m_maxSwitches.Controls.Add(m_maxEffective);

            m_slot1Enabled.CheckedChanged += Slot1SelectionChanged;
            m_slot1Device.SelectedIndexChanged += Slot1SelectionChanged;
            m_slot2Enabled.CheckedChanged += Slot2SelectionChanged;
            m_slot2Device.SelectedIndexChanged += Slot2SelectionChanged;
            m_automatic.CheckedChanged += SwitchChanged;
            m_ym.CheckedChanged += SwitchChanged;
            m_saa.CheckedChanged += SwitchChanged;
            m_gs.CheckedChanged += SwitchChanged;
            m_soundDrive.CheckedChanged += SwitchChanged;
            m_tsFmSaaCompatibility.CheckedChanged += SwitchChanged;
            m_maxAutomatic.CheckedChanged += SwitchChanged;
            m_maxYm.CheckedChanged += SwitchChanged;
            m_maxSaa.CheckedChanged += SwitchChanged;
            m_maxGs.CheckedChanged += SwitchChanged;
            m_maxSoundDrive.CheckedChanged += SwitchChanged;
            m_maxTsFmSaaCompatibility.CheckedChanged += SwitchChanged;
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
                    TryRestoreBoard(PentEvoZxBusDevice.NeoGS);
                }
                var multiSound = m_bmgr.FindDevice<ZxMultiSoundDevice>();
                if (multiSound != null && !IsMultiSoundBoardEnabled)
                {
                    TryRestoreBoard(PentEvoZxBusDevice.MultiSound);
                }
                var max = m_bmgr.FindDevice<ZxMultiSoundMaxDevice>();
                if (max != null && !IsMultiSoundMaxBoardEnabled)
                    TryRestoreBoard(PentEvoZxBusDevice.MultiSoundMax);
                if (m_bmgr.FindDevice<ZxmMoonSoundDevice>() != null &&
                    !IsMoonSoundBoardEnabled)
                {
                    TryRestoreBoard(PentEvoZxBusDevice.MoonSound);
                }
                if (m_bmgr.FindDevice<ZxNetUsbDevice>() != null &&
                    !IsZxNetUsbBoardEnabled)
                {
                    TryRestoreBoard(PentEvoZxBusDevice.ZXNetUSB);
                }
                m_automatic.Checked = multiSound == null ||
                    multiSound.AutomaticConfiguration;
                m_ym.Checked = multiSound == null || multiSound.YmEnabled;
                m_saa.Checked = multiSound == null || multiSound.SaaEnabled;
                m_gs.Checked = multiSound == null ||
                    multiSound.GeneralSoundEnabled;
                m_soundDrive.Checked = multiSound == null ||
                    multiSound.SoundDriveEnabled;
                m_tsFmSaaCompatibility.Checked = multiSound != null &&
                    multiSound.TsFmSaaPortCompatibility;
                m_maxAutomatic.Checked = max == null ||
                    max.AutomaticConfiguration;
                m_maxYm.Checked = max == null || max.YmEnabled;
                m_maxSaa.Checked = max == null || max.SaaEnabled;
                m_maxGs.Checked = max == null ||
                    max.GeneralSoundEnabled;
                m_maxSoundDrive.Checked = max == null ||
                    max.SoundDriveEnabled;
                m_maxTsFmSaaCompatibility.Checked = max != null &&
                    max.TsFmSaaPortCompatibility;
            }
            finally
            {
                m_updatingSlots = false;
            }
            UpdateControls();
        }

        private void TryRestoreBoard(PentEvoZxBusDevice board)
        {
            if (!m_slot1Enabled.Checked ||
                GetSlotValue(m_slot1Device) == PentEvoZxBusDevice.Empty)
            {
                m_slot1Enabled.Checked = true;
                SelectSlotDevice(m_slot1Device, board);
            }
            else if (!m_slot2Enabled.Checked ||
                GetSlotValue(m_slot2Device) == PentEvoZxBusDevice.Empty)
            {
                m_slot2Enabled.Checked = true;
                SelectSlotDevice(m_slot2Device, board);
            }
            // An old or hand-edited profile can contain more board devices
            // than the two physical connectors can represent.  Do not hide
            // that mistake by silently replacing an already occupied slot.
        }

        public override void Apply()
        {
            m_device = m_bmgr.FindDevice<UlaPentEvo>();
            if (m_device == null)
            {
                RemoveBoard<NeoGsDevice>();
                RemoveBoard<ZxMultiSoundDevice>();
                RemoveBoard<ZxMultiSoundMaxDevice>();
                RemoveBoard<ZxmMoonSoundDevice>();
                RemoveBoard<ZxNetUsbDevice>();
                return;
            }

            NormalizeDuplicate(0);
            var slot1Device = GetSlotValue(m_slot1Device);
            var slot2Device = GetSlotValue(m_slot2Device);
            var neoGsSelected = IsNeoGsBoardEnabled;
            var multiSoundSelected = IsMultiSoundBoardEnabled;
            var maxSelected = IsMultiSoundMaxBoardEnabled;
            var moonSoundSelected = IsMoonSoundBoardEnabled;
            var zxNetUsbSelected = IsZxNetUsbBoardEnabled;

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
            if (maxSelected && !m_maxAutomatic.Checked)
            {
                if (m_maxGs.Checked && neoGsSelected)
                    throw new InvalidOperationException(
                        "ZX-MultiSound Max GS and NeoGS both decode #B3/#BB. " +
                        "Disable Max GS or use Automatic mode.");
                if (m_maxYm.Checked &&
                    (m_bmgr.FindDevice<AYCHRV>() != null ||
                     m_bmgr.FindDevice<TurboSoundFmPro>() != null))
                    throw new InvalidOperationException(
                        "ZX-MultiSound Max YM conflicts with internal AY/TSFM. " +
                        "Select Music: None or use Automatic mode.");
            }
            if (multiSoundSelected && maxSelected &&
                ((m_automatic.Checked == false &&
                    (m_ym.Checked || m_saa.Checked || m_gs.Checked ||
                     m_soundDrive.Checked)) ||
                 (m_maxAutomatic.Checked == false &&
                    (m_maxYm.Checked || m_maxSaa.Checked || m_maxGs.Checked ||
                     m_maxSoundDrive.Checked))))
                throw new InvalidOperationException(
                    "Two MultiSound cards share YM, SAA, GS and SoundDrive " +
                    "ports. Use Automatic protection on both cards.");

            SetBoardPresence<NeoGsDevice>(neoGsSelected);
            SetBoardPresence<ZxmMoonSoundDevice>(moonSoundSelected);
            SetBoardPresence<ZxNetUsbDevice>(zxNetUsbSelected);
            SetBoardPresence<ZxMultiSoundMaxDevice>(maxSelected);
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
                multiSound.TsFmSaaPortCompatibility =
                    m_tsFmSaaCompatibility.Checked;

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
                    m_bmgr.FindDevice<TurboSoundFmPro>() != null,
                    maxSelected && m_slot1Enabled.Checked &&
                    slot1Device == PentEvoZxBusDevice.MultiSoundMax);
            }

            var maxBoard = m_bmgr.FindDevice<ZxMultiSoundMaxDevice>();
            if (maxBoard != null)
            {
                maxBoard.AutomaticConfiguration = m_maxAutomatic.Checked;
                maxBoard.YmEnabled = m_maxYm.Checked;
                maxBoard.SaaEnabled = m_maxSaa.Checked;
                maxBoard.GeneralSoundEnabled = m_maxGs.Checked;
                maxBoard.SoundDriveEnabled = m_maxSoundDrive.Checked;
                maxBoard.TsFmSaaPortCompatibility =
                    m_maxTsFmSaaCompatibility.Checked;
                if (maxBoard.AutomaticConfiguration && maxBoard.YmEnabled)
                {
                    foreach (var ay in m_bmgr.FindDevices<AYCHRV>().ToArray())
                        m_bmgr.Remove(ay);
                    foreach (var tsfm in m_bmgr
                        .FindDevices<TurboSoundFmPro>().ToArray())
                        m_bmgr.Remove(tsfm);
                    m_device.InternalSound = PentEvoInternalSound.None;
                }
                maxBoard.ResolveConfiguration(neoGsSelected,
                    m_bmgr.FindDevice<AYCHRV>() != null ||
                    m_bmgr.FindDevice<TurboSoundFmPro>() != null,
                    multiSoundSelected && m_slot1Enabled.Checked &&
                    slot1Device == PentEvoZxBusDevice.MultiSound);
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
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.MoonSound, "ZXM-MoonSound Rev.01"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.ZXNetUSB, "ZXNetUSB Rev.C (Ethernet)"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.MultiSoundMax, "ZX-MultiSound Max"));
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
            var max = IsMultiSoundMaxBoardEnabled;
            m_multiSoundSwitches.Visible = multiSound;
            m_maxSwitches.Visible = max;
            var maxTop = multiSound ? 438 : 170;
            AutoScrollMinSize = new Size(0,
                max ? maxTop + m_maxSwitches.Height + 8 :
                multiSound ? 170 + m_multiSoundSwitches.Height + 8 : 0);
            var scroll = AutoScrollPosition;
            var maxLocation = new Point(4 + scroll.X, maxTop + scroll.Y);
            if (m_maxSwitches.Location != maxLocation)
                m_maxSwitches.Location = maxLocation;
            m_tsFmSaaCompatibility.Enabled = multiSound && m_saa.Checked;
            m_automatic.Enabled = multiSound;
            m_ym.Enabled = multiSound;
            m_saa.Enabled = multiSound;
            m_gs.Enabled = multiSound;
            m_soundDrive.Enabled = multiSound;
            m_maxAutomatic.Enabled = max;
            m_maxYm.Enabled = max;
            m_maxSaa.Enabled = max;
            m_maxGs.Enabled = max;
            m_maxSoundDrive.Enabled = max;
            m_maxTsFmSaaCompatibility.Enabled = max && m_maxSaa.Checked;

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

            if (!max)
                m_maxEffective.Text = "MultiSound Max is not installed.";
            else if (m_maxAutomatic.Checked)
            {
                var second = multiSound && IsSelected(
                    PentEvoZxBusDevice.MultiSound,
                    m_slot1Enabled, m_slot1Device);
                m_maxEffective.Text = second
                    ? "Rev.A2 owns YM/SAA/GS/SD; Max OPL3 remains active."
                    : "Effective: YM " + OnOff(m_maxYm.Checked) +
                      ", SAA " + OnOff(m_maxSaa.Checked) +
                      ", GS " + OnOff(m_maxGs.Checked && !IsNeoGsBoardEnabled) +
                      ", SD " + OnOff(m_maxSoundDrive.Checked) +
                      ", OPL3 " + (IsMoonSoundBoardEnabled
                          ? "off (MoonSound)" : "on") + ".";
            }
            else
                m_maxEffective.Text = "Manual: conflicting ports are " +
                    "rejected on Apply.";

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
