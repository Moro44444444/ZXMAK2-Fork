using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Reflection;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.General;
using ZXMAK2.Model.Tape.Entities;
using ZXMAK2.Model.Tape.Interfaces;
using ZXMAK2.Serializers.TapeSerializers;

// Focused regression probe for BaseConf tape timing.  TAP pulse lengths are
// defined in 3.5 MHz Z80 tacts, while BaseConf uses a 28 MHz master-tact
// counter.  This test deliberately exercises the serializer through the
// public ITapeDevice API used by the emulator.
internal static class TapeClockProbeB40
{
    private sealed class ProbeTape : ITapeDevice
    {
        public ProbeTape(int pulseClockMultiplier)
        {
            TapePulseClockMultiplier = pulseClockMultiplier;
            Blocks = new List<ITapeBlock>();
        }

        public void Play() { }
        public void Stop() { }
        public void Rewind() { }
        public void Reset() { }
        public bool IsPlay { get { return false; } }
        public int TactsPerSecond { get { return 3500000; } }
        public int TapePulseClockMultiplier { get; private set; }
        public List<ITapeBlock> Blocks { get; private set; }
        public event EventHandler TapeStateChanged { add { } remove { } }
        public int CurrentBlock { get; set; }
        public int Position { get { return 0; } }
        public bool UseTraps { get; set; }
        public bool UseAutoPlay { get; set; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static int FirstPilotPeriod(int multiplier)
    {
        // One minimal data TAP block.  Its first pilot pulse is 2168 tacts.
        var tape = new ProbeTape(multiplier);
        var serializer = new TapSerializer(tape);
        using (var stream = new MemoryStream(new byte[] { 0x02, 0x00, 0xFF, 0xFF }))
            serializer.Deserialize(stream);

        Check(tape.Blocks.Count == 1, "TAP parser did not produce one block.");
        return ((TapeBlock)tape.Blocks[0]).Periods[0];
    }

    private static int FirstTzxPilotPeriod(int multiplier)
    {
        // TZX header followed by one standard-speed data block containing
        // the same two-byte TAP payload used above.
        var image = new byte[]
        {
            (byte)'Z', (byte)'X', (byte)'T', (byte)'a', (byte)'p', (byte)'e',
            (byte)'!', 0x1A, 0x01, 0x14,
            0x10, 0x00, 0x00, 0x02, 0x00, 0xFF, 0xFF
        };
        var tape = new ProbeTape(multiplier);
        var serializer = new TzxSerializer(tape);
        using (var stream = new MemoryStream(image))
            serializer.Deserialize(stream);

        Check(tape.Blocks.Count == 2, "TZX parser did not produce header and data blocks.");
        return ((TapeBlock)tape.Blocks[1]).Periods[0];
    }

    private static void CheckProfileIsolation()
    {
        var config = new XmlDocument();
        config.Load(Path.Combine(Directory.GetCurrentDirectory(), "src", "ZXMAK2", "machines.config"));

        var baseConfTape = config.SelectSingleNode(
            "/Machines/Bus[@name='ZX-Evo BSconf']/Device[@type='ZXMAK2.Hardware.General.TapeDevice']") as XmlElement;
        Check(baseConfTape != null && baseConfTape.GetAttribute("pulseClockMultiplier") == "8",
            "BaseConf must explicitly select the 8x tape master-clock conversion.");

        var otherTapes = config.SelectNodes(
            "/Machines/Bus[@name!='ZX-Evo BSconf']/Device[@type='ZXMAK2.Hardware.General.TapeDevice']");
        foreach (XmlElement tape in otherTapes)
            Check(!tape.HasAttribute("pulseClockMultiplier"),
                "A non-BaseConf machine must retain standard tape timing.");
    }

    private static void CheckStaleProfileGuard()
    {
        var tape = new TapeDevice();
        tape.TapePulseClockMultiplier = 8;
        var timingField = typeof(TapeDevice).GetField("m_isBaseConfTiming",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(timingField != null, "Tape timing machine guard is missing.");

        timingField.SetValue(tape, false);
        Check(tape.TapePulseClockMultiplier == 1,
            "A stale BaseConf multiplier must not affect another machine.");
        timingField.SetValue(tape, true);
        Check(tape.TapePulseClockMultiplier == 8,
            "BaseConf must retain its configured tape multiplier.");
    }

    public static int Main()
    {
        int standardPeriod = FirstPilotPeriod(1);
        int baseConfPeriod = FirstPilotPeriod(8);
        Check(standardPeriod == 2168,
            "Standard Spectrum TAP pulse has unexpected duration: " + standardPeriod + ".");
        Check(baseConfPeriod == 17344,
            "BaseConf TAP pulse has unexpected duration: " + baseConfPeriod + ".");

        Check(FirstTzxPilotPeriod(1) == 2168,
            "Standard Spectrum TZX pulse must retain its 3.5 MHz duration.");
        Check(FirstTzxPilotPeriod(8) == 17344,
            "BaseConf TZX pulse must be converted to eight master tacts.");
        CheckProfileIsolation();
        CheckStaleProfileGuard();
        Console.WriteLine("Tape clock probe B40: PASS (TAP/TZX 1x/8x; profile and stale VMZ isolated)");
        return 0;
    }
}
