using System;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;


namespace ZXMAK2.Hardware.Circuits.Ata
{
    public class AtaDeviceInfo
    {
        private const string DefaultSerial = "00000000001234567890";
        private const string DefaultModel = "ZXMAK2 HDD IMAGE";
        
        public string FileName { get; private set; }
        public uint Cylinders { get; private set; }
        public uint Heads { get; private set; }
        public uint Sectors { get; private set; }
        public uint Lba { get; private set; }
        public bool ReadOnly { get; private set; }
        public bool IsCdrom { get; private set; }

        public string SerialNumber { get; private set; }        // 20 chars
        public string FirmwareRevision { get; private set; }    // 8 chars
        public string ModelNumber { get; private set; }         // 40 chars

        
        public AtaDeviceInfo()
        {
            Cylinders = 20; 
            Heads = 16;
            Sectors = 63; 
            Lba = 20160;
            ReadOnly = true;
            IsCdrom = false;
            SerialNumber = DefaultSerial;
            FirmwareRevision = GetVersion();
            ModelNumber = DefaultModel;
        }

        /// <summary>
        /// Configures a raw ATA image.  The image length is the authoritative
        /// LBA size.  A neighbouring .inf file is used only when it contains a
        /// complete, internally consistent CHS description.
        /// </summary>
        public void ConfigureImage(string fileName, bool readOnly)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Disconnect();
                return;
            }

