using System;
using System.IO;
using System.Reflection;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Circuits.SecureDigital;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// NeoGS Rev. C-VS core.  The implementation follows the current NedoPC
    /// FPGA register map and runs the card Z80 synchronously with the host.
    /// This keeps command/data handshakes deterministic and avoids the race
    /// conditions of the old, unused background-thread prototype.
    /// </summary>
    public sealed class NeoGsDevice : SoundDeviceBase, ISoundMixerConfiguration
    {
        private const string RomResourceName =
            "ZXMAK2.Hardware.Resources.NeoGS-1.11.rom";
        private const int RomSize = 512 * 1024;
        private const int RamSize = 4 * 1024 * 1024;
        private const int PageSize = 16 * 1024;

        // 120 MHz is the common integer time base for 10/12/20/24 MHz.
        private const long MasterClock = 120000000L;
        private const long MasterTicksPerFrame = MasterClock / 50;
        private const long MasterTicksPerDac = MasterClock / 37500;
        private const int OutputGainNumerator = 3;
        private const int OutputGainDenominator = 2;

        private readonly byte[] m_rom = new byte[RomSize];
        private readonly byte[] m_ram = new byte[RamSize];
        private readonly byte[] m_page = new byte[4];
        private readonly byte[] m_volume = new byte[8];
        private readonly byte[] m_sample = new byte[8];
        private readonly uint[] m_dmaAddress = new uint[4];
        private readonly bool[] m_dmaEnabled = new bool[4];
        private readonly object m_sdSync = new object();

        private CpuUnit m_cpu;
        private MemoryPentEvo m_hostMemory;
        private SdCard m_sdCard;
        private NeoGsVs10xx m_codec;
        private bool m_sandbox;
        private string m_sdImageFileName = string.Empty;
        private byte m_sdReceived;
        private byte m_commandFromHost;
        private byte m_dataFromHost;
        private byte m_dataToHost;
        private byte m_status;
        private byte m_config;
        private byte m_serialControl;
        private byte m_timerRate;
        private byte m_interruptEnable;
        private byte m_interruptRequest;
        private byte m_dmaModule;
        private bool m_dmaReadPrimed;
        private byte m_dmaReadLatch;
        private PeripheralDmaState m_sdDmaState;
        private int m_sdDmaCount;
        private int m_mp3DmaCount;
        private bool m_led;
        private bool m_romLoaded;
        private bool m_frameStarted;
        private long m_boardMaster;
        private long m_frameMasterStart;
        private long m_nextDacMaster;
        private long m_nmiReleaseMaster;
        private long m_dacTickCount;
        private long m_nextPeripheralDmaMaster;
        private long m_nextMp3Master;
        private long m_mp3StepRemainder;
        private int m_activeMp3SampleRate;
        private int m_dacLeft;
        private int m_dacRight;
        private int m_mp3Left;
        private int m_mp3Right;

        public NeoGsDevice()
        {
            Name = "NeoGS rev.C-VS";
            Description =
                "NeoGS ZXBUS music card, Rev. C-VS, official ROM 1.11, " +
                "4 MB RAM, 4/8-channel DAC, microSD and VS1011/MP3";
            Category = BusDeviceCategory.Music;
        }

        public bool RejectDc
        {
            get { return true; }
        }

        public string SdImageFileName
        {
            get { return m_sdImageFileName ?? string.Empty; }
            set
            {
                m_sdImageFileName = value ?? string.Empty;
                if (!m_sandbox && m_sdCard != null)
                    RestoreConfiguredCard();
            }
        }

        public string ConfiguredSdImageFileName
        {
            get { return SdImageFileName; }
        }

        public void ConfigureSdCard(string fileName)
        {
            SdImageFileName = fileName;
        }

        public bool IsSdCardMounted
        {
            get
            {
                lock (m_sdSync)
                {
                    return m_sdCard != null &&
                        !string.IsNullOrEmpty(m_sdCard.MountedFileName);
                }
            }
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            m_sandbox = bmgr.IsSandbox;
            m_hostMemory = bmgr.FindDevice<MemoryPentEvo>();
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00BB, HostReadStatus);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BB, HostWriteCommand);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00B3, HostReadData);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00B3, HostWriteData);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0033, HostWriteControl);
            bmgr.Events.SubscribeRdMem(0xC000, 0x0000, HostReadDma);
            bmgr.Events.SubscribeRdMemM1(0xC000, 0x0000, HostReadDma);
            bmgr.Events.SubscribeWrMem(0xC000, 0x0000, HostWriteDma);
        }

        public override void BusConnect()
        {
            base.BusConnect();
            LoadRom();
            for (var i = 0; i < m_ram.Length; i++)
                m_ram[i] = 0xFF;
            m_boardMaster = 0;
            m_frameMasterStart = 0;
            m_nextDacMaster = MasterTicksPerDac;
            m_nextPeripheralDmaMaster = 0;
            m_nextMp3Master = long.MaxValue;
            m_mp3StepRemainder = 0;
            m_activeMp3SampleRate = 0;
            m_dacTickCount = 0;
            m_frameStarted = false;
            lock (m_sdSync)
            {
                if (m_sdCard != null)
                    m_sdCard.Close();
                m_sdCard = new SdCard();
                m_sdReceived = 0xFF;
            }
            if (!m_sandbox)
                RestoreConfiguredCard();
            if (m_codec != null)
                m_codec.Dispose();
            m_codec = new NeoGsVs10xx();
            ResetCard();
        }

        public override void BusDisconnect()
        {
            lock (m_sdSync)
            {
                if (m_sdCard != null)
                {
                    m_sdCard.Close();
                    m_sdCard = null;
                }
            }
            if (m_codec != null)
            {
                m_codec.Dispose();
                m_codec = null;
            }
            base.BusDisconnect();
        }

        protected override void OnConfigLoad(XmlNode itemNode)
        {
            base.OnConfigLoad(itemNode);
            m_sdImageFileName = Utils.GetXmlAttributeAsString(
                itemNode, "sdImage", string.Empty);
        }

        protected override void OnConfigSave(XmlNode itemNode)
        {
            base.OnConfigSave(itemNode);
            Utils.SetXmlAttribute(
                itemNode, "sdImage", m_sdImageFileName ?? string.Empty);
        }

        public override void ResetState()
        {
            ResetCard();
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            if (m_frameStarted)
                m_frameMasterStart += MasterTicksPerFrame;
            else
                m_frameStarted = true;
        }

        protected override void OnEndFrame()
        {
            ExecuteTo(m_frameMasterStart + MasterTicksPerFrame);
            base.OnEndFrame();
        }

        private void HostReadStatus(
            ushort address,
            ref byte value,
            ref bool handled)
        {
            SyncToHost();
            value = (byte)(m_status | 0x7E);
            handled = true;
        }

        private void HostWriteCommand(
            ushort address,
            byte value,
            ref bool handled)
        {
            SyncToHost();
            m_commandFromHost = value;
            m_status |= 0x01;
            handled = true;
        }

        private void HostReadData(
            ushort address,
            ref byte value,
            ref bool handled)
        {
            SyncToHost();
            value = m_dataToHost;
            m_status &= 0x7F;
            handled = true;
        }

        private void HostWriteData(
            ushort address,
            byte value,
            ref bool handled)
        {
            SyncToHost();
            m_dataFromHost = value;
            m_status |= 0x80;
            handled = true;
        }

        private void HostWriteControl(
            ushort address,
            byte value,
            ref bool handled)
        {
            SyncToHost();
            switch (value & 0xE0)
            {
                case 0x80:
                    ResetCard();
                    break;
                case 0x40:
                    m_cpu.NMI = true;
                    m_nmiReleaseMaster =
                        m_boardMaster + 4L * GetMasterTicksPerCpuTact();
                    break;
                case 0x20:
                    m_led = !m_led;
                    break;
            }
            handled = true;
        }

        private void HostReadDma(ushort address, ref byte value)
        {
            if (!m_dmaEnabled[1] || !IsHostRomMapped())
                return;
            SyncToHost();
            var next = m_ram[(int)(m_dmaAddress[1] & (RamSize - 1))];
            if (m_dmaReadPrimed)
                value = m_dmaReadLatch;
            m_dmaReadLatch = next;
            m_dmaReadPrimed = true;
            IncrementDmaAddress(1);
        }

        private void HostWriteDma(ushort address, byte value)
        {
            if (!m_dmaEnabled[1])
                return;
            SyncToHost();
            m_ram[(int)(m_dmaAddress[1] & (RamSize - 1))] = value;
            IncrementDmaAddress(1);
        }

        private bool IsHostRomMapped()
        {
            if (m_hostMemory == null)
                return true;
            return Array.IndexOf(
                m_hostMemory.RomPages,
                m_hostMemory.Window0000) >= 0;
        }

        private void SyncToHost()
        {
            var fraction = GetFrameTime();
            if (fraction < 0D)
                fraction = 0D;
            if (fraction > 1D)
                fraction = 1D;
            ExecuteTo(
                m_frameMasterStart +
                (long)(fraction * MasterTicksPerFrame + 0.5D));
        }

        private void ExecuteTo(long targetMaster)
        {
            ProcessTimedAudio(targetMaster);
            while (m_boardMaster < targetMaster)
            {
                var segmentEnd = Math.Min(
                    targetMaster,
                    Math.Min(m_nextDacMaster, m_nextMp3Master));
                while (m_boardMaster < segmentEnd)
                {
                    UpdateCpuSignals();
                    var ticksPerTact = GetMasterTicksPerCpuTact();
                    var before = m_cpu.Tact;
                    m_cpu.ExecCycle();
                    var elapsed = m_cpu.Tact - before;
                    if (elapsed < 1)
                        elapsed = 1;
                    m_boardMaster += elapsed * ticksPerTact;
                    ProcessPeripheralDma();
                }
                ProcessTimedAudio(targetMaster);
            }
            UpdateCpuSignals();
        }

        private void ProcessTimedAudio(long targetMaster)
        {
            ProcessDacTicks(targetMaster);
            ProcessMp3Ticks(targetMaster);
        }

        private void ProcessDacTicks(long targetMaster)
        {
            while (m_nextDacMaster <= m_boardMaster &&
                m_nextDacMaster <= targetMaster)
            {
                m_dacTickCount++;
                var divider = GetTimerDivider();
                if ((m_dacTickCount % divider) == 0)
                    m_interruptRequest |= 0x01;
                UpdateInterruptLine();
                UpdateBoardDac(m_nextDacMaster);
                m_nextDacMaster += MasterTicksPerDac;
            }
        }

        private void ProcessMp3Ticks(long targetMaster)
        {
            var sampleRate = m_codec == null ? 0 : m_codec.SampleRate;
            if (sampleRate != m_activeMp3SampleRate)
            {
                m_activeMp3SampleRate = sampleRate;
                m_mp3StepRemainder = 0;
                m_nextMp3Master = sampleRate > 0
                    ? m_boardMaster
                    : long.MaxValue;
            }
            while (m_nextMp3Master <= m_boardMaster &&
                m_nextMp3Master <= targetMaster &&
                m_activeMp3SampleRate > 0)
            {
                short left;
                short right;
                if (m_codec.TryReadSample(out left, out right))
                {
                    m_mp3Left = left;
                    m_mp3Right = right;
                }
                else
                {
                    m_mp3Left = 0;
                    m_mp3Right = 0;
                }
                OutputMixed(m_nextMp3Master);
                AdvanceMp3Clock();
            }
        }

        private void AdvanceMp3Clock()
        {
            m_mp3StepRemainder += MasterClock;
            var delta = m_mp3StepRemainder / m_activeMp3SampleRate;
            m_mp3StepRemainder %= m_activeMp3SampleRate;
            m_nextMp3Master += Math.Max(1, delta);
        }

        private void UpdateBoardDac(long masterTime)
        {
            int left;
            int right;
            if ((m_config & 0x04) != 0)
            {
                left = MixChannel(0, 0) + MixChannel(1, 1) +
                    MixChannel(4, 4) + MixChannel(5, 5);
                right = MixChannel(2, 2) + MixChannel(3, 3) +
                    MixChannel(6, 6) + MixChannel(7, 7);
            }
            else if ((m_config & 0x40) != 0)
            {
                left = MixChannel(0, 0) + MixChannel(1, 1) +
                    MixChannel(2, 4) + MixChannel(3, 5);
                right = MixChannel(0, 2) + MixChannel(1, 3) +
                    MixChannel(2, 6) + MixChannel(3, 7);
            }
            else
            {
                // The Rev. C FPGA feeds four multiply cycles to each DAC;
                // in four-channel mode the two samples on each side repeat.
                left = 2 * (MixChannel(0, 0) + MixChannel(1, 1));
                right = 2 * (MixChannel(2, 2) + MixChannel(3, 3));
            }
            m_dacLeft = ApplyOutputGain(left);
            m_dacRight = ApplyOutputGain(right);
            OutputMixed(masterTime);
        }

        private void OutputMixed(long masterTime)
        {
            var frameTime = (double)(masterTime - m_frameMasterStart) /
                (double)MasterTicksPerFrame;
            UpdateDac(
                frameTime,
                Clamp16(m_dacLeft + m_mp3Left),
                Clamp16(m_dacRight + m_mp3Right));
        }

        private static int ApplyOutputGain(int value)
        {
            return value * OutputGainNumerator / OutputGainDenominator;
        }

        private int MixChannel(int sampleIndex, int volumeIndex)
        {
            var sample = m_sample[sampleIndex];
            if ((m_config & 0x80) != 0)
                sample ^= 0x80;
            return (sample - 0x80) * (m_volume[volumeIndex] & 0x3F);
        }

        private static short Clamp16(int value)
        {
            if (value < short.MinValue)
                return short.MinValue;
            if (value > short.MaxValue)
                return short.MaxValue;
            return (short)value;
        }

        private byte ReadMemory(ushort address)
        {
            var page = m_page[address >> 14];
            byte value;
            if ((m_config & 0x01) == 0 && (address >> 14) != 1)
            {
                var romAddress = ((page & 0x1F) * PageSize) +
                    (address & (PageSize - 1));
                value = m_rom[romAddress];
            }
            else
            {
                var ramAddress = (page * PageSize) +
                    (address & (PageSize - 1));
                value = m_ram[ramAddress];
            }

            if (address >= 0x6000 && address <= 0x7FFF)
            {
                var channel = (m_config & 0x04) != 0
                    ? ((address >> 8) & 7)
                    : ((address >> 8) & 3);
                m_sample[channel] = value;
            }
            return value;
        }

        private void WriteMemory(ushort address, byte value)
        {
            var window = address >> 14;
            var page = m_page[window];
            if ((m_config & 0x01) == 0 && window != 1)
                return;
            if ((m_config & 0x03) == 0x03 && (page & 0xFE) == 0)
                return;
            var ramAddress = (page * PageSize) +
                (address & (PageSize - 1));
            m_ram[ramAddress] = value;
        }

        private byte ReadPort(ushort address)
        {
            switch (address & 0x3F)
            {
                case 0x01:
                    return m_commandFromHost;
                case 0x02:
                    m_status &= 0x7F;
                    return m_dataFromHost;
                case 0x03:
                    return 0xFF;
                case 0x04:
                    return m_status;
                case 0x05:
                    m_status &= 0xFE;
                    return 0xFF;
                case 0x0A:
                    m_status = (byte)((m_status & 0x7F) |
                        ((m_page[2] & 1) == 0 ? 0x80 : 0x00));
                    return 0xFF;
                case 0x0B:
                    m_status = (byte)((m_status & 0xFE) |
                        ((m_volume[3] >> 5) & 1));
                    return 0xFF;
                case 0x0C:
                    return m_interruptEnable;
                case 0x0D:
                    return m_interruptRequest;
                case 0x0E:
                    return m_timerRate;
                case 0x0F:
                    return m_config;
                case 0x11:
                    return m_serialControl;
                case 0x12:
                    return GetSerialStatus();
                case 0x13:
                    return m_sdReceived;
                case 0x14:
                    var received = m_sdReceived;
                    m_sdReceived = SdTransfer(0xFF);
                    return received;
                case 0x15:
                    return m_codec == null ? (byte)0xFF : m_codec.ReadControl();
                case 0x1B:
                    return m_dmaModule;
                case 0x1C:
                case 0x1D:
                case 0x1E:
                case 0x1F:
                    return ReadDmaRegister((address & 0x3F) - 0x1C);
                default:
                    return 0xFF;
            }
        }

        private void WritePort(ushort address, byte value)
        {
            var port = address & 0x3F;
            switch (port)
            {
                case 0x00:
                    if ((m_config & 0x08) == 0)
                    {
                        m_page[2] = (byte)((value & 0x7F) << 1);
                        m_page[3] = (byte)(m_page[2] | 1);
                    }
                    else
                    {
                        m_page[2] = RotatePage(value);
                    }
                    break;
                case 0x01:
                    m_led = (value & 1) == 0;
                    break;
                case 0x03:
                    m_dataToHost = value;
                    m_status |= 0x80;
                    break;
                case 0x05:
                    m_status &= 0xFE;
                    break;
                case 0x06:
                case 0x07:
                case 0x08:
                case 0x09:
                    m_volume[port - 0x06] = (byte)(value & 0x3F);
                    break;
                case 0x0A:
                    m_status = (byte)((m_status & 0x7F) |
                        ((m_page[2] & 1) == 0 ? 0x80 : 0x00));
                    break;
                case 0x0B:
                    m_status = (byte)((m_status & 0xFE) |
                        ((m_volume[3] >> 5) & 1));
                    break;
                case 0x0C:
                    ApplySetClear(ref m_interruptEnable, value, 0x07);
                    UpdateInterruptLine();
                    break;
                case 0x0D:
                    ApplySetClear(ref m_interruptRequest, value, 0x07);
                    UpdateInterruptLine();
                    break;
                case 0x0E:
                    m_timerRate = (byte)(value & 7);
                    break;
                case 0x0F:
                    m_config = value;
                    break;
                case 0x10:
                    if ((m_config & 0x08) != 0)
                        m_page[3] = RotatePage(value);
                    break;
                case 0x11:
                    var oldSerialControl = m_serialControl;
                    ApplySetClear(ref m_serialControl, value, 0x3F);
                    ApplySerialControl(oldSerialControl);
                    break;
                case 0x13:
                    m_sdReceived = SdTransfer(value);
                    break;
                case 0x14:
                    if (m_codec != null)
                        m_codec.WriteData(value);
                    break;
                case 0x15:
                    if (m_codec != null)
                        m_codec.WriteControl(value);
                    break;
                case 0x16:
                case 0x17:
                case 0x18:
                case 0x19:
                    m_volume[4 + port - 0x16] = (byte)(value & 0x3F);
                    break;
                case 0x1B:
                    m_dmaModule = (byte)(value & 7);
                    break;
                case 0x1C:
                case 0x1D:
                case 0x1E:
                case 0x1F:
                    WriteDmaRegister(port - 0x1C, value);
                    break;
                case 0x20:
                case 0x21:
                case 0x22:
                case 0x23:
                    m_page[port - 0x20] = value;
                    break;
            }
        }

        private byte GetSerialStatus()
        {
            var value = 0x0C; // writable SD card, control SPI ready
            if (!IsSdCardMounted)
                value |= 0x02;
            if (m_codec != null && m_codec.DataRequest)
                value |= 0x01;
            return (byte)value;
        }

        private void ApplySerialControl(byte oldValue)
        {
            if (m_codec == null)
                return;
            var resetReleased = (m_serialControl & 0x04) != 0;
            if (((oldValue ^ m_serialControl) & 0x04) != 0)
                m_codec.HardwareReset(resetReleased);
            m_codec.SetControlSelected((m_serialControl & 0x02) == 0);
        }

        private byte SdTransfer(byte value)
        {
            lock (m_sdSync)
            {
                if ((m_serialControl & 0x01) != 0 || m_sdCard == null ||
                    string.IsNullOrEmpty(m_sdCard.MountedFileName))
                    return 0xFF;
                m_sdCard.Wr(value);
                return m_sdCard.Rd();
            }
        }

        private byte ReadDmaRegister(int register)
        {
            var module = m_dmaModule & 7;
            if (module < 1 || module > 3)
                return 0xFF;
            switch (register)
            {
                case 0:
                    return (byte)((m_dmaAddress[module] >> 16) & 0x3F);
                case 1:
                    return (byte)(m_dmaAddress[module] >> 8);
                case 2:
                    return (byte)m_dmaAddress[module];
                default:
                    return m_dmaEnabled[module] ? (byte)0x80 : (byte)0x00;
            }
        }

        private void WriteDmaRegister(int register, byte value)
        {
            var module = m_dmaModule & 7;
            if (module < 1 || module > 3)
                return;
            switch (register)
            {
                case 0:
                    m_dmaAddress[module] =
                        (m_dmaAddress[module] & 0x00FFFFU) |
                        ((uint)(value & 0x3F) << 16);
                    break;
                case 1:
                    m_dmaAddress[module] =
                        (m_dmaAddress[module] & 0x3F00FFU) |
                        ((uint)value << 8);
                    break;
                case 2:
                    m_dmaAddress[module] =
                        (m_dmaAddress[module] & 0x3FFF00U) | value;
                    break;
                case 3:
                    SetDmaEnabled(module, (value & 0x80) != 0);
                    break;
            }
        }

        private void SetDmaEnabled(int module, bool enabled)
        {
            m_dmaEnabled[module] = enabled;
            m_nextPeripheralDmaMaster = m_boardMaster;
            if (module == 1)
            {
                m_dmaReadPrimed = false;
            }
            else if (module == 2)
            {
                m_sdDmaState = enabled
                    ? PeripheralDmaState.WaitToken
                    : PeripheralDmaState.Idle;
                m_sdDmaCount = 0;
            }
            else if (module == 3)
            {
                m_mp3DmaCount = 0;
            }
        }

        private void IncrementDmaAddress(int module)
        {
            m_dmaAddress[module] =
                (m_dmaAddress[module] + 1U) & 0x3FFFFFU;
        }

        private void ProcessPeripheralDma()
        {
            if (m_boardMaster < m_nextPeripheralDmaMaster)
                return;
            var processed = false;
            if (m_dmaEnabled[2])
            {
                ProcessSdDmaByte();
                processed = true;
            }
            if (m_dmaEnabled[3] && m_codec != null && m_codec.DataRequest)
            {
                var value = m_ram[(int)(m_dmaAddress[3] & (RamSize - 1))];
                if (m_codec.WriteData(value))
                {
                    IncrementDmaAddress(3);
                    m_mp3DmaCount++;
                    if (m_mp3DmaCount >= 512)
                        FinishPeripheralDma(3, 0x04);
                }
                processed = true;
            }
            if (processed)
            {
                var halfSpeed = (m_serialControl & 0x10) != 0;
                var cpuTacts = halfSpeed && m_dmaEnabled[3] ? 32 : 16;
                m_nextPeripheralDmaMaster = m_boardMaster +
                    cpuTacts * GetMasterTicksPerCpuTact();
            }
            else
            {
                m_nextPeripheralDmaMaster = m_boardMaster;
            }
        }

        private void ProcessSdDmaByte()
        {
            var value = SdTransfer(0xFF);
            switch (m_sdDmaState)
            {
                case PeripheralDmaState.WaitToken:
                    if (value == 0xFF)
                        return;
                    if (value == 0xFE)
                    {
                        m_sdDmaState = PeripheralDmaState.Data;
                        return;
                    }
                    FinishPeripheralDma(2, 0x02);
                    break;
                case PeripheralDmaState.Data:
                    m_ram[(int)(m_dmaAddress[2] & (RamSize - 1))] = value;
                    IncrementDmaAddress(2);
                    m_sdDmaCount++;
                    if (m_sdDmaCount >= 512)
                        m_sdDmaState = PeripheralDmaState.Crc1;
                    break;
                case PeripheralDmaState.Crc1:
                    m_sdDmaState = PeripheralDmaState.Crc2;
                    break;
                case PeripheralDmaState.Crc2:
                    FinishPeripheralDma(2, 0x02);
                    break;
            }
        }

        private void FinishPeripheralDma(int module, byte interruptMask)
        {
            m_dmaEnabled[module] = false;
            if (module == 2)
                m_sdDmaState = PeripheralDmaState.Idle;
            m_interruptRequest |= interruptMask;
            UpdateInterruptLine();
        }

        private void RestoreConfiguredCard()
        {
            lock (m_sdSync)
            {
                if (m_sdCard == null)
                    return;
                m_sdCard.Close();
                m_sdReceived = 0xFF;
                if (string.IsNullOrEmpty(m_sdImageFileName) ||
                    !File.Exists(m_sdImageFileName))
                    return;
                try
                {
                    m_sdCard.Open(m_sdImageFileName);
                }
                catch (IOException)
                {
                    m_sdCard.Close();
                }
                catch (UnauthorizedAccessException)
                {
                    m_sdCard.Close();
                }
            }
        }

        private void ResetCard()
        {
            m_commandFromHost = 0;
            m_dataFromHost = 0;
            m_dataToHost = 0;
            m_status = 0;
            m_config = 0x30;
            m_page[0] = 0;
            m_page[1] = 3;
            m_page[2] = 0;
            m_page[3] = 1;
            Array.Clear(m_volume, 0, m_volume.Length);
            Array.Clear(m_sample, 0, m_sample.Length);
            // FPGA reset: SD/MP3 deselected, decoder held in reset,
            // control SPI at Fcpu/4 and MP3 data SPI at Fcpu/2.
            m_serialControl = 0x0B;
            m_timerRate = 0;
            m_interruptEnable = 1;
            m_interruptRequest = 0;
            m_dmaModule = 0;
            Array.Clear(m_dmaAddress, 0, m_dmaAddress.Length);
            Array.Clear(m_dmaEnabled, 0, m_dmaEnabled.Length);
            m_dmaReadPrimed = false;
            m_dmaReadLatch = 0xFF;
            m_sdDmaState = PeripheralDmaState.Idle;
            m_sdDmaCount = 0;
            m_mp3DmaCount = 0;
            m_dacLeft = 0;
            m_dacRight = 0;
            m_mp3Left = 0;
            m_mp3Right = 0;
            m_nextMp3Master = long.MaxValue;
            m_activeMp3SampleRate = 0;
            m_mp3StepRemainder = 0;
            lock (m_sdSync)
            {
                if (m_sdCard != null)
                    m_sdCard.Reset();
                m_sdReceived = 0xFF;
            }
            if (m_codec != null)
                m_codec.HardwareReset(false);
            m_led = true;
            m_nmiReleaseMaster = 0;
            CreateCpu();
        }

        private enum PeripheralDmaState
        {
            Idle,
            WaitToken,
            Data,
            Crc1,
            Crc2,
        }

        private void CreateCpu()
        {
            m_cpu = new CpuUnit();
            m_cpu.RDMEM_M1 = ReadMemory;
            m_cpu.RDMEM = ReadMemory;
            m_cpu.WRMEM = WriteMemory;
            m_cpu.RDPORT = ReadPort;
            m_cpu.WRPORT = WritePort;
            m_cpu.RESET = Dummy;
            m_cpu.NMIACK_M1 = NmiAcknowledge;
            m_cpu.INTACK_M1 = InterruptAcknowledge;
            m_cpu.REFRESH = Dummy;
            m_cpu.RDNOMREQ = DummyAddress;
            m_cpu.WRNOMREQ = DummyAddress;
            m_cpu.SCANSIG = Dummy;
            m_cpu.BUS = 0xFF;
            m_cpu.RST = true;
            m_cpu.ExecCycle();
            m_cpu.RST = false;
        }

        private void InterruptAcknowledge()
        {
            var pending = (byte)(m_interruptRequest & m_interruptEnable);
            if ((pending & 1) != 0)
            {
                m_interruptRequest &= 0xFE;
                m_cpu.BUS = 0xFF;
            }
            else if ((pending & 2) != 0)
            {
                m_interruptRequest &= 0xFD;
                m_cpu.BUS = 0xF7;
            }
            else if ((pending & 4) != 0)
            {
                m_interruptRequest &= 0xFB;
                m_cpu.BUS = 0xEF;
            }
            UpdateInterruptLine();
        }

        private void NmiAcknowledge()
        {
            m_cpu.NMI = false;
            m_nmiReleaseMaster = 0;
        }

        private void UpdateCpuSignals()
        {
            if (m_nmiReleaseMaster != 0 &&
                m_boardMaster >= m_nmiReleaseMaster)
            {
                m_cpu.NMI = false;
                m_nmiReleaseMaster = 0;
            }
            UpdateInterruptLine();
        }

        private void UpdateInterruptLine()
        {
            m_cpu.INT = (m_interruptRequest & m_interruptEnable & 7) != 0;
        }

        private int GetMasterTicksPerCpuTact()
        {
            switch (m_config & 0x30)
            {
                case 0x30:
                    return 12;
                case 0x10:
                    return 10;
                case 0x20:
                    return 6;
                default:
                    return 5;
            }
        }

        private int GetTimerDivider()
        {
            switch (m_timerRate & 7)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 4;
                case 3: return 8;
                case 4: return 16;
                case 5: return 64;
                case 6: return 256;
                default: return 1024;
            }
        }

        private static byte RotatePage(byte value)
        {
            return (byte)(((value & 0x7F) << 1) | (value >> 7));
        }

        private static void ApplySetClear(
            ref byte register,
            byte value,
            byte validMask)
        {
            var selected = (byte)(value & validMask);
            if ((value & 0x80) != 0)
                register |= selected;
            else
                register &= (byte)~selected;
        }

        private void LoadRom()
        {
            if (m_romLoaded)
                return;
            using (var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(RomResourceName))
            {
                if (stream == null || stream.Length != RomSize)
                    throw new InvalidDataException(
                        "NeoGS ROM 1.11 resource is missing or invalid");
                var offset = 0;
                while (offset < m_rom.Length)
                {
                    var read = stream.Read(
                        m_rom,
                        offset,
                        m_rom.Length - offset);
                    if (read <= 0)
                        throw new EndOfStreamException("NeoGS ROM is truncated");
                    offset += read;
                }
            }
            m_romLoaded = true;
        }

        private static void Dummy()
        {
        }

        private static void DummyAddress(ushort address)
        {
        }
    }
}
