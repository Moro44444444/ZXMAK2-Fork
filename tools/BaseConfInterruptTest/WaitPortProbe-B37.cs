using System;
using System.IO;
using System.Reflection;


internal static class WaitPortProbeB37
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
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
        for (var current = target.GetType(); current != null; current = current.BaseType)
        {
            var method = current.GetMethod(name, Instance | BindingFlags.DeclaredOnly);
            if (method != null)
                return method.Invoke(target, args);
        }
        throw new MissingMethodException(target.GetType().FullName, name);
    }

    private static void SetNormal(object memory, bool cmosEnabled)
    {
        // A8=1 keeps the memory manager enabled; A9=1 disables CPM.
        FindField(memory.GetType(), "m_aFF77").SetValue(memory, 0x0300);
        FindField(memory.GetType(), "m_pXXBF").SetValue(memory, (byte)0);
        FindField(memory.GetType(), "m_pEFF7").SetValue(memory,
            (byte)(cmosEnabled ? 0x80 : 0));
        FindField(memory.GetType(), "m_dosen").SetValue(memory, false);
    }

    private static void SetShadow(object memory)
    {
        FindField(memory.GetType(), "m_aFF77").SetValue(memory, 0x0300);
        FindField(memory.GetType(), "m_pXXBF").SetValue(memory, (byte)1);
        FindField(memory.GetType(), "m_pEFF7").SetValue(memory, (byte)0);
        FindField(memory.GetType(), "m_dosen").SetValue(memory, false);
    }

    private static bool IsAvrWait(object memory, ushort port)
    {
        return (bool)Invoke(memory, "IsAvrWaitPort", port);
    }

    private static void TestDecode(object memory)
    {
        SetNormal(memory, false);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            bool expected = (port & 0xFF) == 0xEF;
            Check(IsAvrWait(memory, (ushort)port) == expected,
                "Normal CMOS-off WAIT decode mismatch at #" + port.ToString("X4"));
        }

        SetNormal(memory, true);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            bool expected = (port & 0xFF) == 0xEF ||
                ((port & 0xFF) == 0xF7 && (port & 0x0100) != 0 &&
                 (port & 0x4000) == 0);
            Check(IsAvrWait(memory, (ushort)port) == expected,
                "Normal CMOS-on WAIT decode mismatch at #" + port.ToString("X4"));
        }

        SetShadow(memory);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            bool expected = (port & 0xFF) == 0xEF ||
                ((port & 0xFF) == 0xF7 && (port & 0x0100) == 0 &&
                 (port & 0x4000) == 0);
            Check(IsAvrWait(memory, (ushort)port) == expected,
                "Shadow WAIT decode mismatch at #" + port.ToString("X4"));
        }
    }

    private static byte ReadPort(object cmos, string method, ushort port, out bool handled)
    {
        var args = new object[] { port, (byte)0xCC, false };
        Invoke(cmos, method, args);
        handled = (bool)args[2];
        return (byte)args[1];
    }

    private static bool WritePort(object cmos, string method, ushort port, byte value)
    {
        var args = new object[] { port, value, false };
        Invoke(cmos, method, args);
        return (bool)args[2];
    }

    private static void TestF7AndUart(object memory, Type cmosType)
    {
        var cmos = Activator.CreateInstance(cmosType);
        FindField(cmosType, "mem").SetValue(cmos, memory);
        Invoke(cmos, "Reset");

        SetNormal(memory, true);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            if ((port & 0xFF) != 0xF7)
                continue; // EventManager low-byte subscription rejects it.
            bool handled;
            ReadPort(cmos, "RdPortF7", (ushort)port, out handled);
            bool expected = (port & 0x0100) != 0 &&
                (port & 0x4000) == 0;
            Check(handled == expected,
                "Normal F7 read equation mismatch at #" + port.ToString("X4"));
        }

        SetShadow(memory);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            if ((port & 0xFF) != 0xF7)
                continue; // EventManager low-byte subscription rejects it.
            bool handled;
            ReadPort(cmos, "RdPortF7", (ushort)port, out handled);
            bool expected = (port & 0x0100) == 0 &&
                (port & 0x4000) == 0;
            Check(handled == expected,
                "Shadow F7 read equation mismatch at #" + port.ToString("X4"));
        }

        SetNormal(memory, true);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            if ((port & 0xFF) != 0xF7)
                continue;
            bool handled = WritePort(cmos, "WrPortF7", (ushort)port, 0xAA);
            bool expected = (port & 0x0100) != 0 &&
                ((port & 0x2000) == 0 || (port & 0x4000) == 0);
            Check(handled == expected,
                "Normal F7 write equation mismatch at #" + port.ToString("X4"));
        }

        SetShadow(memory);
        for (var port = 0; port <= 0xFFFF; port++)
        {
            if ((port & 0xFF) != 0xF7)
                continue;
            bool handled = WritePort(cmos, "WrPortF7", (ushort)port, 0xAA);
            bool expected = (port & 0x0100) == 0 &&
                ((port & 0x2000) == 0 || (port & 0x4000) == 0);
            Check(handled == expected,
                "Shadow F7 write equation mismatch at #" + port.ToString("X4"));
        }

        // Address-only, data-only and the dual-strobe alias.
        Check(WritePort(cmos, "WrPortF7", 0xDEF7, 0xFE), "#DEF7 address write missed.");
        Check(WritePort(cmos, "WrPortF7", 0xBEF7, 0x31), "#BEF7 data write missed.");
        Check(WritePort(cmos, "WrPortF7", 0x9EF7, 0xFE), "F7 dual-strobe alias missed.");
        Check(!WritePort(cmos, "WrPortF7", 0xEFF7, 0x55), "#EFF7 was stolen by AVR.");

        Invoke(cmos, "ResetRs232");
        bool h;
        Check(ReadPort(cmos, "RdComPort", 0xF8EF, out h) == 0 && h,
            "Reset DAT value/handling mismatch.");
        Check(ReadPort(cmos, "RdComPort", 0xFAEF, out h) == 0x01, "Reset ISR mismatch.");
        Check(ReadPort(cmos, "RdComPort", 0xFDEF, out h) == 0x60, "Reset LSR mismatch.");
        Check(ReadPort(cmos, "RdComPort", 0xFEEF, out h) == 0xA0, "Reset MSR mismatch.");
        Check(ReadPort(cmos, "RdComPort", 0xFFEF, out h) == 0xFF, "Reset SCR mismatch.");

        WritePort(cmos, "WrComPort", 0xFBEF, 0x80);
        WritePort(cmos, "WrComPort", 0xF8EF, 0x34);
        WritePort(cmos, "WrComPort", 0xF9EF, 0x12);
        Check(ReadPort(cmos, "RdComPort", 0x18EF, out h) == 0x34,
            "DLL or low-byte EF alias mismatch.");
        Check(ReadPort(cmos, "RdComPort", 0xA9EF, out h) == 0x12,
            "DLM or low-byte EF alias mismatch.");
        WritePort(cmos, "WrComPort", 0xFBEF, 0x00);
        WritePort(cmos, "WrComPort", 0xF9EF, 0xFF);
        Check(ReadPort(cmos, "RdComPort", 0xF9EF, out h) == 0x0F, "IER mask mismatch.");
        WritePort(cmos, "WrComPort", 0xFCEF, 0xFF);
        Check(ReadPort(cmos, "RdComPort", 0xFCEF, out h) == 0x1F, "MCR mask mismatch.");
        WritePort(cmos, "WrComPort", 0xFFEF, 0x5A);
        Check(ReadPort(cmos, "RdComPort", 0x07EF, out h) == 0x5A, "SCR alias mismatch.");

        // All 256 high-byte aliases select the UART register with A10:A8.
        Invoke(cmos, "ResetRs232");
        WritePort(cmos, "WrComPort", 0xF9EF, 0x03);
        WritePort(cmos, "WrComPort", 0xFBEF, 0x24);
        WritePort(cmos, "WrComPort", 0xFCEF, 0x05);
        WritePort(cmos, "WrComPort", 0xFFEF, 0x7A);
        byte[] expectedRegisters = { 0x00, 0x03, 0x01, 0x24, 0x05, 0x60, 0xA0, 0x7A };
        for (var high = 0; high < 256; high++)
        {
            ushort port = (ushort)((high << 8) | 0xEF);
            Check(ReadPort(cmos, "RdComPort", port, out h) == expectedRegisters[high & 7] && h,
                "COM read alias/register mismatch at #" + port.ToString("X4"));
        }

        // Write aliases are checked through the scratch register because it
        // is side-effect-free and covers every A15:A11 combination.
        for (var high = 7; high < 256; high += 8)
        {
            ushort port = (ushort)((high << 8) | 0xEF);
            byte marker = (byte)(high ^ 0xA5);
            Check(WritePort(cmos, "WrComPort", port, marker),
                "COM write alias was not handled at #" + port.ToString("X4"));
            Check(ReadPort(cmos, "RdComPort", 0xFFEF, out h) == marker,
                "COM write alias selected the wrong register at #" + port.ToString("X4"));
        }
    }

    private static void TestWaitLifecycle(object memory, Assembly hardware, Assembly engine)
    {
        var ulaType = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
        var ula = Activator.CreateInstance(ulaType);
        var busType = engine.GetType("ZXMAK2.Engine.BusManager", true);
        var bus = Activator.CreateInstance(busType);
        var cpu = busType.GetProperty("Cpu").GetValue(bus, null);
        var ulaBase = hardware.GetType("ZXMAK2.Hardware.UlaDeviceBase", true);
        FindField(ulaBase, "CPU").SetValue(ula, cpu);
        FindField(ulaBase, "m_memory").SetValue(ula, memory);
        FindField(memory.GetType(), "m_ulaAtm").SetValue(memory, ula);
        FindField(memory.GetType(), "m_ula").SetValue(memory, ula);

        SetNormal(memory, true);
        FindField(memory.GetType(), "m_activeCpuClockMultiplier").SetValue(memory, 1);
        Invoke(ula, "BeginFrameTiming", 0L);
        FindField(cpu.GetType(), "Tact").SetValue(cpu, 100L);
        Check((int)Invoke(memory, "GetPortWait", (ushort)0xBFF7) == 1,
            "AVR data WAIT was not one synchronous handshake clock at 3.5 MHz.");
        Check((int)ulaType.GetProperty("FrameInterruptWaitClocks").GetValue(ula, null) == 1,
            "AVR WAIT did not pause the B36 INT counter.");
        Check((int)Invoke(memory, "GetPortWait", (ushort)0xF8EF) == 1,
            "COM WAIT missing.");
        Check((int)ulaType.GetProperty("FrameInterruptWaitClocks").GetValue(ula, null) == 2,
            "COM WAIT did not pause the B36 INT counter.");

        FindField(memory.GetType(), "m_activeCpuClockMultiplier").SetValue(memory, 4);
        Check((int)Invoke(memory, "GetPortWait", (ushort)0xFFFD) == 6,
            "Existing 14 MHz AY WAIT changed.");
        Check((int)ulaType.GetProperty("FrameInterruptWaitClocks").GetValue(ula, null) == 2,
            "Ordinary AY WAIT incorrectly paused frame INT.");
        Check((int)Invoke(memory, "GetPortWait", (ushort)0xFDEF) == 1,
            "COM WAIT changed with CPU multiplier.");
        Invoke(ula, "BusIntAcknowledge");
        int acknowledgedWait = (int)ulaType.GetProperty("FrameInterruptWaitClocks")
            .GetValue(ula, null);
        Invoke(memory, "GetPortWait", (ushort)0xF8EF);
        Check((int)ulaType.GetProperty("FrameInterruptWaitClocks").GetValue(ula, null) ==
            acknowledgedWait, "External WAIT extended an acknowledged INT.");
    }

    private static void TestDosStall(object memory, Assembly engine)
    {
        var accessType = engine.GetType("ZXMAK2.Engine.Interfaces.CpuMemoryAccess", true);
        var opcode = Enum.Parse(accessType, "Opcode");
        var read = Enum.Parse(accessType, "Read");

        SetNormal(memory, false);
        var ru2 = (int[])FindField(memory.GetType(), "m_ru2").GetValue(memory);
        ru2[4] = 0x300; // XOR descriptor is ROM with the DOS-entry gate set.
        FindField(memory.GetType(), "m_cmr0").SetValue(memory, (byte)0x10);
        var m1Args = new object[] { (ushort)0x3D00, (byte)0xFF };
        Invoke(memory, "BusReadM1", m1Args);
        Check((bool)FindField(memory.GetType(), "m_dosEntryStallPending").GetValue(memory),
            "#3Dxx M1 did not arm DOS settling stall.");
        Check((bool)memory.GetType().GetProperty("DOSEN").GetValue(memory, null),
            "#3Dxx M1 did not enter DOS mapping.");

        Check((int)Invoke(memory, "ConsumeDosEntryStall", read, 2) == 2,
            "Non-M1 consumed DOS stall.");
        Check((bool)FindField(memory.GetType(), "m_dosEntryStallPending").GetValue(memory),
            "Non-M1 cleared DOS stall.");
        Check((int)Invoke(memory, "ConsumeDosEntryStall", opcode, 0) == 3,
            "DOS entry did not stall for three master clocks.");
        Check((int)Invoke(memory, "ConsumeDosEntryStall", opcode, 8) == 8,
            "Cleared DOS stall altered the next opcode.");
        FindField(memory.GetType(), "m_dosEntryStallPending").SetValue(memory, true);
        Check((int)Invoke(memory, "ConsumeDosEntryStall", opcode, 8) == 8,
            "DOS stall added to, rather than overlapped, a longer delay.");
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: WaitPortProbe-B37.exe <release-dir>");
            var dir = Path.GetFullPath(args[0]);
            var hardware = Assembly.LoadFrom(Path.Combine(dir, "ZXMAK2.Hardware.dll"));
            var engine = Assembly.LoadFrom(Path.Combine(dir, "ZXMAK2.Engine.dll"));
            var memoryType = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true);
            var memory = Activator.CreateInstance(memoryType);
            var cmosType = hardware.GetType("ZXMAK2.Hardware.Evo.CmosPentEvo", true);

            TestDecode(memory);
            TestF7AndUart(memory, cmosType);
            TestWaitLifecycle(memory, hardware, engine);
            TestDosStall(memory, engine);

            Console.WriteLine("PASS: {0} B37 WAIT/AVR/COM/DOS checks.", s_checks);
            Console.WriteLine("LIMIT: structural probe only; runtime acceptance remains a user test.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }
}