            var fullPath = Path.GetFullPath(fileName);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("HDD image not found", fullPath);
            }

            var length = new FileInfo(fullPath).Length;
            if (length <= 0 || (length & 511L) != 0)
            {
                throw new InvalidDataException("HDD image size must be a positive multiple of 512 bytes");
            }

            var lba64 = length >> 9;
            if (lba64 > UInt32.MaxValue)
            {
                throw new InvalidDataException("HDD image is too large for the emulated ATA controller");
            }

            var lba = (uint)lba64;
            uint cylinders;
            uint heads;
            uint sectors;
            if (!TryReadInfGeometry(Path.ChangeExtension(fullPath, ".inf"), lba,
                out cylinders, out heads, out sectors))
            {
                ChooseCompatibleGeometry(lba, out cylinders, out heads, out sectors);
            }

            var attributes = File.GetAttributes(fullPath);
            var effectiveReadOnly = readOnly || (attributes & FileAttributes.ReadOnly) != 0;
            if (!effectiveReadOnly)
            {
                try
                {
                    using (new FileStream(
                        fullPath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite))
                    {
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    effectiveReadOnly = true;
                }
                catch (IOException)
                {
                    effectiveReadOnly = true;
                }
            }
            Configure(
                fullPath,
                effectiveReadOnly,
                cylinders,
                heads,
                sectors,
                lba);
        }

        public void Disconnect()
        {
            Configure(string.Empty, false, 20, 16, 63, 20160);
        }

        public void Configure(
            string fileName,
            bool readOnly,
            uint cylinders,
            uint heads,
            uint sectors,
            uint lba)
        {
            if (cylinders == 0 || cylinders > UInt16.MaxValue)
                throw new ArgumentOutOfRangeException("cylinders");
            if (heads == 0 || heads > 16)
                throw new ArgumentOutOfRangeException("heads");
            if (sectors == 0 || sectors > Byte.MaxValue)
                throw new ArgumentOutOfRangeException("sectors");
            if (!string.IsNullOrEmpty(fileName) && lba == 0)
                throw new ArgumentOutOfRangeException("lba");

            FileName = fileName ?? string.Empty;
            ReadOnly = readOnly;
            IsCdrom = false;
            Cylinders = cylinders;
            Heads = heads;
            Sectors = sectors;
            Lba = lba;
        }

        private static bool TryReadInfGeometry(
            string fileName,
            uint imageLba,
            out uint cylinders,
            out uint heads,
            out uint sectors)
        {
            cylinders = 0;
            heads = 0;
            sectors = 0;
            if (!File.Exists(fileName))
                return false;

            uint descriptorLba = 0;
            foreach (var line in File.ReadAllLines(fileName))
            {
                var separator = line.IndexOf(':');
                if (separator < 0)
                    continue;

                var key = line.Substring(0, separator).Trim().ToLowerInvariant();
                var match = Regex.Match(line.Substring(separator + 1), @"\d+");
                uint value;
                if (!match.Success || !UInt32.TryParse(match.Value, out value))
                    continue;

                if (key.StartsWith("cylinder") || key.StartsWith("cilinder"))
                    cylinders = value;
                else if (key.StartsWith("head"))
                    heads = value;
                else if (key.StartsWith("sector"))
                    sectors = value;
                else if (key == "lba")
                    descriptorLba = value;
            }

            if (cylinders == 0 || cylinders > UInt16.MaxValue ||
                heads == 0 || heads > 16 ||
                sectors == 0 || sectors > Byte.MaxValue)
                return false;

            var chsLba = (ulong)cylinders * heads * sectors;
            return chsLba == imageLba &&
                (descriptorLba == 0 || descriptorLba == imageLba);
        }

        private static void ChooseCompatibleGeometry(
            uint lba,
            out uint cylinders,
            out uint heads,
            out uint sectors)
        {
            // Prefer the conventional legacy ATA translation (16 heads,
            // 63 sectors) used by ATM/PentEvo images, but keep an exact CHS
            // product when another valid factorisation exists.
            for (uint h = 16; h >= 1; h--)
            {
                for (uint s = 63; s >= 1; s--)
                {
                    var track = h * s;
                    if (lba % track != 0)
                        continue;
                    var c = lba / track;
                    if (c >= 1 && c <= UInt16.MaxValue)
                    {
                        cylinders = c;
                        heads = h;
                        sectors = s;
                        return;
                    }
                }
            }

            heads = 16;
            sectors = 63;
            var rounded = ((ulong)lba + heads * sectors - 1) / (heads * sectors);
            cylinders = (uint)Math.Min(rounded, UInt16.MaxValue);
        }
        
        public void Save(string fileName)
        {
            XmlDocument xml = new XmlDocument();
            XmlNode root = xml.AppendChild(xml.CreateElement("IdeDiskDescriptor"));
            XmlNode imageNode = root.AppendChild(xml.CreateElement("Image"));
            string imageFile = FileName ?? string.Empty;
            if (imageFile != string.Empty &&
                Path.GetDirectoryName(imageFile) == Path.GetDirectoryName(fileName))
            {
                imageFile = Path.GetFileName(imageFile);
            }
            Utils.SetXmlAttribute(imageNode, "fileName", imageFile);
            Utils.SetXmlAttribute(imageNode, "isReadOnly", ReadOnly);
            Utils.SetXmlAttribute(imageNode, "isCdrom", IsCdrom);
            Utils.SetXmlAttribute(imageNode, "serial", SerialNumber);
            Utils.SetXmlAttribute(imageNode, "revision", FirmwareRevision);
            Utils.SetXmlAttribute(imageNode, "model", ModelNumber);
            XmlNode geometryNode = root.AppendChild(xml.CreateElement("Geometry"));
            Utils.SetXmlAttribute(geometryNode, "cylinders", Cylinders);
            Utils.SetXmlAttribute(geometryNode, "heads", Heads);
            Utils.SetXmlAttribute(geometryNode, "sectors", Sectors);
            Utils.SetXmlAttribute(geometryNode, "lba", Lba);
            xml.Save(fileName);
        }

        public void Load(string fileName)
        {
            XmlDocument xml = new XmlDocument();
            xml.Load(fileName);
            XmlNode root = xml["IdeDiskDescriptor"];
            XmlNode imageNode = root["Image"];
            XmlNode geometryNode = root["Geometry"];
            FileName = Utils.GetXmlAttributeAsString(imageNode, "fileName", FileName ?? string.Empty);
            if (FileName != string.Empty && !Path.IsPathRooted(FileName))
                FileName = Utils.GetFullPathFromRelativePath(FileName, Path.GetDirectoryName(fileName));
            SerialNumber = Utils.GetXmlAttributeAsString(imageNode, "serial", SerialNumber);
            FirmwareRevision = Utils.GetXmlAttributeAsString(imageNode, "revision", FirmwareRevision);
            ModelNumber = Utils.GetXmlAttributeAsString(imageNode, "model", ModelNumber);
            IsCdrom = Utils.GetXmlAttributeAsBool(imageNode, "isCdrom", false);
            ReadOnly = Utils.GetXmlAttributeAsBool(imageNode, "isReadOnly", true);
            Cylinders = Utils.GetXmlAttributeAsUInt32(geometryNode, "cylinders", Cylinders);
            Heads = Utils.GetXmlAttributeAsUInt32(geometryNode, "heads", Heads);
            Sectors = Utils.GetXmlAttributeAsUInt32(geometryNode, "sectors", Sectors);
            Lba = Utils.GetXmlAttributeAsUInt32(geometryNode, "lba", Lba);
        }

        private static string GetVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.Revision.ToString();
        }
    }
}
