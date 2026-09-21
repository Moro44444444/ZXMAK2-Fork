using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;


internal static class ConfigPortProbeB35
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
        for (var index = 0; index < pages.Length; index++)
            pages[index] = new byte[0x4000];
        return pages;
    }

    private sealed class Rig
    {
        public object Memory;
        public Type MemoryType;
        public Type MemoryBase;
        public object Ula;
        public Type UlaType;
        public byte[][] Ram;
        public byte[][] Rom;
        public byte[] Trash;
    }

    private static Rig CreateRig(Assembly hardware, Assembly engine)
    {
        var rig = new Rig();
        rig.MemoryType = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
        rig.MemoryBase = rig.MemoryType.BaseType;
        rig.Memory = FormatterServices.GetUninitializedObject(rig.MemoryType);
        rig.Ram = Pages(256);
        rig.Rom = Pages(32);
        rig.Trash = new byte[0x4000];

        rig.MemoryBase.GetField("m_ramPages", Instance).SetValue(rig.Memory, rig.Ram);
        rig.MemoryBase.GetField("m_romPages", Instance).SetValue(rig.Memory, rig.Rom);
        rig.MemoryBase.GetField("m_map48", Instance).SetValue(rig.Memory, new int[4]);
        rig.MemoryBase.GetField("m_trashPage", Instance).SetValue(rig.Memory, rig.Trash);
        rig.MemoryBase.GetField("m_dosen", Instance).SetValue(rig.Memory, true);
        rig.MemoryType.GetField("m_ru2", Instance).SetValue(rig.Memory,
            new[] { 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340 });
        rig.MemoryType.GetField("m_romMask", Instance).SetValue(rig.Memory, 31);
        rig.MemoryType.GetField("m_ramMask", Instance).SetValue(rig.Memory, 255);
        rig.MemoryType.GetField("m_aFF77", Instance).SetValue(rig.Memory, 0);
        rig.MemoryType.GetField("m_pFF77", Instance).SetValue(rig.Memory, 3);

        rig.UlaType = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
        rig.Ula = Activator.CreateInstance(rig.UlaType);
        var ulaBase = hardware.GetType("ZXMAK2.Hardware.UlaDeviceBase", true);
        var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
        var bus = Activator.CreateInstance(busType);
        var cpu = busType.GetProperty("Cpu").GetValue(bus, null);
        ulaBase.GetField("m_memory", Instance).SetValue(rig.Ula, rig.Memory);
        ulaBase.GetField("CPU", Instance).SetValue(rig.Ula, cpu);
        rig.MemoryBase.GetField("m_ula", Instance).SetValue(rig.Memory, rig.Ula);
        rig.MemoryType.GetField("m_ulaAtm", Instance).SetValue(rig.Memory, rig.Ula);
        rig.MemoryType.GetField("m_cpu", Instance).SetValue(rig.Memory, cpu);

        Invoke(rig.Memory, "UpdateMapping");
        return rig;
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, Instance);
        if (method == null)
            throw new MissingMethodException(target.GetType().FullName, name);
        return method.Invoke(target, args);
    }

    private static bool Write(Rig rig, string method, ushort addr, byte value, bool initiallyHandled)
    {
        var args = new object[] { addr, value, initiallyHandled };
        Invoke(rig.Memory, method, args);
        return (bool)args[2];
    }

    private static byte Read(Rig rig, ushort addr, byte initial, bool initiallyHandled, out bool handled)
    {
        var args = new object[] { addr, initial, initiallyHandled };
        Invoke(rig.Memory, "BusReadPortXXBD_BE_CFG", args);
        handled = (bool)args[2];
        return (byte)args[1];
    }

    private static byte ReadBf(Rig rig, ushort addr, byte initial, bool initiallyHandled, out bool handled)
    {
        var args = new object[] { addr, initial, initiallyHandled };
        Invoke(rig.Memory, "BusReadPortXXBF_EVO", args);
        handled = (bool)args[2];
        return (byte)args[1];
    }

    private static void TestBfLatchAndRomWrite(Rig rig)
    {
        for (var written = 0; written < 256; written++)
        {
            Write(rig, "BusWritePortXXBF_EVO", 0xA5BF, (byte)written, false);
            bool handled;
            var read = ReadBf(rig, 0x5ABF, 0xFF, false, out handled);
            Check(handled, "#BF read was not handled.");
            Check(read == (written & 0x3F), "#BF did not return D5:D0 with D7:D6 low.");
        }

        bool guarded;
        var untouched = ReadBf(rig, 0x00BF, 0xA5, true, out guarded);
        Check(guarded && untouched == 0xA5, "#BF overrode an earlier bus responder.");

        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0, false);
        Check(Object.ReferenceEquals(rig.Trash,
            rig.MemoryBase.GetField("MapWrite0000", Instance).GetValue(rig.Memory)),
            "ROM was writable while BF.D1 was clear.");
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 2, false);
        Check(Object.ReferenceEquals(rig.Rom[31],
            rig.MemoryBase.GetField("MapWrite0000", Instance).GetValue(rig.Memory)),
            "BF.D1 did not make the PEN ROM window writable.");
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0, false);
        Check(Object.ReferenceEquals(rig.Trash,
            rig.MemoryBase.GetField("MapWrite0000", Instance).GetValue(rig.Memory)),
            "Clearing BF.D1 did not restore ROM write protection.");

        // Normal mapper: ROMRW remains subordinate to the per-window write-disable latch.
        rig.MemoryType.GetField("m_aFF77", Instance).SetValue(rig.Memory, 0x4100);
        rig.MemoryType.GetField("m_writeDisable", Instance).SetValue(rig.Memory, (byte)0);
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 2, false);
        Check(Object.ReferenceEquals(
            rig.MemoryBase.GetField("MapRead0000", Instance).GetValue(rig.Memory),
            rig.MemoryBase.GetField("MapWrite0000", Instance).GetValue(rig.Memory)),
            "BF.D1 did not make a normally mapped ROM writable.");
        rig.MemoryType.GetField("m_writeDisable", Instance).SetValue(rig.Memory, (byte)1);
        Invoke(rig.Memory, "UpdateMapping");
        Check(Object.ReferenceEquals(rig.Trash,
            rig.MemoryBase.GetField("MapWrite0000", Instance).GetValue(rig.Memory)),
            "#xBF7 write-disable did not override BF.D1.");
        rig.MemoryType.GetField("m_writeDisable", Instance).SetValue(rig.Memory, (byte)0);
        rig.MemoryType.GetField("m_aFF77", Instance).SetValue(rig.Memory, 0);

        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0x3F, false);
        Check((bool)rig.MemoryType.GetProperty("SHADOW").GetValue(rig.Memory, null), "BF.D0 property mismatch.");
        Check((bool)rig.MemoryType.GetProperty("ROMRW").GetValue(rig.Memory, null), "BF.D1 property mismatch.");
        Check((bool)rig.MemoryType.GetProperty("FNTWR").GetValue(rig.Memory, null), "BF.D2 property mismatch.");
        Check((bool)rig.MemoryType.GetProperty("NMIREQ").GetValue(rig.Memory, null), "BF.D3 property mismatch.");
        Check((bool)rig.MemoryType.GetProperty("BreakpointEnabled").GetValue(rig.Memory, null), "BF.D4 property mismatch.");
        Check((bool)rig.MemoryType.GetProperty("Palette444Enabled").GetValue(rig.Memory, null), "BF.D5 property mismatch.");
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

    private static void TestBdRegisters(Rig rig)
    {
        Write(rig, "BusWritePortXXBD_CFG", 0x10BD, 0x34, false);
        Write(rig, "BusWritePortXXBD_CFG", 0x11BD, 0x12, false);
        Check((ushort)rig.MemoryType.GetProperty("BreakpointAddress").GetValue(rig.Memory, null) == 0x1234,
            "#10BD/#11BD did not form the breakpoint address.");

        bool handled;
        Check(Read(rig, 0x10BD, 0, false, out handled) == 0x34 && handled,
            "#10BD breakpoint-low readback mismatch.");
        Check(Read(rig, 0x11BD, 0, false, out handled) == 0x12 && handled,
            "#11BD breakpoint-high readback mismatch.");
        Check(Read(rig, 0x10BE, 0, false, out handled) == 0x34 && handled,
            "Legacy #10BE read alias mismatch.");

        // B39A gives #BF.D5 its documented readback meaning.  Clear it here
        // because this B35 vector verifies the legacy upper-pair path.
        Write(rig, "BusWritePortXXBF_EVO", 0x00BF, 0, false);
        var colors = (byte[])FindField(rig.UlaType, "m_atm_pal").GetValue(rig.Ula);
        FindField(rig.UlaType, "m_borderAttr").SetValue(rig.Ula, 5);
        colors[5] = 0x2D;
        var expectedPalette = (byte)(((0x2D & 0x38) << 2) | ((0x2D & 0x04) << 2) | 0x0C | (0x2D & 3));
        Check(Read(rig, 0x0DBD, 0, false, out handled) == expectedPalette && handled,
            "#0DBD palette readback does not match grbG11RB.");
        Check(Read(rig, 0x0FBD, 0xA0, false, out handled) == 0xA5 && handled,
            "#0FBD did not expose the four-bit border latch.");
        Check(Read(rig, 0x0EBD, 0, false, out handled) == 0xFF && handled,
            "#0EBD non-text font readback must be #FF.");

        var fddValue = Read(rig, 0x13BD, 0x69, false, out handled);
        Check(!handled && fddValue == 0x69, "#13BD was stolen from the FDD-mask device.");
        var undefined = Read(rig, 0x1FBD, 0x96, false, out handled);
        Check(handled && undefined == 0x96, "Undefined #xxBD read did not preserve the open-bus value.");

        Invoke(rig.Memory, "BusReset");
        Check((ushort)rig.MemoryType.GetProperty("BreakpointAddress").GetValue(rig.Memory, null) == 0,
            "Breakpoint address survived reset.");
        bool bfHandled;
        Check(ReadBf(rig, 0x00BF, 0xFF, false, out bfHandled) == 0 && bfHandled,
            "#BF latch survived reset.");
    }

    private static void TestBeExit(Rig rig)
    {
        rig.MemoryType.GetField("m_fddIoRamActive", Instance).SetValue(rig.Memory, true);
        rig.MemoryType.GetField("m_fddIoWriteDisabled", Instance).SetValue(rig.Memory, true);
        Invoke(rig.Memory, "UpdateMapping");
        Check((bool)rig.MemoryType.GetProperty("FddIoRamActive").GetValue(rig.Memory, null),
            "FDD handler did not become active.");
        Check(Write(rig, "BusWritePortXXBE_FDD_EXIT", 0x00BE, 0xA5, false),
            "#BE exit strobe was not handled.");
        Check(!(bool)rig.MemoryType.GetProperty("FddIoRamActive").GetValue(rig.Memory, null),
            "#BE did not leave the page-#FE FDD handler.");
        Check(Write(rig, "BusWritePortXXBE_FDD_EXIT", 0xFFBE, 0, false),
            "Inactive #BE strobe was not decoded.");
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: ConfigPortProbe-B35 <release-directory>");
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
            TestBfLatchAndRomWrite(rig);
            TestBdRegisters(rig);
            TestBeExit(rig);
            Console.WriteLine("PASS: " + s_checks +
                " B35 configuration-port checks: #BF latch/ROMRW, #BD readback/breakpoint and #BE clear.");
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
