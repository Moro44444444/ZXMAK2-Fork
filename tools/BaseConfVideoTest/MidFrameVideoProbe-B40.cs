using System;
using System.IO;
using System.Reflection;


internal static class MidFrameVideoProbeB40
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

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: MidFrameVideoProbe-B40 <release-directory>");
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
            var ula = Activator.CreateInstance(ulaType);
            var memory = Activator.CreateInstance(memoryType);
            var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
            var bus = Activator.CreateInstance(busType);
            var cpu = busType.GetProperty("Cpu").GetValue(bus, null);
            var tact = cpu.GetType().GetField("Tact");

            FindField(ulaType, "CPU").SetValue(ula, cpu);
            FindField(ulaType, "m_memory").SetValue(ula, memory);
            FindField(ulaType, "m_rasterOrigin").SetValue(ula, 0L);

            var setMode = ulaType.GetMethod("SetPageMappingAtm", Instance);
            var rendererField = FindField(ulaType, "m_renderer");
            var lastTactField = FindField(ulaType, "m_lastFrameTact");
            Action<int, long> switchMode = delegate(int raw, long masterTact)
            {
                tact.SetValue(cpu, masterTact);
                setMode.Invoke(ula, new object[] {
                    Enum.ToObject(videoModeType, raw), 5, -1, 5, 2, 0
                });
            };

            switchMode(3, 0);
            Check(rendererField.GetValue(ula).GetType().Name == "SpectrumRenderer",
                "Initial ZX route was not selected.");
            switchMode(0, 1600);
            Check(rendererField.GetValue(ula).GetType().Name == "Atm320Renderer",
                "ATM 320 route did not become active at the write tact.");
            Check(!(bool)ulaType.GetProperty("VideoModePending").GetValue(ula, null),
                "Immediate picture-mode write remained pending.");
            Check((int)lastTactField.GetValue(ula) == 200,
                "Old renderer was not flushed through the mode-write tact.");

            switchMode(6, 2400);
            Check(rendererField.GetValue(ula).GetType().Name == "AtmTxtRenderer",
                "ATM text route did not become active mid-frame.");
            Check((int)lastTactField.GetValue(ula) == 300,
                "Mode chain lost its continuous frame position.");
            switchMode(7, 3200);
            Check(rendererField.GetValue(ula).GetType().Name == "EvoTxtRenderer",
                "BaseConf one-page text route did not become active mid-frame.");

            tact.SetValue(cpu, 4000L);
            ulaType.GetMethod("SetPaletteBaseConf6", Instance)
                .Invoke(ula, new object[] { (byte)0x55 });
            Check((int)lastTactField.GetValue(ula) == 500,
                "Six-bit palette write did not split the rendered frame.");
            tact.SetValue(cpu, 4800L);
            ulaType.GetMethod("SetPaletteBaseConf444", Instance)
                .Invoke(ula, new object[] { (ushort)0xA5FF, (byte)0x5A });
            Check((int)lastTactField.GetValue(ula) == 600,
                "4:4:4 palette write did not split the rendered frame.");

            Console.WriteLine("PASS: " + s_checks +
                " B40 mid-frame mode/palette checks.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
