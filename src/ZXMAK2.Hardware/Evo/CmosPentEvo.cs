using System;
using System.IO;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Attributes;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    public class CmosPentEvo : BusDeviceBase, IKeyboardDevice, IFrameDiagnosticProvider, IPersistentStateDevice
    {
        #region Fields

        private const int KeyboardBufferSize = 256;

        // AVR BaseConf config0: D0 video output, D3 beeper/tape-out source,
        // D4:D5 raster.  D2 is the live physical tape input and is supplied
        // by TapeDevice directly to the FE/F6 reads.
        private const byte AvrVideoMask = 0x31;
        private const byte AvrPersistentMask = 0x39;
        private const int AvrModeNvramAddress = 0xFE;
        private byte m_avrVideoConfiguration;
        private bool m_beeperTapeOut;
        private bool m_scrollPrevious;
        private bool m_numLockPrevious;
        private int m_scrollPressCount;
        private UlaPentEvo m_rasterUla;

        [HardwareValue("AVRVIDEO", Description = "AVR video bits D0/D4/D5")]
        public byte AvrVideoConfiguration { get { return m_avrVideoConfiguration; } }

        [HardwareValue("BEEPERMUX", Description = "AVR D3: false=D4 beeper, true=D3 tape-out")]
        public bool BeeperTapeOutSelected { get { return m_beeperTapeOut; } }

        [HardwareValue("AVRCONFIG", Description = "Persistent BaseConf AVR config bits D0/D3/D4/D5")]
        public byte AvrConfiguration
        {
            get { return (byte)(m_avrVideoConfiguration | (m_beeperTapeOut ? 0x08 : 0)); }
        }

        internal event Action BeeperMuxChanged;

        public string FrameDiagnosticText
        {
            get
            {
                int requested = (m_avrVideoConfiguration >> 4) & 3;
                int active = requested;
                if (m_rasterUla != null)
                {
                    requested = m_rasterUla.RequestedRasterMode;
                    active = m_rasterUla.ActiveRasterMode;
                }
                return string.Format(
                    "ZX-Evo B38: Scroll={0} AVR={1:X2} mux={2} TV/VGA={3} raster req={4}:{5} active={6}:{7}{8}",
                    m_scrollPressCount,
                    AvrConfiguration,
                    m_beeperTapeOut ? "D3" : "D4",
                    m_avrVideoConfiguration & 1,
                    requested,
                    GetRasterName(requested),
                    active,
                    GetRasterName(active),
                    requested == active ? string.Empty : " pending");
            }
        }

        private static string GetRasterName(int mode)
        {
            switch (mode)
            {
                case 0: return "normal";
                case 1: return "60Hz";
                case 2: return "48K";
                case 3: return "128K";
                default: return "unknown";
            }
        }

        internal void SetAvrVideoConfiguration(byte value)
        {
            m_avrVideoConfiguration = (byte)(value & AvrVideoMask);
            // Existing .cmos storage contains this previously inaccessible slot.
            // Preserve unsupported persisted flags; don't pretend to emulate them.
            eeprom[AvrModeNvramAddress] = (byte)((eeprom[AvrModeNvramAddress] & ~AvrVideoMask) | m_avrVideoConfiguration);
            if (m_rasterUla != null)
                m_rasterUla.RequestRaster((m_avrVideoConfiguration >> 4) & 3);
        }

        internal void SetBeeperTapeOut(bool tapeOut)
        {
            var changed = m_beeperTapeOut != tapeOut;
            m_beeperTapeOut = tapeOut;
            eeprom[AvrModeNvramAddress] = (byte)(
                (eeprom[AvrModeNvramAddress] & ~AvrPersistentMask) |
                m_avrVideoConfiguration |
                (m_beeperTapeOut ? 0x08 : 0));
            if (changed)
            {
                var handler = BeeperMuxChanged;
                if (handler != null)
                    handler();
            }
        }

        private void RestoreAvrVideoConfiguration()
        {
            m_beeperTapeOut = (eeprom[AvrModeNvramAddress] & 0x08) != 0;
            SetAvrVideoConfiguration(eeprom[AvrModeNvramAddress]);
        }

        private void AdvanceAvrVideoConfiguration()
        {
            // AVR r1364 zx.c: increment only the noncontiguous video-mask bits.
            byte next = unchecked((byte)((m_avrVideoConfiguration | (byte)~AvrVideoMask) + 1));
            byte change = (byte)((next ^ m_avrVideoConfiguration) & AvrVideoMask);
            SetAvrVideoConfiguration((byte)(m_avrVideoConfiguration ^ change));
        }

        private static readonly Key[] KeyboardKeys =
        {
            Key.Escape,
            Key.D1, Key.D2, Key.D3, Key.D4, Key.D5,
            Key.D6, Key.D7, Key.D8, Key.D9, Key.D0,
            Key.Minus, Key.Equals, Key.BackSpace, Key.Tab,
            Key.Q, Key.W, Key.E, Key.R, Key.T,
            Key.Y, Key.U, Key.I, Key.O, Key.P,
            Key.LeftBracket, Key.RightBracket, Key.Return,
            Key.LeftControl,
            Key.A, Key.S, Key.D, Key.F, Key.G,
            Key.H, Key.J, Key.K, Key.L,
            Key.SemiColon, Key.Apostrophe, Key.Grave,
            Key.LeftShift, Key.BackSlash,
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M,
            Key.Comma, Key.Period, Key.Slash, Key.RightShift,
            Key.NumPadStar, Key.LeftAlt, Key.Space, Key.CapsLock,
            Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F6,
            Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12,
            Key.NumPad7, Key.NumPad8, Key.NumPad9, Key.NumPadMinus,
            Key.NumPad4, Key.NumPad5, Key.NumPad6, Key.NumPadPlus,
            Key.NumPad1, Key.NumPad2, Key.NumPad3,
            Key.NumPad0, Key.NumPadPeriod,
            Key.NumPadEnter, Key.RightControl, Key.NumPadSlash,
            Key.RightAlt,
            Key.Home, Key.UpArrow, Key.PageUp, Key.LeftArrow,
            Key.RightArrow, Key.End, Key.DownArrow, Key.PageDown,
            Key.Insert, Key.Delete,
            Key.LeftWindows, Key.RightWindows,
            Key.ScrollLock, Key.NumLock
        };

        // Low byte is PS/2 Scan Code Set 2. Bit 8 means an E0 prefix.
        private static readonly ushort[] KeyboardScanCodes =
        {
            0x076,
            0x016, 0x01E, 0x026, 0x025, 0x02E,
            0x036, 0x03D, 0x03E, 0x046, 0x045,
            0x04E, 0x055, 0x066, 0x00D,
            0x015, 0x01D, 0x024, 0x02D, 0x02C,
            0x035, 0x03C, 0x043, 0x044, 0x04D,
            0x054, 0x05B, 0x05A,
            0x014,
            0x01C, 0x01B, 0x023, 0x02B, 0x034,
            0x033, 0x03B, 0x042, 0x04B,
            0x04C, 0x052, 0x00E,
            0x012, 0x05D,
            0x01A, 0x022, 0x021, 0x02A, 0x032, 0x031, 0x03A,
            0x041, 0x049, 0x04A, 0x059,
            0x07C, 0x011, 0x029, 0x058,
            0x005, 0x006, 0x004, 0x00C, 0x003, 0x00B,
            0x083, 0x00A, 0x001, 0x009, 0x078, 0x007,
            0x06C, 0x075, 0x07D, 0x07B,
            0x06B, 0x073, 0x074, 0x079,
            0x069, 0x072, 0x07A,
            0x070, 0x071,
            0x15A, 0x114, 0x14A,
            0x111,
            0x16C, 0x175, 0x17D, 0x16B,
            0x174, 0x169, 0x172, 0x17A,
            0x170, 0x171,
            0x11F, 0x127,
            0x07E, 0x077
        };

        private bool sandbox;
        private MemoryPentEvo mem;
        private FileStream eepromFile;
        private byte[] eeprom;
        private Mode mode;
        private byte addr;

        // AVR firmware exposes the documented Kondratyev/16550-compatible
        // register block through BaseConf comport_addr[2:0].  Physical host
        // serial transport is intentionally outside B37; these latches and
        // status values follow rs232.c so firmware can probe the interface.
        private byte m_rs232Dll;
        private byte m_rs232Dlm;
        private byte m_rs232Ier;
        private byte m_rs232Isr;
        private byte m_rs232Lcr;
        private byte m_rs232Mcr;
        private byte m_rs232Lsr;
        private byte m_rs232Msr;
        private byte m_rs232Scr;

        private byte[] BaseConfData;
        private byte[] BootVerData;
        private string m_fileName = null;

        private readonly byte[] keyboardBuffer = new byte[KeyboardBufferSize];
        private readonly bool[] keyboardPrevious = new bool[KeyboardKeys.Length];
        private int keyboardPush;
        private int keyboardPop;
        private bool keyboardFull;
        private IKeyboardState keyboardState;

        #endregion


        public CmosPentEvo()
        {
            Category = BusDeviceCategory.Other;
            Name = "CMOS PentEvo";
            Description = "PentEvo RTC";
            
            eeprom = new byte[256];
            BaseConfData = new byte[16];
            BootVerData = new byte[16];
        }


        public bool SHADOW
        {
            get { return (mem == null) ? false : mem.DOSEN || mem.SYSEN || mem.SHADOW; }
        }

        public string PersistentStateFileName
        {
            get { return m_fileName; }
        }

        public void ResetPersistentState()
        {
            Array.Clear(eeprom, 0, eeprom.Length);
            m_avrVideoConfiguration = 0;
            m_beeperTapeOut = false;
            Reset();
            if (m_rasterUla != null)
            {
                m_rasterUla.RequestRaster(0);
            }
            SaveEeprom();
        }

        private bool visable
        {
            get { return (mem == null) ? false : mem.CMOSEN; }
        }


        #region BusDeviceBase

        public override void BusInit(IBusManager bmgr)
        {
            sandbox = bmgr.IsSandbox;
            mem = bmgr.FindDevice<MemoryPentEvo>();
            m_rasterUla = bmgr.FindDevice<UlaPentEvo>();
            m_fileName = bmgr.GetSatelliteFileName("cmos");

            bmgr.Events.SubscribeReset(Reset);
            // zports.v decodes F7 by low byte, then applies A8/A13/A14
            // equations in the handler.  This retains all RTL aliases.
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00F7, WrPortF7);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00F7, RdPortF7);
            // comport_rd/comport_wr decode only low byte EF; A10:A8 select
            // the eight official #F8EF..#FFEF UART registers.
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00EF, WrComPort);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00EF, RdComPort);
            bmgr.Events.SubscribeEndFrame(ScanKeyboard);
        }

        public override void BusConnect()
        {
            if (!sandbox)
            {
                using (eepromFile = File.Open(m_fileName, FileMode.OpenOrCreate))
                {
                    if (eepromFile.Length < 256)
                        eepromFile.Write(eeprom, 0, 256);
                    else
                        eepromFile.Read(eeprom, 0, 256);

                    eepromFile.Flush();
                    eepromFile.Close();
                }

                SetData(BaseConfData, "BaseConf Emu", new DateTime(2011, 4, 3));
                SetData(BootVerData, "Boot Emu", new DateTime(2012, 04, 5));
            }
            RestoreAvrVideoConfiguration();
        }

        public override void BusDisconnect()
        {
            SaveEeprom();
        }

        #endregion


        #region Private

        private void SaveEeprom()
        {
            if (sandbox || string.IsNullOrEmpty(m_fileName))
            {
                return;
            }
            using (eepromFile = File.Open(m_fileName, FileMode.OpenOrCreate))
            {
                eepromFile.Write(eeprom, 0, eeprom.Length);
                eepromFile.Flush();
                eepromFile.Close();
            }
        }

        void Reset()
        {
            mode = Mode.BaseConfVer; // посмотреть состояние по сбросу
            addr = 0;
            ResetRs232();
            ClearKeyboardBuffer();
            SyncKeyboardState();
        }

        private bool IsSelectedPortF7(ushort port)
        {
            return SHADOW ? (port & 0x0100) == 0 : (port & 0x0100) != 0;
        }

        private void WrPortF7(ushort port, byte val, ref bool handled)
        {
            if (!IsSelectedPortF7(port) || !(SHADOW || visable))
                return;

            bool addressWrite = (port & 0x2000) == 0;
            bool dataWrite = (port & 0x4000) == 0;
            if (!addressWrite && !dataWrite)
                return;
            handled = true;
            // Both strobes may be active for an RTL alias.  The FPGA sends
            // the address byte first and services the data transaction next.
            if (addressWrite)
                addr = val;
            if (dataWrite)
                WrCMOS(val);
        }

        private void RdPortF7(ushort port, ref byte val, ref bool handled)
        {
            if (!IsSelectedPortF7(port) || !(SHADOW || visable) ||
                (port & 0x4000) != 0)
                return;
            handled = true;
            val = RdCMOS();
        }

        private void WrComPort(ushort port, byte val, ref bool handled)
        {
            handled = true;
            WriteRs232((port >> 8) & 7, val);
        }

        private void RdComPort(ushort port, ref byte val, ref bool handled)
        {
            handled = true;
            val = ReadRs232((port >> 8) & 7);
        }

        private void ResetRs232()
        {
            // pentevo/avr/current/rs232.c:rs232_init().
            m_rs232Dll = 0x01;
            m_rs232Dlm = 0x00;
            m_rs232Ier = 0x00;
            m_rs232Isr = 0x01;
            m_rs232Lcr = 0x00;
            m_rs232Mcr = 0x00;
            m_rs232Lsr = 0x60;
            m_rs232Msr = 0xA0;
            m_rs232Scr = 0xFF;
        }

        private byte ReadRs232(int index)
        {
            switch (index & 7)
            {
                case 0:
                    if ((m_rs232Lcr & 0x80) != 0)
                        return m_rs232Dll;
                    // No physical receive transport is attached: DAT=0 and
                    // data-ready remains clear, as an empty firmware FIFO.
                    m_rs232Lsr &= 0xFE;
                    return 0;
                case 1:
                    return (m_rs232Lcr & 0x80) != 0 ? m_rs232Dlm : m_rs232Ier;
                case 2: return m_rs232Isr;
                case 3: return m_rs232Lcr;
                case 4: return m_rs232Mcr;
                case 5: return m_rs232Lsr;
                case 6:
                    byte result = m_rs232Msr;
                    // rs232_zx_read clears delta flags after an MSR read.
                    m_rs232Msr &= 0xF0;
                    return result;
                default: return m_rs232Scr;
            }
        }

        private void WriteRs232(int index, byte value)
        {
            switch (index & 7)
            {
                case 0:
                    if ((m_rs232Lcr & 0x80) != 0)
                        m_rs232Dll = value;
                    else
                    {
                        // B37 has no host serial endpoint.  Treat the local
                        // sink as completing immediately, leaving THR/TEMT
                        // asserted instead of manufacturing a stuck UART.
                        m_rs232Lsr |= 0x60;
                    }
                    break;
                case 1:
                    if ((m_rs232Lcr & 0x80) != 0)
                        m_rs232Dlm = value;
                    else
                        m_rs232Ier = (byte)(value & 0x0F);
                    break;
                case 2:
                    // FIFO is permanently enabled in the AVR implementation.
                    if ((value & 1) != 0)
                    {
                        if ((value & 2) != 0)
                            m_rs232Lsr &= 0xFC;
                        if ((value & 4) != 0)
                            m_rs232Lsr |= 0x60;
                    }
                    break;
                case 3: m_rs232Lcr = value; break;
                case 4: m_rs232Mcr = (byte)(value & 0x1F); break;
                case 5: break; // LSR is read-only in rs232.c.
                case 6: break; // MSR is read-only in rs232.c.
                case 7: m_rs232Scr = value; break;
            }
        }

        #endregion


        #region RTC emu

        DateTime dt = DateTime.Now;
        bool UF = false;

        byte RdCMOS()
        {
            var curDt = DateTime.Now;

            if (curDt.Subtract(dt).Seconds > 0 || curDt.Millisecond / 500 != dt.Millisecond / 500)
            {
                dt = curDt;
                UF = true;
            }

            if (addr < 0xF0)
            {
                switch (addr)
                {
                    case 0x00:
                        return BDC(dt.Second);
                    case 0x02:
                        return BDC(dt.Minute);
                    case 0x04:
                        return BDC(dt.Hour);
                    case 0x06:
                        return (byte)(dt.DayOfWeek);
                    case 0x07:
                        return BDC(dt.Day);
                    case 0x08:
                        return BDC(dt.Month);
                    case 0x09:
                        return BDC(dt.Year % 100);
                    case 0x0A:
                        return 0x00;
                    case 0x0B:
                        return 0x02;
                    case 0x0C:
                        var res = (byte)(UF ? 0x1C : 0x0C);
                        UF = false;
                        return res;
                    case 0x0D:
                        return 0x80;

                    default:
                        return eeprom[addr];
                }
            }
            else
            {
                switch (mode)
                {
                    case Mode.BaseConfVer:
                        return BaseConfData[addr & 0x0F];
                    case Mode.BootVer:
                        return BootVerData[addr & 0x0F];
                    case Mode.PS2Keyboard:
                        return PopKeyboardByte();
                    case Mode.ReadConfig:
                        return (addr & 0x0F) == 0 ? AvrConfiguration : (byte)0xFF;
                    default:
                        return 0xFF;
                }
            }
        }

        void WrCMOS(byte val)
        {
            if (addr < 0xF0)
            {
                if (addr == 0x0C && (val & 0x01) != 0)
                    ClearKeyboardBuffer();
                eeprom[addr] = val;
            }
            else
            {
                switch (val)
                {
                    case 0:
                        mode = Mode.BaseConfVer;
                        ClearKeyboardBuffer();
                        break;
                    case 1:
                        mode = Mode.BootVer;
                        ClearKeyboardBuffer();
                        break;
                    case 2:
                        mode = Mode.PS2Keyboard;
                        ClearKeyboardBuffer();
                        SyncKeyboardState();
                        break;

                    case 3:
                        mode = Mode.ReadConfig;
                        ClearKeyboardBuffer();
                        break;

                    default:
                        // AVR r1364 retains the byte selector; unknown types read FF.
                        mode = (Mode)val;
                        ClearKeyboardBuffer();
                        break;
                }
            }
        }

        private void ScanKeyboard()
        {
            if (keyboardState == null)
            {
                m_scrollPrevious = false;
                m_numLockPrevious = false;
                return;
            }

            var scroll = keyboardState[Key.ScrollLock];
            if (scroll && !m_scrollPrevious)
            {
                m_scrollPressCount++;
                AdvanceAvrVideoConfiguration();
            }
            m_scrollPrevious = scroll;

            // BaseConf AVR binds NumLock to func_beeper(): each rising edge
            // toggles config0.D3 between port-FE D4 and D3.
            var numLock = keyboardState[Key.NumLock];
            if (numLock && !m_numLockPrevious)
                SetBeeperTapeOut(!m_beeperTapeOut);
            m_numLockPrevious = numLock;

            var enabled = mode == Mode.PS2Keyboard;
            for (var i = 0; i < KeyboardKeys.Length; i++)
            {
                var pressed = keyboardState[KeyboardKeys[i]];
                if (enabled && pressed != keyboardPrevious[i])
                    PushKeyboardScanCode(KeyboardScanCodes[i], pressed);
                keyboardPrevious[i] = pressed;
            }
        }

        private void SyncKeyboardState()
        {
            m_scrollPrevious = keyboardState != null && keyboardState[Key.ScrollLock];
            m_numLockPrevious = keyboardState != null && keyboardState[Key.NumLock];
            for (var i = 0; i < KeyboardKeys.Length; i++)
                keyboardPrevious[i] = keyboardState != null && keyboardState[KeyboardKeys[i]];
        }

        private void PushKeyboardScanCode(ushort scanCode, bool pressed)
        {
            if ((scanCode & 0x100) != 0)
                PushKeyboardByte(0xE0);
            if (!pressed)
                PushKeyboardByte(0xF0);
            PushKeyboardByte((byte)scanCode);
        }

        private void PushKeyboardByte(byte value)
        {
            if (keyboardFull)
                return;

            keyboardBuffer[keyboardPush++] = value;
            if (keyboardPush == KeyboardBufferSize)
                keyboardPush = 0;
            if (keyboardPush == keyboardPop)
                keyboardFull = true;
        }

        private byte PopKeyboardByte()
        {
            if (keyboardFull)
                return 0xFF;
            if (keyboardPush == keyboardPop)
                return 0x00;

            var value = keyboardBuffer[keyboardPop++];
            if (keyboardPop == KeyboardBufferSize)
                keyboardPop = 0;
            return value;
        }

        private void ClearKeyboardBuffer()
        {
            keyboardPush = 0;
            keyboardPop = 0;
            keyboardFull = false;
        }

        byte BDC(int val)
        {
            var res = val;

            if ((eeprom[11] & 4) == 0)
            {
                var rem = 0;
                res = Math.DivRem(val, 10, out rem);
                res = (res * 16 + rem);
            }

            return (byte)res;
        }

        #endregion

        #region IKeyboardDevice

        public IKeyboardState KeyboardState
        {
            get { return keyboardState; }
            set { keyboardState = value; }
        }

        #endregion

        private void SetData(byte[] BaseConfData, string name, DateTime dateTime)
        {
            var tmp = name.PadRight(12, (char)0).Substring(0, 12);

            for (var i = 0; i < 12; i++)
                BaseConfData[i] = (byte)tmp[i];

            BaseConfData[13] = (byte)(((dateTime.Year - 2000) & 0x3F) << 1);
            var mnt = (byte)(dateTime.Month & 0x0F);
            BaseConfData[13] |= (byte)(mnt >> 3);
            BaseConfData[12] |= (byte)(mnt << 5);
            BaseConfData[12] |= (byte)(dateTime.Day & 0x1F);

        }

        private enum Mode : byte
        {
            BaseConfVer = 0,
            BootVer = 1,
            PS2Keyboard = 2,
            ReadConfig = 3
        }
    }
}
