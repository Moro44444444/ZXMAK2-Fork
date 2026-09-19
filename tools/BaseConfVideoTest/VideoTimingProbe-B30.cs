using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Xml;
using System.Reflection.Emit;

public static class VideoTimingProbeB30
{
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type mt, ut, ub, bt, ct;
    private static object memory, ula, bus, cpu, cmos, events, beeper;
    private static int checks, ready;
    private static readonly int[] Period = { 573440, 469504, 559104, 567264 };
    private static readonly int[] Line = { 224, 224, 224, 228 };
    private static readonly int[] Wide = { 76, 42, 60, 59 };
    private static readonly int[] IntLine = { 0, 0, 1, 1 };
    private static readonly int[] IntHorizontal = { 2, 2, 126, 130 };
    private static readonly int[] Video = { 0, 2, 3, 6, 7, 11, 19 };
    private static object keys;
    private static bool scroll, shift;
    private static int scrollKey, shiftKey;

    public static bool ReadKey(int key) { return key == scrollKey ? scroll : key == shiftKey && shift; }
    private static object CreateKeyboard(Assembly host)
    {
        Type kt = host.GetType("ZXMAK2.Host.Entities.Key", true);
        Type it = host.GetType("ZXMAK2.Host.Interfaces.IKeyboardState", true);
        scrollKey = Convert.ToInt32(Enum.Parse(kt, "ScrollLock"));
        shiftKey = Convert.ToInt32(Enum.Parse(kt, "LeftShift"));
        AssemblyBuilder a = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("RasterKeys"), AssemblyBuilderAccess.Run);
        TypeBuilder t = a.DefineDynamicModule("Keys").DefineType("RasterKeyboard", TypeAttributes.Public);
        t.AddInterfaceImplementation(it);
        MethodBuilder getter = t.DefineMethod("get_Item", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName, typeof(bool), new Type[] { kt });
        ILGenerator il = getter.GetILGenerator(); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Call, typeof(VideoTimingProbeB30).GetMethod("ReadKey")); il.Emit(OpCodes.Ret);
        t.DefineMethodOverride(getter, it.GetMethod("get_Item"));
        return Activator.CreateInstance(t.CreateType());
    }

    private static void Check(bool ok, string why)
    {
        if (!ok) throw new Exception(why);
        checks++;
    }
    private static byte[][] Pages(int count)
    {
        var pages = new byte[count][];
        for (int i = 0; i < count; i++) pages[i] = new byte[16384];
        return pages;
    }
    private static void Time(long value) { cpu.GetType().GetField("Tact").SetValue(cpu, value); }
    private static long Origin { get { return (long)ut.GetProperty("RasterFrameOrigin").GetValue(ula, null); } }
    private static int Active { get { return (int)ut.GetProperty("ActiveRasterMode").GetValue(ula, null); } }
    private static int Pos(long t) { return (int)ut.GetMethod("GetFrameTact").Invoke(ula, new object[] { t }); }
    private static void Request(int mode) { ut.GetMethod("RequestRaster", F).Invoke(ula, new object[] { mode }); }
    private static void Begin(long t) { ut.GetMethod("BeginFrameTiming").Invoke(ula, new object[] { t }); }
    private static void Scan() { ct.GetMethod("ScanKeyboard", F).Invoke(cmos, null); }
    private static void Config(byte value) { ct.GetMethod("SetAvrVideoConfiguration", F).Invoke(cmos, new object[] { value }); }
    private static byte ConfigRead()
    {
        ct.GetField("addr", F).SetValue(cmos, (byte)0xF0);
        ct.GetMethod("WrCMOS", F).Invoke(cmos, new object[] { (byte)3 });
        return (byte)ct.GetMethod("RdCMOS", F).Invoke(cmos, null);
    }
    private static void CheckRenderers(int mode)
    {
        foreach (int video in Video)
        {
            mt.GetProperty("VIDEO").SetValue(memory, Enum.ToObject(mt.GetProperty("VIDEO").PropertyType, video), null);
            mt.GetMethod("UpdateMapping", F).Invoke(memory, null);
            Begin(Origin); // B21 commits the requested picture format at the frame boundary.
            Check(!(bool)ut.GetProperty("VideoModePending").GetValue(ula, null), "Picture mode remained pending.");
            Check((int)ut.GetProperty("ActiveVideoSelector").GetValue(ula, null) == video, "Wrong active picture selector.");
            Check((int)ut.GetProperty("FrameTactCount").GetValue(ula, null) == Period[mode], "Renderer frame length.");
            Check((int)bt.GetProperty("FrameTactCount").GetValue(bus, null) == Period[mode], "Engine period not updated.");
            var renderer = ub.GetProperty("Renderer", F).GetValue(ula, null);
            var parameters = renderer.GetType().GetProperty("Params").GetValue(renderer, null);
            Type pt = parameters.GetType();
            bool standard = video == 3 || video == 11 || video == 19;
            int firstLine = Wide[mode] + (standard ? 4 : 0);
            int firstTact = standard ? 66 : 54;
            int borderTop = standard ? 32 : 28;
            int intBegin = IntLine[mode] * Line[mode] + IntHorizontal[mode] / 2;
            int borderStage = (2 - (intBegin & 3) + 4) & 3;
            Check((int)pt.GetField("c_ulaLineTime").GetValue(parameters) == Line[mode], "Renderer line period.");
            Check((int)pt.GetField("c_ulaFirstPaperLine").GetValue(parameters) == firstLine, "RTL paper line.");
            Check((int)pt.GetField("c_ulaFirstPaperTact").GetValue(parameters) == firstTact, "RTL paper tact.");
            Check((int)pt.GetField("c_ulaBorderTop").GetValue(parameters) == borderTop, "Shared top border.");
            Check(firstLine - borderTop == Wide[mode] - 28, "Vertical viewport is not shared.");
            Check(firstTact - (int)pt.GetField("c_ulaBorderLeftT").GetValue(parameters) ==
                (standard ? 50 : 54), "Wrong renderer output phase.");
            if (standard && mode == 0)
                Check(firstTact - intBegin == 65,
                    "Normal 256x192 phase does not match the B18 control.");
            Check((int)pt.GetField("c_ulaIntBegin").GetValue(parameters) == intBegin, "RTL INT origin.");
            Check((int)pt.GetField("c_ulaIntLength").GetValue(parameters) == 32, "RTL INT length.");
            Check((bool)pt.GetField("c_ulaBorder4T").GetValue(parameters) == (mode >= 2), "48/128 border latch enable.");
            Check((int)pt.GetField("c_ulaBorder4Tstage").GetValue(parameters) == borderStage, "48/128 border latch phase.");
            // Force a complete draw: action tables, RAM pointers, video buffers and bounds.
            ut.GetMethod("ForceRedrawFrame").Invoke(ula, null);
            var data = ut.GetProperty("VideoData").GetValue(ula, null);
            Check(data != null, "Video buffer absent.");
        }
        Check((bool)ut.GetMethod("CheckInt").Invoke(ula, new object[] { 0 }), "INT missing at frame epoch.");
        Check((bool)ut.GetMethod("CheckInt").Invoke(ula, new object[] { 255 }), "INT shorter than RTL pulse.");
        Check(!(bool)ut.GetMethod("CheckInt").Invoke(ula, new object[] { 256 }), "INT longer than RTL pulse.");
    }
    private static void CheckFetch(int mode)
    {
        long epoch = Origin / 4;
        foreach (int rg in new int[] { 0, 2, 3, 6, 7 })
        foreach (int pent in new int[] { 0, 1, 2, 3 })
        {
            mt.GetField("m_dramProfileMode", F).SetValue(memory, rg);
            mt.GetField("m_dramProfilePent", F).SetValue(memory, pent);
            bool wide = rg != 3, text = rg == 6 || rg == 7;
            int first = Wide[mode] + (wide ? 0 : 4);
            int left = wide ? (text ? 87 : 91) : 123, end = wide ? 411 : 379;
            int count = Period[mode] / 4, width = Line[mode] * 2;
            foreach (int v in new int[] { first - 1, first, first + (wide ? 199 : 191), first + (wide ? 200 : 192) })
            foreach (int h in new int[] { left - 1, left, end - 1, end })
            foreach (int wrap in new int[] { 0, 1 })
            {
                long physical = v * (long)width + h;
                long intCycle = IntLine[mode] * (long)width + IntHorizontal[mode];
                long relative = (physical - intCycle + count) % count;
                object[] args = { epoch + relative + wrap * (long)count, false, 0 };
                mt.GetMethod("GetVideoFetch", F).Invoke(memory, args);
                bool expected = v >= first && v < first + (wide ? 200 : 192) && h >= left && h < end;
                Check((bool)args[1] == expected && (int)args[2] == (rg == 3 && pent != 2 ? 0 : 1), "Anchored DRAM fetch.");
            }
        }
    }
    private static void TestTransitions()
    {
        foreach (int old in new int[] { 0, 1, 2, 3 })
        foreach (int next in new int[] { 0, 1, 2, 3 })
        {
            Request(old); Time(0); Begin(0);
            int oldPeriod = Period[old];
            Time(oldPeriod - 1); Request(next);
            Check(Active == old && Pos(oldPeriod - 1) == oldPeriod - 1, "Request activated before boundary.");
            Check(!(bool)ut.GetMethod("IsFrameComplete").Invoke(ula, new object[] { (long)oldPeriod - 1 }), "Early boundary.");
            Check((bool)ut.GetMethod("IsFrameComplete").Invoke(ula, new object[] { (long)oldPeriod }), "Missing boundary.");
            Time(oldPeriod); Begin(oldPeriod);
            Check(Active == next && Origin == oldPeriod && Pos(oldPeriod) == 0, "Epoch discarded on period change.");
            Check(Pos(oldPeriod + Period[next] - 1L) == Period[next] - 1, "New period tail.");
            foreach (long overrun in new long[] { 0, 1, 2, 3, 7, 31 })
                Check(Pos(oldPeriod + overrun) == overrun, "Master-phase overrun lost.");
            CheckRenderers(next);
            CheckFetch(next);
        }
        // Latest pending request, multiple-frame advances, and snapshot/rewind reanchor.
        Request(0); Begin(0);
        Request(1); Request(3); Time(Period[0] * 3L + 7); Begin(Period[0] * 3L + 7);
        Check(Active == 3 && Origin == Period[0] * 3L && Pos(Origin + 7) == 7, "Latest request / long advance.");
        Time(0); Begin(0);
        Check(Origin == 0 && Pos(0) == 0, "Rewind did not reanchor.");
        try { Request(4); throw new Exception("Invalid raster accepted."); }
        catch (TargetInvocationException e) { Check(e.InnerException is ArgumentOutOfRangeException, "Wrong mode validation."); }
        Type arbType = mt.GetNestedType("EvoDramArbiter", BindingFlags.NonPublic);
        object arb = Activator.CreateInstance(arbType, true);
        arbType.GetMethod("Step").Invoke(arb, new object[] { true, 1, true });
        mt.GetField("m_dramArbiter", F).SetValue(memory, arb);
        mt.GetField("m_dramNextCycle", F).SetValue(memory, Origin / 4);
        mt.GetMethod("ActivateRaster", F).Invoke(memory, new object[] { ut.GetProperty("ActiveRaster", F).GetValue(ula, null), Origin });
        Check(Object.ReferenceEquals(arb, mt.GetField("m_dramArbiter", F).GetValue(memory)), "Raster switch replaced in-flight arbiter.");
        Check((int)arbType.GetField("blockRemaining", F).GetValue(arb) == 7 &&
            (int)arbType.GetField("videoRemaining", F).GetValue(arb) == 2, "In-flight video quota lost.");
        mt.GetField("m_dramArbiter", F).SetValue(memory, null);
    }
    private static void TestAvr()
    {
        byte[] nvram = (byte[])ct.GetField("eeprom", F).GetValue(cmos);
        for (int value = 0; value < 256; value++)
        {
            nvram[0xFE] = (byte)value;
            ct.GetMethod("RestoreAvrVideoConfiguration", F).Invoke(cmos, null);
            Check(ConfigRead() == (value & 0x31), "Supported video readback mask.");
            Check(nvram[0xFE] == (byte)value, "Unsupported NVRAM flags damaged.");
        }
        scroll = false; shift = false; Scan(); Config(0);
        foreach (byte expected in new byte[] { 0x01, 0x10, 0x11, 0x20, 0x21, 0x30, 0x31, 0x00 })
        {
            scroll = true; Scan();
            Check(ConfigRead() == expected, "Scroll Lock cycle.");
            Scan(); Check(ConfigRead() == expected, "Held Scroll Lock repeated.");
            scroll = false; Scan(); Check(ConfigRead() == expected, "Release changed modes.");
            Check((int)ut.GetProperty("RequestedRasterMode").GetValue(ula, null) == (expected >> 4), "Raster request not propagated.");
            ct.GetMethod("Reset", F).Invoke(cmos, null);
            Check(ConfigRead() == expected, "Z80 reset incorrectly resets AVR video state.");
        }
        Config(0); shift = true; scroll = true; Scan();
        Check(ConfigRead() == 1, "Implemented obsolete Shift+Scroll behavior.");
        scroll = false; shift = false; Scan();
        Config(0);
        // The new key logs its original nonextended PS/2 Set-2 make/break bytes.
        ct.GetField("addr", F).SetValue(cmos, (byte)0xF0);
        ct.GetMethod("WrCMOS", F).Invoke(cmos, new object[] { (byte)2 });
        scroll = true; Scan(); scroll = false; Scan();
        Check((byte)ct.GetMethod("PopKeyboardByte", F).Invoke(cmos, null) == 0x7E, "Scroll make log.");
        Check((byte)ct.GetMethod("PopKeyboardByte", F).Invoke(cmos, null) == 0xF0, "Scroll break prefix.");
        Check((byte)ct.GetMethod("PopKeyboardByte", F).Invoke(cmos, null) == 0x7E, "Scroll break code.");
        Config(0); Request(0); Begin(0);
    }
    private static void TestEngineAndSound(Assembly hardware)
    {
        Type st = hardware.GetType("ZXMAK2.Hardware.SoundDeviceBase", true);
        Time(0);
        bt.GetMethod("OnBeginFrame", F).Invoke(bus, null);
        object buffer = st.GetField("m_audioBuffer", F).GetValue(beeper);
        int expectedReady = ready;
        foreach (int mode in new int[] { 1, 2, 3, 0 })
        {
            Config((byte)(mode << 4));
            long boundary = Origin + Period[Active];
            Time(boundary + 7);
            bt.GetMethod("ExecCycle").Invoke(bus, null);
            Check(ready == ++expectedReady, "FrameReady duplicate/missing during change.");
            Check(Active == mode && Origin == boundary, "Engine did not commit requested raster.");
            Check((int)bt.GetProperty("FrameTactCount").GetValue(bus, null) == Period[mode], "Engine dynamic frame count.");
            Check((long)st.GetField("m_startStamp", F).GetValue(beeper) == boundary, "Sound epoch differs from ULA.");
            Check((int)st.GetField("m_frameTactCount", F).GetValue(beeper) == Period[mode], "Sound retained old frame length.");
            Check(Object.ReferenceEquals(buffer, st.GetField("m_audioBuffer", F).GetValue(beeper)), "FrameSound buffer identity lost.");
            long now = (long)cpu.GetType().GetField("Tact").GetValue(cpu);
            Check((int)bt.GetMethod("GetFrameTact").Invoke(bus, null) == now - boundary, "Post-change cycle uses absolute modulo.");
        }
        bt.GetMethod("OnEndFrame", F).Invoke(bus, null);
        var legacy = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.Atm.UlaAtm450", true));
        bt.GetField("m_ula", F).SetValue(bus, legacy);
        bt.GetField("m_frameTactCount", F).SetValue(bus, 69888);
        Time(69888 * 2L + 3);
        Check((int)bt.GetMethod("GetFrameTact").Invoke(bus, null) == 3 &&
            (int)bt.GetProperty("FrameTactCount").GetValue(bus, null) == 69888, "Legacy cached/modulo path changed.");
    }
    private static void TestResources(string release)
    {
        Assembly winforms = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Host.WinForms.dll"));
        Type resources = winforms.GetType("ZXMAK2.Host.WinForms.Properties.Resources", true);
        foreach (string name in new string[] { "Keyboard_WinForms", "Keyboard_Mdx" })
        {
            string xml = (string)resources.GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null, null);
            var doc = new XmlDocument(); doc.LoadXml(xml);
            var node = doc.SelectSingleNode("//*[local-name()='Key' and @Name='ScrollLock']");
            Check(node != null && node.Attributes["Value"].Value == "Scroll", "Compiled keyboard resource missing Scroll Lock.");
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            string release = Path.GetFullPath(args[0]); Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                string file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            Assembly hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            Assembly engine = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Engine.dll"));
            keys = CreateKeyboard(Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Host.dll")));
            mt = hardware.GetType("ZXMAK2.Hardware.Evo.MemoryPentEvo", true); ut = hardware.GetType("ZXMAK2.Hardware.Evo.UlaPentEvo", true);
            ub = hardware.GetType("ZXMAK2.Hardware.UlaDeviceBase", true); ct = hardware.GetType("ZXMAK2.Hardware.Evo.CmosPentEvo", true);
            bt = engine.GetType("ZXMAK2.Engine.BusManager", true); bus = Activator.CreateInstance(bt);
            bt.GetField("m_sandBox", F).SetValue(bus, true);
            memory = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(mt);
            Type mb = mt.BaseType;
            mb.GetField("m_ramPages", F).SetValue(memory, Pages(256));
            mb.GetField("m_romPages", F).SetValue(memory, Pages(32));
            mb.GetField("m_map48", F).SetValue(memory, new int[4]); mb.GetField("m_trashPage", F).SetValue(memory, new byte[16384]);
            mt.GetField("m_ru2", F).SetValue(memory, new int[] { 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340, 0x340 });
            mt.GetField("m_romMask", F).SetValue(memory, 31); mt.GetField("m_ramMask", F).SetValue(memory, 255);
            mt.GetField("m_aFF77", F).SetValue(memory, 0x0377); mt.GetField("m_pFF77", F).SetValue(memory, 3);
            ula = Activator.CreateInstance(ut); cpu = bt.GetProperty("Cpu").GetValue(bus, null);
            ub.GetField("m_memory", F).SetValue(ula, memory); ub.GetField("CPU", F).SetValue(ula, cpu);
            var devices = (IList)bt.GetField("m_deviceList", F).GetValue(bus); devices.Add(memory); devices.Add(ula);
            cmos = Activator.CreateInstance(ct); devices.Add(cmos); ct.GetProperty("KeyboardState").SetValue(cmos, keys, null);
            bt.GetField("m_ula", F).SetValue(bus, ula); bt.GetField("m_frameTactCount", F).SetValue(bus, Period[0]);
            mt.GetMethod("BusInit").Invoke(memory, new object[] { bus });
            ct.GetMethod("BusInit").Invoke(cmos, new object[] { bus });
            mt.GetMethod("UpdateMapping", F).Invoke(memory, null);
            events = bt.GetField("m_eventManager", F).GetValue(bus);
            events.GetType().GetMethod("SubscribeBeginFrame").Invoke(events, new object[] { Delegate.CreateDelegate(typeof(Action), ula, ub.GetMethod("BeginFrame", F)) });
            events.GetType().GetMethod("SubscribeEndFrame").Invoke(events, new object[] { Delegate.CreateDelegate(typeof(Action), ula, ub.GetMethod("EndFrame", F)) });
            beeper = Activator.CreateInstance(hardware.GetType("ZXMAK2.Hardware.General.BeeperDevice", true));
            beeper.GetType().GetMethod("BusInit").Invoke(beeper, new object[] { bus });
            bt.GetEvent("FrameReady").AddEventHandler(bus, new Action(delegate { ready++; }));
            TestResources(release); TestAvr(); TestTransitions(); TestEngineAndSound(hardware);
            Console.WriteLine("PASS: " + checks + " B30 video timing checks: raw RTL raster/INT/fetch plus the explicit 256x192 ZXMAK2 renderer-phase adapter, frame-boundary picture commit, all Scroll states, 16 transitions, seven renderers, Engine/FrameReady/sound-buffer and legacy path.");
            Console.WriteLine("LIMIT: compiled structural/runtime probe only; physical VGA output and runtime visual acceptance remain user tests.");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
