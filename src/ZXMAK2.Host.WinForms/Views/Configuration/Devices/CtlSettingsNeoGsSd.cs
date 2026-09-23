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
    /// Media panel for the microSD socket physically located on NeoGS.
    /// NeoGS itself remains managed by the PENTEVO ZXBUS slot selectors.
    /// </summary>
    public sealed class CtlSettingsNeoGsSd : ConfigScreenControl
    {
        private BusManager m_bmgr;
        private readonly GroupBox m_group;
        private readonly CheckBox m_connected;
        private readonly TextBox m_path;
        private readonly Button m_browse;
        private readonly Button m_eject;
        private readonly Label m_status;
        private readonly Label m_hint;
        private bool m_boardEnabled;

        public CtlSettingsNeoGsSd()
        {
            Size = new Size(300, 240);

            m_group = new GroupBox();
            m_group.Text = "NeoGS microSD Card Settings:";
            m_group.Dock = DockStyle.Fill;
            Controls.Add(m_group);

            m_connected = new CheckBox();
            m_connected.Text = "SD card connected";
            m_connected.AutoSize = true;
            m_connected.Location = new Point(9, 24);
            m_connected.CheckedChanged += connected_CheckedChanged;
            m_group.Controls.Add(m_connected);

            m_path = new TextBox();
            m_path.Location = new Point(9, 51);
            m_path.Size = new Size(236, 20);
            m_path.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            m_path.TextChanged += path_TextChanged;
            m_group.Controls.Add(m_path);

            m_browse = new Button();
            m_browse.Text = "...";
            m_browse.Location = new Point(251, 49);
            m_browse.Size = new Size(40, 23);
            m_browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_browse.Click += browse_Click;
            m_group.Controls.Add(m_browse);

            m_eject = new Button();
            m_eject.Text = "Eject";
            m_eject.Location = new Point(216, 80);
            m_eject.Size = new Size(75, 23);
            m_eject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_eject.Click += eject_Click;
            m_group.Controls.Add(m_eject);

            m_status = new Label();
            m_status.AutoSize = false;
            m_status.Location = new Point(9, 87);
            m_status.Size = new Size(195, 34);
            m_status.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            m_group.Controls.Add(m_status);

            m_hint = new Label();
            m_hint.AutoSize = false;
            m_hint.Location = new Point(9, 128);
            m_hint.Size = new Size(282, 58);
            m_hint.Anchor = AnchorStyles.Top | AnchorStyles.Left |
                AnchorStyles.Right;
            m_hint.Text = "This socket belongs to NeoGS and is available only " +
                "while NeoGS is active in a ZXBUS slot.";
            m_group.Controls.Add(m_hint);
        }

        // Deliberately not named Init: this is a navigation panel for media,
        // not an independent bus device discovered by the reflection mapper.
        public void Initialize(BusManager bmgr, IHostService host)
        {
            m_bmgr = bmgr;
            var neoGs = m_bmgr.FindDevice<NeoGsDevice>();
            SetImagePath(neoGs == null
                ? string.Empty
                : neoGs.ConfiguredSdImageFileName);
        }

        public void SetBoardEnabled(bool enabled)
        {
            m_boardEnabled = enabled;
            UpdateEnabled();
            UpdateStatus();
        }

        public override void Apply()
        {
            if (!m_boardEnabled)
                return;

            var neoGs = m_bmgr.FindDevice<NeoGsDevice>();
            if (neoGs == null)
                throw new InvalidOperationException(
                    "NeoGS must be active in a ZXBUS slot");
            if (!m_connected.Checked)
            {
                neoGs.ConfigureSdCard(string.Empty);
                return;
            }
            if (string.IsNullOrWhiteSpace(m_path.Text))
            {
                throw new InvalidOperationException(
                    "Select a NeoGS microSD image or eject the card");
            }
            neoGs.ConfigureSdCard(m_path.Text.Trim());
        }

        private void SetImagePath(string fileName)
        {
            m_path.Text = fileName ?? string.Empty;
            m_path.SelectionStart = m_path.Text.Length;
            m_connected.Checked = !string.IsNullOrEmpty(m_path.Text);
            UpdateEnabled();
            UpdateStatus();
        }

        private void browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select NeoGS microSD image";
                dialog.Filter =
                    "Disk image file (*.img, *.ima, *.vhd)|*.img;*.ima;*.vhd";
                dialog.DefaultExt = "img";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(m_path.Text))
                    dialog.FileName = m_path.Text;
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;
                SetImagePath(dialog.FileName);
            }
        }

        private void eject_Click(object sender, EventArgs e)
        {
            SetImagePath(string.Empty);
        }

        private void connected_CheckedChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
            UpdateStatus();
        }

        private void path_TextChanged(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateEnabled()
        {
            m_connected.Enabled = m_boardEnabled;
            m_path.Enabled = m_boardEnabled && m_connected.Checked;
            m_browse.Enabled = m_boardEnabled;
            m_eject.Enabled = m_boardEnabled &&
                (m_connected.Checked || !string.IsNullOrEmpty(m_path.Text));
        }

        private void UpdateStatus()
        {
            if (!m_boardEnabled)
            {
                m_status.Text = "NeoGS is not active in ZXBUS";
                return;
            }
            if (!m_connected.Checked || string.IsNullOrWhiteSpace(m_path.Text))
            {
                m_status.Text = "No SD card image selected";
                return;
            }
            try
            {
                var fullPath = Path.GetFullPath(m_path.Text.Trim());
                m_status.Text = File.Exists(fullPath)
                    ? "Selected: " + Path.GetFileName(fullPath)
                    : "Selected image is not available";
            }
            catch
            {
                m_status.Text = "Selected image path is invalid";
            }
        }
    }
}
