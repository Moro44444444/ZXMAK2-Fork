using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ZXMAK2.Hardware;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine;

internal static class AudioPathProbe
{
    private const BindingFlags AllInstance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;

    private static int s_checks;

    private sealed class Metrics
    {
        public double MeanLeft;
        public double MeanRight;
        public double RmsLeft;
        public double RmsRight;
        public double AcRmsLeft;
        public double AcRmsRight;
        public int PeakLeft;
        public int PeakRight;
        public short FirstLeft;
        public short FirstRight;
        public short LastLeft;
        public short LastRight;
        public int MaxStepLeft;
        public int MaxStepRight;
    }

    private static void Require(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
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

    private static MethodInfo FindMethod(Type type, string name, params Type[] parameters)
    {
        while (type != null)
        {
            MethodInfo method = type.GetMethod(
                name,
                AllInstance,
                null,
                parameters,
                null);
            if (method != null)
                return method;
            type = type.BaseType;
        }
        throw new MissingMethodException(name);
    }

    private static FieldInfo FindField(Type type, string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, AllInstance);
            if (field != null)
                return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static void Prepare(SoundDeviceBase device)
    {
        FindMethod(
            typeof(SoundDeviceBase),
            "ApplyTimings",
            typeof(int),
            typeof(int)).Invoke(device, new object[] { 3500000, 44100 });
        BeginFrame(device);
    }

    private static void BeginFrame(SoundDeviceBase device)
    {
        FindField(typeof(SoundDeviceBase), "m_lastDacTact").SetValue(device, -1);
        FindMethod(typeof(SoundDeviceBase), "OnBeginFrame", Type.EmptyTypes)
            .Invoke(device, null);
    }

    private static uint[] EndFrame(SoundDeviceBase device)
    {
        FindMethod(device.GetType(), "OnEndFrame", Type.EmptyTypes)
            .Invoke(device, null);
        uint[] result = (uint[])device.GetType().GetProperty("AudioBuffer")
            .GetValue(device, null);
        return (uint[])result.Clone();
    }

    private static uint[] RenderIdleFrame(SoundDeviceBase device)
    {
        uint[] result = EndFrame(device);
        BeginFrame(device);
        return result;
    }

    private static uint[] RenderLevelFrame(SoundDeviceBase device, ushort level)
    {
        FindMethod(
            typeof(SoundDeviceBase),
            "UpdateDac",
            typeof(double),
            typeof(ushort),
            typeof(ushort)).Invoke(
                device,
                new object[] { 0.1D, level, level });
        return RenderIdleFrame(device);
    }

    private static Metrics Measure(uint[] buffer)
    {
        Require(buffer != null && buffer.Length > 0, "Empty PCM buffer");
        double sumLeft = 0D;
        double sumRight = 0D;
        double sumSquaresLeft = 0D;
        double sumSquaresRight = 0D;
        int peakLeft = 0;
        int peakRight = 0;
        int maxStepLeft = 0;
        int maxStepRight = 0;
        short previousLeft = Left(buffer[0]);
        short previousRight = Right(buffer[0]);
        for (int i = 0; i < buffer.Length; i++)
        {
            short left = Left(buffer[i]);
            short right = Right(buffer[i]);
            sumLeft += left;
            sumRight += right;
            sumSquaresLeft += (double)left * left;
            sumSquaresRight += (double)right * right;
            peakLeft = Math.Max(peakLeft, Math.Abs((int)left));
            peakRight = Math.Max(peakRight, Math.Abs((int)right));
            if (i > 0)
            {
                maxStepLeft = Math.Max(maxStepLeft, Math.Abs((int)left - previousLeft));
                maxStepRight = Math.Max(maxStepRight, Math.Abs((int)right - previousRight));
            }
            previousLeft = left;
            previousRight = right;
        }

        double meanLeft = sumLeft / buffer.Length;
        double meanRight = sumRight / buffer.Length;
        double acSquaresLeft = 0D;
        double acSquaresRight = 0D;
        for (int i = 0; i < buffer.Length; i++)
        {
            double left = Left(buffer[i]) - meanLeft;
            double right = Right(buffer[i]) - meanRight;
            acSquaresLeft += left * left;
            acSquaresRight += right * right;
        }

        return new Metrics
        {
            MeanLeft = meanLeft,
            MeanRight = meanRight,
            RmsLeft = Math.Sqrt(sumSquaresLeft / buffer.Length),
            RmsRight = Math.Sqrt(sumSquaresRight / buffer.Length),
            AcRmsLeft = Math.Sqrt(acSquaresLeft / buffer.Length),
            AcRmsRight = Math.Sqrt(acSquaresRight / buffer.Length),
            PeakLeft = peakLeft,
            PeakRight = peakRight,
            FirstLeft = Left(buffer[0]),
            FirstRight = Right(buffer[0]),
            LastLeft = Left(buffer[buffer.Length - 1]),
            LastRight = Right(buffer[buffer.Length - 1]),
            MaxStepLeft = maxStepLeft,
            MaxStepRight = maxStepRight
        };
    }

    private static void Print(string name, uint[] buffer, uint[] previous)
    {
        Metrics metrics = Measure(buffer);
        int boundaryLeft = previous == null
            ? 0
            : Math.Abs((int)metrics.FirstLeft - Left(previous[previous.Length - 1]));
        int boundaryRight = previous == null
            ? 0
            : Math.Abs((int)metrics.FirstRight - Right(previous[previous.Length - 1]));
        Console.WriteLine(
            "{0,-22} mean={1,10:F2}/{2,10:F2} rms={3,10:F2}/{4,10:F2} " +
            "ac={5,9:F2}/{6,9:F2} peak={7,5}/{8,5} first={9,6}/{10,6} " +
            "last={11,6}/{12,6} step={13,5}/{14,5} boundary={15,5}/{16,5}",
            name,
            metrics.MeanLeft,
            metrics.MeanRight,
            metrics.RmsLeft,
            metrics.RmsRight,
            metrics.AcRmsLeft,
            metrics.AcRmsRight,
            metrics.PeakLeft,
            metrics.PeakRight,
            metrics.FirstLeft,
            metrics.FirstRight,
            metrics.LastLeft,
            metrics.LastRight,
            metrics.MaxStepLeft,
            metrics.MaxStepRight,
            boundaryLeft,
            boundaryRight);
    }

    private static FrameSound CreateFrameSound(
        IEnumerable<uint[]> sources,
        bool rejectDc,
        out bool filterAvailable)
    {
        ConstructorInfo constructor = typeof(FrameSound).GetConstructor(
            new[] { typeof(int), typeof(IEnumerable<uint[]>), typeof(bool) });
        filterAvailable = constructor != null;
        if (constructor != null)
        {
            return (FrameSound)constructor.Invoke(
                new object[] { 44100, sources, rejectDc });
        }
        return new FrameSound(44100, sources);
    }

    private static uint[] Mix(FrameSound frame)
    {
        frame.Refresh();
        return (uint[])frame.GetBuffer().Clone();
    }

    private static void VerifyRejectDc()
    {
        var source = Enumerable.Repeat(Pack(-12000, 8000), 882).ToArray();
        bool available;
        FrameSound filtered = CreateFrameSound(new[] { source }, true, out available);
        Require(available, "RejectDC FrameSound constructor is missing");

        uint[] first = Mix(filtered);
        uint[] second = Mix(filtered);
        Metrics firstMetrics = Measure(first);
        Metrics secondMetrics = Measure(second);
        int boundaryLeft = Math.Abs((int)secondMetrics.FirstLeft - firstMetrics.LastLeft);
        int boundaryRight = Math.Abs((int)secondMetrics.FirstRight - firstMetrics.LastRight);
        Require(Math.Abs(firstMetrics.MeanLeft) < 1D &&
            Math.Abs(firstMetrics.MeanRight) < 1D,
            "Cold-start DC was not primed as digital silence");
        Require(Math.Abs(secondMetrics.MeanLeft) < 1D,
            "Left DC did not converge to digital silence");
        Require(Math.Abs(secondMetrics.MeanRight) < 1D,
            "Right DC did not converge to digital silence");
        Require(Math.Abs((int)secondMetrics.FirstLeft) < 4,
            "Left filter state was restarted at a frame boundary");
        Require(Math.Abs((int)secondMetrics.FirstRight) < 4,
            "Right filter state was restarted at a frame boundary");
        Require(boundaryLeft <= 1 && boundaryRight <= 1,
            "RejectDC introduced a frame-boundary discontinuity");

        FrameSound unfiltered = CreateFrameSound(new[] { source }, false, out available);
        Metrics rawMetrics = Measure(Mix(unfiltered));
        Require(Math.Abs(rawMetrics.MeanLeft + 12000D) < 0.01D,
            "Default mixer unexpectedly changed the left channel");
        Require(Math.Abs(rawMetrics.MeanRight - 8000D) < 0.01D,
            "Default mixer unexpectedly changed the right channel");

        var stereo = new uint[882];
        for (int i = 0; i < stereo.Length; i++)
            stereo[i] = Pack((short)((i & 1) == 0 ? 4000 : -4000), 0);
        FrameSound signal = CreateFrameSound(new[] { stereo }, true, out available);
        Metrics signalMetrics = Measure(Mix(signal));
        Require(signalMetrics.AcRmsLeft > 3900D,
            "RejectDC removed useful alternating left-channel signal");
        Require(signalMetrics.PeakRight == 0,
            "RejectDC leaked the left signal into the right channel");
    }

    private static bool CallsMethod(MethodBase caller, Func<MethodBase, bool> predicate)
    {
        MethodBody body = caller.GetMethodBody();
        if (body == null)
            return false;
        byte[] il = body.GetILAsByteArray();
        for (int i = 0; i <= il.Length - 4; i++)
        {
            int token = BitConverter.ToInt32(il, i);
            try
            {
                MethodBase target = caller.Module.ResolveMethod(token);
                if (target != null && predicate(target))
                    return true;
            }
            catch (ArgumentException)
            {
            }
        }
        return false;
    }

    private static void VerifyBusManagerWiring()
    {
        MethodInfo connect = typeof(BusManager).GetMethod("Connect");
        Require(connect != null, "BusManager.Connect is missing");
        Require(CallsMethod(connect, delegate(MethodBase method)
        {
            if (method.DeclaringType != typeof(FrameSound) || !method.IsConstructor)
                return false;
            return method.GetParameters().Length == 3;
        }), "BusManager does not construct the policy-aware FrameSound");

        bool readsRejectDc = false;
        const BindingFlags methods = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;
        foreach (Type type in typeof(BusManager).Assembly.GetTypes())
        {
            if (type != typeof(BusManager) && type.DeclaringType != typeof(BusManager))
                continue;
            foreach (MethodInfo method in type.GetMethods(methods))
            {
                if (CallsMethod(method, delegate(MethodBase target)
                {
                    return target.DeclaringType == typeof(ISoundMixerConfiguration) &&
                        target.Name == "get_RejectDc";
                }))
                {
                    readsRejectDc = true;
                    break;
                }
            }
            if (readsRejectDc)
                break;
        }
        Require(readsRejectDc,
            "Compiled BusManager does not read the optional RejectDC policy");
    }

    private static int Main(string[] args)
    {
        try
        {
            Require(args.Length >= 1, "Expected release directory argument");
            string release = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                string file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };

            var ay = new AYCHRV();
            var beeper = new BeeperDevice();
            var covox = new CovoxMono();
            Prepare(ay);
            Prepare(beeper);
            Prepare(covox);

            uint[] ayFirst = RenderIdleFrame(ay);
            uint[] beeperFirst = RenderIdleFrame(beeper);
            uint[] covoxFirst = RenderIdleFrame(covox);
            uint[] aySecond = RenderIdleFrame(ay);
            uint[] beeperSecond = RenderIdleFrame(beeper);
            uint[] covoxSecond = RenderIdleFrame(covox);

            Print("AY idle frame 1", ayFirst, null);
            Print("Beeper idle frame 1", beeperFirst, null);
            Print("Covox idle frame 1", covoxFirst, null);
            Print("AY idle frame 2", aySecond, ayFirst);
            Print("Beeper idle frame 2", beeperSecond, beeperFirst);
            Print("Covox idle frame 2", covoxSecond, covoxFirst);

            bool filterAvailable;
            FrameSound finalMix = CreateFrameSound(
                new[] { ay.AudioBuffer, beeper.AudioBuffer, covox.AudioBuffer },
                false,
                out filterAvailable);
            uint[] rawMix = Mix(finalMix);
            Print("Final idle mix", rawMix, null);

            FrameSound evoMix = CreateFrameSound(
                new[] { ay.AudioBuffer, beeper.AudioBuffer, covox.AudioBuffer },
                true,
                out filterAvailable);
            uint[] filteredMixFirst = Mix(evoMix);
            uint[] filteredMixSecond = Mix(evoMix);
            Print("Filtered idle frame 1", filteredMixFirst, null);
            Print("Filtered idle frame 2", filteredMixSecond, filteredMixFirst);

            uint[] beeperZero = RenderLevelFrame(beeper, 0);
            uint[] covoxZero = RenderLevelFrame(covox, 0);
            Print("Beeper unsigned 0", beeperZero, beeperSecond);
            Print("Covox unsigned 0", covoxZero, covoxSecond);

            Metrics ayIdle = Measure(aySecond);
            Metrics beeperIdle = Measure(beeperSecond);
            Metrics covoxIdle = Measure(covoxSecond);
            Metrics mixIdle = Measure(rawMix);
            Require(ayIdle.MeanLeft < -32000D && ayIdle.MeanRight < -32000D,
                "AY reset level is not the expected unsigned zero");
            Require(Math.Abs(beeperIdle.MeanLeft) < 0.01D &&
                Math.Abs(covoxIdle.MeanLeft) < 0.01D,
                "Unwritten Beeper/Covox buffers are not digital PCM silence");
            Require(mixIdle.MeanLeft < -10000D && mixIdle.MeanRight < -10000D,
                "Final idle mix did not expose the expected DC component");

            if (args.Any(arg => string.Equals(arg, "--verify", StringComparison.OrdinalIgnoreCase)))
            {
                var mixerConfiguration = ay as ISoundMixerConfiguration;
                Require(mixerConfiguration != null && mixerConfiguration.RejectDc,
                    "AYCHRV did not opt the ZX-Evo final mixer into RejectDC");
                Require(!(beeper is ISoundMixerConfiguration) &&
                    !(covox is ISoundMixerConfiguration),
                    "Generic Beeper/Covox unexpectedly changed machine mixer policy");
                Metrics filteredIdle = Measure(filteredMixSecond);
                Metrics filteredIdleFirst = Measure(filteredMixFirst);
                Require(Math.Abs(filteredIdleFirst.MeanLeft) < 1D &&
                    Math.Abs(filteredIdleFirst.MeanRight) < 1D,
                    "ZX-Evo cold idle did not start at digital silence");
                Require(Math.Abs(filteredIdle.MeanLeft) < 1D &&
                    Math.Abs(filteredIdle.MeanRight) < 1D,
                    "ZX-Evo measured idle DC did not converge to silence");
                VerifyRejectDc();
                VerifyBusManagerWiring();
                Console.WriteLine("AudioPathProbe B25+B26: {0} PASS", s_checks);
            }
            else
            {
                Console.WriteLine(
                    "AudioPathProbe B25 baseline: {0} checks; RejectDC available={1}",
                    s_checks,
                    filterAvailable);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("AudioPathProbe FAILED: " + ex);
            return 1;
        }
    }
}
