using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Circuits.Sound;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZX-MultiSound Rev.A2 for ZXBUS/NemoBus.
    /// Address decoding and reset states follow the official CPLD top.v.
    /// </summary>
    public sealed class ZxMultiSoundDevice : SoundDeviceBase,
        IAdditionalSoundRenderers,
        ISoundMixerConfiguration,
        IPsgDevice
    {
        private const string GsRomResourceName =
            "ZXMAK2.Hardware.Resources.ZX-MultiSound-GS-1.05b.rom";
        private const int GsRomSize = 32 * 1024;
        private const int GsRamSize = 1024 * 1024;
        private const long GsMasterClock = 48000000L;
        private const long GsMasterTicksPerFrame = GsMasterClock / 50;
        private const long GsMasterTicksPerCpuTact = GsMasterClock / 16000000L;
        // The CPLD advances a 9-bit counter from the 12 MHz clock and reloads
        // it when the old value reaches 320.  Because the RTL uses
        // non-blocking assignments, the complete interval is 321 clocks, not
        // the rounded 320 clocks implied by "37.5 kHz" in old GS notes.
        private const long GsInterruptPeriod = 321L * 4L;
        // gint_n goes low on the reload edge and returns high when the old
        // counter value reaches 32.  That makes the active-low pulse 33
        // 12-MHz clocks wide (reload plus counter values 0..31).
        private const long GsInterruptLength = 33L * 4L;

        private readonly TsFmPsgRenderer[] m_psg =
        {
            new TsFmPsgRenderer("MultiSound YM2203 D1 SSG", PanType.Abc),
            new TsFmPsgRenderer("MultiSound YM2203 D2 SSG", PanType.Cba),
        };
        private readonly TsFmFmRenderer m_fm = new TsFmFmRenderer();
        private readonly TsFmSaa1099Renderer m_saa =
            new TsFmSaa1099Renderer();
        private readonly ISoundRenderer[] m_renderers;
        private readonly byte[] m_ymRegister = new byte[2];
        private readonly PsgPortState[] m_ira =
        {
            new PsgPortState(0xFF), new PsgPortState(0xFF),
        };
        private readonly PsgPortState[] m_irb =
        {
            new PsgPortState(0xFF), new PsgPortState(0xFF),
        };
        private readonly byte[] m_gsRom = new byte[GsRomSize];
        private readonly byte[] m_gsRam = new byte[GsRamSize];
        private readonly byte[] m_dacSample = new byte[4];
        private readonly byte[] m_dacVolume = new byte[4];

        private CpuUnit m_gsCpu;
        private bool m_gsRomLoaded;
        private bool m_hostRomM1Access;
        private bool m_frameStarted;
        private long m_gsMaster;
        private long m_frameMasterStart;
        private long m_gsInstructionMaster;
        private long m_gsInstructionTact;
        private byte m_ymChip;
        private bool m_ymReadRegister;
        private bool m_fmEnabled;
        private bool m_saaClockEnabled;
        private byte m_gsPage;
        private byte m_gsCommand;
        private byte m_gsHostData;
        private byte m_gsOutputData;
        private bool m_gsCommandPending;
        private bool m_gsDataPending;
        private int m_lastDacLeft;
        private int m_lastDacRight;
        private int m_volume;
        private bool m_effectiveYm;
        private bool m_effectiveSaa;
        private bool m_effectiveGs;
        private bool m_effectiveSoundDrive;

        public ZxMultiSoundDevice()
        {
            Name = "ZX-MultiSound Rev.A2";
            Description =
                "ZX-MultiSound Rev.A2 for ZXBUS: 2 x YM2203, SAA1099, " +
                "General Sound 16 MHz/1 MB with ROM 1.05b and SounDrive. " +
                "The external SAM2695 synthesizer is not emulated.";
            Category = BusDeviceCategory.Music;
            AutomaticConfiguration = true;
            YmEnabled = true;
            SaaEnabled = true;
            GeneralSoundEnabled = true;
            SoundDriveEnabled = true;
            m_effectiveYm = true;
            m_effectiveSaa = true;
            m_effectiveGs = true;
            m_effectiveSoundDrive = true;
            m_volume = 100;
            m_renderers = new ISoundRenderer[]
            {
                m_psg[0], m_psg[1], m_fm, m_saa,
            };
            ResetBoard();
        }

        public bool AutomaticConfiguration { get; set; }
        public bool YmEnabled { get; set; }
        public bool SaaEnabled { get; set; }
        public bool GeneralSoundEnabled { get; set; }
        public bool SoundDriveEnabled { get; set; }

        public bool EffectiveYmEnabled { get { return m_effectiveYm; } }
        public bool EffectiveSaaEnabled { get { return m_effectiveSaa; } }
        public bool EffectiveGeneralSoundEnabled { get { return m_effectiveGs; } }
        public bool EffectiveSoundDriveEnabled { get { return m_effectiveSoundDrive; } }

        public bool RejectDc { get { return true; } }

        public IEnumerable<ISoundRenderer> SoundRenderers
        {
            get { return m_renderers; }
        }

        public new int Volume
        {
            get { return m_volume; }
            set
            {
                value = Math.Max(0, Math.Min(100, value));
                m_volume = value;
                base.Volume = value;
                foreach (var renderer in m_renderers)
                    renderer.Volume = value;
                OnConfigChanged();
            }
        }

        public byte RegAddr
        {
            get { return m_ymRegister[m_ymChip]; }
            set
            {
                m_ymRegister[m_ymChip] = value;
                m_psg[m_ymChip].RegAddr = value;
            }
        }

        public byte GetReg(int index)
        {
            return m_psg[m_ymChip].GetReg(index);
        }

        public void SetReg(int index, byte value)
        {
            m_psg[m_ymChip].SetReg(index, value);
        }

        public event Action<IPsgDevice, PsgPortState> IraHandler;
        public event Action<IPsgDevice, PsgPortState> IrbHandler;

        /// <summary>
        /// Resolves the physical switches against other boards before the bus
        /// subscribes to ports. Automatic mode always chooses a contention-free
        /// combination. Manual mode rejects impossible wiring explicitly.
        /// </summary>
        public void ResolveConfiguration(
            bool neoGsInstalled,
            bool internalMusicInstalled)
        {
            if (!AutomaticConfiguration)
            {
                if (GeneralSoundEnabled && neoGsInstalled)
                    throw new InvalidOperationException(
                        "ZX-MultiSound GS and NeoGS both decode ports #B3/#BB. " +
                        "Disable the MultiSound GS switch or use Automatic mode.");
                if (YmEnabled && internalMusicInstalled)
                    throw new InvalidOperationException(
                        "ZX-MultiSound YM/TSFM conflicts with internal AY/TSFM. " +
                        "Select Music: None or use Automatic mode.");
            }

            m_effectiveYm = YmEnabled &&
                (!internalMusicInstalled || !AutomaticConfiguration);
            m_effectiveSaa = SaaEnabled;
            m_effectiveGs = GeneralSoundEnabled &&
                (!neoGsInstalled || !AutomaticConfiguration);
            m_effectiveSoundDrive = SoundDriveEnabled;
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            ResolveConfiguration(
                bmgr.FindDevice<NeoGsDevice>() != null,
                bmgr.FindDevice<AYCHRV>() != null ||
                    bmgr.FindDevice<TurboSoundFmPro>() != null);

            foreach (var renderer in m_renderers)
            {
                var device = renderer as BusDeviceBase;
                if (device != null)
                    device.BusInit(bmgr);
            }

            if (m_effectiveYm || m_effectiveSaa)
                bmgr.Events.SubscribeWrIo(0xC00F, 0xC00D, WriteYmAddress);
            if (m_effectiveYm)
            {
                bmgr.Events.SubscribeRdIo(0xC00F, 0xC00D, ReadYmAddress);
                bmgr.Events.SubscribeWrIo(0xC00F, 0x800D, WriteYmData);
            }
            if (m_effectiveSaa)
                bmgr.Events.SubscribeWrIo(0x00FF, 0x00FF, WriteSaa);
            if (m_effectiveGs)
            {
                bmgr.Events.SubscribeRdIo(0x00FF, 0x00BB, HostReadGsStatus);
                bmgr.Events.SubscribeWrIo(0x00FF, 0x00BB, HostWriteGsCommand);
                bmgr.Events.SubscribeRdIo(0x00FF, 0x00B3, HostReadGsData);
                bmgr.Events.SubscribeWrIo(0x00FF, 0x00B3, HostWriteGsData);
            }
            if (m_effectiveSoundDrive)
                bmgr.Events.SubscribeWrIo(0x00AF, 0x000F, WriteSoundDrive);
            if (m_effectiveSaa || m_effectiveSoundDrive)
                bmgr.Events.SubscribeRdMemM1(0x0000, 0x0000, TrackHostM1);
            bmgr.Events.SubscribeReset(ResetBoard);
        }

        public override void BusConnect()
        {
            base.BusConnect();
            if (m_effectiveGs)
            {
                LoadGsRom();
                for (var i = 0; i < m_gsRam.Length; i++)
                    m_gsRam[i] = 0xFF;
            }
            ResetBoard();
        }

        public override void BusDisconnect()
        {
            foreach (var renderer in m_renderers)
            {
                var device = renderer as BusDeviceBase;
                if (device != null)
                    device.BusDisconnect();
            }
            base.BusDisconnect();
        }

        public override void ResetState()
        {
            ResetBoard();
        }

        protected override void OnConfigLoad(XmlNode node)
        {
            base.OnConfigLoad(node);
            AutomaticConfiguration = Utils.GetXmlAttributeAsBool(
                node, "automatic", AutomaticConfiguration);
            YmEnabled = Utils.GetXmlAttributeAsBool(node, "ym", YmEnabled);
            SaaEnabled = Utils.GetXmlAttributeAsBool(node, "saa", SaaEnabled);
            GeneralSoundEnabled = Utils.GetXmlAttributeAsBool(
                node, "gs", GeneralSoundEnabled);
            SoundDriveEnabled = Utils.GetXmlAttributeAsBool(
                node, "soundDrive", SoundDriveEnabled);
            Volume = Utils.GetXmlAttributeAsInt32(node, "volume", Volume);
        }

        protected override void OnConfigSave(XmlNode node)
        {
            base.OnConfigSave(node);
            Utils.SetXmlAttribute(node, "automatic", AutomaticConfiguration);
            Utils.SetXmlAttribute(node, "ym", YmEnabled);
            Utils.SetXmlAttribute(node, "saa", SaaEnabled);
            Utils.SetXmlAttribute(node, "gs", GeneralSoundEnabled);
            Utils.SetXmlAttribute(node, "soundDrive", SoundDriveEnabled);
            Utils.SetXmlAttribute(node, "volume", Volume);
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            if (m_frameStarted)
                m_frameMasterStart += GsMasterTicksPerFrame;
            else
                m_frameStarted = true;
        }

        protected override void OnEndFrame()
        {
            if (m_effectiveGs)
                ExecuteGsTo(m_frameMasterStart + GsMasterTicksPerFrame);
            base.OnEndFrame();
        }

        private void ResetBoard()
        {
            m_ymChip = 0;
            m_ymReadRegister = false;
            m_fmEnabled = false;
            m_saaClockEnabled = false;
            m_hostRomM1Access = false;
            Array.Clear(m_ymRegister, 0, m_ymRegister.Length);
            m_psg[0].ResetChip();
            m_psg[1].ResetChip();
            m_fm.ResetChip();
            m_fm.Enabled = false;
            m_saa.ResetChip();
            m_gsPage = 0;
            m_gsCommand = 0;
            m_gsHostData = 0;
            m_gsOutputData = 0;
            m_gsCommandPending = false;
            m_gsDataPending = false;
            Array.Clear(m_dacSample, 0, m_dacSample.Length);
            Array.Clear(m_dacVolume, 0, m_dacVolume.Length);
            m_lastDacLeft = 0;
            m_lastDacRight = 0;
            m_gsMaster = 0;
            m_frameMasterStart = 0;
            m_frameStarted = false;
            if (m_effectiveGs)
                CreateGsCpu();
            else
                m_gsCpu = null;
        }

        private void WriteYmAddress(ushort address, byte value, ref bool handled)
        {
            if ((value & 0xF0) == 0xF0)
            {
                if (m_effectiveYm)
                {
                    m_ymChip = (byte)(value & 1);
                    m_ymReadRegister = (value & 2) != 0;
                    m_fmEnabled = (value & 4) == 0;
                    m_fm.Enabled = m_fmEnabled;
                }
                if (m_effectiveSaa)
                    m_saaClockEnabled = (value & 8) == 0;
                return;
            }
            if (!m_effectiveYm)
                return;
            m_ymRegister[m_ymChip] = value;
            m_psg[m_ymChip].RegAddr = value;
        }

        private void WriteYmData(ushort address, byte value, ref bool handled)
        {
            var chip = m_ymChip;
            var register = m_ymRegister[chip];
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

        private void ReadYmAddress(ushort address, ref byte value, ref bool handled)
        {
            if (handled)
                return;
            handled = true;
            var chip = m_ymChip;
            // FM output enable controls the analog gate only.  It does not
            // change YM A0 or disconnect the chip's readable status bus.
            if (!m_ymReadRegister)
            {
                value = m_fm.GetStatus(chip);
                return;
            }
            var register = m_ymRegister[chip];
            if ((register & 0x0F) == PsgRegId.IRA)
                value = ReadIra(chip);
            else if ((register & 0x0F) == PsgRegId.IRB)
                value = ReadIrb(chip);
            else if (register < 0x10)
                value = m_psg[chip].GetReg(register);
            else
                value = m_fm.GetRegister(chip, register);
        }

        private void WriteSaa(ushort address, byte value, ref bool handled)
        {
            if (!m_saaClockEnabled || m_hostRomM1Access)
                return;
            if ((address & 0x0100) != 0)
                m_saa.RegAddr = value;
            else
                m_saa.SetReg(m_saa.RegAddr, value);
        }

        private void WriteSoundDrive(
            ushort address,
            byte value,
            ref bool handled)
        {
            if (m_hostRomM1Access)
                return;
            // GS and SounDrive feed the same four physical DAC registers.
            // Bring the independent GS Z80 up to the host write first so the
            // two producers remain ordered exactly as they are on the board.
            if (m_effectiveGs)
                SyncGsToHost();
            var channel = ((address >> 5) & 2) | ((address >> 4) & 1);
            m_dacSample[channel] = value;
            m_dacVolume[channel] = 63;
            UpdateCombinedDac(GetFrameTime());
        }

        private void TrackHostM1(ushort address, ref byte value)
        {
            // Exact rom_m1_access latch from the Rev.A2 CPLD.  It keys from
            // address lines only; the expansion board cannot see which host
            // memory chip actually answered the fetch.
            m_hostRomM1Access = (address & 0xC000) == 0;
        }

        private void HostWriteGsCommand(
            ushort address,
            byte value,
            ref bool handled)
        {
            SyncGsToHost();
            m_gsCommand = value;
            m_gsCommandPending = true;
            handled = true;
        }

        private void HostWriteGsData(
            ushort address,
            byte value,
            ref bool handled)
        {
            SyncGsToHost();
            m_gsHostData = value;
            m_gsDataPending = true;
            handled = true;
        }

        private void HostReadGsStatus(
            ushort address,
            ref byte value,
            ref bool handled)
        {
            if (handled)
                return;
            SyncGsToHost();
            value = (byte)((m_gsDataPending ? 0x80 : 0) | 0x7E |
                (m_gsCommandPending ? 1 : 0));
            handled = true;
        }

        private void HostReadGsData(
            ushort address,
            ref byte value,
            ref bool handled)
        {
            if (handled)
                return;
            SyncGsToHost();
            value = m_gsOutputData;
            m_gsDataPending = false;
            handled = true;
        }

        private void SyncGsToHost()
        {
            var fraction = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            ExecuteGsTo(m_frameMasterStart +
                (long)(fraction * GsMasterTicksPerFrame + 0.5D));
        }

        private void ExecuteGsTo(long target)
        {
            if (m_gsCpu == null)
                return;
            while (m_gsMaster < target)
            {
                // On reset the CPLD leaves INT inactive for one complete
                // counter interval.  Later pulses start at each reload.
                m_gsCpu.INT = m_gsMaster >= GsInterruptPeriod &&
                    (m_gsMaster % GsInterruptPeriod) < GsInterruptLength;
                var before = m_gsCpu.Tact;
                m_gsInstructionMaster = m_gsMaster;
                m_gsInstructionTact = before;
                m_gsCpu.ExecCycle();
                var elapsed = Math.Max(1L, m_gsCpu.Tact - before);
                m_gsMaster += elapsed * GsMasterTicksPerCpuTact;
            }
        }

        private byte ReadGsMemory(ushort address)
        {
            byte value;
            if (address < 0x4000 || (address >= 0x8000 && m_gsPage == 0))
            {
                value = m_gsRom[address & 0x7FFF];
            }
            else
            {
                var physical = GetGsRamAddress(address);
                value = m_gsRam[physical];
            }
            if ((address & 0xE000) == 0x6000)
            {
                var channel = (address >> 8) & 3;
                m_dacSample[channel] = value;
                UpdateCombinedDac(GetGsFrameTime());
            }
            return value;
        }

        private void WriteGsMemory(ushort address, byte value)
        {
            if (address < 0x4000 || (address >= 0x8000 && m_gsPage == 0))
                return;
            m_gsRam[GetGsRamAddress(address)] = value;
        }

        private int GetGsRamAddress(ushort address)
        {
            var page = address < 0x8000 ? 1 : (m_gsPage & 0x1F);
            return ((page << 15) | (address & 0x7FFF)) & (GsRamSize - 1);
        }

        private byte ReadGsPort(ushort address)
        {
            switch (address & 0x0F)
            {
                case 0x01: return m_gsCommand;
                case 0x02:
                    m_gsDataPending = false;
                    return m_gsHostData;
                case 0x04:
                    return (byte)((m_gsDataPending ? 0x80 : 0) | 0x7E |
                        (m_gsCommandPending ? 1 : 0));
                case 0x05:
                    m_gsCommandPending = false;
                    return 0xFF;
                case 0x0A:
                    m_gsDataPending = (m_gsPage & 1) == 0;
                    return 0xFF;
                case 0x0B:
                    m_gsCommandPending = (m_dacVolume[3] & 0x20) != 0;
                    return 0xFF;
                default:
                    return 0xFF;
            }
        }

        private void WriteGsPort(ushort address, byte value)
        {
            switch (address & 0x0F)
            {
                case 0x00:
                    m_gsPage = (byte)(value & 0x7F);
                    break;
                case 0x03:
                    m_gsOutputData = value;
                    m_gsDataPending = true;
                    break;
                case 0x05:
                    m_gsCommandPending = false;
                    break;
                case 0x06:
                case 0x07:
                case 0x08:
                case 0x09:
                    m_dacVolume[(address & 0x0F) - 6] =
                        (byte)(value & 0x3F);
                    UpdateCombinedDac(GetGsFrameTime());
                    break;
                case 0x0A:
                    m_gsDataPending = (m_gsPage & 1) == 0;
                    break;
                case 0x0B:
                    m_gsCommandPending = (m_dacVolume[3] & 0x20) != 0;
                    break;
            }
        }

        private double GetGsFrameTime()
        {
            // CpuUnit invokes memory/port callbacks during ExecCycle.  Use
            // the GS CPU's own 16-MHz progress inside that instruction;
            // using GetFrameTime() here timestamps a whole GS burst at the
            // host Z80's single position and SoundDeviceBase then discards
            // every DAC transition after the first one.
            var elapsedCpuTacts = Math.Max(
                0L, m_gsCpu.Tact - m_gsInstructionTact);
            var eventMaster = m_gsInstructionMaster +
                elapsedCpuTacts * GsMasterTicksPerCpuTact;
            return (double)(eventMaster - m_frameMasterStart) /
                (double)GsMasterTicksPerFrame;
        }

        private void UpdateCombinedDac(double frameTime)
        {
            var left = MixDac(0) + MixDac(1);
            var right = MixDac(2) + MixDac(3);
            if (left == m_lastDacLeft && right == m_lastDacRight)
                return;
            m_lastDacLeft = left;
            m_lastDacRight = right;
            var gain = Volume / 100D;
            UpdateDac(
                frameTime,
                Clamp16((int)(left * gain)),
                Clamp16((int)(right * gain)));
        }

        private int MixDac(int channel)
        {
            return (m_dacSample[channel] - 0x80) *
                (m_dacVolume[channel] & 0x3F) * 2;
        }

        private static short Clamp16(int value)
        {
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));
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
            state.OutState = value;
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x40) != 0;
            var handler = IraHandler;
            if (handler != null)
                handler(this, state);
        }

        private void WriteIrb(int chip, byte value)
        {
            var state = m_irb[chip];
            state.OutState = value;
            state.DirOut = (m_psg[chip].GetReg(PsgRegId.MIXER_CONTROL) & 0x80) != 0;
            var handler = IrbHandler;
            if (handler != null)
                handler(this, state);
        }

        private void LoadGsRom()
        {
            if (m_gsRomLoaded)
                return;
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(GsRomResourceName))
            {
                if (stream == null || stream.Length != GsRomSize)
                    throw new InvalidDataException(
                        "ZX-MultiSound GS ROM 1.05b is missing or invalid");
                var offset = 0;
                while (offset < m_gsRom.Length)
                {
                    var read = stream.Read(
                        m_gsRom, offset, m_gsRom.Length - offset);
                    if (read <= 0)
                        throw new EndOfStreamException(
                            "ZX-MultiSound GS ROM is truncated");
                    offset += read;
                }
            }
            m_gsRomLoaded = true;
        }

        private void CreateGsCpu()
        {
            m_gsCpu = new CpuUnit();
            m_gsCpu.RDMEM_M1 = ReadGsMemory;
            m_gsCpu.RDMEM = ReadGsMemory;
            m_gsCpu.WRMEM = WriteGsMemory;
            m_gsCpu.RDPORT = ReadGsPort;
            m_gsCpu.WRPORT = WriteGsPort;
            m_gsCpu.RESET = Dummy;
            m_gsCpu.NMIACK_M1 = Dummy;
            m_gsCpu.INTACK_M1 = Dummy;
            m_gsCpu.REFRESH = Dummy;
            m_gsCpu.RDNOMREQ = DummyAddress;
            m_gsCpu.WRNOMREQ = DummyAddress;
            m_gsCpu.SCANSIG = Dummy;
            m_gsCpu.BUS = 0xFF;
            m_gsCpu.RST = true;
            m_gsCpu.ExecCycle();
            m_gsCpu.RST = false;
        }

        private static void Dummy() { }
        private static void DummyAddress(ushort address) { }
    }
}
