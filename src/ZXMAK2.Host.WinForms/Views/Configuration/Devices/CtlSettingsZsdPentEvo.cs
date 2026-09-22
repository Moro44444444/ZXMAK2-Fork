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
    /// SD settings intentionally mirror the IDE panel: the path is part of
    /// the machine profile, Eject clears it, and applying a changed medium is
    /// completed by FormMachineSettings with the common cold power-cycle.
    /// </summary>
    public class CtlSettingsZsdPentEvo : ConfigScreenControl
    {
        private ZsdPentEvo m_device;
        private readonly GroupBox m_group;
        private readonly CheckBox m_connected;
        private readonly TextBox m_path;
        private readonly Button m_browse;
        private readonly Button m_eject;
        private readonly Label m_status;
        private readonly Label m_hint;

        public CtlSettingsZsdPentEvo()
        {
            Size = new Size(300, 240);

            m_group = new GroupBox();
            m_group.Text = "PentEvo SD Card Settings:";
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
            m_path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
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
            m_status.Size = new Size(195, 24);
            m_status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_group.Controls.Add(m_status);

            m_hint = new Label();
            m_hint.AutoSize = false;
            m_hint.Location = new Point(9, 128);
            m_hint.Size = new Size(282, 58);
            m_hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_hint.Text = "The selected IMG, IMA or VHD stays connected after restart until you replace or eject it.";
            m_group.Controls.Add(m_hint);
        }

        public void Init(BusManager bmgr, IHostService host, ZsdPentEvo device)
        {
            m_device = device;
            SetImagePath(device.ConfiguredImageFileName);
        }

        /// <summary>
        /// FormMachineSettings supplies the live mounted path as a fallback
        /// when a legacy profile has not yet serialized its SD attribute.
        /// This updates only the detached settings copy until Apply is used.
        /// </summary>
        public void SetMountedImageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }
            SetImagePath(fileName);
        }

        private void SetImagePath(string fileName)
        {
            m_path.Text = fileName ?? string.Empty;
            m_path.SelectionStart = m_path.Text.Length;
            m_connected.Checked = !string.IsNullOrEmpty(m_path.Text);
            UpdateEnabled();
            UpdateStatus();
        }

        public override void Apply()
        {
            if (!m_connected.Checked)
            {
                m_device.ConfigureCard(string.Empty);
                return;
            }
            if (string.IsNullOrWhiteSpace(m_path.Text))
            {
                throw new InvalidOperationException("Select an SD card image or eject the SD card");
            }
            m_device.ConfigureCard(m_path.Text.Trim());
        }

        private void browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select PentEvo SD Card image";
                dialog.Filter = "Disk image file (*.img, *.ima, *.vhd)|*.img;*.ima;*.vhd";
                dialog.DefaultExt = "img";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(m_path.Text))
                {
                    dialog.FileName = m_path.Text;
                }
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                m_path.Text = dialog.FileName;
                m_path.SelectionStart = m_path.Text.Length;
                m_connected.Checked = true;
            }
        }

        private void eject_Click(object sender, EventArgs e)
        {
            m_connected.Checked = false;
            m_path.Text = string.Empty;
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
            var enabled = m_connected.Checked;
            m_path.Enabled = enabled;
            m_browse.Enabled = true;
            m_eject.Enabled = enabled || !string.IsNullOrEmpty(m_path.Text);
        }

        private void UpdateStatus()
        {
            if (!m_connected.Checked || string.IsNullOrWhiteSpace(m_path.Text))
            {
                m_status.Text = "No SD card image selected";
                return;
            }
            try
            {
                var fullPath = Path.GetFullPath(m_path.Text.Trim());
                m_status.Text = File.Exists(fullPath) ?
                    "Selected: " + Path.GetFileName(fullPath) :
                    "Selected image is not available";
            }
            catch
            {
                m_status.Text = "Selected image path is invalid";
            }
        }
    }
}
