using System;
using System.Reflection;
using System.Text;
using System.Xml;
using ZXMAK2.Hardware.Circuits.Ata;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.WinForms.Views;


internal static class AtapiCdromProbeB41
{
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void SelectSlave(AtaDevice device)
    {
        device.write(AtaReg.HeadAndDrive, 0xB0);
    }

    private static void SendPacket(AtaDevice device, byte[] packet)
    {
        SelectSlave(device);
        device.write(AtaReg.CylinderLow, 0x00);
        device.write(AtaReg.CylinderHigh, 0x08);
        device.write(AtaReg.CommandStatus, 0xA0);
        for (var index = 0; index < 12; index += 2)
        {
            device.write_data((ushort)(packet[index] | (packet[index + 1] << 8)));
        }
    }

    private static byte[] ReadData(AtaDevice device, int count)
    {
        var result = new byte[count];
        for (var index = 0; index < count; index += 2)
        {
            var word = device.read_data();
            result[index] = (byte)word;
            if (index + 1 < count)
                result[index + 1] = (byte)(word >> 8);
        }
        return result;
    }

    private static byte[] RequestSense(AtaDevice device)
    {
        SendPacket(device, new byte[] { 0x03, 0, 0, 0, 18, 0, 0, 0, 0, 0, 0, 0 });
        return ReadData(device, 18);
    }

    private static string DescribeAtapiTransfer(AtaDevice device)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = device.GetType();
        var state = type.GetField("state", flags).GetValue(device);
        var readLba = type.GetField("atapiReadLba", flags).GetValue(device);
        var readBlocks = type.GetField("atapiReadBlocks", flags).GetValue(device);
        var response = (byte[])type.GetField("atapiResponse", flags).GetValue(device);
        return string.Format(
            "state {0}, LBA {1}, blocks {2}, response {3} bytes",
            state,
            readLba,
            readBlocks,
            response == null ? 0 : response.Length);
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: AtapiCdromProbe-B41 <Windows CD/DVD drive, e.g. N:\\>");

