using System;
using System.Collections.Generic;
using System.Xml;
using ZXMAK2.Dependency;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Circuits.Sound;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// NedoPC TurboSound FM Pro Rev. C.
    ///
    /// The board replaces the machine's AY/YM socket.  Its CPLD selects two
    /// YM2203 chips and one SAA1099 through the ordinary AY address/data ports.
    /// Writing 1111xxxx to the address port changes the board configuration:
    /// bit 3 disables SAA, bit 2 disables FM DAC, bit 1 selects register/status
    /// reads and bit 0 selects a YM2203.
    /// </summary>
    public sealed class TurboSoundFmPro : BusDeviceBase,
        IPsgDevice,
        ISoundMixerConfiguration,
        IAdditionalSoundRenderers
    {
        private const int AyMask = 0xC0FF;
        private const int AyAddressPort = 0xC0FD;
        private const int AyDataPort = 0x80FD;

        private readonly TsFmPsgRenderer[] m_psg = new TsFmPsgRenderer[2];
        private readonly TsFmFmRenderer m_fm;
        private readonly TsFmSaa1099Renderer m_saa;
        private readonly ISoundRenderer[] m_renderers;
        private readonly byte[] m_register = new byte[2];
        private readonly PsgPortState[] m_ira =
        {
            new PsgPortState(0xFF),
            new PsgPortState(0xFF),
        };
        private readonly PsgPortState[] m_irb =
        {
            new PsgPortState(0xFF),
            new PsgPortState(0xFF),
        };

        private byte m_configuration;
        private int m_volume;

        public TurboSoundFmPro()
        {
            Category = BusDeviceCategory.Music;
            Name = "TurboSound FM Pro Rev. C";
            Description =
                "NedoPC TurboSound FM Pro Rev. C\n" +
                "AY/YM socket replacement: 2 x YM2203 at 3.5 MHz and " +
                "SAA1099 at 8 MHz.\n\n" +
                "Board selector: #FFFD = %1111<SAA off><FM off>" +
                "<status/register><D1/D2>.\n" +
                "SAA1099 uses the normal #FFFD/#BFFD pair after selection.";

            m_psg[0] = new TsFmPsgRenderer("TSFM YM2203 D1 SSG", PanType.Abc);
            m_psg[1] = new TsFmPsgRenderer("TSFM YM2203 D2 SSG", PanType.Cba);
            m_fm = new TsFmFmRenderer();
            m_saa = new TsFmSaa1099Renderer();
            m_renderers = new ISoundRenderer[]
            {
                m_psg[0],
                m_psg[1],
                m_fm,
                m_saa,
            };
            m_volume = 100;
            ResetBoard();
        }

        public IEnumerable<ISoundRenderer> SoundRenderers
        {
            get { return m_renderers; }
        }

        public bool RejectDc
        {
            get { return true; }
        }

        public int Volume
        {
            get { return m_volume; }
            set
            {
                value = Math.Max(0, Math.Min(100, value));
                if (m_volume == value)
                    return;
                m_volume = value;
                foreach (var renderer in m_renderers)
                    renderer.Volume = value;
                OnConfigChanged();
            }
        }

        public byte RegAddr
        {
            get { return m_register[ActiveChip]; }
            set
            {
                m_register[ActiveChip] = value;
                m_psg[ActiveChip].RegAddr = value;
            }
        }

        public byte GetReg(int index)
        {
            return m_psg[ActiveChip].GetReg(index);
        }

        public void SetReg(int index, byte value)
        {
            m_psg[ActiveChip].SetReg(index, value);
        }

        public event Action<IPsgDevice, PsgPortState> IraHandler;
        public event Action<IPsgDevice, PsgPortState> IrbHandler;

        private int ActiveChip
        {
            get { return m_configuration & 1; }
        }

        private bool ReadRegister
        {
            get { return (m_configuration & 2) != 0; }
        }

        private bool FmEnabled
        {
            get { return (m_configuration & 4) == 0; }
        }

        private bool SaaSelected
        {
            get { return (m_configuration & 8) == 0; }
        }

        public override void BusInit(IBusManager bmgr)
        {
            foreach (var renderer in m_renderers)
            {
                var device = renderer as BusDeviceBase;
                if (device != null)
                    device.BusInit(bmgr);
            }

            bmgr.Events.SubscribeWrIo(AyMask, AyAddressPort, WriteAddressPort);
            bmgr.Events.SubscribeRdIo(AyMask, AyAddressPort, ReadPort);
            bmgr.Events.SubscribeWrIo(AyMask, AyDataPort, WriteDataPort);
            bmgr.Events.SubscribeReset(ResetBoard);
        }

        public override void BusConnect()
        {
        }

        public override void BusDisconnect()
        {
            foreach (var renderer in m_renderers)
            {
                var device = renderer as BusDeviceBase;
                if (device != null)
                    device.BusDisconnect();
            }
        }

        protected override void OnConfigLoad(XmlNode node)
        {
            base.OnConfigLoad(node);
            Volume = Utils.GetXmlAttributeAsInt32(node, "volume", Volume);
        }

        protected override void OnConfigSave(XmlNode node)
        {
            base.OnConfigSave(node);
            Utils.SetXmlAttribute(node, "volume", Volume);
        }

        private void ResetBoard()
        {
            // cfg.v in the official Rev. C source resets the four CPLD bits to
            // 1111: SAA and FM disabled, register read, YM selector high.
            m_configuration = 0x0F;
            m_register[0] = 0;
            m_register[1] = 0;
            m_psg[0].ResetChip();
            m_psg[1].ResetChip();
            m_fm.ResetChip();
            m_fm.Enabled = false;
            m_saa.ResetChip();
        }

        private void WriteAddressPort(ushort addr, byte value, ref bool handled)
        {
            if ((value & 0xF0) == 0xF0)
            {
                m_configuration = (byte)(value & 0x0F);
                m_fm.Enabled = FmEnabled;
                return;
            }

            if (SaaSelected)
            {
                m_saa.RegAddr = value;
                return;
            }

            m_register[ActiveChip] = value;
            m_psg[ActiveChip].RegAddr = value;
        }

        private void WriteDataPort(ushort addr, byte value, ref bool handled)
        {
            if (SaaSelected)
            {
                m_saa.SetReg(m_saa.RegAddr, value);
                return;
            }

            int chip = ActiveChip;
            int register = m_register[chip];
            if (register < 0x10)
            {
                m_psg[chip].SetReg(register, value);
                if ((register & 0x0F) == PsgRegId.IRA)
                    WriteIra(chip, value);
                else if ((register & 0x0F) == PsgRegId.IRB)
                    WriteIrb(chip, value);
            }
            else
            {
                m_fm.SetRegister(chip, register, value);
            }
        }

        private void ReadPort(ushort addr, ref byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            int chip = ActiveChip;
            if (FmEnabled && !ReadRegister)
            {
                value = m_fm.GetStatus(chip);
                return;
            }

            int register = m_register[chip];
            if ((register & 0x0F) == PsgRegId.IRA)
                value = ReadIra(chip);
            else if ((register & 0x0F) == PsgRegId.IRB)
                value = ReadIrb(chip);
            else if (register < 0x10)
                value = m_psg[chip].GetReg(register);
            else
                value = m_fm.GetRegister(chip, register);
        }

        private byte ReadIra(int chip)
        {
            var state = m_ira[chip];
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x40) != 0;
            state.InState = state.DirOut ? state.OutState : (byte)0;
            var handler = IraHandler;
            if (handler != null)
                handler(this, state);
            return state.InState;
        }

        private byte ReadIrb(int chip)
        {
            var state = m_irb[chip];
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x80) != 0;
            state.InState = state.DirOut ? state.OutState : (byte)0xFE;
            var handler = IrbHandler;
            if (handler != null)
                handler(this, state);
            return state.InState;
        }

        private void WriteIra(int chip, byte value)
        {
            var state = m_ira[chip];
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x40) != 0;
            state.OutState = value;
            var handler = IraHandler;
            if (handler != null)
            {
                state.InState = state.DirOut ? value : (byte)0;
                handler(this, state);
            }
        }

        private void WriteIrb(int chip, byte value)
        {
            var state = m_irb[chip];
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x80) != 0;
            state.OutState = value;
            var handler = IrbHandler;
            if (handler != null)
            {
                state.InState = state.DirOut ? value : (byte)0xFE;
                handler(this, state);
            }
        }

        /// <summary>
        /// The accepted board model was quieter than the surrounding ZXMAK2
        /// devices. Apply one common +50% gain after each TSFM source has
        /// rendered so AY/YM, FM and SAA retain their established balance.
        /// Saturation prevents signed 16-bit wraparound on loud passages.
        /// </summary>
        internal static void ApplyOutputGain(uint[] buffer)
        {
            if (buffer == null)
                return;
            for (var index = 0; index < buffer.Length; index++)
            {
                var packed = buffer[index];
                var left = (short)(packed & 0xFFFF);
                var right = (short)(packed >> 16);
                var boostedLeft = Math.Max(
                    short.MinValue,
                    Math.Min(short.MaxValue, left * 3 / 2));
                var boostedRight = Math.Max(
                    short.MinValue,
                    Math.Min(short.MaxValue, right * 3 / 2));
                buffer[index] = (uint)(
                    (ushort)(short)boostedLeft |
                    ((uint)(ushort)(short)boostedRight << 16));
            }
        }
    }

    internal sealed class TsFmPsgRenderer : SoundDeviceBase
    {
        private readonly IPsgChip m_chip;
        private double m_lastTime;

        public TsFmPsgRenderer(string name, PanType pan)
        {
            Name = name;
            Description = name;
            m_chip = Locator.Resolve<IPsgChip>();
            m_chip.AmpType = AmpType.Ym2203;
            m_chip.PanType = pan;
            // A YM2203 driven at 3.5 MHz clocks its SSG section after the
            // internal divide-by-two stage.
            m_chip.ChipFrequency = 1750000;
            m_chip.UpdateHandler = UpdateDac;
        }

        public byte RegAddr
        {
            get { return m_chip.RegAddr; }
            set { m_chip.RegAddr = value; }
        }

        public byte GetReg(int index)
        {
            return m_chip.GetReg(index);
        }

        public void SetReg(int index, byte value)
        {
            m_lastTime = m_chip.SetReg(m_lastTime, GetFrameTime(), index, value);
        }

        public void ResetChip()
        {
            m_lastTime = 0D;
            m_chip.Reset();
        }

        public override void BusConnect()
        {
        }

        public override void BusDisconnect()
        {
        }

        protected override void OnProcessConfigChange()
        {
            base.OnProcessConfigChange();
            if (m_chip != null)
                m_chip.Volume = Volume;
        }

        protected override void OnEndFrame()
        {
            m_lastTime = m_chip.Update(m_lastTime, 1D);
            if (m_lastTime >= 1D)
                m_lastTime -= Math.Floor(m_lastTime);
            base.OnEndFrame();
            // Keep the YM2203 SSG (AY-compatible) path at its native level.
            // Its logarithmic AY/YM table already reaches the full signed
            // range.  Applying the board-wide post gain here clipped peaks
            // independently in every PSG renderer and made ordinary AY music
            // sound as if channels or notes were being dropped.  SAA and FM
            // retain the accepted +50% board gain below.
        }
    }

    /// <summary>
    /// SAA1099 audio core clocked at the Rev. C board's fixed 8 MHz.
    /// The register and generator behaviour follows the Philips data sheet;
    /// writes are rendered at their actual position inside the video frame.
    /// </summary>
    internal sealed class TsFmSaa1099Renderer : SoundDeviceBase
    {
        private const int ChannelCount = 6;
        private const double InternalClock = 8000000D / 256D;
        // SAASound 3.5 uses 64x oversampling by default.  Keeping the same
        // internal cadence avoids aliasing high SAA tones and preserves the
        // measured noise/envelope behaviour before the 44.1-kHz output stage.
        private const int Oversample = 64;
        private static readonly int[,] EnvelopePdm =
        {
            {0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0},
            {0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8},
            {0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15},
            {0,2,3,5,6,8,9,11,12,14,15,17,18,20,21,23},
            {0,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30},
            {0,3,5,8,10,13,15,18,20,23,25,28,30,33,35,38},
            {0,3,6,9,12,15,18,21,24,27,30,33,36,39,42,45},
            {0,4,7,11,14,18,21,25,28,32,35,39,42,46,49,53},
        };

        private readonly byte[] m_registers = new byte[0x20];
        private readonly double[] m_toneCounter = new double[ChannelCount];
        private readonly int[] m_toneLevel = new int[ChannelCount];
        private readonly byte[] m_toneOffset = new byte[ChannelCount];
        private readonly byte[] m_toneOctave = new byte[ChannelCount];
        private readonly bool[] m_toneDataPending = new bool[ChannelCount];
        private readonly bool[] m_toneIgnoreOffset = new bool[ChannelCount];
        private readonly double[] m_noiseCounter = new double[2];
        private readonly uint[] m_noiseLfsr = { 1U, 1U };
        private readonly int[] m_envelopePhase = new int[2];
        private readonly int[] m_envelopePosition = new int[2];
        private readonly int[] m_envelopeResolution = { 1, 1 };
        private readonly int[] m_envelopeMode = new int[2];
        private readonly bool[] m_envelopeReverse = new bool[2];
        private readonly bool[] m_envelopeExternal = new bool[2];
        private readonly bool[] m_envelopeEnabled = new bool[2];
        private readonly bool[] m_envelopeEnded = { true, true };
        private readonly byte[] m_envelopePendingData = new byte[2];
        private readonly bool[] m_envelopePending = new bool[2];
        private readonly int[,] m_envelopeValue = new int[2, 2];

        private byte m_register;
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private bool m_sync;

        public TsFmSaa1099Renderer()
        {
            Name = "TSFM SAA1099";
            Description = "Philips SAA1099, 8 MHz, 6 tone/noise channels";
            ResetChip();
        }

        public byte RegAddr
        {
            get { return m_register; }
            set
            {
                RenderToCurrentTime();
                m_register = (byte)(value & 0x1F);
                if (m_register == 0x18 && m_envelopeExternal[0])
                    ClockEnvelope(0);
                else if (m_register == 0x19 && m_envelopeExternal[1])
                    ClockEnvelope(1);
            }
        }

        public void SetReg(int index, byte value)
        {
            RenderToCurrentTime();
            WriteRegister(index & 0x1F, value);
        }

        public void ResetChip()
        {
            RenderToCurrentTime();
            Array.Clear(m_registers, 0, m_registers.Length);
            Array.Clear(m_toneCounter, 0, m_toneCounter.Length);
            for (var i = 0; i < m_toneLevel.Length; i++)
                m_toneLevel[i] = 1;
            Array.Clear(m_toneOffset, 0, m_toneOffset.Length);
            Array.Clear(m_toneOctave, 0, m_toneOctave.Length);
            Array.Clear(m_toneDataPending, 0, m_toneDataPending.Length);
            Array.Clear(m_toneIgnoreOffset, 0, m_toneIgnoreOffset.Length);
            Array.Clear(m_noiseCounter, 0, m_noiseCounter.Length);
            Array.Clear(m_envelopePhase, 0, m_envelopePhase.Length);
            Array.Clear(m_envelopePosition, 0, m_envelopePosition.Length);
            m_envelopeResolution[0] = m_envelopeResolution[1] = 1;
            Array.Clear(m_envelopeMode, 0, m_envelopeMode.Length);
            Array.Clear(m_envelopeReverse, 0, m_envelopeReverse.Length);
            Array.Clear(m_envelopeExternal, 0, m_envelopeExternal.Length);
            Array.Clear(m_envelopeEnabled, 0, m_envelopeEnabled.Length);
            m_envelopeEnded[0] = m_envelopeEnded[1] = true;
            Array.Clear(m_envelopePendingData, 0, m_envelopePendingData.Length);
            Array.Clear(m_envelopePending, 0, m_envelopePending.Length);
            m_noiseLfsr[0] = 1U;
            m_noiseLfsr[1] = 1U;
            m_envelopeValue[0, 0] = m_envelopeValue[0, 1] = 16;
            m_envelopeValue[1, 0] = m_envelopeValue[1, 1] = 16;
            m_register = 0;
            m_sync = false;
        }

        public override void BusConnect()
        {
        }

        public override void BusDisconnect()
        {
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, SampleRate / 50);
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
            TurboSoundFmPro.ApplyOutputGain(AudioBuffer);
        }

        private void RenderToCurrentTime()
        {
            if (!m_rendering)
                return;
            double time = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            RenderTo((int)(time * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            while (m_renderSample < target)
            {
                int left;
                int right;
                RenderSample(out left, out right);
                double frameTime = (double)m_renderSample / m_frameSamples;
                UpdateDac(frameTime, Clamp16(left), Clamp16(right));
                m_renderSample++;
            }
        }

        private void RenderSample(out int left, out int right)
        {
            left = 0;
            right = 0;
            if (m_sync || (m_registers[0x1C] & 1) == 0)
                return;

            long accumulatedLeft = 0;
            long accumulatedRight = 0;
            double internalSampleRate =
                Math.Max(1, SampleRate) * (double)Oversample;
            for (int step = 0; step < Oversample; step++)
            {
                int subLeft;
                int subRight;
                RenderSubSample(
                    internalSampleRate, out subLeft, out subRight);
                accumulatedLeft += subLeft;
                accumulatedRight += subRight;
            }

            int volume = Volume;
            left = (int)(accumulatedLeft * 11.3D /
                Oversample) * volume / 100;
            right = (int)(accumulatedRight * 11.3D /
                Oversample) * volume / 100;
        }

        private void RenderSubSample(
            double sampleRate,
            out int left,
            out int right)
        {
            left = 0;
            right = 0;

            for (int channel = 0; channel < ChannelCount; channel++)
            {
                double frequency = GetToneFrequency(channel);
                m_toneCounter[channel] -= frequency;
                while (m_toneCounter[channel] < 0D)
                {
                    m_toneCounter[channel] += sampleRate;
                    m_toneLevel[channel] ^= 1;
                    if (channel == 0)
                        ClockNoiseFromTone(0);
                    else if (channel == 3)
                        ClockNoiseFromTone(1);
                    if (channel == 1 && !m_envelopeExternal[0])
                        ClockEnvelope(0);
                    if (channel == 4 && !m_envelopeExternal[1])
                        ClockEnvelope(1);
                    ApplyPendingToneData(channel);
                    frequency = GetToneFrequency(channel);
                }
            }

            ClockNoise(0, sampleRate);
            ClockNoise(1, sampleRate);

            for (int channel = 0; channel < ChannelCount; channel++)
            {
                int group = channel / 3;
                bool channelUsesEnvelope = (channel % 3) == 2 &&
                    IsEnvelopeEnabled(group);
                int amplitudeLeftNibble = m_registers[channel] & 0x0F;
                int amplitudeRightNibble = (m_registers[channel] >> 4) & 0x0F;
                bool toneEnabled = (m_registers[0x14] & (1 << channel)) != 0;
                bool noiseEnabled = (m_registers[0x15] & (1 << channel)) != 0;
                int tone = m_toneLevel[channel] & 1;
                int noise = (int)(m_noiseLfsr[group] & 1U);
                int intermediate;
                if (toneEnabled && noiseEnabled)
                    intermediate = tone * (2 - noise);
                else if (toneEnabled)
                    intermediate = tone * 2;
                else if (noiseEnabled)
                    intermediate = noise * 2;
                else
                    intermediate = 0;

                if (channelUsesEnvelope)
                {
                    int envelopeLeft = m_envelopeValue[group, 0];
                    int envelopeRight = m_envelopeValue[group, 1];
                    left += EffectiveEnvelopeAmplitude(
                        amplitudeLeftNibble >> 1, envelopeLeft) *
                        (2 - intermediate);
                    right += EffectiveEnvelopeAmplitude(
                        amplitudeRightNibble >> 1, envelopeRight) *
                        (2 - intermediate);
                }
                else
                {
                    left += amplitudeLeftNibble * intermediate * 16;
                    right += amplitudeRightNibble * intermediate * 16;
                }
            }

        }

        private void ClockNoise(int generator, double sampleRate)
        {
            int parameter = (m_registers[0x16] >> (generator * 4)) & 3;
            if (parameter == 3)
                return;
            double frequency = InternalClock / (1 << parameter);
            m_noiseCounter[generator] -= frequency;
            while (m_noiseCounter[generator] < 0D)
            {
                m_noiseCounter[generator] += sampleRate;
                AdvanceNoise(generator);
            }
        }

        private void ClockNoiseFromTone(int generator)
        {
            int parameter = (m_registers[0x16] >> (generator * 4)) & 3;
            if (parameter == 3)
                AdvanceNoise(generator);
        }

        private void AdvanceNoise(int generator)
        {
            uint value = m_noiseLfsr[generator];
            m_noiseLfsr[generator] = (value & 1U) != 0
                ? (value >> 1) ^ 0x20400U
                : value >> 1;
        }

        private double GetToneFrequency(int channel)
        {
            return (InternalClock * (1 << m_toneOctave[channel])) /
                (511D - m_toneOffset[channel]);
        }

        private void ApplyPendingToneData(int channel)
        {
            if (!m_toneDataPending[channel])
                return;
            int shift = (channel & 1) * 4;
            m_toneOctave[channel] = (byte)
                ((m_registers[0x10 + channel / 2] >> shift) & 7);
            if (!m_toneIgnoreOffset[channel])
            {
                m_toneOffset[channel] = m_registers[0x08 + channel];
                m_toneDataPending[channel] = false;
            }
            m_toneIgnoreOffset[channel] = false;
        }

        private void WriteRegister(int index, byte value)
        {
            if (index > 0x1C)
                return;
            m_registers[index] = value;

            if (index >= 0x08 && index <= 0x0D)
            {
                int channel = index - 0x08;
                if (m_sync)
                {
                    m_toneOffset[channel] = value;
                    int shift = (channel & 1) * 4;
                    m_toneOctave[channel] = (byte)
                        ((m_registers[0x10 + channel / 2] >> shift) & 7);
                    m_toneDataPending[channel] = false;
                    m_toneIgnoreOffset[channel] = false;
                }
                else
                {
                    m_toneDataPending[channel] = true;
                    int shift = (channel & 1) * 4;
                    int nextOctave =
                        (m_registers[0x10 + channel / 2] >> shift) & 7;
                    if (nextOctave == m_toneOctave[channel])
                        m_toneIgnoreOffset[channel] = true;
                }
            }
            else if (index >= 0x10 && index <= 0x12)
            {
                int first = (index - 0x10) * 2;
                SetToneOctave(first, value & 7);
                SetToneOctave(first + 1, (value >> 4) & 7);
            }
            else if (index == 0x18 || index == 0x19)
            {
                int envelope = index - 0x18;
                SetEnvelopeControl(envelope, value);
            }
            else if (index == 0x1C)
            {
                bool sync = (value & 2) != 0;
                if (sync != m_sync)
                {
                    m_sync = sync;
                    if (sync)
                    {
                        Array.Clear(m_toneCounter, 0, m_toneCounter.Length);
                        Array.Clear(m_noiseCounter, 0, m_noiseCounter.Length);
                        for (int channel = 0; channel < ChannelCount; channel++)
                        {
                            m_toneLevel[channel] = 1;
                            int shift = (channel & 1) * 4;
                            m_toneOctave[channel] = (byte)
                                ((m_registers[0x10 + channel / 2] >> shift) & 7);
                            m_toneOffset[channel] =
                                m_registers[0x08 + channel];
                            m_toneDataPending[channel] = false;
                            m_toneIgnoreOffset[channel] = false;
                        }
                    }
                }
            }
        }

        private void SetToneOctave(int channel, int octave)
        {
            if (m_sync)
            {
                m_toneOctave[channel] = (byte)octave;
                m_toneOffset[channel] = m_registers[0x08 + channel];
                m_toneDataPending[channel] = false;
                m_toneIgnoreOffset[channel] = false;
            }
            else
            {
                m_toneDataPending[channel] = true;
                m_toneIgnoreOffset[channel] = false;
            }
        }

        private bool IsEnvelopeEnabled(int generator)
        {
            return m_envelopeEnabled[generator];
        }

        private void ClockEnvelope(int generator)
        {
            if (!m_envelopeEnabled[generator])
            {
                m_envelopeEnded[generator] = true;
                m_envelopePhase[generator] = 0;
                m_envelopePosition[generator] = 0;
                return;
            }

            if (m_envelopeEnded[generator])
                return;

            m_envelopePosition[generator] += m_envelopeResolution[generator];
            bool acceptPending = false;
            if (m_envelopePosition[generator] >= 16)
            {
                m_envelopePhase[generator]++;
                int phases = IsTwoPhaseEnvelope(m_envelopeMode[generator]) ? 2 : 1;
                if (m_envelopePhase[generator] >= phases)
                {
                    acceptPending = true;
                    if (IsLoopingEnvelope(m_envelopeMode[generator]))
                    {
                        m_envelopePhase[generator] = 0;
                        m_envelopePosition[generator] -= 16;
                    }
                    else
                    {
                        m_envelopeEnded[generator] = true;
                    }
                }
                else
                {
                    m_envelopePosition[generator] -= 16;
                }
            }

            if (acceptPending && m_envelopePending[generator])
            {
                ApplyEnvelopeData(
                    generator, m_envelopePendingData[generator]);
                m_envelopePending[generator] = false;
            }
            else
            {
                UpdateEnvelopeLevels(generator);
            }
        }

        private void SetEnvelopeControl(int generator, byte data)
        {
            bool enabled = (data & 0x80) != 0;
            if (!enabled && !m_envelopeEnabled[generator])
                return;
            m_envelopeEnabled[generator] = enabled;
            if (!enabled)
            {
                m_envelopeEnded[generator] = true;
                m_envelopeValue[generator, 0] = 16;
                m_envelopeValue[generator, 1] = 16;
                return;
            }

            int resolution = (data & 0x10) != 0 ? 2 : 1;
            if (m_envelopeResolution[generator] == 1 && resolution == 2)
                m_envelopePosition[generator] &= 0x0E;
            else if (m_envelopeResolution[generator] == 2 && resolution == 1)
                m_envelopePosition[generator] |= 1;
            m_envelopeResolution[generator] = resolution;

            if (m_envelopeEnded[generator])
            {
                ApplyEnvelopeData(generator, data);
                m_envelopePending[generator] = false;
            }
            else
            {
                m_envelopePending[generator] = true;
                m_envelopePendingData[generator] = data;
                UpdateEnvelopeLevels(generator);
            }
        }

        private void ApplyEnvelopeData(int generator, byte data)
        {
            m_envelopePhase[generator] = 0;
            m_envelopePosition[generator] = 0;
            m_envelopeMode[generator] = (data >> 1) & 7;
            m_envelopeReverse[generator] = (data & 1) != 0;
            m_envelopeExternal[generator] = (data & 0x20) != 0;
            m_envelopeResolution[generator] = (data & 0x10) != 0 ? 2 : 1;
            m_envelopeEnabled[generator] = (data & 0x80) != 0;
            m_envelopeEnded[generator] = !m_envelopeEnabled[generator];
            UpdateEnvelopeLevels(generator);
        }

        private void UpdateEnvelopeLevels(int generator)
        {
            int maximum = m_envelopeResolution[generator] == 2 ? 14 : 15;
            int value;
            if (m_envelopeEnded[generator] &&
                !IsLoopingEnvelope(m_envelopeMode[generator]))
            {
                value = 0;
            }
            else
            {
                int position = m_envelopePosition[generator] & 15;
                switch (m_envelopeMode[generator])
                {
                    case 0: value = 0; break;
                    case 1: value = maximum; break;
                    case 2:
                    case 3: value = 15 - position; break;
                    case 4:
                    case 5:
                        value = m_envelopePhase[generator] == 0
                            ? position : 15 - position;
                        break;
                    default: value = position; break;
                }
                if (m_envelopeResolution[generator] == 2)
                    value &= 0x0E;
            }
            m_envelopeValue[generator, 0] = value;
            m_envelopeValue[generator, 1] = m_envelopeReverse[generator]
                ? maximum - value : value;
        }

        private static bool IsTwoPhaseEnvelope(int mode)
        {
            return mode == 4 || mode == 5;
        }

        private static bool IsLoopingEnvelope(int mode)
        {
            return mode == 1 || mode == 3 || mode == 5 || mode == 7;
        }

        private static int EffectiveEnvelopeAmplitude(int ampDiv2, int envelope)
        {
            return EnvelopePdm[ampDiv2 & 7, envelope & 15] * 4;
        }

        private static short Clamp16(int value)
        {
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));
        }
    }

    /// <summary>
    /// YM2203 OPN FM section.  It models both chips, all four operators,
    /// algorithms, feedback, key events, multipliers, levels and ADSR state.
    /// Timers/status are kept independently from the audio generators.
    /// </summary>
    internal sealed class TsFmFmRenderer : SoundDeviceBase
    {
        private const int ChipCount = 2;
        private const int ChannelCount = 3;
        private const int OperatorCount = 4;
        private const double ChipClock = 3500000D;
        private static readonly int[] OperatorMap = { 0, 2, 1, 3 };

        private readonly byte[][] m_registers =
        {
            new byte[256],
            new byte[256],
        };
        private readonly FmOperator[,,] m_operators =
            new FmOperator[ChipCount, ChannelCount, OperatorCount];
        private readonly double[,] m_feedback = new double[ChipCount, ChannelCount];
        private readonly byte[] m_status = new byte[ChipCount];
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private bool m_enabled;

        public TsFmFmRenderer()
        {
            Name = "TSFM YM2203 FM";
            Description = "Two YM2203 OPN FM sections at 3.5 MHz";
            for (int chip = 0; chip < ChipCount; chip++)
                for (int channel = 0; channel < ChannelCount; channel++)
                    for (int op = 0; op < OperatorCount; op++)
                        m_operators[chip, channel, op] = new FmOperator();
        }

        public bool Enabled
        {
            get { return m_enabled; }
            set
            {
                if (m_enabled == value)
                    return;
                RenderToCurrentTime();
                m_enabled = value;
            }
        }

        public byte GetStatus(int chip)
        {
            return m_status[chip & 1];
        }

        public byte GetRegister(int chip, int index)
        {
            return m_registers[chip & 1][index & 0xFF];
        }

        public void SetRegister(int chip, int index, byte value)
        {
            RenderToCurrentTime();
            chip &= 1;
            index &= 0xFF;
            m_registers[chip][index] = value;

            if (index == 0x28)
            {
                int channel = value & 3;
                if (channel < ChannelCount)
                {
                    for (int op = 0; op < OperatorCount; op++)
                        SetKey(m_operators[chip, channel, op],
                            (value & (0x10 << op)) != 0);
                }
            }
            else if (index == 0x27)
            {
                if ((value & 0x10) != 0) m_status[chip] &= 0xFE;
                if ((value & 0x20) != 0) m_status[chip] &= 0xFD;
            }
        }

        public void ResetChip()
        {
            RenderToCurrentTime();
            Array.Clear(m_registers[0], 0, m_registers[0].Length);
            Array.Clear(m_registers[1], 0, m_registers[1].Length);
            Array.Clear(m_feedback, 0, m_feedback.Length);
            Array.Clear(m_status, 0, m_status.Length);
            for (int chip = 0; chip < ChipCount; chip++)
                for (int channel = 0; channel < ChannelCount; channel++)
                    for (int op = 0; op < OperatorCount; op++)
                        m_operators[chip, channel, op].Reset();
            m_enabled = false;
        }

        public override void BusConnect()
        {
        }

        public override void BusDisconnect()
        {
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, SampleRate / 50);
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
            TurboSoundFmPro.ApplyOutputGain(AudioBuffer);
        }

        private void RenderToCurrentTime()
        {
            if (!m_rendering)
                return;
            double time = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            RenderTo((int)(time * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            while (m_renderSample < target)
            {
                double mixed = 0D;
                for (int chip = 0; chip < ChipCount; chip++)
                    for (int channel = 0; channel < ChannelCount; channel++)
                        mixed += RenderChannel(chip, channel);

                if (!m_enabled)
                    mixed = 0D;
                int sample = (int)(mixed * 3000D * Volume / 100D);
                short output = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, sample));
                UpdateDac((double)m_renderSample / m_frameSamples, output, output);
                m_renderSample++;
            }
        }

        private double RenderChannel(int chip, int channel)
        {
            byte[] registers = m_registers[chip];
            int fNumber = registers[0xA0 + channel] |
                ((registers[0xA4 + channel] & 7) << 8);
            int block = (registers[0xA4 + channel] >> 3) & 7;
            if (fNumber == 0)
                return 0D;

            double baseFrequency = fNumber * Math.Pow(2D, block) * ChipClock /
                (144D * 1048576D);
            double[] value = new double[OperatorCount];
            int algorithm = registers[0xB0 + channel] & 7;
            int feedback = (registers[0xB0 + channel] >> 3) & 7;
            double fb = feedback == 0 ? 0D : m_feedback[chip, channel] *
                Math.Pow(2D, feedback - 7) * 8D;

            switch (algorithm)
            {
                case 0:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, value[1] * 5D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, value[2] * 5D);
                    break;
                case 1:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, 0D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, (value[0] + value[1]) * 5D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, value[2] * 5D);
                    break;
                case 2:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, 0D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, (value[1] + value[2]) * 5D);
                    break;
                case 3:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, value[0] * 5D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, (value[1] + value[2]) * 5D);
                    break;
                case 4:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, 0D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, value[2] * 5D);
                    return (value[1] + value[3]) * 0.5D;
                case 5:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, value[0] * 5D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, value[0] * 5D);
                    return (value[1] + value[2] + value[3]) / 3D;
                case 6:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, value[0] * 5D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, 0D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, 0D);
                    return (value[1] + value[2] + value[3]) / 3D;
                default:
                    value[0] = RenderOperator(chip, channel, 0, baseFrequency, fb);
                    value[1] = RenderOperator(chip, channel, 1, baseFrequency, 0D);
                    value[2] = RenderOperator(chip, channel, 2, baseFrequency, 0D);
                    value[3] = RenderOperator(chip, channel, 3, baseFrequency, 0D);
                    return (value[0] + value[1] + value[2] + value[3]) * 0.25D;
            }

            m_feedback[chip, channel] = value[0];
            return value[3];
        }

        private double RenderOperator(
            int chip,
            int channel,
            int op,
            double baseFrequency,
            double modulation)
        {
            FmOperator state = m_operators[chip, channel, op];
            int slot = Array.IndexOf(OperatorMap, op);
            int offset = channel + slot * 4;
            byte[] registers = m_registers[chip];
            int multiplier = registers[0x30 + offset] & 0x0F;
            double multiple = multiplier == 0 ? 0.5D : multiplier;
            double frequency = baseFrequency * multiple;
            state.Phase += 2D * Math.PI * frequency / Math.Max(1, SampleRate);
            if (state.Phase >= 2D * Math.PI)
                state.Phase -= Math.Floor(state.Phase / (2D * Math.PI)) * 2D * Math.PI;

            UpdateEnvelope(state,
                registers[0x50 + offset] & 0x1F,
                registers[0x60 + offset] & 0x1F,
                registers[0x70 + offset] & 0x1F,
                (registers[0x80 + offset] >> 4) & 0x0F,
                registers[0x80 + offset] & 0x0F);

            int totalLevel = registers[0x40 + offset] & 0x7F;
            double level = Math.Pow(10D, -totalLevel * 0.75D / 20D);
            return Math.Sin(state.Phase + modulation) * state.Level * level;
        }

        private void UpdateEnvelope(
            FmOperator state,
            int attack,
            int decay,
            int sustainRate,
            int sustainLevel,
            int release)
        {
            double sampleRate = Math.Max(1, SampleRate);
            switch (state.Stage)
            {
                case EnvelopeStage.Attack:
                    if (attack == 0)
                        break;
                    state.Level += (1D - state.Level) * RateStep(attack, 0.00010D, sampleRate);
                    if (state.Level >= 0.999D)
                    {
                        state.Level = 1D;
                        state.Stage = EnvelopeStage.Decay;
                    }
                    break;
                case EnvelopeStage.Decay:
                    double sustain = sustainLevel == 15
                        ? 0D
                        : Math.Pow(10D, -sustainLevel * 3D / 20D);
                    state.Level -= RateStep(decay, 0.000012D, sampleRate);
                    if (state.Level <= sustain)
                    {
                        state.Level = sustain;
                        state.Stage = EnvelopeStage.Sustain;
                    }
                    break;
                case EnvelopeStage.Sustain:
                    state.Level = Math.Max(0D,
                        state.Level - RateStep(sustainRate, 0.000006D, sampleRate));
                    break;
                case EnvelopeStage.Release:
                    state.Level = Math.Max(0D,
                        state.Level - RateStep(release * 2 + 1, 0.000010D, sampleRate));
                    if (state.Level <= 0D)
                        state.Stage = EnvelopeStage.Off;
                    break;
            }
        }

        private static double RateStep(int rate, double scale, double sampleRate)
        {
            if (rate <= 0)
                return 0D;
            return Math.Pow(2D, rate / 4D) * scale * 44100D / sampleRate;
        }

        private static void SetKey(FmOperator state, bool enabled)
        {
            if (enabled && !state.KeyOn)
                state.Stage = EnvelopeStage.Attack;
            else if (!enabled && state.KeyOn)
                state.Stage = EnvelopeStage.Release;
            state.KeyOn = enabled;
        }

        private enum EnvelopeStage
        {
            Off,
            Attack,
            Decay,
            Sustain,
            Release,
        }

        private sealed class FmOperator
        {
            public double Phase;
            public double Level;
            public bool KeyOn;
            public EnvelopeStage Stage;

            public void Reset()
            {
                Phase = 0D;
                Level = 0D;
                KeyOn = false;
                Stage = EnvelopeStage.Off;
            }
        }
    }
}
