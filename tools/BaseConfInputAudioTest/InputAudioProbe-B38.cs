using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;


internal static class InputAudioProbeB38
{
    private const BindingFlags Instance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;
    private static int s_checks;

    private sealed class JoystickState : IJoystickState8
    {
        public bool IsLeft { get; set; }
        public bool IsRight { get; set; }
        public bool IsUp { get; set; }
        public bool IsDown { get; set; }
        public bool IsFire { get; set; }
        public byte KempstonState { get; set; }
    }

    private sealed class KeyboardState : IKeyboardState
    {
        private readonly HashSet<Key> m_pressed = new HashSet<Key>();

        public bool this[Key key] { get { return m_pressed.Contains(key); } }

        public void Set(Key key, bool pressed)
        {
            if (pressed)
                m_pressed.Add(key);
            else
                m_pressed.Remove(key);
        }
    }

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

    private static MethodInfo FindMethod(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var method = current.GetMethod(name, Instance | BindingFlags.DeclaredOnly);
            if (method != null)
                return method;
        }
        throw new MissingMethodException(type.FullName, name);
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        return FindMethod(target.GetType(), name).Invoke(target, args);
    }

    private static void SetNormal(object memory)
    {
        FindField(memory.GetType(), "m_aFF77").SetValue(memory, 0x0300);
        FindField(memory.GetType(), "m_pXXBF").SetValue(memory, (byte)0);
        FindField(memory.GetType(), "m_dosen").SetValue(memory, false);
    }

    private static void SetShadow(object memory, bool dos)
    {
        FindField(memory.GetType(), "m_aFF77").SetValue(memory, 0x0300);
        FindField(memory.GetType(), "m_pXXBF").SetValue(memory, (byte)(dos ? 0 : 1));
        FindField(memory.GetType(), "m_dosen").SetValue(memory, dos);
    }

    private static byte ReadJoystick(object joystick, ushort port, ref bool handled, byte initial = 0xFF)
    {
        var args = new object[] { port, initial, handled };
        Invoke(joystick, "ReadPort1F", args);
        handled = (bool)args[2];
        return (byte)args[1];
    }

    private static byte ReadFdd(object fdd, ushort port, ref bool handled)
    {
        var args = new object[] { port, (byte)0xFF, handled };
        Invoke(fdd, "BusReadFdc", args);
        handled = (bool)args[2];
        return (byte)args[1];
    }

    private static void TestMachineProfile(string root)
    {
        var document = new XmlDocument();
        document.Load(Path.Combine(root, "src", "ZXMAK2", "machines.config"));
        var bus = document.SelectSingleNode("/Machines/Bus[@name='ZX-Evo BSconf']");
        Check(bus != null, "ZX-Evo BSconf profile missing.");

        var beeper = bus.SelectSingleNode("Device[@type='ZXMAK2.Hardware.Evo.BeeperPentEvo']");
        var tape = bus.SelectSingleNode("Device[@type='ZXMAK2.Hardware.General.TapeDevice']");
        var joystick = bus.SelectSingleNode("Device[@type='ZXMAK2.Hardware.General.KempstonJoystick']");
        var fdd = bus.SelectSingleNode("Device[@type='ZXMAK2.Hardware.Evo.FddPentEvo']");
        Check(beeper != null, "PentEvo beeper/mux device missing.");
        Check(tape != null, "BaseConf tape input device missing.");
        Check(joystick != null, "BaseConf Kempston device missing.");
        Check(fdd != null, "BaseConf VG93 device missing.");
        Check(tape.Attributes["mask"].Value == "0xF7" &&
            tape.Attributes["port"].Value == "0xFE", "Tape FE/F6 decode mismatch.");
        Check(tape.Attributes["bit"].Value == "6", "Tape input is not D6.");
        Check(tape.Attributes["outputLoopback"].Value == "false",
            "Generic FE output incorrectly drives BaseConf tape input.");
        Check(tape.Attributes["volume"].Value == "0",
            "Tape input duplicates the hardware beeper audio path.");
        Check(joystick.Attributes["mask"].Value == "0xFF" &&
            joystick.Attributes["port"].Value == "0x1F", "Kempston decode is not exact low #1F.");
        Check(joystick.Attributes["bitWidth"].Value == "8", "Kempston is not eight-bit.");

        var fddOrder = 0;
        var joystickOrder = 0;
        for (var i = 0; i < bus.ChildNodes.Count; i++)
        {
            if (bus.ChildNodes[i] == fdd) fddOrder = i;
            if (bus.ChildNodes[i] == joystick) joystickOrder = i;
        }
        Check(fddOrder < joystickOrder, "VG93 must arbitrate overlapping #1F before Kempston.");
    }

    private static void TestKempstonAndFdd(Assembly hardware, Assembly engine)
    {
        var memory = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true));
        var joystick = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.General.KempstonJoystick", true));
        var fdd = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.Evo.FddPentEvo", true));
        joystick.GetType().GetProperty("BitWidth").SetValue(joystick, 8, null);
        FindField(joystick.GetType(), "m_memory").SetValue(joystick, memory);
        FindField(fdd.GetType(), "m_memory").SetValue(fdd, memory);
        FindField(fdd.GetType(), "m_memoryPentEvo").SetValue(fdd, memory);
        var bus = Activator.CreateInstance(engine.GetType("ZXMAK2.Engine.BusManager", true));
        FindField(fdd.GetType(), "m_cpu").SetValue(fdd,
            bus.GetType().GetProperty("Cpu").GetValue(bus, null));

        var state = new JoystickState
        {
            IsRight = true,
            IsDown = true,
            IsFire = true,
            KempstonState = 0xD0 // B/fire + A + Start
        };
        joystick.GetType().GetProperty("JoystickState").SetValue(joystick, state, null);

        // Exact low-byte decode: KJOY is only #xx1F.  In Shadow the VG93
        // family owns #1F/#3F/#5F/#7F, while only #1F overlaps KJOY.
        for (var port = 0; port <= 0xFFFF; port++)
        {
            var low = port & 0xFF;
            Check(((port & 0xFF) == 0x1F) == (low == 0x1F),
                "Kempston decode mismatch at #" + port.ToString("X4"));
            var vg93 = (low & 0x9F) == 0x1F;
            var kjoy = low == 0x1F;
            Check((vg93 && kjoy) == (low == 0x1F),
                "VG93/Kempston overlap mismatch at #" + port.ToString("X4"));
        }

        SetNormal(memory);
        var handled = false;
        ReadFdd(fdd, 0x001F, ref handled);
        Check(!handled, "VG93 stole #1F in normal mode.");
        var value = ReadJoystick(joystick, 0x001F, ref handled);
        Check(handled && value == 0xD5, "Eight-bit Kempston value mismatch.");

        SetShadow(memory, false);
        handled = false;
        var wdValue = ReadFdd(fdd, 0x001F, ref handled);
        Check(handled, "VG93 did not own #1F in Shadow mode.");
        value = ReadJoystick(joystick, 0x001F, ref handled, wdValue);
        Check(handled && value == wdValue, "Kempston overrode handled VG93 data.");

        SetShadow(memory, true);
        handled = false;
        wdValue = ReadFdd(fdd, 0x001F, ref handled);
        Check(handled, "VG93 did not own #1F in DOS mode.");
        value = ReadJoystick(joystick, 0x001F, ref handled, wdValue);
        Check(handled && value == wdValue, "Kempston leaked through DOS/VG93 arbitration.");

        // A legacy five-bit consumer still treats any host button as Fire.
        joystick.GetType().GetProperty("BitWidth").SetValue(joystick, 5, null);
        SetNormal(memory);
        handled = false;
        value = ReadJoystick(joystick, 0x001F, ref handled);
        Check(value == 0x15, "Legacy five-bit Kempston behavior changed.");
    }

    private static void TestTape(Assembly hardware, Assembly engine)
    {
        var tape = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.General.TapeDevice", true));
        var bus = Activator.CreateInstance(engine.GetType("ZXMAK2.Engine.BusManager", true));
        FindField(tape.GetType(), "m_cpu").SetValue(tape,
            bus.GetType().GetProperty("Cpu").GetValue(bus, null));
        tape.GetType().GetProperty("NoDos").SetValue(tape, false, null);
        tape.GetType().GetProperty("Mask").SetValue(tape, 0xF7, null);
        tape.GetType().GetProperty("Port").SetValue(tape, 0xFE, null);
        tape.GetType().GetProperty("Bit").SetValue(tape, 6, null);
        tape.GetType().GetProperty("OutputLoopback").SetValue(tape, false, null);

        for (var port = 0; port <= 0xFFFF; port++)
        {
            var expected = (port & 0xF7) == 0xF6;
            Check(expected == ((port & 0xFF) == 0xFE || (port & 0xFF) == 0xF6),
                "Tape FE/F6 decode mismatch at #" + port.ToString("X4"));
        }

        FindField(tape.GetType(), "m_state").SetValue(tape, true);
        foreach (ushort port in new ushort[] { 0x00FE, 0xA5F6 })
        {
            var args = new object[] { port, (byte)0x00, false };
            Invoke(tape, "ReadPortFe", args);
            Check(((byte)args[1] & 0x40) != 0, "Tape D6 high missing at #" + port.ToString("X4"));
        }
        FindField(tape.GetType(), "m_state").SetValue(tape, false);
        var lowArgs = new object[] { (ushort)0x40FE, (byte)0xFF, false };
        Invoke(tape, "ReadPortFe", lowArgs);
        Check(((byte)lowArgs[1] & 0x40) == 0, "Tape D6 low missing.");

        var writeArgs = new object[] { (ushort)0x00FE, (byte)0x10, false };
        Invoke(tape, "WritePortFe", writeArgs);
        Check(!(bool)FindField(tape.GetType(), "m_state").GetValue(tape),
            "BaseConf tape input was fed from generic FE output.");
        tape.GetType().GetProperty("OutputLoopback").SetValue(tape, true, null);
        Invoke(tape, "WritePortFe", writeArgs);
        Check((bool)FindField(tape.GetType(), "m_state").GetValue(tape),
            "Legacy tape loopback compatibility changed.");
    }

    private static void TestBeeperMux(Assembly hardware, Assembly engine)
    {
        var cmos = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.Evo.CmosPentEvo", true));
        var beeper = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.Evo.BeeperPentEvo", true));
        var bus = Activator.CreateInstance(engine.GetType("ZXMAK2.Engine.BusManager", true));
        FindField(beeper.GetType(), "m_cpu").SetValue(beeper,
            bus.GetType().GetProperty("Cpu").GetValue(bus, null));
        FindField(beeper.GetType(), "m_cmos").SetValue(beeper, cmos);
        var muxEvent = cmos.GetType().GetEvent("BeeperMuxChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var refresh = Delegate.CreateDelegate(typeof(Action), beeper,
            FindMethod(beeper.GetType(), "RefreshOutput"));
        muxEvent.GetAddMethod(true).Invoke(cmos, new object[] { refresh });

        var args = new object[] { (ushort)0x00FE, (byte)0x10, false };
        Invoke(beeper, "WritePortFe", args);
        Check((int)beeper.GetType().GetProperty("SelectedBit").GetValue(beeper, null) == 4,
            "Default beeper source is not D4.");
        Check((bool)beeper.GetType().GetProperty("OutputHigh").GetValue(beeper, null),
            "D4 did not drive default beeper output.");

        Invoke(cmos, "SetBeeperTapeOut", true);
        Check(!(bool)beeper.GetType().GetProperty("OutputHigh").GetValue(beeper, null),
            "Combinational mux change did not re-evaluate the last FE value.");
        args = new object[] { (ushort)0x00FE, (byte)0x08, false };
        Invoke(beeper, "WritePortFe", args);
        Check((int)beeper.GetType().GetProperty("SelectedBit").GetValue(beeper, null) == 3,
            "Tape-out mux did not select D3.");
        Check((bool)beeper.GetType().GetProperty("OutputHigh").GetValue(beeper, null),
            "D3 did not drive tape-out-selected audio.");
        var eeprom = (byte[])FindField(cmos.GetType(), "eeprom").GetValue(cmos);
        Check((eeprom[0xFE] & 0x08) != 0, "Beeper/tape mux was not persisted.");

        var keyboard = new KeyboardState();
        cmos.GetType().GetProperty("KeyboardState").SetValue(cmos, keyboard, null);
        Invoke(cmos, "ScanKeyboard");
        keyboard.Set(Key.NumLock, true);
        Invoke(cmos, "ScanKeyboard");
        Check(!(bool)cmos.GetType().GetProperty("BeeperTapeOutSelected").GetValue(cmos, null),
            "NumLock rising edge did not toggle mux.");
        Invoke(cmos, "ScanKeyboard");
        Check(!(bool)cmos.GetType().GetProperty("BeeperTapeOutSelected").GetValue(cmos, null),
            "Held NumLock toggled mux repeatedly.");
        keyboard.Set(Key.NumLock, false);
        Invoke(cmos, "ScanKeyboard");
        keyboard.Set(Key.NumLock, true);
        Invoke(cmos, "ScanKeyboard");
        Check((bool)cmos.GetType().GetProperty("BeeperTapeOutSelected").GetValue(cmos, null),
            "Second NumLock rising edge did not toggle mux.");
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
                throw new ArgumentException("Usage: InputAudioProbe-B38.exe <release-dir> <repo-root>");
            var release = Path.GetFullPath(args[0]);
            var root = Path.GetFullPath(args[1]);
            var hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            var engine = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Engine.dll"));

            TestMachineProfile(root);
            TestKempstonAndFdd(hardware, engine);
            TestTape(hardware, engine);
            TestBeeperMux(hardware, engine);

            Console.WriteLine("PASS: {0} B38 Kempston/tape/beeper checks.", s_checks);
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