            var driveName = AtapiPasser.NormalizeDriveName(args[0]);
            var device = new AtaDevice(0x10);
            try
            {
                device.DeviceInfo.ConfigureCdromDrive(driveName);
                device.Open();

                SelectSlave(device);
                device.write(AtaReg.CommandStatus, 0xA1); // IDENTIFY PACKET DEVICE
                var identifyWord0 = device.read_data();
                Check(identifyWord0 == 0x8580, "ATAPI identify word 0 is not removable CD/DVD.");
                ReadData(device, 510);

                SendPacket(device, new byte[] { 0x12, 0, 0, 0, 36, 0, 0, 0, 0, 0, 0, 0 });
                var inquiry = ReadData(device, 36);
                Check(inquiry[0] == 0x05, "INQUIRY does not identify a CD/DVD device.");
                Check((inquiry[1] & 0x80) != 0, "INQUIRY does not mark the medium removable.");
                Check(Encoding.ASCII.GetString(inquiry, 8, 8).Trim() == "ZXMAK2", "INQUIRY vendor is wrong.");
                Check(Encoding.ASCII.GetString(inquiry, 16, 16).Trim() == "CD/DVD-ROM", "INQUIRY product is wrong.");

                SendPacket(device, new byte[] { 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                Check((device.read(AtaReg.CommandStatus) & (byte)HD_STATUS.STATUS_ERR) != 0,
                    "Unsupported packet command did not fail.");
                var illegalSense = RequestSense(device);
                Check((illegalSense[2] & 0x0F) == 0x05 && illegalSense[12] == 0x20,
                    "Unsupported packet command did not return ILLEGAL REQUEST/INVALID COMMAND.");

                SendPacket(device, new byte[] { 0x00, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                var status = device.read(AtaReg.CommandStatus);
                if ((status & (byte)HD_STATUS.STATUS_ERR) != 0)
                {
                    var noMediumSense = RequestSense(device);
                    Check((noMediumSense[2] & 0x0F) == 0x02 && noMediumSense[12] == 0x3A,
                        "Empty optical drive did not report NOT READY/MEDIUM NOT PRESENT.");
                }
                else
                {
                    SendPacket(device, new byte[] { 0x25, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                    var capacity = ReadData(device, 8);
                    Check(capacity[4] == 0 && capacity[5] == 0 && capacity[6] == 8 && capacity[7] == 0,
                        "READ CAPACITY did not report 2048-byte optical blocks.");
                    SendPacket(device, new byte[] { 0x28, 0, 0, 0, 0, 16, 0, 0, 1, 0, 0, 0 });
                    var readStartStatus = device.read(AtaReg.CommandStatus);
                    Check((readStartStatus & (byte)HD_STATUS.STATUS_DRQ) != 0 &&
                        (readStartStatus & (byte)HD_STATUS.STATUS_ERR) == 0,
                        string.Format("READ (10) did not start a data phase: status #{0:X2}, {1}.",
                            readStartStatus,
                            DescribeAtapiTransfer(device)));
                    var sector = ReadData(device, 2048);
                    var readStatus = device.read(AtaReg.CommandStatus);
                    if ((readStatus & (byte)HD_STATUS.STATUS_ERR) != 0)
                    {
                        var readSense = RequestSense(device);
                        Check(false, string.Format(
                            "READ (10) reported status #{0:X2}, sense #{1:X2}/#{2:X2}/#{3:X2} for a ready optical medium.",
                            readStatus, readSense[2] & 0x0F, readSense[12], readSense[13]) +
                            " Transfer before sense: " + DescribeAtapiTransfer(device));
                    }
                    Check(true, "READ (10) completed without an error.");
                    Check(sector.Length == 2048, "READ (10) did not return one complete optical block.");
                }

                var current = new IdePentEvo();
                var pending = new IdePentEvo();
                current.ConfigureCdRom(driveName);
                pending.ConfigureCdRom(driveName);
                var isMediaChanged = typeof(FormMachineSettings).GetMethod(
                    "IsIdeMediaChanged",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Check(isMediaChanged != null, "IDE media comparison is missing.");
                Check(!(bool)isMediaChanged.Invoke(null, new object[] { current, pending }),
                    "An unchanged CD/DVD drive was reported as replaced.");
                pending.DisconnectCdRom();
                Check((bool)isMediaChanged.Invoke(null, new object[] { current, pending }),
                    "CD/DVD eject was not reported as a cold-cycle media change.");

                var document = new XmlDocument();
                var item = document.CreateElement("Device");
                document.AppendChild(item);
                current.SaveConfigXml(item);
                Check(bool.Parse(item.GetAttribute("ideCdConfigured")),
                    "Connected CD/DVD drive was not persisted as enabled.");
                Check(item.GetAttribute("ideCdDrive") == driveName,
                    "Connected CD/DVD drive was not persisted.");
                var restored = new IdePentEvo();
                restored.LoadConfigXml(item);
                Check(restored.CdRom.IsCdrom && restored.CdRom.FileName == driveName,
                    "Connected CD/DVD drive did not survive profile reload.");
                restored.DisconnectCdRom();
                var disconnectedDocument = new XmlDocument();
                var disconnectedItem = disconnectedDocument.CreateElement("Device");
                disconnectedDocument.AppendChild(disconnectedItem);
                restored.SaveConfigXml(disconnectedItem);
                Check(!bool.Parse(disconnectedItem.GetAttribute("ideCdConfigured")) &&
                    disconnectedItem.GetAttribute("ideCdDrive") == string.Empty,
                    "Disconnected CD/DVD drive was not saved as disabled.");

                Console.WriteLine("AtapiCdromProbe-B41: {0} PASS", s_checks);
                return 0;
            }
            finally
            {
                device.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
