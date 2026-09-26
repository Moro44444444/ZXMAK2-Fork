using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Drawing;
using System.Drawing.Imaging;
using ZXMAK2.Engine;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Atm;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Serializers.SnapshotSerializers;

internal static class UlaPlusProbe
{
    private static int checks;
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }

    private static object Field(object target, string name)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance |
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field != null) return field.GetValue(target);
        }
        throw new MissingFieldException(name);
    }

    private static uint Hash(int[] data)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var value in data) hash = (hash ^ (uint)value) * 16777619;
            return hash;
        }
    }

    private static uint Expected(byte value)
    {
        var level = new[] { 0, 36, 73, 109, 146, 182, 219, 255 };
        var blue = new[] { 0, 109, 182, 255 };
        return 0xFF000000U | ((uint)level[(value >> 2) & 7] << 16) |
            ((uint)level[value >> 5] << 8) | (uint)blue[value & 3];
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            var spec = new Spectrum();
            var bus = spec.BusManager;
            bus.Init(spec, true);
            bus.Disconnect();
            bus.Clear();
            var ula = new UlaPentEvo();
            var memory = new MemoryPentEvo();
            bus.Add(memory);
            bus.Add(ula);
            Check(bus.Connect(), "Bus connection failed");
            var renderer = (SpectrumRenderer)Field(ula, "SpectrumRenderer");
            var cpu = bus.Cpu;
            Action<byte, byte> write = (register, data) =>
            {
                cpu.WRPORT(0xBF3B, register);
                cpu.WRPORT(0xFF3B, data);
            };
            for (var i = 0; i < memory.RamPages[5].Length; i++)
                memory.RamPages[5][i] = (byte)(i * 37);
            ula.SetPageMappingAtm(AtmVideoMode.Std256x192, 5, -1, 5, 2, 0);
            ula.ForceRedrawFrame();
            var standard = Hash(ula.VideoData.Buffer);
            Check(!ula.UlaPlusEnabled && !ula.UlaPlusActive, "Default was not disabled");
            write(64, 1);
            write(0, 255);
            Check(!ula.UlaPlusActive && ula.GetUlaPlusPalette()[0] == 0,
                "Disabled ULAplus reacted to software");
            ula.UlaPlusEnabled = true;
            ula.ForceRedrawFrame();
            Check(!ula.UlaPlusActive && Hash(ula.VideoData.Buffer) == standard,
                "Permission alone changed ordinary video");

            for (var i = 0; i < 64; i++)
            for (var value = 0; value < 256; value++)
            {
                write((byte)i, (byte)value);
                Check(cpu.RDPORT(0xFF3B) == value, "Palette readback failed");
                Check(UlaPentEvo.DecodeUlaPlusColor((byte)value) == Expected((byte)value),
                    "GRB conversion failed");
            }
            var palette = new byte[64];
            for (var i = 0; i < 64; i++)
            {
                palette[i] = (byte)(i * 3 + 17);
                write((byte)i, palette[i]);
            }
            ula.ForceRedrawFrame();
            Check(Hash(ula.VideoData.Buffer) == standard,
                "Inactive palette writes changed standard rendering");
            write(64, 1);
            Check(ula.UlaPlusActive && cpu.RDPORT(0xFF3B) == 1, "Mode activation/read failed");
            var ink = (uint[])Field(renderer, "m_ulaInk");
            var paper = (uint[])Field(renderer, "m_ulaPaper");
            for (var attribute = 0; attribute < 256; attribute++)
            {
                var group = (attribute >> 6) * 16;
                Check(ink[attribute] == Expected(palette[group + (attribute & 7)]), "INK lookup");
                Check(paper[attribute] == Expected(palette[group + 8 + ((attribute >> 3) & 7)]), "PAPER lookup");
                Check(ink[attribute] == ink[attribute + 256] &&
                    paper[attribute] == paper[attribute + 256], "FLASH was not disabled");
            }
            for (var border = 0; border < 16; border++)
            {
                renderer.UpdateBorder(border);
                Check((uint)Field(renderer, "m_borderColor") == Expected(palette[8 + (border & 7)]),
                    "Border was not PAPER in CLUT 0");
            }
            renderer.UpdateBorder(0);
            foreach (var mode in new[] { 0, 2, 6, 7, 11, 19 })
            {
                ula.SetPageMappingAtm((AtmVideoMode)mode, 5, -1, 5, 2, 0);
                ula.ForceRedrawFrame();
                var activeHash = Hash(ula.VideoData.Buffer);
                write(64, 0);
                ula.ForceRedrawFrame();
                Check(Hash(ula.VideoData.Buffer) == activeHash, "ULAplus changed extended mode " + mode);
                write(64, 1);
            }
            ula.SetPageMappingAtm(AtmVideoMode.Std256x192, 5, -1, 5, 2, 0);
            ula.ForceRedrawFrame();
            Check(Hash(ula.VideoData.Buffer) != standard, "Active palette did not change the picture");
            var clone = renderer.Clone();
            Check((uint[])Field(clone, "m_ulaInk") != ink &&
                ((uint[])Field(clone, "m_ulaInk"))[255] == ink[255], "Renderer clone lost ULAplus");
            write(128, 0);
            Check(ula.UlaPlusActive, "Reserved group changed the active mode");
            write(64, 0);
            ula.ForceRedrawFrame();
            Check(Hash(ula.VideoData.Buffer) == standard, "Mode off did not restore standard video");

            var screen = new byte[6976];
            for (var i = 0; i < 6144; i++) screen[i] = 0xAA;
            for (var y = 0; y < 24; y++)
            for (var x = 0; x < 32; x++)
                screen[6144 + y * 32 + x] = (byte)(((y / 6) << 6) | (((x + 3) & 7) << 3) | (x & 7));
            Array.Copy(palette, 0, screen, 6912, 64);
            ula.LoadScreenData(new MemoryStream(screen));
            Check(ula.UlaPlusActive, "Extended SCR did not enable ULAplus");
            using (var saved = new MemoryStream())
            {
                ula.SaveScreenData(saved);
                Check(saved.Length == 6976, "SCR save lost palette");
            }
            ula.ForceRedrawFrame();
            if (args.Length > 0)
            {
                File.WriteAllBytes(Path.Combine(args[0], "ULAplus-v55-test.scr"), screen);
                using (var image = new Bitmap(ula.VideoData.Size.Width, ula.VideoData.Size.Height))
                {
                    for (var y = 0; y < image.Height; y++)
                    for (var x = 0; x < image.Width; x++)
                        image.SetPixel(x, y, Color.FromArgb(ula.VideoData.Buffer[y * image.Width + x]));
                    image.Save(Path.Combine(args[0], "ULAplus-v55-test.png"), ImageFormat.Png);
                }
            }
            var serializer = new SzxSerializer(spec);
            using (var snapshot = new MemoryStream())
            {
                serializer.Serialize(snapshot);
                ula.UlaPlusEnabled = false;
                ula.UlaPlusEnabled = true;
                snapshot.Position = 0;
                serializer.Deserialize(snapshot);
                Check(ula.UlaPlusActive && ula.GetUlaPlusPalette()[63] == palette[63],
                    "SZX PLTT restore failed");
                ula.UlaPlusEnabled = false;
                snapshot.Position = 0;
                serializer.Deserialize(snapshot);
                Check(!ula.UlaPlusActive && !ula.UlaPlusEnabled, "SZX bypassed permission");
            }
            ula.LoadScreenData(new MemoryStream(screen));
            Check(!ula.UlaPlusActive, "SCR bypassed permission");
            ula.UlaPlusEnabled = true;
            ula.LoadScreenData(new MemoryStream(screen));
            ula.LoadScreenData(new MemoryStream(new byte[6912]));
            Check(!ula.UlaPlusActive, "Normal SCR did not restore ordinary mode");
            write(64, 1);
            ula.ResetState();
            Check(ula.UlaPlusEnabled && !ula.UlaPlusActive, "Reset did not disable software mode");
            var xml = new XmlDocument();
            var node = xml.AppendChild(xml.CreateElement("Device"));
            ula.SaveConfigXml(node);
            var restored = new UlaPentEvo();
            restored.LoadConfigXml(node);
            Check(restored.UlaPlusEnabled && !restored.UlaPlusActive, "Config persistence failed");
            cpu.Tact = 0;
            ula.BeginFrameTiming(0);
            typeof(UlaDeviceBase).GetMethod("BeginFrame", BindingFlags.Instance |
                BindingFlags.NonPublic).Invoke(ula, null);
            cpu.Tact = 1600;
            write(0, 0xE3);
            Check((int)Field(ula, "m_lastFrameTact") == 200,
                "Palette write did not flush the previous frame segment");
            cpu.Tact = 2400;
            write(64, 1);
            Check((int)Field(ula, "m_lastFrameTact") == 300,
                "Mode write did not flush the previous frame segment");
            cpu.WRPORT(0x003B, 0);
            cpu.WRPORT(0x403B, 0x5A);
            Check(cpu.RDPORT(0x7F3B) == 0x5A, "BaseConf A14/low-byte aliases failed");
            bus.Disconnect();
            Console.WriteLine("PASS: " + checks + " ULAplus ports, GRB, attributes, border, isolation, SCR/SZX, reset/config checks");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
