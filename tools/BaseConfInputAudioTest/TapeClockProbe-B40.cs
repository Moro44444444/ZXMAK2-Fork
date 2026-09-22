using System;
using System.Collections.Generic;
using System.IO;
using ZXMAK2.Engine.Interfaces;
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
        Console.WriteLine("Tape clock probe B40: PASS (TAP 1x/8x)");
        return 0;
    }
}
