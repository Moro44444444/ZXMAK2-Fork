using System;
using System.IO;
using System.Runtime.InteropServices;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// The MultiSound MIDI input is a 31,250-baud serial line on AY port A2.
    /// Each YM has its own transmit line; both feed one virtual GM synthesizer.
    /// </summary>
    internal sealed class MidiSynthRenderer : SoundDeviceBase
    {
        private const string BankName = "GeneralUser-GS.sf2";
        private readonly int[] m_bitPosition = new int[2];
        private readonly int[] m_rxByte = new int[2];
        private IntPtr m_synth;
        private short[] m_pcm = new short[0];
        private int m_frameSamples;
        private int m_renderSample;
        private int m_nativeSampleRate;
        private bool m_rendering;

        public MidiSynthRenderer()
        {
            Name = "MultiSound MIDI";
            Description = "Serial MIDI on YM port A2, General MIDI synthesizer";
            Category = BusDeviceCategory.Music;
        }

        public bool IsAvailable { get { return m_synth != IntPtr.Zero; } }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            string folder = Utils.GetAppFolder();
            string path = Path.Combine(folder, BankName);
            if (!File.Exists(path))
                path = Path.Combine(folder, "roms", BankName);
            if (!File.Exists(path))
            {
                Logger.Error("MultiSound MIDI: missing " + BankName);
                return;
            }
            byte[] bank = File.ReadAllBytes(path);
            m_nativeSampleRate = Math.Max(1, SampleRate);
            m_synth = Native.Create(bank, bank.Length, m_nativeSampleRate);
            if (m_synth == IntPtr.Zero)
                Logger.Error("MultiSound MIDI: cannot open " + BankName);
            ResetChip();
        }

        public override void BusDisconnect()
        {
            m_rendering = false;
            if (m_synth != IntPtr.Zero)
            {
                Native.Destroy(m_synth);
                m_synth = IntPtr.Zero;
            }
            base.BusDisconnect();
        }

        public override void ResetState()
        {
            ResetChip();
        }

        public void ResetChip()
        {
            RenderToCurrentTime();
            Array.Clear(m_bitPosition, 0, m_bitPosition.Length);
            Array.Clear(m_rxByte, 0, m_rxByte.Length);
            if (m_synth != IntPtr.Zero)
                Native.Reset(m_synth);
        }

        /// <summary>Accept one AY register 14 write with port A configured as output.</summary>
        public void WritePortA(int chip, byte value)
        {
            chip &= 1;
            bool high = (value & 0x04) != 0;
            int bit = m_bitPosition[chip];
            if (bit == 0)
            {
                if (!high)
                {
                    m_bitPosition[chip] = 1; // start bit
                    m_rxByte[chip] = 0;
                }
                return;
            }
            if (bit <= 8)
            {
                if (high)
                    m_rxByte[chip] |= 1 << (bit - 1);
                m_bitPosition[chip] = bit + 1;
                return;
            }
            m_bitPosition[chip] = 0;
            if (high && m_synth != IntPtr.Zero) // stop bit
            {
                RenderToCurrentTime();
                Native.Byte(m_synth, (byte)m_rxByte[chip]);
            }
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, AudioBuffer.Length);
            m_renderSample = 0;
            m_rendering = true;
            if (m_synth != IntPtr.Zero && m_nativeSampleRate != SampleRate)
            {
                m_nativeSampleRate = SampleRate;
                Native.SetRate(m_synth, m_nativeSampleRate);
            }
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
        }

        private void RenderToCurrentTime()
        {
            if (m_rendering)
                RenderTo((int)(Math.Max(0D, Math.Min(1D,
                    GetFrameTime())) * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            int samples = target - m_renderSample;
            if (samples <= 0)
                return;
            if (m_pcm.Length < samples * 2)
                m_pcm = new short[samples * 2];
            if (m_synth != IntPtr.Zero)
                Native.Render(m_synth, m_pcm, samples);
            for (int i = 0; i < samples; ++i)
            {
                int left = m_synth == IntPtr.Zero ? 0 : m_pcm[i * 2];
                int right = m_synth == IntPtr.Zero ? 0 : m_pcm[i * 2 + 1];
                double gain = Volume / 100D;
                UpdateDac((double)(m_renderSample + i) / m_frameSamples,
                    Clamp16((int)(left * gain)),
                    Clamp16((int)(right * gain)));
            }
            m_renderSample = target;
        }

        private static short Clamp16(int value)
        {
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));
        }

        private static class Native
        {
            private const string Library = "omni_midi.dll";

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_create")]
            internal static extern IntPtr Create(byte[] bank, int length, int rate);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_destroy")]
            internal static extern void Destroy(IntPtr synth);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_reset")]
            internal static extern void Reset(IntPtr synth);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_set_rate")]
            internal static extern void SetRate(IntPtr synth, int rate);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_byte")]
            internal static extern void Byte(IntPtr synth, byte value);

            [DllImport(Library, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "omni_midi_render")]
            internal static extern void Render(IntPtr synth,
                [Out] short[] output, int samples);
        }
    }
}
