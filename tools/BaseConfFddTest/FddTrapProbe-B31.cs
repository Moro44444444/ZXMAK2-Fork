using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;

public static class FddTrapProbeB31
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        checks++;
    }

    private static byte[][] Pages(int count)
    {
        var pages = new byte[count][];
        for (int i = 0; i < pages.Length; i++)
            pages[i] = new byte[0x4000];
        return pages;
    }

    private static object CreateMemory(Assembly hardware, Assembly engine, bool pen2, bool dos, bool romAt0000,
        out Type memoryType, out Type memoryBase, out byte[][] ram, out byte[][] rom, out byte[] trash)
    {
        memoryType = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
        memoryBase = memoryType.BaseType;
        object memory = FormatterServices.GetUninitializedObject(memoryType);

        ram = Pages(256);
        rom = Pages(32);
        trash = new byte[0x4000];
        memoryBase.GetField("m_ramPages", Instance).SetValue(memory, ram);
        memoryBase.GetField("m_romPages", Instance).SetValue(memory, rom);
        memoryBase.GetField("m_map48", Instance).SetValue(memory, new int[4]);
        memoryBase.GetField("m_trashPage", Instance).SetValue(memory, trash);
        memoryBase.GetField("m_dosen", Instance).SetValue(memory, dos);

        memoryType.GetField("m_ru2", Instance).SetValue(memory,
            new int[] { 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340 });
        memoryType.GetField("m_romMask", Instance).SetValue(memory, 31);
        memoryType.GetField("m_ramMask", Instance).SetValue(memory, 255);
        // PEN is active (bit 8 clear). PEN2 is active when bit 14 is clear.
        memoryType.GetField("m_aFF77", Instance).SetValue(memory, pen2 ? 0 : 0x4000);
        memoryType.GetField("m_pFF77", Instance).SetValue(memory, 3);

        Type ulaType = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
        object ula = Activator.CreateInstance(ulaType);
        Type ulaBase = hardware.GetType("ZXMAK2.Hardware.UlaDeviceBase", true);
        Type busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
        object bus = Activator.CreateInstance(busType);
        object cpu = busType.GetProperty("Cpu").GetValue(bus, null);
        ulaBase.GetField("m_memory", Instance).SetValue(ula, memory);
        ulaBase.GetField("CPU", Instance).SetValue(ula, cpu);
        memoryBase.GetField("m_ula", Instance).SetValue(memory, ula);
        memoryType.GetField("m_ulaAtm", Instance).SetValue(memory, ula);

        memoryBase.GetField("MapRead0000", Instance).SetValue(memory, romAt0000 ? rom[0] : ram[0]);
        memoryBase.GetField("MapWrite0000", Instance).SetValue(memory, trash);
        return memory;
    }

    private static void Rejected(Assembly hardware, Assembly engine, bool pen2, bool dos, bool romAt0000, string name)
    {
        Type mt, mb;
        byte[][] ram, rom;
        byte[] trash;
        object memory = CreateMemory(hardware, engine, pen2, dos, romAt0000, out mt, out mb, out ram, out rom, out trash);
        byte[] original = (byte[])mb.GetField("MapRead0000", Instance).GetValue(memory);
        bool entered = (bool)mt.GetMethod("TryEnterFddIoRam").Invoke(memory, null);
        Check(!entered, name + ": trap entered unexpectedly.");
        Check(!(bool)mt.GetProperty("FddIoRamActive").GetValue(memory, null), name + ": active latch changed.");
        Check(Object.ReferenceEquals(original, mb.GetField("MapRead0000", Instance).GetValue(memory)),
            name + ": window #0000 changed.");
    }

    private static void Accepted(Assembly hardware, Assembly engine)
    {
        Type mt, mb;
        byte[][] ram, rom;
        byte[] trash;
        object memory = CreateMemory(hardware, engine, false, true, true, out mt, out mb, out ram, out rom, out trash);
        bool entered = (bool)mt.GetMethod("TryEnterFddIoRam").Invoke(memory, null);
        Check(entered, "DOS+ROM+!PEN2 did not enter the FDD handler.");
        Check((bool)mt.GetProperty("FddIoRamActive").GetValue(memory, null), "FDD handler active latch not set.");
        Check(Object.ReferenceEquals(ram[0xFE], mb.GetField("MapRead0000", Instance).GetValue(memory)),
            "RAM page #FE was not mapped at #0000.");
        Check(Object.ReferenceEquals(trash, mb.GetField("MapWrite0000", Instance).GetValue(memory)),
            "Page #FE was not write-protected before the first M1.");

        object[] args = { (ushort)0, (byte)0 };
        mt.GetMethod("BusReadM1", Instance).Invoke(memory, args);
        Check(Object.ReferenceEquals(ram[0xFE], mb.GetField("MapWrite0000", Instance).GetValue(memory)),
            "Page #FE write protection did not release on the first M1.");
    }

    public static int Main(string[] args)
    {
        try
        {
            string release = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                string file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };

            Assembly hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            Assembly engine = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Engine.dll"));
            Accepted(hardware, engine);
            Rejected(hardware, engine, true, true, true, "PEN2");
            Rejected(hardware, engine, false, false, true, "DOS off");
            Rejected(hardware, engine, false, true, false, "RAM at #0000");
            Console.WriteLine("PASS: " + checks + " B31 FDD trap checks: official DOS+ROM+!PEN2 gate, page #FE map and first-M1 write protection.");
            Console.WriteLine("LIMIT: compiled structural probe only; NedoOS and Rage runtime acceptance remain user tests.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
