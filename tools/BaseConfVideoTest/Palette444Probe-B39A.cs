using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;


internal static class Palette444ProbeB39A
{
    private const BindingFlags Instance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static FieldInfo FindField(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
            if (field != null)
                return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, Instance);
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, name);
        return method.Invoke(target, args);
    }

    private static int ActiveLowPair(int source, int highBit, int lowBit)
    {
        return ((((source >> highBit) & 1) ^ 1) << 1) |
            (((source >> lowBit) & 1) ^ 1);
    }

    private static uint Expected444(ushort addr, byte value)
    {
        var red = (ActiveLowPair(value, 1, 6) << 2) |
            ActiveLowPair(addr, 9, 14);
        var green = (ActiveLowPair(value, 4, 7) << 2) |
            ActiveLowPair(addr, 12, 15);
        var blue = (ActiveLowPair(value, 0, 5) << 2) |
            ActiveLowPair(addr, 8, 13);
        return 0xFF000000U | ((uint)(red * 17) << 16) |
            ((uint)(green * 17) << 8) | (uint)(blue * 17);
    }

    private static uint ExpectedLegacy(byte raw)
    {
        byte atm = (byte)((raw & 3) | ((raw >> 2) & 0xFC));
        uint value = (uint)(atm ^ 0xFF);
        uint packed =
            ((value & 0x20) << 1) |
            ((value & 0x10) >> 1) |
            ((value & 0x08) >> 3) |
            ((value & 0x04) << 5) |
            ((value & 0x02) << 3) |
            ((value & 0x01) << 1);
        uint green = ((packed >> 6) & 3) * 85;
        uint red = ((packed >> 3) & 3) * 85;
        uint blue = (packed & 3) * 85;
        return 0xFF000000U | (red << 16) | (green << 8) | blue;
    }

    private static byte Expected444Readback(uint color)
    {
        var red = ((int)(color >> 16) & 0xFF) / 17;
        var green = ((int)(color >> 8) & 0xFF) / 17;
        var blue = ((int)color & 0xFF) / 17;
        return (byte)(
            (((~green) & 1) << 7) |
            (((~red) & 1) << 6) |
            (((~blue) & 1) << 5) |
            (((~green >> 1) & 1) << 4) |
            0x0C |
            (((~red >> 1) & 1) << 1) |
            ((~blue >> 1) & 1));
    }

    private static byte ExpectedLegacyReadback(byte raw)
    {
        byte atm = (byte)((raw & 3) | ((raw >> 2) & 0xFC));
        return (byte)(((atm & 0x38) << 2) |
            ((atm & 0x04) << 2) | 0x0C | (atm & 3));
    }

    private static ushort AddressFromPattern(int pattern)
    {
        int[] lines = { 8, 9, 12, 13, 14, 15 };
        var address = 0x00FF;
        for (var bit = 0; bit < lines.Length; bit++)
        {
            if ((pattern & (1 << bit)) != 0)
                address |= 1 << lines[bit];
        }
        return (ushort)address;
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: Palette444Probe-B39A <release-directory>");
            var release = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                var file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };

            var hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            var engine = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Engine.dll"));
            var ulaType = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
            var memoryType = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
            var memoryBase = memoryType.BaseType;
            var ula = Activator.CreateInstance(ulaType);
            var memory = FormatterServices.GetUninitializedObject(memoryType);

            var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
            var bus = Activator.CreateInstance(busType);
            var cpu = busType.GetProperty("Cpu").GetValue(bus, null);
            FindField(ulaType, "CPU").SetValue(ula, cpu);
            FindField(memoryType, "m_ulaAtm").SetValue(memory, ula);
            FindField(memoryBase, "m_dosen").SetValue(memory, true);
            FindField(memoryType, "m_aFF77").SetValue(memory, 0);

            FindField(ulaType, "m_borderAttr").SetValue(ula, 5);
            string[] rendererFields = {
                "SpectrumRenderer", "Atm320Renderer", "Atm640Renderer",
                "AtmTxtRenderer", "EvoTxtRenderer", "EvoHwmRenderer",
                "EvoA16Renderer"
            };
            var palettes = new List<uint[]>();
            foreach (var rendererField in rendererFields)
            {
                var renderer = FindField(ulaType, rendererField).GetValue(ula);
                palettes.Add((uint[])renderer.GetType().GetProperty("Palette")
                    .GetValue(renderer, null));
            }

            // D5=0 is the accepted B23/B38 route: address lines have no
            // effect and all seven renderers receive the legacy color.
            FindField(memoryType, "m_pXXBF").SetValue(memory, (byte)0);
            for (var raw = 0; raw < 256; raw++)
            {
                var writeArgs = new object[] { (ushort)0xA5FF, (byte)raw, false };
                Invoke(memory, "BusWritePortXXFF_PAL", writeArgs);
                var expected = ExpectedLegacy((byte)raw);
                for (var renderer = 0; renderer < palettes.Count; renderer++)
                    Check(palettes[renderer][5] == expected,
                        "D5=0 changed renderer " + renderer + " for " + raw.ToString("X2"));
            }

            // D5=1: exhaust every data byte and every combination of the six
            // address bits used by atm_paldatalow in the official RTL.
            FindField(memoryType, "m_pXXBF").SetValue(memory, (byte)0x20);
            for (var raw = 0; raw < 256; raw++)
            {
                for (var pattern = 0; pattern < 64; pattern++)
                {
                    var address = AddressFromPattern(pattern);
                    var writeArgs = new object[] { address, (byte)raw, false };
                    Invoke(memory, "BusWritePortXXFF_PAL", writeArgs);
                    var expected = Expected444(address, (byte)raw);
                    for (var renderer = 0; renderer < palettes.Count; renderer++)
                        Check(palettes[renderer][5] == expected,
                            "4:4:4 mismatch for renderer " + renderer +
                            ", data " + raw.ToString("X2") +
                            ", address " + address.ToString("X4"));

                    var readArgs = new object[] { (ushort)0x0DBD, (byte)0, false };
                    Invoke(memory, "BusReadPortXXBD_BE_CFG", readArgs);
                    Check((bool)readArgs[2] && (byte)readArgs[1] == Expected444Readback(expected),
                        "4:4:4 #0DBD readback mismatch.");
                }
            }

            // D5 only changes the write/read interpretation.  It must not
            // rewrite palette RAM, and after clearing it the inherited latch
            // must still expose the upper pairs from the most recent write.
            var retained = palettes[0][5];
            var lastRaw = (byte)0xFF;
            FindField(memoryType, "m_pXXBF").SetValue(memory, (byte)0);
            Check(palettes[0][5] == retained, "Clearing D5 rewrote palette RAM.");
            var legacyReadArgs = new object[] { (ushort)0x0DBD, (byte)0, false };
            Invoke(memory, "BusReadPortXXBD_BE_CFG", legacyReadArgs);
            Check((byte)legacyReadArgs[1] == ExpectedLegacyReadback(lastRaw),
                "D5=0 did not restore upper-pair readback.");
            FindField(memoryType, "m_pXXBF").SetValue(memory, (byte)0x20);
            Check(palettes[0][5] == retained, "Setting D5 rewrote palette RAM.");

            Console.WriteLine("PASS: " + s_checks +
                " B39A palette checks: unchanged D5=0 route, exhaustive " +
                "4:4:4 data/address vectors, seven renderers, readback and D5 retention.");
            Console.WriteLine("LIMIT: compiled structural probe only; runtime acceptance remains a user test.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
