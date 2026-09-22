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
    /// <summary>
    /// BaseConf has one IDE channel with a master and a slave. Keep the HDD
    /// image as master and expose an optional Windows CD/DVD drive as slave.
    /// </summary>
    public class CtlSettingsIdePentEvo : ConfigScreenControl
    {
        private IdePentEvo m_device;
        private readonly GroupBox m_group;
        private readonly Label m_hddTitle;
        private readonly CheckBox m_connected;
        private readonly TextBox m_path;
        private readonly Button m_browse;
        private readonly Button m_eject;
        private readonly CheckBox m_readOnly;
        private readonly Label m_geometry;
        private readonly Label m_hint;
        private readonly GroupBox m_cdGroup;
        private readonly CheckBox m_cdConnected;
        private readonly ComboBox m_cdDrive;
        private readonly Button m_cdRefresh;
        private readonly Button m_cdEject;
        private readonly Label m_cdStatus;
        private readonly Label m_cdHint;

        public CtlSettingsIdePentEvo()
        {
            Size = new Size(300, 430);

            m_group = new GroupBox();
            m_group.Text = "PentEvo IDE Settings:";
            m_group.Dock = DockStyle.Fill;
            Controls.Add(m_group);

            m_hddTitle = new Label();
            m_hddTitle.Text = "HDD (IDE Master)";
            m_hddTitle.AutoSize = true;
            m_hddTitle.Location = new Point(9, 22);
            m_group.Controls.Add(m_hddTitle);

            m_connected = new CheckBox();
            m_connected.Text = "HDD connected";
            m_connected.AutoSize = true;
            m_connected.Location = new Point(9, 43);
            m_connected.CheckedChanged += connected_CheckedChanged;
            m_group.Controls.Add(m_connected);

            m_path = new TextBox();
            m_path.Location = new Point(9, 69);
            m_path.Size = new Size(236, 20);
            m_path.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_path.TextChanged += path_TextChanged;
            m_group.Controls.Add(m_path);

            m_browse = new Button();
            m_browse.Text = "...";
            m_browse.Location = new Point(251, 67);
            m_browse.Size = new Size(40, 23);
            m_browse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_browse.Click += browse_Click;
            m_group.Controls.Add(m_browse);

            m_eject = new Button();
            m_eject.Text = "Eject";
            m_eject.Location = new Point(216, 98);
            m_eject.Size = new Size(75, 23);
            m_eject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_eject.Click += eject_Click;
            m_group.Controls.Add(m_eject);

            m_readOnly = new CheckBox();
            m_readOnly.Text = "Read only";
            m_readOnly.AutoSize = true;
            m_readOnly.Location = new Point(9, 102);
            m_readOnly.CheckedChanged += readOnly_CheckedChanged;
            m_group.Controls.Add(m_readOnly);

            m_geometry = new Label();
            m_geometry.AutoSize = false;
            m_geometry.Location = new Point(9, 136);
            m_geometry.Size = new Size(282, 38);
            m_geometry.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_group.Controls.Add(m_geometry);

            m_hint = new Label();
            m_hint.AutoSize = false;
            m_hint.Location = new Point(9, 178);
            m_hint.Size = new Size(282, 39);
            m_hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_hint.Text = "The image size defines exact LBA. A matching .inf file is read automatically; otherwise compatible CHS is calculated.";
            m_group.Controls.Add(m_hint);

            m_cdGroup = new GroupBox();
            m_cdGroup.Text = "CD/DVD-ROM (IDE Slave)";
            m_cdGroup.Location = new Point(9, 223);
            m_cdGroup.Size = new Size(282, 180);
            m_cdGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_group.Controls.Add(m_cdGroup);

            m_cdConnected = new CheckBox();
            m_cdConnected.Text = "CD/DVD-ROM connected";
            m_cdConnected.AutoSize = true;
            m_cdConnected.Location = new Point(9, 22);
            m_cdConnected.CheckedChanged += cdConnected_CheckedChanged;
            m_cdGroup.Controls.Add(m_cdConnected);

            m_cdDrive = new ComboBox();
            m_cdDrive.DropDownStyle = ComboBoxStyle.DropDownList;
            m_cdDrive.Location = new Point(9, 49);
            m_cdDrive.Size = new Size(178, 21);
            m_cdDrive.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_cdDrive.SelectedIndexChanged += cdDrive_SelectedIndexChanged;
            m_cdGroup.Controls.Add(m_cdDrive);

            m_cdRefresh = new Button();
            m_cdRefresh.Text = "Refresh";
            m_cdRefresh.Location = new Point(193, 47);
            m_cdRefresh.Size = new Size(89, 23);
            m_cdRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_cdRefresh.Click += cdRefresh_Click;
            m_cdGroup.Controls.Add(m_cdRefresh);

            m_cdEject = new Button();
            m_cdEject.Text = "Eject";
            m_cdEject.Location = new Point(207, 78);
            m_cdEject.Size = new Size(75, 23);
            m_cdEject.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            m_cdEject.Click += cdEject_Click;
            m_cdGroup.Controls.Add(m_cdEject);

            m_cdStatus = new Label();
            m_cdStatus.AutoSize = false;
            m_cdStatus.Location = new Point(9, 81);
            m_cdStatus.Size = new Size(189, 36);
            m_cdStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_cdGroup.Controls.Add(m_cdStatus);

            m_cdHint = new Label();
            m_cdHint.AutoSize = false;
            m_cdHint.Location = new Point(9, 122);
            m_cdHint.Size = new Size(273, 46);
            m_cdHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            m_cdHint.Text = "Uses a Windows CD/DVD drive, including a mounted virtual drive. Read-only; Eject disconnects it from PentEvo.";
            m_cdGroup.Controls.Add(m_cdHint);
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

            var cdrom = device.CdRom;
            PopulateCdDrives(cdrom.FileName);
            m_cdConnected.Checked = cdrom.IsCdrom && !string.IsNullOrEmpty(cdrom.FileName);
            UpdateEnabled();
            UpdateCdEnabled();
            UpdateCdStatus();
        }

        public override void Apply()
        {
            if (!m_connected.Checked)
            {
                m_device.DisconnectHardDisk();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(m_path.Text))
                    throw new InvalidOperationException("Select an HDD image or disconnect the HDD");
                m_device.ConfigureHardDisk(m_path.Text.Trim(), m_readOnly.Checked);
            }

            if (!m_cdConnected.Checked)
            {
                m_device.DisconnectCdRom();
                return;
            }

            var driveName = m_cdDrive.SelectedItem as string;
            if (string.IsNullOrEmpty(driveName))
                throw new InvalidOperationException("Select a Windows CD/DVD drive or disconnect the CD/DVD-ROM");
            m_device.ConfigureCdRom(driveName);
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

        private void cdConnected_CheckedChanged(object sender, EventArgs e)
        {
            UpdateCdEnabled();
            UpdateCdStatus();
        }

        private void cdDrive_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateCdStatus();
        }

        private void cdRefresh_Click(object sender, EventArgs e)
        {
            PopulateCdDrives(m_cdDrive.SelectedItem as string);
            UpdateCdStatus();
        }

        private void cdEject_Click(object sender, EventArgs e)
        {
            m_cdConnected.Checked = false;
            m_cdDrive.SelectedIndex = -1;
            UpdateCdStatus();
        }

        private void PopulateCdDrives(string selectedDrive)
        {
            var normalized = AtapiPasser.NormalizeDriveName(selectedDrive);
            m_cdDrive.BeginUpdate();
            try
            {
                m_cdDrive.Items.Clear();
                foreach (var drive in AtapiPasser.GetOpticalDrives())
                {
                    m_cdDrive.Items.Add(drive);
                }
                if (!string.IsNullOrEmpty(normalized) && m_cdDrive.FindStringExact(normalized) < 0)
                {
                    // Keep a saved selection visible even if the host drive
                    // was temporarily removed between emulator launches.
                    m_cdDrive.Items.Add(normalized);
                }
                m_cdDrive.SelectedIndex = string.IsNullOrEmpty(normalized) ?
                    -1 : m_cdDrive.FindStringExact(normalized);
            }
            finally
            {
                m_cdDrive.EndUpdate();
            }
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

        private void UpdateCdEnabled()
        {
            m_cdDrive.Enabled = m_cdConnected.Checked;
            m_cdRefresh.Enabled = true;
            m_cdEject.Enabled = m_cdConnected.Checked || m_cdDrive.SelectedIndex >= 0;
        }

        private void UpdateCdStatus()
        {
            var driveName = m_cdDrive.SelectedItem as string;
            if (!m_cdConnected.Checked || string.IsNullOrEmpty(driveName))
            {
                m_cdStatus.Text = "No CD/DVD drive selected";
                return;
            }
            try
            {
                var drive = new DriveInfo(driveName);
                if (drive.DriveType != DriveType.CDRom)
                {
                    m_cdStatus.Text = "Selected drive is not available";
                }
                else if (!drive.IsReady)
                {
                    m_cdStatus.Text = drive.Name + " selected; no disc inserted";
                }
                else
                {
                    m_cdStatus.Text = string.Format(
                        "{0} ready - {1} MiB", drive.Name, drive.TotalSize / (1024 * 1024));
                }
            }
            catch
            {
                m_cdStatus.Text = "Selected drive is not available";
            }
        }
    }
}
