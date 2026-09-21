using System;
using System.IO;
using System.Reflection;

internal static class ContentionFloatingBusProbeB40
{
    private const BindingFlags AnyInstance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        s_checks++;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        return target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);
    }

    private static void SetField(Type type, object target, string name, object value)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, AnyInstance);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static long MasterAt(int line, int horizontal,
        int lineCycles, int frameCycles, int intCycle)
    {
        long cycle = line * (long)lineCycles + horizontal - intCycle;
        cycle %= frameCycles;
        if (cycle < 0)
            cycle += frameCycles;
        return cycle * 4;
    }

    private static void SetProfile(Type memoryType, object memory,
        Type rasterType, int rasterMode, int videoMode, int multiplier)
    {
        object raster = rasterType.GetMethod("ForMode").Invoke(null,
            new object[] { rasterMode });
        SetField(memoryType, memory, "m_dramRaster", raster);
        SetField(memoryType, memory, "m_dramRasterOrigin", 0L);
        SetField(memoryType, memory, "m_pFF77", videoMode & 7);
        SetField(memoryType, memory, "m_activeCpuClockMultiplier", multiplier);
    }

    private static int Wait(object memory, ushort address, bool io, long tact)
    {
        return (int)Invoke(memory, "GetContentionWait", address, io, tact);
    }

    private static void TestContention(Assembly hardware)
    {
        Type memoryType = hardware.GetType(
            "ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
        Type rasterType = hardware.GetType(
            "ZXMAK2.Hardware.Evo.EvoRasterTiming", true);
        object memory = Activator.CreateInstance(memoryType);

        int[] lineCycles = { 448, 448, 448, 456 };
        int[] lines = { 320, 262, 312, 311 };
        int[] firstWide = { 76, 42, 60, 59 };
        int[] intLine = { 0, 0, 1, 1 };
        int[] intHorizontal = { 2, 2, 126, 130 };
        int[] expected = { 48, 40, 32, 24, 16, 8, 0, 0 };

        foreach (int mode in new int[] { 2, 3 })
        {
            int frameCycles = lineCycles[mode] * lines[mode];
            int intCycle = intLine[mode] * lineCycles[mode] +
                intHorizontal[mode];
            SetProfile(memoryType, memory, rasterType, mode, 0, 1);
            long start = MasterAt(firstWide[mode], 128,
                lineCycles[mode], frameCycles, intCycle);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                Check(Wait(memory, 0x4000, false, start + slot * 8L) ==
                    expected[slot], "Wrong 48K contention phase.");
            }
            Check(Wait(memory, 0x4000, false, start + 1) == 47,
                "Master-clock contention edge was rounded.");
            Check(Wait(memory, 0x3FFF, false, start) == 0,
                "Uncontended low window was delayed.");
            Check(Wait(memory, 0x8000, false, start) == 0,
                "Uncontended middle window was delayed.");
            Check(Wait(memory, 0x00FE, true, start) == 48,
                "Even I/O contention is missing.");
            Check(Wait(memory, 0x00FF, true, start) == 0,
                "Odd I/O port was contended.");
            Check(Wait(memory, 0x4000, false, start - 4) == 0,
                "Contention began before hcount 128.");
            Check(Wait(memory, 0x4000, false,
                MasterAt(firstWide[mode] - 1, 128,
                    lineCycles[mode], frameCycles, intCycle)) == 0,
                "Contention escaped the vertical pixel window.");

            SetField(memoryType, memory, "m_cmr0", (byte)0);
            Check(Wait(memory, 0xC000, false, start) == 0,
                "Even 128K page was contended.");
            SetField(memoryType, memory, "m_cmr0", (byte)1);
            Check(Wait(memory, 0xC000, false, start) ==
                (mode == 3 ? 48 : 0), "128K high-window decode mismatch.");

            foreach (int multiplier in new int[] { 2, 4 })
            {
                SetProfile(memoryType, memory, rasterType,
                    mode, 0, multiplier);
                Check(Wait(memory, 0x4000, false, start) == 0,
                    "Contention affected turbo mode.");
            }
        }

        foreach (int mode in new int[] { 0, 1 })
        {
            SetProfile(memoryType, memory, rasterType, mode, 0, 1);
            Check(Wait(memory, 0x4000, false, 0) == 0,
                "Pentagon/60 Hz profile enabled Spectrum contention.");
        }

        // Pentagon picture modes begin four lines after wide ATM modes.
        SetProfile(memoryType, memory, rasterType, 2, 3, 1);
        int fc = lineCycles[2] * lines[2];
        int ic = intLine[2] * lineCycles[2] + intHorizontal[2];
        Check(Wait(memory, 0x4000, false,
            MasterAt(firstWide[2], 128, lineCycles[2], fc, ic)) == 0,
            "Pentagon contention used the ATM vertical window.");
        Check(Wait(memory, 0x4000, false,
            MasterAt(firstWide[2] + 4, 128, lineCycles[2], fc, ic)) == 48,
            "Pentagon contention vertical phase mismatch.");
    }

    private static void TestOpenBus(Assembly engine)
    {
        Type busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
        object bus = Activator.CreateInstance(busType);
        object cpu = busType.GetProperty("Cpu").GetValue(bus, null);
        object events = busType.GetField("m_eventManager", AnyInstance)
            .GetValue(bus);
        MethodInfo readPort = events.GetType().GetMethod("RDPORT", AnyInstance);
        foreach (byte value in new byte[] { 0x00, 0x5A, 0xA5, 0xFF })
        {
            cpu.GetType().GetField("BUS").SetValue(cpu, value);
            byte actual = (byte)readPort.Invoke(events,
                new object[] { (ushort)0x1235 });
            Check(actual == value,
                "Unclaimed I/O read did not preserve the free data bus.");
        }
    }

    public static int Main(string[] args)
    {
        try
        {
            string release = Path.GetFullPath(args[0]);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(
                object sender, ResolveEventArgs e)
            {
                string path = Path.Combine(release,
                    new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
            Assembly hardware = Assembly.LoadFrom(
                Path.Combine(release, "ZXMAK2.Hardware.dll"));
            Assembly engine = Assembly.LoadFrom(
                Path.Combine(release, "ZXMAK2.Engine.dll"));
            TestContention(hardware);
            TestOpenBus(engine);
            Console.WriteLine("PASS: {0} B40 contention/open-bus checks.",
                s_checks);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
