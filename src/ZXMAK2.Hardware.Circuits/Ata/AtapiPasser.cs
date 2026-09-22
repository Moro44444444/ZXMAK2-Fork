using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;


namespace ZXMAK2.Hardware.Circuits.Ata
{
    /// <summary>
    /// Read-only bridge to a Windows CD/DVD logical drive.  The guest sees
    /// standard 2048-byte ATAPI blocks; no host write or tray-control request
    /// is ever issued.
    /// </summary>
    public sealed class AtapiPasser : IDisposable
    {
        public const int BlockSize = 2048;

        private const uint GenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x00000080;

        private DriveInfo m_drive;
        private FileStream m_stream;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        public void Dispose()
        {
            Close();
        }

        public bool Open(string driveName)
        {
            Close();

            try
            {
                var normalized = NormalizeDriveName(driveName);
                if (string.IsNullOrEmpty(normalized))
                {
                    return false;
                }

                var drive = new DriveInfo(normalized);
                if (drive.DriveType != DriveType.CDRom)
                {
                    return false;
                }

                // A selected empty optical drive is still a connected ATAPI
                // device. TEST UNIT READY/REQUEST SENSE report the absence of
                // media later, instead of making the device disappear.
                m_drive = drive;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Close();
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (m_stream != null)
                {
                    m_stream.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                m_stream = null;
                m_drive = null;
            }
        }

        public bool IsAttached
        {
            get { return m_drive != null; }
        }

        public bool IsMediaReady
        {
            get
            {
                try
                {
                    return m_drive != null && m_drive.IsReady && m_drive.TotalSize >= BlockSize;
                }
                catch
                {
                    return false;
                }
            }
        }

        public uint BlockCount
        {
            get
            {
                try
                {
                    if (!IsMediaReady)
                    {
                        return 0;
                    }
                    var blocks = (ulong)m_drive.TotalSize / BlockSize;
                    return blocks > UInt32.MaxValue ? UInt32.MaxValue : (uint)blocks;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public bool ReadBlock(uint lba, byte[] destination, int offset)
        {
            if (destination == null || offset < 0 || destination.Length - offset < BlockSize)
            {
                return false;
            }
            if (!IsMediaReady)
            {
                return false;
            }
            if (lba >= BlockCount)
            {
                return false;
            }
            if (!EnsureStream())
            {
                return false;
            }

            try
            {
                var position = (long)lba * BlockSize;
                if (m_stream.Seek(position, SeekOrigin.Begin) != position)
                {
                    return false;
                }

                var read = 0;
                while (read < BlockSize)
                {
                    var current = m_stream.Read(destination, offset + read, BlockSize - read);
                    if (current <= 0)
                    {
                        return false;
                    }
                    read += current;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                CloseStream();
                return false;
            }
        }

        public static string NormalizeDriveName(string driveName)
        {
            if (string.IsNullOrWhiteSpace(driveName))
            {
                return string.Empty;
            }
            var value = driveName.Trim();
            if (value.Length < 2 || value[1] != ':')
            {
                return string.Empty;
            }
            var letter = value[0];
            if (!char.IsLetter(letter))
            {
                return string.Empty;
            }
            return char.ToUpperInvariant(letter) + @":\";
        }

        public static string[] GetOpticalDrives()
        {
            var result = new List<string>();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType == DriveType.CDRom)
                    {
                        result.Add(drive.Name);
                    }
                }
                catch
                {
                    // A changing host-drive list must not make Machine Settings fail.
                }
            }
            return result.ToArray();
        }

        private bool EnsureStream()
        {
            if (m_stream != null)
            {
                return true;
            }
            try
            {
                var devicePath = @"\\.\" + m_drive.Name.Substring(0, 2);
                var handle = CreateFile(
                    devicePath,
                    GenericRead,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    FileAttributeNormal,
                    IntPtr.Zero);
                if (handle == null || handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    if (handle != null)
                    {
                        handle.Dispose();
                    }
                    Logger.Error("ATAPI: cannot open {0}, Win32 error {1}", devicePath, error);
                    return false;
                }
                m_stream = new FileStream(handle, FileAccess.Read, BlockSize, false);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                CloseStream();
                return false;
            }
        }

        private void CloseStream()
        {
            if (m_stream != null)
            {
                try
                {
                    m_stream.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
                finally
                {
                    m_stream = null;
                }
            }
        }
    }
}
