using System;
using System.IO;
using System.Reflection;


internal static class GoldenRendererProbeB40
{
    private const BindingFlags Instance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static int s_checks;

    private sealed class Vector
    {
        public readonly int Selector;
        public readonly string Name;
        public readonly uint Hash;

        public Vector(int selector, string name, uint hash)
        {
            Selector = selector;
            Name = name;
            Hash = hash;
        }
    }

    // FNV-1a hashes are deliberately over the complete native-size buffer.
    // The expected values are populated from the accepted r1364 geometry,
    // page mapping and deterministic RAM pattern below.
    private static readonly Vector[] Vectors = {
        new Vector(3,  "ZX 256x192 attributes", 0x78707931U),
        new Vector(19, "Pentagon 256x192 HWM", 0x1BE1F009U),
        new Vector(11, "Pentagon 256x192 16c", 0xEE3EDB75U),
        new Vector(0,  "ATM 320x200 16c", 0x6AEA819AU),
        new Vector(2,  "ATM 640x200 HWM", 0x9221235DU),
        new Vector(6,  "ATM text 80x25", 0x5D1983FEU),
        new Vector(7,  "BaseConf text 80x25", 0x18A613CEU)
    };

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

    private static uint Hash(int[] values)
    {
        unchecked
        {
            uint hash = 2166136261U;
            for (var i = 0; i < values.Length; i++)
            {
                var value = (uint)values[i];
                hash = (hash ^ (byte)value) * 16777619U;
                hash = (hash ^ (byte)(value >> 8)) * 16777619U;
                hash = (hash ^ (byte)(value >> 16)) * 16777619U;
                hash = (hash ^ (byte)(value >> 24)) * 16777619U;
            }
            return hash;
        }
    }

    private static void FillPages(byte[][] pages)
    {
        for (var page = 0; page < pages.Length; page++)
        for (var offset = 0; offset < pages[page].Length; offset++)
            pages[page][offset] = (byte)((offset * 37 + page * 73 +
                (offset >> 8) * 19) & 0xFF);
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 1 || args.Length > 2)
                throw new ArgumentException(
                    "Usage: GoldenRendererProbe-B40 <release-directory> [--print]");
            var print = args.Length == 2 && args[1] == "--print";
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
            var videoModeType = hardware.GetType("ZXMAK2.Hardware.Atm.AtmVideoMode", true);
            var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
            var ula = Activator.CreateInstance(ulaType);
            var memory = Activator.CreateInstance(memoryType);
            var bus = Activator.CreateInstance(busType);
            var cpu = busType.GetProperty("Cpu").GetValue(bus, null);
            FindField(ulaType, "CPU").SetValue(ula, cpu);
            FindField(ulaType, "m_memory").SetValue(ula, memory);
            FindField(ulaType, "m_rasterOrigin").SetValue(ula, 0L);
            FillPages((byte[][])memoryType.BaseType.GetProperty("RamPages")
                .GetValue(memory, null));

            var setMode = ulaType.GetMethod("SetPageMappingAtm", Instance);
            var force = ulaType.GetMethod("ForceRedrawFrame", Instance);
            var rendererField = FindField(ulaType, "m_renderer");
            foreach (var vector in Vectors)
            {
                setMode.Invoke(ula, new object[] {
                    Enum.ToObject(videoModeType, vector.Selector),
                    5, -1, 5, 2, 0
                });
                force.Invoke(ula, null);
                var renderer = rendererField.GetValue(ula);
                var video = renderer.GetType().GetProperty("VideoData")
                    .GetValue(renderer, null);
                var buffer = (int[])video.GetType().GetProperty("Buffer")
                    .GetValue(video, null);
                var hash = Hash(buffer);
                if (print)
                    Console.WriteLine("{0}: 0x{1:X8}", vector.Name, hash);
                else
                    Check(hash == vector.Hash, vector.Name + ": golden mismatch " +
                        hash.ToString("X8"));
            }
            if (!print)
                Console.WriteLine("PASS: " + s_checks +
                    " B40 seven-renderer golden vectors.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
