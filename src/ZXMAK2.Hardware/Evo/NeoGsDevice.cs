using System;
using System.IO;
using System.Reflection;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;


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

        private readonly byte[] m_rom = new byte[RomSize];
        private readonly byte[] m_ram = new byte[RamSize];
        private readonly byte[] m_page = new byte[4];
        private readonly byte[] m_volume = new byte[8];
        private readonly byte[] m_sample = new byte[8];

        private CpuUnit m_cpu;
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
        private readonly byte[] m_dmaRegisters = new byte[4];
        private bool m_led;
        private bool m_romLoaded;
        private bool m_frameStarted;
        private long m_boardMaster;
        private long m_frameMasterStart;
        private long m_nextDacMaster;
        private long m_nmiReleaseMaster;
        private long m_dacTickCount;

        public NeoGsDevice()
        {
            Name = "NeoGS rev.C-VS";
            Description =
                "NeoGS ZXBUS music card, Rev. C-VS, official ROM 1.11, " +
                "4 MB RAM and 4/8-channel DAC";
            Category = BusDeviceCategory.Music;
        }

        public bool RejectDc
        {
            get { return true; }
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00BB, HostReadStatus);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BB, HostWriteCommand);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00B3, HostReadData);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00B3, HostWriteData);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0033, HostWriteControl);
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
            m_dacTickCount = 0;
            m_frameStarted = false;
            ResetCard();
        }

        public override void BusDisconnect()
        {
            base.BusDisconnect();
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
            ProcessDacTicks(targetMaster);
            while (m_boardMaster < targetMaster)
            {
                var segmentEnd = Math.Min(targetMaster, m_nextDacMaster);
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
                }
                ProcessDacTicks(targetMaster);
            }
            UpdateCpuSignals();
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
                OutputDac(m_nextDacMaster);
                m_nextDacMaster += MasterTicksPerDac;
            }
        }

        private void OutputDac(long masterTime)
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

            var frameTime = (double)(masterTime - m_frameMasterStart) /
                (double)MasterTicksPerFrame;
            UpdateDac(frameTime, Clamp16(left), Clamp16(right));
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
                    // No virtual SD/VS10xx medium is installed on the card.
                    return 0x06;
                case 0x13:
                case 0x14:
                case 0x15:
                    return 0xFF;
                case 0x1B:
                    return m_dmaModule;
                case 0x1C:
                case 0x1D:
                case 0x1E:
                case 0x1F:
                    return m_dmaRegisters[(address & 0x3F) - 0x1C];
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
                    ApplySetClear(ref m_serialControl, value, 0x3F);
                    break;
                case 0x13:
                case 0x14:
                case 0x15:
                    // SPI register timing is accepted; absent peripherals
                    // return idle bus data on their following reads.
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
                    m_dmaRegisters[port - 0x1C] = value;
                    break;
                case 0x20:
                case 0x21:
                case 0x22:
                case 0x23:
                    m_page[port - 0x20] = value;
                    break;
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
            m_serialControl = 0x0B;
            m_timerRate = 0;
            m_interruptEnable = 1;
            m_interruptRequest = 0;
            m_dmaModule = 0;
            Array.Clear(m_dmaRegisters, 0, m_dmaRegisters.Length);
            m_led = true;
            m_nmiReleaseMaster = 0;
            CreateCpu();
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
