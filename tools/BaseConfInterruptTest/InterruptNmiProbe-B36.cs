using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;


internal static class InterruptNmiProbeB36
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static byte[][] Pages(int count)
    {
        var pages = new byte[count][];
        for (var index = 0; index < count; index++)
            pages[index] = new byte[0x4000];
        return pages;
    }

    private sealed class Rig
    {
        public Assembly Engine;
        public object Memory;
        public Type MemoryType;
        public Type MemoryBase;
        public object Ula;
        public Type UlaType;
        public object Cpu;
        public byte[][] Ram;
        public byte[][] Rom;
        public byte[] Trash;
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
        for (var current = target.GetType(); current != null; current = current.BaseType)
        {
            var method = current.GetMethod(name, Instance | BindingFlags.DeclaredOnly);
            if (method != null)
                return method.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().FullName, name);
    }

    private static Rig CreateRig(Assembly hardware, Assembly engine)
    {
        var rig = new Rig();
        rig.Engine = engine;
        rig.MemoryType = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
        rig.MemoryBase = rig.MemoryType.BaseType;
        rig.Memory = FormatterServices.GetUninitializedObject(rig.MemoryType);
        rig.Ram = Pages(256);
        rig.Rom = Pages(32);
        rig.Trash = new byte[0x4000];

        FindField(rig.MemoryType, "m_ramPages").SetValue(rig.Memory, rig.Ram);
        // MemoryPentEvo contains a legacy field with the same name; the
        // MemoryBase property used by CompleteMemoryAccess owns this array.
        rig.MemoryBase.GetField("m_romPages", Instance | BindingFlags.DeclaredOnly)
            .SetValue(rig.Memory, rig.Rom);
        FindField(rig.MemoryType, "m_map48").SetValue(rig.Memory, new int[4]);
        FindField(rig.MemoryType, "m_trashPage").SetValue(rig.Memory, rig.Trash);
        FindField(rig.MemoryType, "m_dosen").SetValue(rig.Memory, true);
        FindField(rig.MemoryType, "m_ru2").SetValue(rig.Memory,
            new[] { 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340 });
        FindField(rig.MemoryType, "m_romMask").SetValue(rig.Memory, 31);
        FindField(rig.MemoryType, "m_ramMask").SetValue(rig.Memory, 255);
        // A8=1 disables PEN and exposes the normal BaseConf mapper.
        FindField(rig.MemoryType, "m_aFF77").SetValue(rig.Memory, 0x4100);
        FindField(rig.MemoryType, "m_pFF77").SetValue(rig.Memory, 3);

        rig.UlaType = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
        rig.Ula = Activator.CreateInstance(rig.UlaType);
        var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
        var bus = Activator.CreateInstance(busType);
        rig.Cpu = busType.GetProperty("Cpu").GetValue(bus, null);
        var ulaBase = hardware.GetType("ZXMAK2.Hardware.UlaDeviceBase", true);
        FindField(ulaBase, "m_memory").SetValue(rig.Ula, rig.Memory);
        FindField(ulaBase, "CPU").SetValue(rig.Ula, rig.Cpu);
        FindField(rig.MemoryType, "m_ula").SetValue(rig.Memory, rig.Ula);
        FindField(rig.MemoryType, "m_ulaAtm").SetValue(rig.Memory, rig.Ula);
        FindField(rig.MemoryType, "m_cpu").SetValue(rig.Memory, rig.Cpu);
        Invoke(rig.Memory, "UpdateMapping");
        return rig;
    }

    private static bool PropertyBool(object target, string name)
    {
        return (bool)target.GetType().GetProperty(name).GetValue(target, null);
    }

    private static int PropertyInt(object target, string name)
    {
        return (int)target.GetType().GetProperty(name).GetValue(target, null);
    }

    private static bool Write(Rig rig, string method, ushort addr, byte value)
    {
        var args = new object[] { addr, value, false };
        Invoke(rig.Memory, method, args);
        return (bool)args[2];
    }

    private static void M1Pre(Rig rig, ushort address)
    {
        var args = new object[] { address, (byte)0xFF };
        Invoke(rig.Memory, "BusReadM1", args);
    }

    private static byte M1PostAndComplete(Rig rig, ushort address, byte initial)
    {
        var args = new object[] { address, initial };
        Invoke(rig.Memory, "BusReadM1AfterMemory", args);
        var value = (byte)args[1];
        var accessType = rig.Engine.GetType("ZXMAK2.Engine.Interfaces.CpuMemoryAccess", true);
        var opcode = Enum.Parse(accessType, "Opcode");
        var completeArgs = new object[] { address, opcode, 0L, value };
        Invoke(rig.Memory, "CompleteMemoryAccess", completeArgs);
        return (byte)completeArgs[3];
    }

    private static object MapRead0000(Rig rig)
    {
        return FindField(rig.MemoryType, "MapRead0000").GetValue(rig.Memory);
    }

    private static void TestInt(Rig rig)
    {
        Invoke(rig.Ula, "BeginFrameTiming", 0L);
        Check((bool)Invoke(rig.Ula, "CheckInt", 0), "INT was not active at int_start.");
        Check((bool)Invoke(rig.Ula, "CheckInt", 255), "INT ended before 256 master clocks.");
        Check(!(bool)Invoke(rig.Ula, "CheckInt", 256), "INT exceeded 256 master clocks.");

        Invoke(rig.Ula, "BeginFrameTiming", 0L);
        Check(!PropertyBool(rig.Ula, "FrameInterruptAcknowledged"), "Frame reset retained INT acknowledge.");
        Invoke(rig.Ula, "BusIntAcknowledge");
        Check(PropertyBool(rig.Ula, "FrameInterruptAcknowledged"), "INT acknowledge was not latched.");
        Check(!(bool)Invoke(rig.Ula, "CheckInt", 1), "INT remained active after acknowledge.");

        Invoke(rig.Ula, "BeginFrameTiming", 0L);
        FindField(rig.Cpu.GetType(), "Tact").SetValue(rig.Cpu, 100L);
        Invoke(rig.Ula, "PauseFrameInterruptForWait", 32);
        Check(PropertyInt(rig.Ula, "FrameInterruptWaitClocks") == 32,
            "External WAIT time was not added to the INT counter.");
        Check((bool)Invoke(rig.Ula, "CheckInt", 287), "WAIT-paused INT ended too early.");
        Check(!(bool)Invoke(rig.Ula, "CheckInt", 288), "WAIT-paused INT ended too late.");
        Invoke(rig.Ula, "BusIntAcknowledge");
        Invoke(rig.Ula, "PauseFrameInterruptForWait", 16);
        Check(PropertyInt(rig.Ula, "FrameInterruptWaitClocks") == 32,
            "WAIT extended an already acknowledged INT pulse.");
    }

    private static void TestDeferredNmi(Rig rig)
    {
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0x08);
        Check(!PropertyBool(rig.Memory, "NmiPending"), "Rising BF.D3 requested NMI.");
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0x00);
        Check(PropertyBool(rig.Memory, "NmiPending"), "Falling BF.D3 did not latch NMI.");
        Invoke(rig.Memory, "BusPreCycle");
        Check(!PropertyBool(rig.Memory, "NmiEntryActive"), "Deferred NMI started before int_start.");
        Invoke(rig.Memory, "BusBeginFrame");
        Check(!PropertyBool(rig.Memory, "NmiPending"), "int_start did not consume pending NMI.");
        Check(PropertyBool(rig.Memory, "NmiEntryActive"), "int_start did not arm NMI entry.");
        Invoke(rig.Memory, "BusPreCycle");
        Check((bool)FindField(rig.Cpu.GetType(), "NMI").GetValue(rig.Cpu), "NMI pin was not asserted.");
        Invoke(rig.Memory, "BusNmiAcknowledge");

        rig.Rom[31][0x0066] = 0xA5;
        rig.Ram[0xFF][0x0067] = 0x67;
        M1Pre(rig, 0x0066);
        Check(M1PostAndComplete(rig, 0x0066, 0xA5) == 0,
            "First #0066 opcode was not forced to NOP.");
        Check(PropertyBool(rig.Memory, "InNmi"), "Page #FF was not enabled after #0066 M1.");
        Check(!PropertyBool(rig.Memory, "NmiEntryActive"), "NMI entry latch survived #0066 M1.");
        Check(Object.ReferenceEquals(MapRead0000(rig), rig.Ram[0xFF]),
            "NMI did not map RAM page #FF into window 0.");
        Check(M1PostAndComplete(rig, 0x0067, 0xFF) == 0x67,
            "Second handler opcode was not read from RAM page #FF.");
    }

    private static void TestBeDelayAndFddPriority(Rig rig)
    {
        FindField(rig.MemoryType, "m_fddIoRamActive").SetValue(rig.Memory, true);
        FindField(rig.MemoryType, "m_fddIoWriteDisabled").SetValue(rig.Memory, true);
        Invoke(rig.Memory, "UpdateMapping");
        Check(Object.ReferenceEquals(MapRead0000(rig), rig.Ram[0xFF]),
            "FDD page #FE overrode active NMI page #FF.");
        Check(Write(rig, "BusWritePortXXBE_FDD_EXIT", 0x00BE, 0), "#BE was not handled in NMI.");
        Check(PropertyInt(rig.Memory, "NmiClearM1Remaining") == 2, "#BE did not arm two-M1 NMI exit.");
        Check(PropertyBool(rig.Memory, "FddIoRamActive"), "#BE incorrectly cleared underlying FDD state in NMI.");

        rig.Ram[0xFF][0x0100] = 0x11;
        rig.Ram[0xFF][0x0101] = 0x22;
        Check(M1PostAndComplete(rig, 0x0100, 0) == 0x11, "First post-#BE opcode missed page #FF.");
        Check(PropertyBool(rig.Memory, "InNmi") && PropertyInt(rig.Memory, "NmiClearM1Remaining") == 1,
            "NMI ended on the first post-#BE M1.");
        Check(M1PostAndComplete(rig, 0x0101, 0) == 0x22, "Second post-#BE opcode missed page #FF.");
        Check(!PropertyBool(rig.Memory, "InNmi"), "NMI page survived the second post-#BE M1.");
        Check(PropertyBool(rig.Memory, "FddIoRamActive"), "NMI exit lost the underlying FDD state.");
        Check(Object.ReferenceEquals(MapRead0000(rig), rig.Ram[0xFE]),
            "NMI exit did not reveal the retained FDD page #FE.");
        Check(Write(rig, "BusWritePortXXBE_FDD_EXIT", 0x00BE, 0), "Second #BE was not handled.");
        Check(!PropertyBool(rig.Memory, "FddIoRamActive"), "Normal #BE did not exit the FDD handler.");
    }

    private static void TestBreakpoint(Assembly hardware, Assembly engine)
    {
        var rig = CreateRig(hardware, engine);
        rig.MemoryType.GetProperty("BreakpointAddress").SetValue(rig.Memory, (ushort)0x4567, null);
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0x10);
        M1Pre(rig, 0x4566);
        Check(!PropertyBool(rig.Memory, "NmiEntryActive"), "Breakpoint fired on a mismatched M1.");
        M1Pre(rig, 0x4567);
        Check(PropertyBool(rig.Memory, "NmiEntryActive"), "Breakpoint did not start immediate NMI.");
        Check(!PropertyBool(rig.Memory, "NmiPending"), "Breakpoint was incorrectly deferred to int_start.");
        Check(PropertyBool(rig.Memory, "BreakpointEnabled"), "Breakpoint disabled itself after matching.");
        Invoke(rig.Memory, "BusPreCycle");
        Check((bool)FindField(rig.Cpu.GetType(), "NMI").GetValue(rig.Cpu),
            "Immediate breakpoint NMI did not assert the NMI pin.");
    }

    private static void TestExternalDeferredNmi(Assembly hardware, Assembly engine)
    {
        var rig = CreateRig(hardware, engine);
        var argsType = engine.GetType("ZXMAK2.Engine.Interfaces.BusCancelArgs", true);
        var first = Activator.CreateInstance(argsType);
        Invoke(rig.Memory, "BusNmiRequest", first);
        Check((bool)argsType.GetProperty("Cancel").GetValue(first, null),
            "External NMI bypassed BaseConf frame alignment.");
        Check(PropertyBool(rig.Memory, "NmiPending"), "External NMI was not latched.");
        Invoke(rig.Memory, "BusBeginFrame");
        var second = Activator.CreateInstance(argsType);
        Invoke(rig.Memory, "BusNmiRequest", second);
        Check(!(bool)argsType.GetProperty("Cancel").GetValue(second, null),
            "Armed frame-aligned NMI was not released to the CPU.");
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: InterruptNmiProbe-B36 <release-directory>");
            var release = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                var file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };

            var hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            var engine = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Engine.dll"));
            var rig = CreateRig(hardware, engine);
            TestInt(rig);
            TestDeferredNmi(rig);
            TestBeDelayAndFddPriority(rig);
            TestBreakpoint(hardware, engine);
            TestExternalDeferredNmi(hardware, engine);
            Console.WriteLine("PASS: " + s_checks +
                " B36 INT/NMI checks: acknowledge, WAIT pause, frame NMI, breakpoint, #0066/#FF and #BE delay.");
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
