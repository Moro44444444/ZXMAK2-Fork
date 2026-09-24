using System;
using System.Drawing;
using System.Windows.Forms;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    /// <summary>
    /// Configuration surface for the two physical ZXBUS connectors on the
    /// ZX Evolution motherboard. It is deliberately separate from ULA and
    /// Music: those devices use the normal Machine Settings pages.
    /// </summary>
    public sealed class CtlSettingsPentEvoZxBus : ConfigScreenControl
    {
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

        public CtlSettingsPentEvoZxBus()
        {
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            Size = new Size(300, 334);

            var zxBus = new GroupBox();
            zxBus.Text = "ZXBUS expansion slots:";
            zxBus.Location = new Point(4, 4);
            zxBus.Size = new Size(292, 220);
            zxBus.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            Controls.Add(zxBus);

            m_slot1Enabled = CreateSlotCheckBox(zxBus, "Slot 1 active", 28);
            m_slot1Device = CreateSlotComboBox(zxBus, 112, 24);
            m_slot2Enabled = CreateSlotCheckBox(zxBus, "Slot 2 active", 70);
            m_slot2Device = CreateSlotComboBox(zxBus, 112, 66);
            m_slot1Enabled.CheckedChanged += Slot1SelectionChanged;
            m_slot1Device.SelectedIndexChanged += Slot1SelectionChanged;
            m_slot2Enabled.CheckedChanged += Slot2SelectionChanged;
            m_slot2Device.SelectedIndexChanged += Slot2SelectionChanged;

            var busHint = new Label();
            busHint.AutoSize = false;
            busHint.Location = new Point(10, 112);
            busHint.Size = new Size(272, 92);
            busHint.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            busHint.Text = "Both physical connectors share one ZXBUS. " +
                "NeoGS Rev. C-VS uses official ROM 1.11 and can occupy " +
                "either slot. One NeoGS board can be active at a time.";
            zxBus.Controls.Add(busHint);
        }

        public void Initialize(
            BusManager bmgr,
            IHostService host,
            UlaPentEvo device)
        {
            m_bmgr = bmgr;
            m_device = device;
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
            UpdateSlotControls();
        }

        public override void Apply()
        {
            // The standard ULA page is applied before this page and may have
            // replaced PentEvo with another ULA type. Always act on the ULA
            // that is actually present in the pending bus, never on a stale
            // object retained from opening the dialog.
            m_device = m_bmgr.FindDevice<UlaPentEvo>();
            if (m_device == null)
            {
                var staleNeoGs = m_bmgr.FindDevice<NeoGsDevice>();
                if (staleNeoGs != null)
                    m_bmgr.Remove(staleNeoGs);
                return;
            }
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
            {
                m_bmgr.Remove(neoGs);
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
            comboBox.Size = new Size(168, 24);
            comboBox.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            comboBox.Items.Add(new SlotChoice(PentEvoZxBusDevice.Empty, "Empty"));
            comboBox.Items.Add(new SlotChoice(
                PentEvoZxBusDevice.NeoGS,
                "NeoGS Rev. C-VS"));
            comboBox.SelectedIndex = 0;
            parent.Controls.Add(comboBox);
            return comboBox;
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
                        SelectSlotDevice(m_slot2Device, PentEvoZxBusDevice.Empty);
                    }
                    else
                    {
                        m_slot1Enabled.Checked = false;
                        SelectSlotDevice(m_slot1Device, PentEvoZxBusDevice.Empty);
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

        private static bool IsNeoGsSelected(CheckBox enabled, ComboBox device)
        {
            return enabled.Checked &&
                GetSlotValue(device) == PentEvoZxBusDevice.NeoGS;
        }

        private static PentEvoZxBusDevice GetSlotValue(ComboBox comboBox)
        {
            var choice = comboBox.SelectedItem as SlotChoice;
            return choice != null ? choice.Value : PentEvoZxBusDevice.Empty;
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
