using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Circuits.Ata;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    public class CtlSettingsIdePentEvo : ConfigScreenControl
    {
        private IdePentEvo m_device;
        private readonly GroupBox m_group;
        private readonly CheckBox m_connected;
        private readonly TextBox m_path;
        private readonly Button m_browse;
        private readonly Button m_eject;
        private readonly CheckBox m_readOnly;
        private readonly Label m_geometry;
        private readonly Label m_hint;

        public CtlSettingsIdePentEvo()
        {
            Size = new Size(300, 240);

            m_group = new GroupBox();
            m_group.Text = "PentEvo IDE Settings:";
            m_group.Dock = DockStyle.Fill;
            Controls.Add(m_group);

            m_connected = new CheckBox();
            m_connected.Text = "HDD connected";
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

            m_readOnly = new CheckBox();
            m_readOnly.Text = "Read only";
            m_readOnly.AutoSize = true;
            m_readOnly.Location = new Point(9, 84);
            m_readOnly.CheckedChanged += readOnly_CheckedChanged;
            m_group.Controls.Add(m_readOnly);

            m_geometry = new Label();
            m_geometry.AutoSize = false;
            m_geometry.Location = new Point(9, 119);
            m_geometry.Size = new Size(282, 38);
            m_geometry.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_group.Controls.Add(m_geometry);

            m_hint = new Label();
            m_hint.AutoSize = false;
            m_hint.Location = new Point(9, 164);
            m_hint.Size = new Size(282, 58);
            m_hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_hint.Text = "The image size defines exact LBA. A matching .inf file is read automatically; otherwise compatible CHS is calculated.";
            m_group.Controls.Add(m_hint);
        }

        public void Init(BusManager bmgr, IHostService host, IdePentEvo device)
        {
            m_device = device;
            var info = device.HardDisk;
            m_path.Text = info.FileName ?? string.Empty;
            m_path.SelectionStart = m_path.Text.Length;
            m_connected.Checked = !string.IsNullOrEmpty(info.FileName);
            m_readOnly.Checked = m_connected.Checked && info.ReadOnly;
            UpdateGeometry(info);
            UpdateEnabled();
        }

        public override void Apply()
        {
            if (!m_connected.Checked)
            {
                m_device.DisconnectHardDisk();
                return;
            }

            if (string.IsNullOrWhiteSpace(m_path.Text))
                throw new InvalidOperationException("Select an HDD image or disconnect the HDD");

            m_device.ConfigureHardDisk(m_path.Text.Trim(), m_readOnly.Checked);
        }

        private void browse_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select PentEvo HDD image";
                dialog.Filter = "HDD images (*.hdd)|*.hdd";
                dialog.DefaultExt = "hdd";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (!string.IsNullOrEmpty(m_path.Text))
                    dialog.FileName = m_path.Text;
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                m_path.Text = dialog.FileName;
                m_path.SelectionStart = m_path.Text.Length;
                m_connected.Checked = true;
                // Writable media is the normal default. ConfigureImage will
                // still force effective read-only for a host read-only file.
                m_readOnly.Checked = false;
                PreviewGeometry();
            }
        }

        private void eject_Click(object sender, EventArgs e)
        {
            m_connected.Checked = false;
            m_path.Text = string.Empty;
            m_readOnly.Checked = false;
            m_geometry.Text = "No HDD image selected";
        }

        private void connected_CheckedChanged(object sender, EventArgs e)
        {
            UpdateEnabled();
            PreviewGeometry();
        }

        private void path_TextChanged(object sender, EventArgs e)
        {
            PreviewGeometry();
        }

        private void readOnly_CheckedChanged(object sender, EventArgs e)
        {
            PreviewGeometry();
        }

        private void PreviewGeometry()
        {
            if (!m_connected.Checked || string.IsNullOrWhiteSpace(m_path.Text))
            {
                m_geometry.Text = "No HDD image selected";
                return;
            }

            try
            {
                var preview = new AtaDeviceInfo();
                preview.ConfigureImage(m_path.Text.Trim(), m_readOnly.Checked);
                UpdateGeometry(preview);
            }
            catch (Exception ex)
            {
                m_geometry.Text = ex.Message;
            }
        }

        private void UpdateGeometry(AtaDeviceInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.FileName))
            {
                m_geometry.Text = "No HDD image selected";
                return;
            }
            m_geometry.Text = string.Format(
                "CHS: {0}/{1}/{2}    LBA: {3}\r\nMode: {4}",
                info.Cylinders,
                info.Heads,
                info.Sectors,
                info.Lba,
                info.ReadOnly ? "read only" : "read/write");
        }

        private void UpdateEnabled()
        {
            var enabled = m_connected.Checked;
            m_path.Enabled = enabled;
            m_browse.Enabled = true;
            m_eject.Enabled = enabled || !string.IsNullOrEmpty(m_path.Text);
            m_readOnly.Enabled = enabled;
        }
    }
}
