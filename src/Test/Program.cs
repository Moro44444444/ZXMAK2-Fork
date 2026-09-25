using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Cpu;
using System.Threading;


namespace Test
{
	public class Program
	{
		static void Main(string[] args)
		{
            if (args.Length >= 1 && args[0].ToLower() == "/zexall")
			{
				runZexall();
				return;
			}
            if (args.Length >= 1 && args[0].ToLower() == "/perf")
            {
                runPerf();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/cpu")
            {
                runCpu();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/tsfm")
            {
                TestTurboSoundFmPro();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/multisound")
            {
                TestZxMultiSound();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/moonsound")
            {
                TestZxmMoonSound();
                return;
            }
            if (args.Length >= 1 && args[0].ToLower() == "/zxnetusb")
            {
                TestZxNetUsb();
                return;
            }

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x00 }, s_patternUla48_Early_NOP);
			SanityUla("DJNZ       ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x10, 0x00 }, s_patternUla48_Early_DJNZ);
			SanityUla("IN A,(#FE) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xDB, 0xFE }, s_patternUla48_Early_INAFE);
			SanityUla("OUT (#FE),A", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xD3, 0xFE }, s_patternUla48_Early_OUTAFE);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0x7E }, s_patternUla48_Early_LDAHL);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xED, 0x78 }, s_patternUla48_Early_INAC);
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xED, 0x79 }, s_patternUla48_Early_OUTCA);
			SanityUla("BIT 7,A    ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0x7F }, s_patternUla48_Early_BIT7A);
			SanityUla("BIT 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0x7E }, s_patternUla48_Early_BIT7HL);
			SanityUla("SET 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48_Early(), new byte[] { 0xCB, 0xFE }, s_patternUla48_Early_SET7HL);

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x00 }, s_patternUla48_Late_NOP);
			SanityUla("DJNZ       ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x10, 0x00 }, s_patternUla48_Late_DJNZ);
			SanityUla("IN A,(#FE) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xDB, 0xFE }, s_patternUla48_Late_INAFE);
			SanityUla("OUT (#FE),A", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xD3, 0xFE }, s_patternUla48_Late_OUTAFE);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0x7E }, s_patternUla48_Late_LDAHL);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xED, 0x78 }, s_patternUla48_Late_INAC);
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xED, 0x79 }, s_patternUla48_Late_OUTCA);
			SanityUla("BIT 7,A    ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0x7F }, s_patternUla48_Late_BIT7A);
			SanityUla("BIT 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0x7E }, s_patternUla48_Late_BIT7HL);
			SanityUla("SET 7,(HL) ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum48(), new byte[] { 0xCB, 0xFE }, s_patternUla48_Late_SET7HL);

			SanityUla("NOP        ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x00 }, s_patternUla128_NOP);
			SanityUla("INC HL     ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x23 }, s_patternUla128_INCHL);
			SanityUla("LD A,(HL)  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x7E }, s_patternUla128_LDA_HL_);
			SanityUla("LD (HL),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0x77 }, s_patternUla128_LDA_HL_); // pattern the same as ld a,(hl)
			SanityUla("OUT (C),A  ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0xED, 0x79, 0x03 }, s_patternUla128_OUTCA);
			SanityUla("IN A,(C)   ", new ZXMAK2.Hardware.Spectrum.UlaSpectrum128(), new byte[] { 0xED, 0x78, 0x03 }, s_patternUla128_OUTCA); // pattern the same as out (c),a

			Console.WriteLine("=====Engine Performance Benchmark (500 frames rendering time)=====");
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            int frameCount = 50 * 10;
			ExecTests("testVideo.z80", frameCount);
			ExecTests("testVideo.z80", frameCount);
			ExecTests("testVideo.z80", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("zexall.sna", frameCount);
			ExecTests("testOutFe.z80", frameCount);
			ExecTests("testOutFe.z80", frameCount);
			ExecTests("testOutFe.z80", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecLightTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
            ExecCpuTests("zexall.sna", frameCount);
        }

		private static void SanityUla(string name, IUlaDevice ula, byte[] opcode, int[] pattern)
		{
			IMemoryDevice mem = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();// MemoryPentagon128();
            var p128 = GetTestMachine(Resources.machines_test);
			p128.BusManager.Disconnect();
			p128.BusManager.Clear();
			p128.BusManager.Add((BusDeviceBase)mem);
			p128.BusManager.Add((BusDeviceBase)ula);
			p128.BusManager.Connect();
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();
			p128.IsRunning = false;

			ushort offset = 0x4000;
			for (int i = 0; i < pattern.Length; i++)
			{
				for (int j = 0; j < opcode.Length; j++)
					mem.WRMEM_DBG(offset++, opcode[j]);
			}
			p128.CPU.regs.PC = 0x4000;
			p128.CPU.regs.IR = 0x4000;
			p128.CPU.regs.SP = 0x4000;
			p128.CPU.regs.AF = 0x4000;
			p128.CPU.regs.HL = 0x4000;
			p128.CPU.regs.DE = 0x4000;
			p128.CPU.regs.BC = 0x4000;
			p128.CPU.regs.IX = 0x4000;
			p128.CPU.regs.IY = 0x4000;
			p128.CPU.regs._AF = 0x4000;
			p128.CPU.regs._HL = 0x4000;
			p128.CPU.regs._DE = 0x4000;
			p128.CPU.regs._BC = 0x4000;
			p128.CPU.regs.MW = 0x4000;
			p128.CPU.IFF1 = p128.CPU.IFF2 = false;
			p128.CPU.IM = 2;
			p128.CPU.BINT = false;
			p128.CPU.FX = CpuModeIndex.None;
			p128.CPU.XFX = CpuModeEx.None;

			long needsTact = pattern[0];
			long frameTact = p128.CPU.Tact % ula.FrameTactCount;
			long deltaTact = needsTact - frameTact;
			if (deltaTact < 0)
				deltaTact += ula.FrameTactCount;
			p128.CPU.Tact += deltaTact;

			//if (pattern == s_patternUla48_Late_LDAHL)
			//    p128.Loader.SaveFileName("TEST-LDAHL-48-LATE.SZX");
			//if (pattern == s_patternUla48_Early_LDAHL)
			//    p128.Loader.SaveFileName("TEST-LDAHL-48-EARLY.SZX");

			for (int i = 0; i < pattern.Length - 1; i++)
			{
				p128.DebugStepInto();
				frameTact = p128.CPU.Tact % ula.FrameTactCount;
				if (frameTact != pattern[i + 1])
				{
					ConsoleColor tmp = Console.ForegroundColor;
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine(
						"Sanity ULA {0} [{1}]:\tfailed @ {2}->{3} (should be {2}->{4})",
						ula.GetType().Name,
						name,
						pattern[i],
						frameTact,
						pattern[i + 1]);
					Console.ForegroundColor = tmp;
					return;
				}
			}
			p128.BusManager.Disconnect();
			ConsoleColor tmp2 = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Sanity ULA {0} [{1}]:\tpassed", ula.GetType().Name, name);
			Console.ForegroundColor = tmp2;
		}

        private static Spectrum GetTestMachine(string configXml)
        {
            var config = new XmlDocument();
            config.LoadXml(configXml);
            var machine = new Spectrum();
            try
            {
                // Spectrum.Init loads the portable profile's default machine.
                // The benchmark must start from its embedded MainTest profile,
                // not reuse devices (and their configuration) from that default.
                machine.Init();
                machine.BusManager.Disconnect();
                machine.BusManager.Clear();
                machine.BusManager.LoadConfigXml(config.DocumentElement);
                //var sxml = new XmlDocument();
                //var node = sxml.AppendChild(sxml.CreateElement("Bus"));
                //machine.BusManager.SaveConfigXml(node);
                //sxml.Save("AAAA.xml");
                return machine;
            }
            catch
            {
                machine.Dispose();
                throw;
            }
        }

        private static void TestTurboSoundFmPro()
        {
            IMemoryDevice memory = new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.TurboSoundFmPro();
            var machine = GetTestMachine(Resources.machines_test);

            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            const ushort chip0ReadAddress = 0x4300;
            const ushort chip1ReadAddress = 0x4301;
            const ushort statusAddress = 0x4302;
            var program = new System.Collections.Generic.List<byte>();

            // D1 and D2 are selected by the low bit of the official
            // 1111xxxx CPLD configuration byte.
            AddPortWrite(program, 0xFFFD, 0xFE);
            AddAyWrite(program, 0x00, 0x20);
            AddAyWrite(program, 0x01, 0x01);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x04);
            AddPortWrite(program, 0xFFFD, 0x00);
            AddPortReadAndStore(program, 0xFFFD, chip0ReadAddress);

            AddPortWrite(program, 0xFFFD, 0xFF);
            AddAyWrite(program, 0x00, 0x40);
            AddAyWrite(program, 0x01, 0x01);
            AddAyWrite(program, 0x07, 0x3E);
            AddAyWrite(program, 0x08, 0x08);
            AddPortWrite(program, 0xFFFD, 0x00);
            AddPortReadAndStore(program, 0xFFFD, chip1ReadAddress);

            // FM enabled, register read disabled: #FFFD returns YM status.
            AddPortWrite(program, 0xFFFD, 0xF9);
            AddAyWrite(program, 0xA0, 0x69);
            AddAyWrite(program, 0xA4, 0x22);
            AddAyWrite(program, 0xB0, 0x07);
            AddAyWrite(program, 0x30, 0x01);
            AddAyWrite(program, 0x34, 0x01);
            AddAyWrite(program, 0x38, 0x01);
            AddAyWrite(program, 0x3C, 0x01);
            AddAyWrite(program, 0x50, 0x1F);
            AddAyWrite(program, 0x54, 0x1F);
            AddAyWrite(program, 0x58, 0x1F);
            AddAyWrite(program, 0x5C, 0x1F);
            AddAyWrite(program, 0x28, 0xF0);
            AddPortReadAndStore(program, 0xFFFD, statusAddress);

            // SAA is selected by bit 3=0.  Its register and data cycles must
            // use the same #FFFD/#BFFD pair, not the unrelated #1FF/#FF pair.
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);

            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            bool hasAudio = false;
            foreach (uint sample in machine.BusManager.SoundFrame.GetBuffer())
            {
                if (sample != 0)
                {
                    hasAudio = true;
                    break;
                }
            }

            var sourceAudio = new System.Collections.Generic.List<bool>();
            var sourceClipped = new System.Collections.Generic.List<int>();
            var sourcePeaks = new System.Collections.Generic.List<int>();
            var sourceLengths = new System.Collections.Generic.List<int>();
            foreach (var renderer in board.SoundRenderers)
            {
                bool sourceHasAudio = false;
                int clipped = 0;
                int peak = 0;
                foreach (uint sample in renderer.AudioBuffer)
                {
                    short left = GetLeft(sample);
                    short right = GetRight(sample);
                    int absLeft = Math.Abs((int)left);
                    int absRight = Math.Abs((int)right);
                    if (left != 0 || right != 0)
                        sourceHasAudio = true;
                    if (left == short.MinValue || left == short.MaxValue)
                        clipped++;
                    if (right == short.MinValue || right == short.MaxValue)
                        clipped++;
                    peak = Math.Max(peak, Math.Max(absLeft, absRight));
                }
                sourceAudio.Add(sourceHasAudio);
                sourceClipped.Add(clipped);
                sourcePeaks.Add(peak);
                sourceLengths.Add(renderer.AudioBuffer.Length);
            }

            var gainMethod = typeof(ZXMAK2.Hardware.Evo.TurboSoundFmPro)
                .GetMethod(
                    "ApplyOutputGain",
                    BindingFlags.Static | BindingFlags.NonPublic);
            var gainSamples = new uint[]
            {
                PackStereo(1000, -1000),
                PackStereo(30000, -30000),
                PackStereo(short.MinValue, short.MaxValue),
            };
            gainMethod.Invoke(null, new object[] { gainSamples });
            var gainPassed =
                GetLeft(gainSamples[0]) == 1500 &&
                GetRight(gainSamples[0]) == -1500 &&
                GetLeft(gainSamples[1]) == short.MaxValue &&
                GetRight(gainSamples[1]) == short.MinValue &&
                GetLeft(gainSamples[2]) == short.MinValue &&
                GetRight(gainSamples[2]) == short.MaxValue;

            byte chip0 = memory.RDMEM_DBG(chip0ReadAddress);
            byte chip1 = memory.RDMEM_DBG(chip1ReadAddress);
            byte status = memory.RDMEM_DBG(statusAddress);
            var psgHeadroomPassed =
                sourceClipped[0] < sourceLengths[0] * 2 &&
                sourceClipped[1] < sourceLengths[1] * 2;
            int[] saaCoveragePeaks;
            var saaCoveragePassed = TestSaa1099Coverage(
                board, machine, out saaCoveragePeaks);
            var multiSoundPortCompatibility =
                TestTurboSoundMultiSoundSaaCompatibility();
            machine.BusManager.Disconnect();
            machine.Dispose();

            bool passed = chip0 == 0x20 && chip1 == 0x40 &&
                status == 0x00 && hasAudio && gainPassed &&
                psgHeadroomPassed && saaCoveragePassed &&
                multiSoundPortCompatibility;
            Console.ForegroundColor = passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "TSFM Rev. C: D1=#{0:X2}, D2=#{1:X2}, status=#{2:X2}, audio={3}: {4}",
                chip0,
                chip1,
                status,
                hasAudio,
                passed ? "PASS" : "FAIL");
            Console.WriteLine(
                "Sources: D1={0}, D2={1}, FM={2}, SAA={3}",
                sourceAudio[0],
                sourceAudio[1],
                sourceAudio[2],
                sourceAudio[3]);
            Console.WriteLine(
                "Source peaks/clipped: D1={0}/{1}, D2={2}/{3}, FM={4}/{5}, SAA={6}/{7}",
                sourcePeaks[0], sourceClipped[0],
                sourcePeaks[1], sourceClipped[1],
                sourcePeaks[2], sourceClipped[2],
                sourcePeaks[3], sourceClipped[3]);
            Console.WriteLine("Output gain +50% with saturation: {0}",
                gainPassed ? "PASS" : "FAIL");
            Console.WriteLine("AY/YM SSG native headroom: {0}",
                psgHeadroomPassed ? "PASS" : "FAIL");
            Console.WriteLine(
                "SAA1099 six tones/noise/envelope peaks: {0}: {1}",
                string.Join(",", saaCoveragePeaks),
                saaCoveragePassed ? "PASS" : "FAIL");
            Console.WriteLine(
                "Optional MultiSound #01FF/#00FF SAA ports: {0}",
                multiSoundPortCompatibility ? "PASS" : "FAIL");
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static bool TestTurboSoundMultiSoundSaaCompatibility()
        {
            var nativeDefault =
                !new ZXMAK2.Hardware.Evo.TurboSoundFmPro()
                    .MultiSoundSaaPortCompatibility;
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.TurboSoundFmPro
            {
                MultiSoundSaaPortCompatibility = true,
            };
            var config = new XmlDocument();
            var configNode = config.AppendChild(config.CreateElement("Device"));
            board.SaveConfigXml(configNode);
            var loadedBoard = new ZXMAK2.Hardware.Evo.TurboSoundFmPro();
            loadedBoard.LoadConfigXml(configNode);
            var configRoundTrip =
                loadedBoard.MultiSoundSaaPortCompatibility;
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            AddPortWrite(program, 0x01FF, 0x00);
            AddPortWrite(program, 0x00FF, 0xFF);
            AddPortWrite(program, 0x01FF, 0x08);
            AddPortWrite(program, 0x00FF, 0x80);
            AddPortWrite(program, 0x01FF, 0x10);
            AddPortWrite(program, 0x00FF, 0x03);
            AddPortWrite(program, 0x01FF, 0x14);
            AddPortWrite(program, 0x00FF, 0x01);
            AddPortWrite(program, 0x01FF, 0x1C);
            AddPortWrite(program, 0x00FF, 0x01);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var renderers =
                new System.Collections.Generic.List<ISoundRenderer>(
                    board.SoundRenderers);
            var audible = renderers.Count > 3 &&
                GetStereoPeak(renderers[3].AudioBuffer) > 256;
            machine.BusManager.Disconnect();
            machine.Dispose();
            return nativeDefault && configRoundTrip && audible;
        }

        private static bool TestSaa1099Coverage(
            ZXMAK2.Hardware.Evo.TurboSoundFmPro board,
            Spectrum machine,
            out int[] peaks)
        {
            ISoundRenderer saa = null;
            foreach (var renderer in board.SoundRenderers)
            {
                if (renderer.GetType().Name.IndexOf(
                    "Saa1099", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    saa = renderer;
                    break;
                }
            }
            if (saa == null)
            {
                peaks = new int[0];
                return false;
            }

            var type = saa.GetType();
            var reset = type.GetMethod("ResetChip");
            var setReg = type.GetMethod("SetReg");
            peaks = new int[8];

            for (int channel = 0; channel < 6; channel++)
            {
                reset.Invoke(saa, null);
                setReg.Invoke(saa, new object[] { channel, (byte)0xFF });
                setReg.Invoke(saa, new object[] { 0x08 + channel, (byte)0x80 });
                int octave = (channel & 1) == 0 ? 0x03 : 0x30;
                setReg.Invoke(saa, new object[]
                {
                    0x10 + channel / 2, (byte)octave,
                });
                setReg.Invoke(saa, new object[]
                {
                    0x14, (byte)(1 << channel),
                });
                setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
                machine.ExecuteFrame();
                peaks[channel] = GetStereoPeak(saa.AudioBuffer);
            }

            // Independent noise clock and an internally-clocked repetitive
            // envelope exercise the two paths that old SAA cores most often
            // approximate incorrectly.
            reset.Invoke(saa, null);
            setReg.Invoke(saa, new object[] { 0x00, (byte)0xFF });
            setReg.Invoke(saa, new object[] { 0x16, (byte)0x02 });
            setReg.Invoke(saa, new object[] { 0x15, (byte)0x01 });
            setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
            machine.ExecuteFrame();
            peaks[6] = GetStereoPeak(saa.AudioBuffer);

            reset.Invoke(saa, null);
            setReg.Invoke(saa, new object[] { 0x02, (byte)0xFF });
            setReg.Invoke(saa, new object[] { 0x09, (byte)0xF0 });
            setReg.Invoke(saa, new object[] { 0x10, (byte)0x70 });
            setReg.Invoke(saa, new object[] { 0x0A, (byte)0x80 });
            setReg.Invoke(saa, new object[] { 0x11, (byte)0x03 });
            setReg.Invoke(saa, new object[] { 0x14, (byte)0x04 });
            setReg.Invoke(saa, new object[] { 0x18, (byte)0x86 });
            setReg.Invoke(saa, new object[] { 0x1C, (byte)0x01 });
            machine.ExecuteFrame();
            peaks[7] = GetStereoPeak(saa.AudioBuffer);

            foreach (var peak in peaks)
                if (peak < 256)
                    return false;
            return true;
        }

        private static int GetStereoPeak(uint[] samples)
        {
            var peak = 0;
            foreach (var sample in samples)
            {
                peak = Math.Max(peak, Math.Abs((int)GetLeft(sample)));
                peak = Math.Max(peak, Math.Abs((int)GetRight(sample)));
            }
            return peak;
        }

        private static void TestZxmMoonSound()
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice();
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect() || !board.CoreAvailable)
                throw new InvalidOperationException(
                    "MoonSound core/ROM is unavailable");

            const ushort start = 0x4000;
            const ushort status0Address = 0x4300;
            const ushort status1Address = 0x4301;
            const ushort ramReadAddress = 0x4302;
            var fm = new System.Collections.Generic.List<byte>();

            // Rev.01 decodes the low byte only. Deliberately use non-zero
            // high bytes for all three port groups.
            AddPortReadAndStore(fm, 0x12C4, status0Address);
            AddMoonSoundFmWrite(fm, 0x34C6, 0x05, 0x03); // NEW1 + NEW2
            AddPortReadAndStore(fm, 0x56C6, status1Address);

            // Verify the physical 1 MiB SRAM immediately after the 2 MiB
            // YRW801-M ROM through the OPL4 memory access registers.
            AddMoonSoundWaveWrite(fm, 0x127E, 0x02, 0x01);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x03, 0x20);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x04, 0x00);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x05, 0x00);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x06, 0x5A);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x03, 0x20);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x04, 0x00);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x05, 0x00);
            AddPortWrite(fm, 0x347E, 0x06);
            AddPortReadAndStore(fm, 0x347F, ramReadAddress);
            AddMoonSoundWaveWrite(fm, 0x127E, 0x02, 0x00);

            // One OPL3 voice. #C4/#C5 are the low register bank.
            AddMoonSoundWaveWrite(fm, 0x127E, 0xF8, 0x00);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x20, 0x01);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x23, 0x01);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x40, 0x10);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x43, 0x00);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x60, 0xF0);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x63, 0xF0);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x80, 0x77);
            AddMoonSoundFmWrite(fm, 0x34C4, 0x83, 0x77);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xC0, 0x30);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xA0, 0x98);
            AddMoonSoundFmWrite(fm, 0x34C4, 0xB0, 0x31);
            fm.Add(0x18); fm.Add(0xFE); // JR $
            for (var index = 0; index < fm.Count; ++index)
                memory.WRMEM_DBG((ushort)(start + index), fm[index]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var fmPeak = GetStereoPeak(board.AudioBuffer);
            var status0 = memory.RDMEM_DBG(status0Address);
            var status1 = memory.RDMEM_DBG(status1Address);
            var ramValue = memory.RDMEM_DBG(ramReadAddress);

            // Reset and exercise wavetable channel 0 with ROM waveform 0.
            var pcm = new System.Collections.Generic.List<byte>();
            AddMoonSoundFmWrite(pcm, 0x78C6, 0x05, 0x03);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0xF9, 0x00);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x08, 0x00);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x20, 0xFE);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x38, 0x07);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x50, 0x01);
            AddMoonSoundWaveWrite(pcm, 0x9A7E, 0x68, 0x80);
            pcm.Add(0x18); pcm.Add(0xFE);
            for (var index = 0; index < pcm.Count; ++index)
                memory.WRMEM_DBG((ushort)(start + index), pcm[index]);
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();
            var pcmPeak = GetStereoPeak(board.AudioBuffer);

            // The Rev.01 CPLD releases its decoded ports when PentEvo
            // enables TR-DOS I/O. Exercise that motherboard gate without
            // replacing the Spectrum memory used by the audio program.
            var pentEvoMemory = new ZXMAK2.Hardware.Evo.MemoryPentEvo();
            typeof(ZXMAK2.Hardware.MemoryBase)
                .GetField("m_dosen",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(pentEvoMemory, true);
            typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                .GetField("m_hostMemory",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(board, pentEvoMemory);
            var gatedRead = typeof(ZXMAK2.Hardware.Evo.ZxmMoonSoundDevice)
                .GetMethod("ReadWavePort",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
            var gatedArguments = new object[]
            {
                (ushort)0x127E, (byte)0xA5, false,
            };
            gatedRead.Invoke(board, gatedArguments);
            var dosGatePassed = !(bool)gatedArguments[2] &&
                (byte)gatedArguments[1] == 0xA5;

            machine.BusManager.Disconnect();
            machine.Dispose();
            var passed = status0 != 0xFF && status1 != 0xFF &&
                ramValue == 0x5A && fmPeak > 64 && pcmPeak > 64 &&
                dosGatePassed;
            Console.ForegroundColor = passed
                ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "ZXM-MoonSound Rev.01: status=#{0:X2}/#{1:X2}, " +
                "SRAM=#{2:X2}, FM peak={3}, PCM peak={4}, " +
                "TR-DOS gate={5}: {6}",
                status0, status1, ramValue, fmPeak, pcmPeak,
                dosGatePassed ? "PASS" : "FAIL",
                passed ? "PASS" : "FAIL");
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static void AddMoonSoundFmWrite(
            System.Collections.Generic.List<byte> program,
            ushort addressPort,
            byte register,
            byte value)
        {
            AddPortWrite(program, addressPort, register);
            AddPortWrite(program, (ushort)(addressPort + 1), value);
        }

        private static void TestZxNetUsb()
        {
            TestZxNetUsbPortDecode();
            var board = new ZXMAK2.Hardware.Evo.ZxNetUsbDevice();
            TcpListener listener = null;
            TcpClient peer = null;
            try
            {
                board.BusConnect();
                ZxNetWritePort(board, 0x82AB, 0x10);

                byte idHigh = ZxNetReadRegister(board, 0x00FE);
                byte idLow = ZxNetReadRegister(board, 0x00FF);
                if (idHigh != 0x53 || idLow != 0x00)
                    throw new InvalidOperationException(
                        "ZXNetUSB did not expose W5300 ID #5300");

                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                const int socket0 = 0x200;
                ZxNetWriteRegister(board, socket0 + 0x01, 0x01);
                ZxNetWriteWord(board, socket0 + 0x0A, 49152);
                ZxNetWriteRegister(board, socket0 + 0x14, 127);
                ZxNetWriteRegister(board, socket0 + 0x15, 0);
                ZxNetWriteRegister(board, socket0 + 0x16, 0);
                ZxNetWriteRegister(board, socket0 + 0x17, 1);
                ZxNetWriteWord(board, socket0 + 0x12, port);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x01);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x04);

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline &&
                    ZxNetReadRegister(board, socket0 + 0x09) != 0x17)
                    Thread.Sleep(5);
                if (ZxNetReadRegister(board, socket0 + 0x09) != 0x17)
                    throw new InvalidOperationException(
                        "W5300 TCP socket did not connect");
                peer = listener.AcceptTcpClient();
                peer.ReceiveTimeout = 2000;

                byte[] request = System.Text.Encoding.ASCII.GetBytes("PING");
                for (int index = 0; index < request.Length; index++)
                    ZxNetWriteRegister(board,
                        socket0 + 0x2E + (index & 1), request[index]);
                ZxNetWriteWord(board, socket0 + 0x22, request.Length);
                ZxNetWriteRegister(board, socket0 + 0x03, 0x20);
                byte[] received = new byte[request.Length];
                int receivedCount = peer.GetStream().Read(received, 0,
                    received.Length);
                if (receivedCount != request.Length ||
                    System.Text.Encoding.ASCII.GetString(received) != "PING")
                    throw new InvalidOperationException(
                        "W5300 TCP transmit path failed");

                byte[] response = System.Text.Encoding.ASCII.GetBytes("PONG");
                peer.GetStream().Write(response, 0, response.Length);
                deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline &&
                    ZxNetReadWord(board, socket0 + 0x2A) == 0)
                    Thread.Sleep(5);
                int packetLength = (ZxNetReadRegister(board,
                    socket0 + 0x30) << 8) |
                    ZxNetReadRegister(board, socket0 + 0x31);
                byte[] reply = new byte[packetLength];
                for (int index = 0; index < reply.Length; index++)
                    reply[index] = ZxNetReadRegister(board,
                        socket0 + 0x30 + (index & 1));
                ZxNetWriteRegister(board, socket0 + 0x03, 0x40);
                if (System.Text.Encoding.ASCII.GetString(reply) != "PONG")
                    throw new InvalidOperationException(
                        "W5300 TCP receive path failed");

                // NedoOS normally obtains its address through DHCP.  Verify
                // the built-in host-side DHCP reply without using the PC's
                // privileged UDP/68 service.
                const int socket1 = 0x240;
                ZxNetWriteRegister(board, socket1 + 0x01, 0x02);
                ZxNetWriteWord(board, socket1 + 0x0A, 68);
                ZxNetWriteRegister(board, socket1 + 0x14, 255);
                ZxNetWriteRegister(board, socket1 + 0x15, 255);
                ZxNetWriteRegister(board, socket1 + 0x16, 255);
                ZxNetWriteRegister(board, socket1 + 0x17, 255);
                ZxNetWriteWord(board, socket1 + 0x12, 67);
                ZxNetWriteRegister(board, socket1 + 0x03, 0x01);
                byte[] discover = new byte[244];
                discover[0] = 1; discover[1] = 1; discover[2] = 6;
                discover[4] = 0x12; discover[5] = 0x34;
                discover[6] = 0x56; discover[7] = 0x78;
                discover[236] = 0x63; discover[237] = 0x82;
                discover[238] = 0x53; discover[239] = 0x63;
                discover[240] = 53; discover[241] = 1;
                discover[242] = 1; discover[243] = 255;
                for (int index = 0; index < discover.Length; index++)
                    ZxNetWriteRegister(board,
                        socket1 + 0x2E + (index & 1), discover[index]);
                ZxNetWriteWord(board, socket1 + 0x22, discover.Length);
                ZxNetWriteRegister(board, socket1 + 0x03, 0x20);
                int udpLength = ZxNetReadWord(board, socket1 + 0x2A);
                byte[] udpHeader = new byte[8];
                for (int index = 0; index < udpHeader.Length; index++)
                    udpHeader[index] = ZxNetReadRegister(board,
                        socket1 + 0x30 + (index & 1));
                int dhcpLength = (udpHeader[6] << 8) | udpHeader[7];
                byte[] offer = new byte[dhcpLength];
                for (int index = 0; index < offer.Length; index++)
                    offer[index] = ZxNetReadRegister(board,
                        socket1 + 0x30 + (index & 1));
                bool dhcpPassed = udpLength >= dhcpLength + 8 &&
                    offer.Length > 19 && offer[0] == 2 &&
                    offer[16] == 10 && offer[17] == 0 &&
                    offer[18] == 2 && offer[19] == 15;
                if (!dhcpPassed)
                    throw new InvalidOperationException(
                        "ZXNetUSB DHCP reply failed");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    "ZXNetUSB Rev.C: Z80 I/O=PASS, W5300 ID=#5300, " +
                    "TCP TX/RX=PASS, DHCP=10.0.2.15 PASS");
                Console.ResetColor();
            }
            finally
            {
                if (peer != null)
                    peer.Close();
                if (listener != null)
                    listener.Stop();
                board.BusDisconnect();
            }
        }

        private static void TestZxNetUsbPortDecode()
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxNetUsbDevice();
            var machine = GetTestMachine(Resources.machines_test);
            try
            {
                machine.BusManager.Disconnect();
                machine.BusManager.Clear();
                machine.BusManager.Add((BusDeviceBase)memory);
                machine.BusManager.Add((BusDeviceBase)ula);
                machine.BusManager.Add(board);
                machine.BusManager.Connect();

                const ushort programAddress = 0x4000;
                const ushort resultAddress = 0x4300;
                var program = new System.Collections.Generic.List<byte>();
                AddPortWrite(program, 0x82AB, 0x10);
                AddPortWrite(program, 0x81AB, 0x03);
                AddPortReadAndStore(program, 0x3EAB, resultAddress);
                AddPortReadAndStore(program, 0x3FAB,
                    (ushort)(resultAddress + 1));
                for (int index = 0; index < program.Count; index++)
                    memory.WRMEM_DBG((ushort)(programAddress + index),
                        program[index]);

                machine.IsRunning = false;
                machine.DebugReset();
                machine.CPU.regs.PC = programAddress;
                while (machine.CPU.regs.PC <
                    programAddress + program.Count)
                    machine.DebugStepInto();

                if (memory.RDMEM_DBG(resultAddress) != 0x53 ||
                    memory.RDMEM_DBG((ushort)(resultAddress + 1)) != 0x00)
                    throw new InvalidOperationException(
                        "ZXNetUSB Z80 I/O decode did not expose W5300 ID");
            }
            finally
            {
                machine.BusManager.Disconnect();
                machine.Dispose();
            }
        }

        private static void ZxNetWriteWord(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address,
            int value)
        {
            ZxNetWriteRegister(board, address, (byte)(value >> 8));
            ZxNetWriteRegister(board, address + 1, (byte)value);
        }

        private static int ZxNetReadWord(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address)
        {
            return (ZxNetReadRegister(board, address) << 8) |
                ZxNetReadRegister(board, address + 1);
        }

        private static void ZxNetWriteRegister(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address,
            byte value)
        {
            ZxNetWritePort(board, 0x81AB, (byte)(address >> 6));
            // Use the lower mirror exactly like the current NedoOS driver.
            ushort port = (ushort)((address & 0x3F) << 8 | 0xAB);
            ZxNetWritePort(board, port, value);
        }

        private static byte ZxNetReadRegister(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, int address)
        {
            ZxNetWritePort(board, 0x81AB, (byte)(address >> 6));
            ushort port = (ushort)((address & 0x3F) << 8 | 0xAB);
            return ZxNetReadPort(board, port);
        }

        private static void ZxNetWritePort(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, ushort port,
            byte value)
        {
            string name = port == 0x81AB ? "WriteAddress" :
                port == 0x82AB ? "WriteControl" :
                port == 0x83AB ? "WriteInterrupt" : "WriteW5300";
            MethodInfo method = board.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { port, value, false };
            method.Invoke(board, arguments);
            if (!(bool)arguments[2])
                throw new InvalidOperationException(
                    "ZXNetUSB did not handle write to #" +
                    port.ToString("X4"));
        }

        private static byte ZxNetReadPort(
            ZXMAK2.Hardware.Evo.ZxNetUsbDevice board, ushort port)
        {
            string name = port == 0x81AB ? "ReadAddress" :
                port == 0x82AB ? "ReadControl" :
                port == 0x83AB ? "ReadInterrupt" : "ReadW5300";
            MethodInfo method = board.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            object[] arguments = { port, (byte)0xFF, false };
            method.Invoke(board, arguments);
            if (!(bool)arguments[2])
                throw new InvalidOperationException(
                    "ZXNetUSB did not handle read from #" +
                    port.ToString("X4"));
            return (byte)arguments[1];
        }

        private static void AddMoonSoundWaveWrite(
            System.Collections.Generic.List<byte> program,
            ushort addressPort,
            byte register,
            byte value)
        {
            AddPortWrite(program, addressPort, register);
            AddPortWrite(program, (ushort)(addressPort + 1), value);
        }

        private static void TestZxMultiSound()
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            var machine = GetTestMachine(Resources.machines_test);

            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("MultiSound bus connect failed");

            const ushort programAddress = 0x4000;
            const ushort ymReadAddress = 0x4300;
            const ushort gsStatusAddress = 0x4301;
            const ushort gsReadyStatusAddress = 0x4302;
            const ushort gsReadyProgramAddress = 0x4400;
            var program = new System.Collections.Generic.List<byte>();

            // SAA address/data are #1FF/#FF, unlike the socket TSFM board.
            // The real CPLD keeps these writes active while saa_clk_en is
            // false.  Preload the complete voice before #F7 so a regression
            // that silently discards stopped-clock writes cannot pass.
            AddPortWrite(program, 0x01FF, 0x00);
            AddPortWrite(program, 0x00FF, 0xFF);
            AddPortWrite(program, 0x01FF, 0x08);
            AddPortWrite(program, 0x00FF, 0x80);
            AddPortWrite(program, 0x01FF, 0x10);
            AddPortWrite(program, 0x00FF, 0x03);
            AddPortWrite(program, 0x01FF, 0x14);
            AddPortWrite(program, 0x00FF, 0x01);
            AddPortWrite(program, 0x01FF, 0x1C);
            AddPortWrite(program, 0x00FF, 0x01);

            // Rev.A2 uses partial YM decoding. These aliases deliberately are
            // not the canonical #FFFD/#BFFD pair. Configure both physical
            // YM2203 SSG sections with independent tones; a register-only
            // check used by the older probe could pass with one silent chip.
            AddPortWrite(program, 0xD00D, 0xF6);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortWrite(program, 0x900D, 0x34);
            AddPortWrite(program, 0xD00D, 0x01);
            AddPortWrite(program, 0x900D, 0x01);
            AddPortWrite(program, 0xD00D, 0x07);
            AddPortWrite(program, 0x900D, 0x3E);
            AddPortWrite(program, 0xD00D, 0x08);
            AddPortWrite(program, 0x900D, 0x0F);

            AddPortWrite(program, 0xD00D, 0xF7);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortWrite(program, 0x900D, 0x62);
            AddPortWrite(program, 0xD00D, 0x01);
            AddPortWrite(program, 0x900D, 0x02);
            AddPortWrite(program, 0xD00D, 0x07);
            AddPortWrite(program, 0x900D, 0x3E);
            AddPortWrite(program, 0xD00D, 0x08);
            AddPortWrite(program, 0x900D, 0x0F);

            // Return to D1 with the SAA clock enabled, then select register 0
            // and verify the D1 value through another partial port alias.
            AddPortWrite(program, 0xD00D, 0xF6);
            AddPortWrite(program, 0xD00D, 0x00);
            AddPortReadAndStore(program, 0xE00D, ymReadAddress);

            // Four physical DAC aliases and a GS command/status handshake.
            AddPortWrite(program, 0x000F, 0x20);
            AddPortWrite(program, 0x001F, 0x60);
            AddPortWrite(program, 0x004F, 0xA0);
            AddPortWrite(program, 0x005F, 0xE0);
            AddPortWrite(program, 0x00BB, 0x00);
            AddPortReadAndStore(program, 0x00BB, gsStatusAddress);

            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var ym = memory.RDMEM_DBG(ymReadAddress);
            var gsStatus = memory.RDMEM_DBG(gsStatusAddress);
            var saaAudio = false;
            var soundRenderers = new System.Collections.Generic.List<ISoundRenderer>(board.SoundRenderers);
            var ymD1Audio = soundRenderers.Count > 0 &&
                GetStereoPeak(soundRenderers[0].AudioBuffer) > 0;
            var ymD2Audio = soundRenderers.Count > 1 &&
                GetStereoPeak(soundRenderers[1].AudioBuffer) > 0;
            foreach (var renderer in soundRenderers)
            {
                if (renderer.GetType().Name.IndexOf("Saa1099",
                    StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                foreach (var sample in renderer.AudioBuffer)
                    saaAudio |= sample != 0;
            }
            var dacAudio = false;
            foreach (var sample in board.AudioBuffer)
                dacAudio |= sample != 0;

            // Rev.A2 remembers whether the last host opcode fetch was in the
            // lower 16K and locks SAA/SounDrive in that state.  Exercise the
            // write guard directly so this CPLD quirk cannot regress silently.
            var boardType = typeof(ZXMAK2.Hardware.Evo.ZxMultiSoundDevice);
            var romLockField = boardType.GetField(
                "m_hostRomM1Access",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dacSampleField = boardType.GetField(
                "m_dacSample",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var writeSoundDrive = boardType.GetMethod(
                "WriteSoundDrive",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var beforeLockedWrite = ((byte[])dacSampleField.GetValue(board))[0];
            romLockField.SetValue(board, true);
            var lockedWriteArgs = new object[]
            {
                (ushort)0x000F, (byte)(beforeLockedWrite ^ 0xFF), false,
            };
            writeSoundDrive.Invoke(board, lockedWriteArgs);
            var romLockPassed =
                ((byte[])dacSampleField.GetValue(board))[0] == beforeLockedWrite;
            romLockField.SetValue(board, false);

            // Keep the host Z80 in a harmless loop while the independent GS
            // Z80 completes the official ROM RAM test and consumes command 0.
            memory.WRMEM_DBG(0x4500, 0x18);
            memory.WRMEM_DBG(0x4501, 0xFE);
            machine.CPU.regs.PC = 0x4500;
            for (var frame = 1; frame < 200; frame++)
                machine.ExecuteFrame();

            var readyProgram = new System.Collections.Generic.List<byte>();
            AddPortReadAndStore(
                readyProgram, 0x00BB, gsReadyStatusAddress);
            for (var i = 0; i < readyProgram.Count; i++)
                memory.WRMEM_DBG(
                    (ushort)(gsReadyProgramAddress + i), readyProgram[i]);
            machine.CPU.regs.PC = gsReadyProgramAddress;
            while (machine.CPU.regs.PC <
                gsReadyProgramAddress + readyProgram.Count)
                machine.DebugStepInto();

            var gsReadyStatus = memory.RDMEM_DBG(gsReadyStatusAddress);

            var auto = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            auto.ResolveConfiguration(true, true);
            var autoPolicy = !auto.EffectiveGeneralSoundEnabled &&
                !auto.EffectiveYmEnabled && auto.EffectiveSaaEnabled &&
                auto.EffectiveSoundDriveEnabled;
            var manualRejected = false;
            var manual = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            manual.AutomaticConfiguration = false;
            try
            {
                manual.ResolveConfiguration(true, false);
            }
            catch (InvalidOperationException)
            {
                manualRejected = true;
            }

            int gsDacTransitions;
            int soundDriveTransitions;
            var gsDacTimeline = TestMultiSoundGsDacTimeline(
                out gsDacTransitions);
            var soundDriveTimeline = TestMultiSoundSoundDriveTimeline(
                out soundDriveTransitions);
            var tsFmPortCompatibility =
                TestMultiSoundTsFmSaaCompatibility();

            machine.BusManager.Disconnect();
            machine.Dispose();
            var passed = ym == 0x34 && ymD1Audio && ymD2Audio &&
                (gsStatus & 1) != 0 &&
                (gsReadyStatus & 1) == 0 &&
                saaAudio && dacAudio && romLockPassed &&
                autoPolicy && manualRejected && gsDacTimeline &&
                soundDriveTimeline && tsFmPortCompatibility;
            Console.ForegroundColor = passed
                ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(
                "ZX-MultiSound A2: YM=#{0:X2}, D1={1}, D2={2}, " +
                "GSSTAT=#{3:X2}->#{4:X2}, SAA-PRELOAD={5}, DAC={6}, " +
                "ROM-LOCK={7}, AUTO={8}, MANUAL-GUARD={9}: {10}",
                ym, ymD1Audio, ymD2Audio, gsStatus, gsReadyStatus,
                saaAudio, dacAudio, romLockPassed, autoPolicy, manualRejected,
                passed ? "PASS" : "FAIL");
            Console.WriteLine(
                "GS DAC timeline transitions={0}: {1}; " +
                "SounDrive transitions={2}: {3}",
                gsDacTransitions, gsDacTimeline ? "PASS" : "FAIL",
                soundDriveTransitions,
                soundDriveTimeline ? "PASS" : "FAIL");
            Console.WriteLine(
                "Optional TSFM #FFFD/#BFFD SAA ports: {0}",
                tsFmPortCompatibility ? "PASS" : "FAIL");
            Console.ResetColor();
            if (!passed)
                Environment.ExitCode = 1;
        }

        private static bool TestMultiSoundTsFmSaaCompatibility()
        {
            var nativeDefault =
                !new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice()
                    .TsFmSaaPortCompatibility;
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                AutomaticConfiguration = false,
                YmEnabled = false,
                SaaEnabled = true,
                GeneralSoundEnabled = false,
                SoundDriveEnabled = false,
                TsFmSaaPortCompatibility = true,
            };
            var config = new XmlDocument();
            var configNode = config.AppendChild(config.CreateElement("Device"));
            board.SaveConfigXml(configNode);
            var loadedBoard = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice();
            loadedBoard.LoadConfigXml(configNode);
            var configRoundTrip = loadedBoard.TsFmSaaPortCompatibility;
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            machine.BusManager.Connect();

            const ushort programAddress = 0x4000;
            var program = new System.Collections.Generic.List<byte>();
            AddPortWrite(program, 0xFFFD, 0xF7);
            AddAyWrite(program, 0x00, 0xFF);
            AddAyWrite(program, 0x08, 0x80);
            AddAyWrite(program, 0x10, 0x03);
            AddAyWrite(program, 0x14, 0x01);
            AddAyWrite(program, 0x1C, 0x01);
            for (var i = 0; i < program.Count; i++)
                memory.WRMEM_DBG((ushort)(programAddress + i), program[i]);

            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = programAddress;
            while (machine.CPU.regs.PC < programAddress + program.Count)
                machine.DebugStepInto();
            machine.ExecuteFrame();

            var renderers =
                new System.Collections.Generic.List<ISoundRenderer>(
                    board.SoundRenderers);
            var audible = renderers.Count > 3 &&
                GetStereoPeak(renderers[3].AudioBuffer) > 256;
            machine.BusManager.Disconnect();
            machine.Dispose();
            return nativeDefault && configRoundTrip && audible;
        }

        private static bool TestMultiSoundGsDacTimeline(out int transitions)
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                YmEnabled = false,
                SaaEnabled = false,
                SoundDriveEnabled = false,
                GeneralSoundEnabled = true,
            };
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException("GS DAC test connect failed");

            machine.IsRunning = false;
            machine.DebugReset();
            memory.WRMEM_DBG(0x4000, 0x18); // JR $ keeps the host alive
            memory.WRMEM_DBG(0x4001, 0xFE);
            machine.CPU.regs.PC = 0x4000;

            var boardType = board.GetType();
            var gsRam = (byte[])boardType.GetField(
                "m_gsRam", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(board);
            var gsCpu = (CpuUnit)boardType.GetField(
                "m_gsCpu", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(board);

            // Fixed GS RAM page: #4000 -> physical #C000, #6000 -> #E000.
            // This tiny private-Z80 loop alternates two PCM values on DAC 0.
            byte[] program =
            {
                0x3E, 0x3F,       // LD A,#3F
                0xD3, 0x06,       // OUT (#06),A: channel 0 volume
                0x3A, 0x00, 0x60, // LD A,(#6000): DAC sample #10
                0x06, 0x50,       // LD B,#50: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0x3A, 0x01, 0x60, // LD A,(#6001): DAC sample #F0
                0x06, 0x50,       // LD B,#50: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0xC3, 0x04, 0x40, // JP #4004
            };
            Array.Copy(program, 0, gsRam, 0xC000, program.Length);
            gsRam[0xE000] = 0x10;
            gsRam[0xE001] = 0xF0;
            gsCpu.regs.PC = 0x4000;
            machine.ExecuteFrame();

            transitions = CountLeftSignTransitions(board.AudioBuffer);
            machine.BusManager.Disconnect();
            machine.Dispose();
            return transitions > 20;
        }

        private static bool TestMultiSoundSoundDriveTimeline(
            out int transitions)
        {
            IMemoryDevice memory =
                new ZXMAK2.Hardware.Spectrum.MemorySpectrum48();
            var ula = new ZXMAK2.Hardware.Spectrum.UlaSpectrum48();
            var board = new ZXMAK2.Hardware.Evo.ZxMultiSoundDevice
            {
                YmEnabled = false,
                SaaEnabled = false,
                GeneralSoundEnabled = false,
                SoundDriveEnabled = true,
            };
            var machine = GetTestMachine(Resources.machines_test);
            machine.BusManager.Disconnect();
            machine.BusManager.Clear();
            machine.BusManager.Add((BusDeviceBase)memory);
            machine.BusManager.Add((BusDeviceBase)ula);
            machine.BusManager.Add(board);
            if (!machine.BusManager.Connect())
                throw new InvalidOperationException(
                    "SounDrive DAC test connect failed");

            const ushort start = 0x4000;
            byte[] program =
            {
                0x01, 0x0F, 0x00, // LD BC,#000F
                0x3E, 0x10,       // LD A,#10
                0xED, 0x79,       // OUT (C),A
                0x06, 0x28,       // LD B,#28: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0x3E, 0xF0,       // LD A,#F0
                0xED, 0x79,       // OUT (C),A
                0x06, 0x28,       // LD B,#28: audible-rate hold
                0x10, 0xFE,       // DJNZ $
                0xC3, 0x03, 0x40, // JP #4003
            };
            for (var i = 0; i < program.Length; i++)
                memory.WRMEM_DBG((ushort)(start + i), program[i]);
            machine.IsRunning = false;
            machine.DebugReset();
            machine.CPU.regs.PC = start;
            machine.ExecuteFrame();

            transitions = CountLeftSignTransitions(board.AudioBuffer);
            machine.BusManager.Disconnect();
            machine.Dispose();
            return transitions > 20;
        }

        private static int CountLeftSignTransitions(uint[] samples)
        {
            var transitions = 0;
            var previous = 0;
            var hasPrevious = false;
            foreach (var sample in samples)
            {
                var value = GetLeft(sample);
                var sign = value < 0 ? -1 : value > 0 ? 1 : 0;
                if (sign == 0)
                    continue;
                if (hasPrevious && sign != previous)
                    transitions++;
                previous = sign;
                hasPrevious = true;
            }
            return transitions;
        }

        private static uint PackStereo(short left, short right)
        {
            return (uint)((ushort)left | ((uint)(ushort)right << 16));
        }

        private static short GetLeft(uint sample)
        {
            return (short)(sample & 0xFFFF);
        }

        private static short GetRight(uint sample)
        {
            return (short)(sample >> 16);
        }

        private static void AddAyWrite(
            System.Collections.Generic.List<byte> program,
            byte register,
            byte value)
        {
            AddPortWrite(program, 0xFFFD, register);
            AddPortWrite(program, 0xBFFD, value);
        }

        private static void AddPortWrite(
            System.Collections.Generic.List<byte> program,
            ushort port,
            byte value)
        {
            program.Add(0x01);
            program.Add((byte)port);
            program.Add((byte)(port >> 8));
            program.Add(0x3E);
            program.Add(value);
            program.Add(0xED);
            program.Add(0x79);
        }

        private static void AddPortReadAndStore(
            System.Collections.Generic.List<byte> program,
            ushort port,
            ushort destination)
        {
            program.Add(0x01);
            program.Add((byte)port);
            program.Add((byte)(port >> 8));
            program.Add(0xED);
            program.Add(0x78);
            program.Add(0x32);
            program.Add((byte)destination);
            program.Add((byte)(destination >> 8));
        }

		#region ULA PATTERNS

		private static int[] s_patternUla48_Late_NOP = new int[]
        {
            14300, 14304, 14308, 14312, 14316, 14320, 14324, 14328, 14332, 
            14336, 14346, 14354, 14362, 14370, 14378, 14386, 14394, 14402, 14410, 14418, 14426, 14434, 14442, 14450, 14458, 14466, 14470, 14474, 14478, 14482, 14486, 14490, 14494, 14498, 
            14502, 14506, 14510, 14514, 14518, 14522, 14526, 14530, 14534, 14538, 14542, 14546, 14550, 14554, 14558, 14562, 14570, 14578, 14586, 14594, 14602, 14610, 14618, 14626, 14634,
            14642, 14650, 14658, 14666, 14674, 14682, 14690, 14694, 14698, 14702, 14706, 14710, 14714, 14718, 14722, 14726, 14730, 14734, 14738, 14742, 14746, 14750, 14754, 14758, 14762,
            14766, 14770, 14774, 14778, 14782, 14786, 14794, 14802,
        };

		private static int[] s_patternUla48_Early_NOP = new int[]
        {
            14300, 14304, 14308, 14312, 14316, 14320, 14324, 14328, 14332,
            14336, 14345, 14353, 14361, 14369, 14377, 14385, 14393, 14401, 14409, 14417, 14425, 14433, 14441, 14449, 14457, 14465, 14469, 14473, 14477, 14481, 14485, 14489, 14493, 14497,
            14501, 14505, 14509, 14513, 14517, 14521, 14525, 14529, 14533, 14537, 14541, 14545, 14549, 14553, 14557, 14561, 14569, 14577, 14585, 14593, 14601, 14609, 14617, 14625, 14633,
            14641, 14649, 14657, 14665, 14673, 14681, 14689, 14693, 14697, 14701, 14705, 14709, 14713, 14717, 14721, 14725, 14729, 14733, 
        };

		private static int[] s_patternUla48_Late_DJNZ = new int[]
        {
            14300, 14313, 14326, 14351, 14383, 14415, 14447, 14467, 14480, 14493, 14506, 14519, 14532, 14545, 14558, 14591, 14623, 14655, 14687, 14700, 14713, 14726, 14739, 14752, 14765,
            14778, 14807, 14839, 14871, 14903, 14919, 14932, 14945, 14958, 14971, 14984, 14997, 15016, 15055, 15087, 15119, 15139, 15152, 15165, 15178, 15191, 15204, 15217, 15230, 15263,
            15295, 15327,
        };

		private static int[] s_patternUla48_Early_DJNZ = new int[]
        {
            14300, 14313, 14326, 14351, 14390, 14422, 14454, 14470, 14483, 14496, 14509, 14522, 14535, 14548, 14567, 14606, 14638, 14670, 14690, 14703, 14716, 14729, 14742, 14755, 14768,
            14781, 14814, 14846, 14878, 14910, 14923, 14936, 14949, 14962, 14975, 14988, 15001, 15030, 15062, 15094, 15126, 15142, 15155, 15168, 15181, 15194, 15207, 15220, 15239, 15278,
            15310,
        };

		private static int[] s_patternUla48_Late_INAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14353, 14377, 14401, 14425, 14449, 14469, 14480, 14491, 14502, 14513, 14524, 14535, 14546, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14704,
            14715, 14726, 14737, 14748, 14759, 14770, 14781, 14801, 14825, 14849, 14873, 14897, 14917, 14928, 14939, 14950, 14961, 14972, 14983, 14994, 15005, 15025, 15049, 15073, 15097,
            15121, 15141, 15152,
        };

		private static int[] s_patternUla48_Early_INAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14352, 14376, 14400, 14424, 14448, 14468, 14479, 14490, 14501, 14512, 14523, 14534, 14545, 14556, 14576, 14600, 14624, 14648, 14672, 14692, 14703,
            14714, 14725, 14736, 14747, 14758, 14769, 14780, 14800, 14824, 14848, 14872, 14896, 14916, 14927, 14938, 14949, 14960, 14971, 14982, 14993, 15004, 15024, 15048, 15072, 15096,
            15120, 15140, 15151, 
        };

		private static int[] s_patternUla48_Late_OUTAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14354, 14378, 14402, 14426, 14450, 14469, 14480, 14491, 14502, 14513, 14524, 14535, 14546, 14557, 14578, 14602, 14626, 14650, 14674, 14693, 14704,
            14715, 14726, 14737, 14748, 14759, 14770, 14781, 14802, 14826, 14850, 14874, 14898, 14917, 14928, 14939, 14950, 14961, 14972, 14983, 14994, 15005, 15026, 15050, 15074, 15098,
            15122, 15141, 15152, 15163,
        };

		private static int[] s_patternUla48_Early_OUTAFE = new int[]
        {
            14300, 14311, 14322, 14333, 14353, 14377, 14401, 14425, 14449, 14468, 14479, 14490, 14501, 14512, 14523, 14534, 14545, 14556, 14577, 14601, 14625, 14649, 14673, 14692, 14703, 
            14714, 14725, 14736, 14747, 14758, 14769, 14780, 14801, 14825, 14849, 14873, 14897, 14916, 14927, 14938, 14949, 14960, 14971, 14982, 14993, 15004, 15025, 15049, 15073, 15097, 
            15121, 15140, 15151, 15162,
        };

		private static int[] s_patternUla48_Late_LDAHL = new int[]
        {
            14300, 14307, 14314, 14321, 14328, 14335, 14345, 14361, 14377, 14393, 14409, 14425, 14441, 14457, 14469, 14476, 14483, 14490, 14497, 14504, 14511, 14518, 14525, 14532, 14539,
            14546, 14553, 14560, 14577, 14593, 14609, 14625, 14641, 14657, 14673, 14689, 14696, 14703, 14710, 14717, 14724, 14731, 14738, 14745, 14752, 14759, 14766, 14773, 14780, 14793,
            14809, 14825, 14841, 14857, 14873,
        };

		private static int[] s_patternUla48_Early_LDAHL = new int[]
        {
            14300, 14307, 14314, 14321, 14328, 14335, 14352, 14368, 14384, 14400, 14416, 14432, 14448, 14464, 14471, 14478, 14485, 14492, 14499, 14506, 14513, 14520, 14527, 14534, 14541,
            14548, 14555, 14568, 14584, 14600, 14616, 14632, 14648, 14664, 14680, 14692, 14699, 14706, 14713, 14720, 14727, 14734, 14741, 14748, 14755, 14762, 14769, 14776, 14783, 14800, 
            14816, 14832, 14848, 14864, 14880,
        };

		private static int[] s_patternUla48_Late_INAC = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010,
        };

		private static int[] s_patternUla48_Early_INAC = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009,
        };

		private static int[] s_patternUla48_Late_OUTCA = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010, 15034,
        };

		private static int[] s_patternUla48_Early_OUTCA = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009, 15033,
        };

		private static int[] s_patternUla48_Late_BIT7A = new int[]
        {
            14300, 14308, 14316, 14324, 14332, 14346, 14362, 14378, 14394, 14410, 14426, 14442, 14458, 14470, 14478, 14486, 14494, 14502, 14510, 14518, 14526, 14534, 14542, 14550, 14558,
            14570, 14586, 14602, 14618, 14634, 14650, 14666, 14682, 14694, 14702, 14710, 14718, 14726, 14734, 14742, 14750, 14758, 14766, 14774, 14782, 14794,
        };

		private static int[] s_patternUla48_Early_BIT7A = new int[]
        {
            14300, 14308, 14316, 14324, 14332, 14345, 14361, 14377, 14393, 14409, 14425, 14441, 14457, 14469, 14477, 14485, 14493, 14501, 14509, 14517, 14525, 14533, 14541, 14549, 14557,
            14569, 14585, 14601, 14617, 14633, 14649, 14665, 14681, 14693, 14701, 14709, 14717, 14725, 14733, 14741, 14749, 14757, 14765, 14773, 14781, 14793,
        };

		private static int[] s_patternUla48_Late_BIT7HL = new int[]
        {
            14300, 14312, 14324, 14336, 14362, 14386, 14410, 14434, 14458, 14474, 14486, 14498, 14510, 14522, 14534, 14546, 14558, 14578, 14602, 14626, 14650, 14674, 14694, 14706, 14718,
            14730, 14742, 14754, 14766, 14778, 14794, 14818, 14842, 14866, 14890, 14914, 14926, 14938, 14950, 14962, 14974, 14986, 14998, 15010, 15034
        };

		private static int[] s_patternUla48_Early_BIT7HL = new int[]
        {
            14300, 14312, 14324, 14336, 14361, 14385, 14409, 14433, 14457, 14473, 14485, 14497, 14509, 14521, 14533, 14545, 14557, 14577, 14601, 14625, 14649, 14673, 14693, 14705, 14717,
            14729, 14741, 14753, 14765, 14777, 14793, 14817, 14841, 14865, 14889, 14913, 14925, 14937, 14949, 14961, 14973, 14985, 14997, 15009, 15033,
        };

		private static int[] s_patternUla48_Late_SET7HL = new int[]
        {
            14300, 14315, 14330, 14354, 14386, 14418, 14450, 14473, 14488, 14503, 14518, 14533, 14548, 14569, 14602, 14634, 14666, 14693, 14708, 14723, 14738, 14753, 14768, 14783, 14810,
            14842, 14874, 14906, 14925, 14940, 14955, 14970, 14985, 15000, 15026, 15058, 15090, 15122, 15145, 15160, 15175, 15190, 15205,
        };

		private static int[] s_patternUla48_Early_SET7HL = new int[]
        {
            14300, 14315, 14330, 14353, 14385, 14417, 14449, 14472, 14487, 14502, 14517, 14532, 14547, 14568, 14601, 14633, 14665, 14692, 14707, 14722, 14737, 14752, 14767, 14782, 14809,
            14841, 14873, 14905, 14924, 14939, 14954, 14969, 14984, 14999, 15025, 15057, 15089, 15121,
        };

		private static int[] s_patternUla128_NOP = new int[]
        {
			14362, 14372, 14380, 14388, 14396, 14404, 14412, 14420, 14428, 14436, 14444, 14452, 14460, 14468, 14476, 14484, 14492, 
			14496, 14500, 14504, 14508, 14512, 14516, 14520, 14524, 14528, 14532, 14536, 14540, 14544, 14548, 14552, 14556, 14560, 14564, 14568, 14572, 14576, 14580, 14584, 14588, 14592,
			14600, 14608, 14616, 14624, 14632, 14640, 14648, 14656, 14664, 14672, 14680, 14688, 14696, 14704, 14712, 14720, 14724, 
			14728, 14732, 14736,
        };

		private static int[] s_patternUla128_INCHL = new int[]
        {
			14362, 14378, 14394, 14410, 14426, 14442, 14458, 14474, 14490, 
			14496, 14502, 14508, 14514, 14520, 14526, 14532, 14538, 14544, 14550, 14556, 14562, 14568, 14574, 14580, 14586, 14598,
			14614, 14630, 14646, 14662, 14678, 14694, 14710, 14722, 14728,
			14734, 14740, 14746,
        };

		private static int[] s_patternUla128_LDA_HL_ = new int[]
        {
			14362, 14379, 14395, 14411, 14427, 14443, 14459, 14475, 14491, 
			14498, 14505, 14512, 14519, 14526, 14533, 14540, 14547, 14554, 14561, 14568, 14575, 14582, 14589, 14599,
			14615, 14631, 14647, 14663, 14679, 14695, 14711, 14723, 14730, 
			14737,
        };

		private static int[] s_patternUla128_OUTCA = new int[]
        {
			14362, 14388, 14402, 14434, 14450, 14476, 14490, 
			14502, 14508, 14520, 14526, 14538, 14544, 14556, 14562, 14574, 14580, 14592, 
			14606, 14638, 14654, 14680, 14694, 14720, 
			14726, 14738, 14744, 14756, 14762, 14774, 14780, 14792, 14798, 14810, 14816,
			14842, 14858, 14884, 14898, 14930, 14946, 14958, 14964, 14976, 14982,
        };

		#endregion

		private static void runZexall()
		{
            var p128 = GetTestMachine(Resources.machines_test);
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();
			p128.IsRunning = false;
            foreach (var kbd in p128.BusManager.FindDevices<IKeyboardDevice>())
            {
                kbd.KeyboardState = new FakeKeyboardState(Key.Y);
            }

            using (Stream testStream = GetTestStream("zexall.sna"))
            {
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension("zexall.sna")).Deserialize(testStream);
            }
			p128.IsRunning = true;
			int frame;
			for (frame = 0; frame < 700000; frame++)
			{
				p128.ExecuteFrame();
				if (frame % 30000 == 0 || ((frame > 630000 && frame < 660000) && frame % 10000 == 0))
				{
					Console.WriteLine(string.Format("{0:D8}", frame));
                    p128.BusManager.LoadManager.SaveFileName(string.Format("{0:D8}.PNG", frame));
				}
			}
            p128.BusManager.LoadManager.SaveFileName(string.Format("{0:D8}.PNG", frame));
			p128.BusManager.Disconnect();
		}

        private static void runPerf()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            var frameCount = 50 * 10;
            while (true)
            {
                ExecTests("zexall.sna", frameCount);
            }
        }

        private static void runCpu()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            var frameCount = 50 * 10;
            while (true)
            {
                ExecCpuTests("zexall.sna", frameCount);
            }
        }

		private static void ExecTests(string testName, int frameCount)
		{
            var p128 = GetTestMachine(Resources.machines_test);
			p128.IsRunning = true;
			p128.DebugReset();
			p128.ExecuteFrame();

			p128.IsRunning = false;
			using (Stream testStream = GetTestStream(testName))
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension(testName)).Deserialize(testStream);
			p128.IsRunning = true;


			Stopwatch watch = new Stopwatch();
			watch.Start();
			for (int frame = 0; frame < frameCount; frame++)
				p128.ExecuteFrame();
			watch.Stop();
			Console.WriteLine("{0}:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
			//p128.Loader.SaveFileName(testName);
			p128.BusManager.Disconnect();
		}

        private static void ExecLightTests(string testName, int frameCount)
        {
            var p128 = GetTestMachine(Resources.machines_testLight);
            p128.IsRunning = true;
            p128.DebugReset();
            p128.ExecuteFrame();

            p128.IsRunning = false;
            using (Stream testStream = GetTestStream(testName))
                p128.BusManager.LoadManager.GetSerializer(Path.GetExtension(testName)).Deserialize(testStream);
            p128.IsRunning = true;


            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (int frame = 0; frame < frameCount; frame++)
                p128.ExecuteFrame();
            watch.Stop();
            Console.WriteLine("{0} [light]:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
            //p128.Loader.SaveFileName(testName);
            p128.BusManager.Disconnect();
        }

        private static void ExecCpuTests(string testName, int frameCount)
        {
            var cpu = new CpuUnit();
            cpu.regs.PC = 0;
            cpu.RESET = () => { };
            cpu.NMIACK_M1 = () => { };
            cpu.INTACK_M1 = () => { };
            cpu.RDMEM_M1 = addr => (byte)addr;
            cpu.RDMEM = addr => (byte)addr;
            cpu.WRMEM = (addr,value) => { };
            cpu.RDPORT = addr => 0xFF;
            cpu.WRPORT = (addr, value) => { };
            cpu.RDNOMREQ = addr => { };
            cpu.WRNOMREQ = addr => { };

            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (var cycle = 0L; cycle < 71980 * frameCount; cycle++)
                cpu.ExecCycle();
            watch.Stop();
            Console.WriteLine("{0} [cpu]:\t{1} [ms]", testName, watch.ElapsedMilliseconds);
        }

		private static Stream GetTestStream(string testName)
		{
			testName = string.Format("Test.{0}", testName);
			return Assembly.GetExecutingAssembly().GetManifestResourceStream(testName);
		}
	}

	public class FakeKeyboardState : IKeyboardState
	{
		private readonly bool m_pressSimulation;
		private readonly Key m_key;

		public FakeKeyboardState()
		{
			m_pressSimulation = false;
		}

		public FakeKeyboardState(Key keyPressed)
		{
			m_key = keyPressed;
			m_pressSimulation = true;
		}

		public bool this[Key key]
		{
			get { return m_pressSimulation ? (key == m_key) : false; }
		}
	}
}
