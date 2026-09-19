using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using ZXMAK2.Host.WinForms.Mdx;

internal static class DirectSoundBufferProbe
{
    private static int s_checks;

    private static void Require(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Missing field: " + name);
        field.SetValue(target, value);
    }

    private static short Left(uint sample)
    {
        return unchecked((short)(sample & 0xFFFF));
    }

    private static short Right(uint sample)
    {
        return unchecked((short)(sample >> 16));
    }

    private static uint Pack(short left, short right)
    {
        return (uint)(ushort)left | ((uint)(ushort)right << 16);
    }

    private static unsafe void Main(string[] args)
    {
        var target = FormatterServices.GetUninitializedObject(typeof(DirectSound));
        SetField(target, "_playQueue", new ConcurrentQueue<uint[]>());
        SetField(target, "_lastSample", (uint?)Pack(4000, -2000));

        var method = typeof(DirectSound).GetMethod(
            "OnBufferRequest",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Missing OnBufferRequest");

        var samples = new uint[4];
        bool result;
        fixed (uint* pointer = samples)
        {
            result = (bool)method.Invoke(
                target,
                new object[] { Pointer.Box(pointer, typeof(uint*)), samples.Length });
        }

        Require(result, "Underrun handler rejected the buffer");
        Require(Left(samples[0]) == 3000, "Left ramp start mismatch");
        Require(Left(samples[1]) == 2000, "Left ramp midpoint mismatch");
        Require(Left(samples[2]) == 1000, "Left ramp tail mismatch");
        Require(Left(samples[3]) == 0, "Left channel did not reach silence");
        Require(Right(samples[0]) == -1500, "Right ramp start mismatch");
        Require(Right(samples[1]) == -1000, "Right ramp midpoint mismatch");
        Require(Right(samples[2]) == -500, "Right ramp tail mismatch");
        Require(Right(samples[3]) == 0, "Right channel did not reach silence");

        var lastSample = typeof(DirectSound).GetField(
            "_lastSample",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(lastSample != null, "Missing last-sample state");
        Require((uint?)lastSample.GetValue(target) == 0U,
            "Underrun did not reset the retained sample");

        if (args.Length > 0)
        {
            var source = File.ReadAllText(args[0]);
            var clearPos = source.IndexOf(
                "_soundBuffer.Write(\n                            i * rawBufferLength",
                StringComparison.Ordinal);
            var playPos = source.IndexOf(
                "_soundBuffer.Play(0, DSBPLAY_FLAGS.LOOPING)",
                StringComparison.Ordinal);
            Require(clearPos >= 0, "Startup ring-buffer clear is missing");
            Require(playPos > clearPos, "Playback starts before the ring buffer is cleared");
        }

        Console.WriteLine("DirectSoundBufferProbe: {0} PASS", s_checks);
    }
}
