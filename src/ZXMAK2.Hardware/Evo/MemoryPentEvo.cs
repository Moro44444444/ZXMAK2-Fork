using System;
using ZXMAK2.Hardware.Atm;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Attributes;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Cpu;


namespace ZXMAK2.Hardware.Evo
{
    public class MemoryPentEvo : MemoryBase, ICpuRefreshClock
    {
        #region Fields

        private readonly int[] m_ru2 = new int[8]; // ATM 7.10 / ATM3(4Mb) memory map
        private byte m_writeDisable; // bits 0..3: map 0; bits 4..7: map 1

        protected CpuUnit m_cpu;
        protected byte[][] m_romPages;
        private UlaAtm450 m_ulaAtm;
        private bool m_lock;

        private int m_aFF77;
        private int m_pFF77;

        private byte m_pXXBF;   // port EVO EVO
        private byte m_pEFF7;   // port EVO MOD
        private ushort m_breakpointAddress;

        // BaseConf znmi.v / zbreak.v state.  A software/manual request is
        // deferred to int_start; a breakpoint request starts immediately.
        // The first opcode at #0066 is forced to NOP, then RAM page #FF is
        // mapped into window 0 until the second M1 after OUT (#BE),A.
        private bool m_nmiPending;
        private bool m_externalNmiPending;
        private bool m_nmiSignal;
        private bool m_nmiEntryActive;
        private bool m_inNmi;
        private int m_nmiClearM1Remaining;
        private bool m_nmiSwitchAfterM1;
        private bool m_nmiClearAfterM1;

        // PentEvo can redirect a TR-DOS FDC access to a small handler stored
        // in RAM page #FE.  ERS uses this mechanism for mounted SCL/TRD images
        // and for its RAM disk.
        private const int FddIoRamPage = 0xFE;
        private bool m_fddIoRamActive;
        private bool m_fddIoWriteDisabled;
        private int m_fddIoTraceM1Budget;

        private int m_romMask;
        private int m_ramMask;
        
        #endregion Fields


        public MemoryPentEvo(
            string romSetName,
            int romPageCount,
            int ramPageCount)
            : base(romSetName, romPageCount, ramPageCount)
        {
            Name = "PentEvo";
            Description = "PentEvo 4096K Memory Manager";
        }

        public MemoryPentEvo()
            : this("PentEvo", 32, 256)
        {
        }


        #region IBusDevice

        public override void BusInit(IBusManager bmgr)
        {
            m_cpu = bmgr.CPU;
            m_ulaAtm = bmgr.FindDevice<UlaAtm450>();

            OnSubscribeIo(bmgr);

            bmgr.Events.SubscribeRdMemM1(0x0000, 0x0000, BusReadM1);
            bmgr.Events.SubscribeWrMem(0x0000, 0x0000, WriteMemXXXX);
            bmgr.Events.SubscribeReset(BusReset);

            // Subscribe before MemoryBase.BusInit 
            // to handle memory switches before read
            base.BusInit(bmgr);

            // This handler must run after MemoryBase has supplied the opcode:
            // at #0066 it overrides that byte with NOP and only then changes
            // the mapping for the following opcode at #0067.
            bmgr.Events.SubscribeRdMemM1(0x0000, 0x0000, BusReadM1AfterMemory);
            bmgr.Events.SubscribeBeginFrame(BusBeginFrame);
            bmgr.Events.SubscribePreCycle(BusPreCycle);
            bmgr.Events.SubscribeNmiRq(BusNmiRequest);
            bmgr.Events.SubscribeNmiAck(BusNmiAcknowledge);
        }

        protected virtual void OnSubscribeIo(IBusManager bmgr)
        {
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00FF & 0x00FF, BusWritePortXXFF_PAL);	// atm_writepal(val);

            bmgr.Events.SubscribeWrIo(0x00FF, 0xFF77 & 0x00FF, BusWritePortXX77_SYS);
            // PentEvo has two memory-manager port families.  #x7F7 is
            // decoded with A13..A0, while #xFF7 is decoded by its low
            // 12 bits.  The latter intentionally includes #EFF7 while
            // shadow/DOS ports are enabled, as on the real hardware.
            bmgr.Events.SubscribeWrIo(0x3FFF, 0x37F7, BusWritePortXFF7_WND);
            bmgr.Events.SubscribeWrIo(0x0FFF, 0x0FF7, BusWritePortXFF7_WND);
            // BaseConf: low byte F7, A8=1, A11:A10=10; A15:A14 select window.
            bmgr.Events.SubscribeWrIo(0x0DFF, 0x09F7, BusWritePortXBF7_WPROT);
            // PentEvo decodes #7FFD by A15=0 and the low byte being #FD or #FC.
            // A broader Spectrum-style mask also catches unrelated ports such as #13BD.
            bmgr.Events.SubscribeWrIo(0x80FF, 0x00FD, BusWritePort7FFD_128);
            bmgr.Events.SubscribeWrIo(0x80FF, 0x00FC, BusWritePort7FFD_128);
            // BaseConf zports.v: low byte F7, A8=1, A12=0, outside DOS/Shadow.
            // Shadow gating stays in the handler; pager subscriptions are separate.
            bmgr.Events.SubscribeWrIo(0x11FF, 0x01F7, BusWritePortEFF7_MOD);

            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BF & 0x00FF, BusWritePortXXBF_EVO);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00BF & 0x00FF, BusReadPortXXBF_EVO);

            // ERS 0.59.x reads the configuration registers through #xxBD,
            // while earlier firmware uses #xxBE.  Keep both read aliases
            // so ROMs from either firmware family can restore their map.
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00BD, BusReadPortXXBD_BE_CFG);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00BE, BusReadPortXXBD_BE_CFG);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BD, BusWritePortXXBD_CFG);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BE, BusWritePortXXBE_FDD_EXIT);
        }

        protected virtual void WriteMemXXXX(ushort addr, byte value)
        {
            if (m_ulaAtm != null && FNTWR)
            {
                m_ulaAtm.WriteSgen(addr, value);
            }
        }

        protected virtual void BusReadM1(ushort addr, ref byte value)
        {
            // zbreak.v compares every opcode-fetch address, independently of
            // the currently selected ROM/RAM page.  The NMI is sampled by the
            // CPU at the following instruction boundary.
            if (BreakpointEnabled && addr == m_breakpointAddress &&
                !m_inNmi && !m_nmiEntryActive)
            {
                StartNmi();
            }

            // While the FDD I/O handler is mapped at #0000, its instruction
            // fetches must not change DOSEN or replace page #FE.
            if (m_fddIoRamActive)
            {
                // BaseConf arms trdemu_wr_disable when the I/O cycle enters
                // page #FE and releases it on the first following M1 cycle.
                // This prevents the trailing memory write of INI/INIR from
                // corrupting the handler before its first opcode is fetched.
                if (m_fddIoWriteDisabled)
                {
                    m_fddIoWriteDisabled = false;
                    UpdateMapping();
                }
                if (FddIoTrace && m_fddIoTraceM1Budget-- > 0)
                {
                    Logger.Debug(
                        "EVO-FDD M1 page=FE addr=#{0:X4} op=#{1:X2} PC=#{2:X4} T={3}",
                        addr,
                        MapRead0000[addr],
                        m_cpu.regs.PC,
                        m_cpu.Tact);
                }
                return;
            }

            //LogAgent.Info(
            //    "{0:D3}-{1:D6}: #{2:X4} = #{3:X2}",
            //    m_cpu.Tact / m_ula.FrameTactCount,
            //    m_cpu.Tact % m_ula.FrameTactCount,
            //    addr,
            //    map[addr >> 14][addr & 0x3FFF]);
            var index = (addr >> 14) + ((CMR0 & 0x10) >> 2);
            var w = m_ru2[index] ^ 0x3FF;
            var isRam = (w & 0x40) == 0;
            if (isRam)
            {
                DOSEN = CPM;
            }
            else if (index != 0 && (addr & 0x3F00) == 0x3D00) //ROM2 & RAM & dosgate
            {
                DOSEN = true;
            }
        }

        private void BusReadM1AfterMemory(ushort addr, ref byte value)
        {
            // znmi.v drive_00: the first handler opcode is always NOP.  The
            // page switch happens after this M1, so #0067 and later bytes are
            // fetched from RAM #FF exactly as documented.
            if (m_nmiEntryActive && addr == 0x0066)
            {
                value = 0x00;
                m_nmiSwitchAfterM1 = true;
                return;
            }

            // OUT (#BE),A keeps page #FF through two subsequent M1 fetches.
            // Change the mapping only after the second opcode byte has been
            // read, matching the refresh-edge counter in znmi.v.
            if (m_inNmi && m_nmiClearM1Remaining > 0)
            {
                m_nmiClearM1Remaining--;
                if (m_nmiClearM1Remaining == 0)
                {
                    m_nmiClearAfterM1 = true;
                }
            }
        }

        private void BusBeginFrame()
        {
            // The BaseConf frame epoch is int_start.  Requests from BF.D3 and
            // the board/manual NMI source are consumed at this boundary.
            if (m_nmiPending)
            {
                m_nmiPending = false;
                StartNmi();
            }
        }

        private void BusPreCycle()
        {
            if (m_nmiSignal)
                m_cpu.NMI = true;
        }

        private void BusNmiRequest(BusCancelArgs e)
        {
            // Route the emulator's NMI command through the same deferred
            // BaseConf path as the AVR request.  Once int_start has armed the
            // hardware signal, let EventManager deliver the request too.
            if (m_nmiSignal)
            {
                m_externalNmiPending = false;
                return;
            }
            e.Cancel = true;
            if (m_inNmi || m_nmiEntryActive)
                return;
            if (!m_externalNmiPending)
            {
                m_externalNmiPending = true;
                m_nmiPending = true;
            }
        }

        private void BusNmiAcknowledge()
        {
            m_nmiSignal = false;
        }

        private void StartNmi()
        {
            if (m_inNmi || m_nmiEntryActive)
                return;
            m_nmiEntryActive = true;
            m_nmiSignal = true;
        }

        #endregion

        #region MemoryBase

        public override bool IsMap48 { get { return false; } }

        public override int GetRomIndex(RomId romId)
        {
            switch (romId)
            {
                case RomId.ROM_SOS: return 0x1C;
                case RomId.ROM_DOS: return 0x1D;
                case RomId.ROM_128: return 0x1E;
                case RomId.ROM_SYS: return 0x1F;
            }
            throw new NotImplementedException();
        }

        protected override void UpdateMapping()
        {
            // PentEvo uses bit 5 as an extended RAM page bit while EFF7.2 is clear.
            // The classic Spectrum 128 paging lock applies only in ZX128 mode.
            m_lock = ZX128 && (CMR0 & 0x20) != 0;
            if (PEN)
            {
                int videoPage = (CMR0 & 0x08) == 0 ? 5 : 7;
                if (m_ulaAtm != null)
                {
                    m_ulaAtm.SetPageMappingAtm(
                        VIDEO, 
                        videoPage, 
                        -1, 
                        -1, 
                        -1, 
                        -1);
                }
                else
                {
                    m_ula.SetPageMapping(
                        videoPage, -1, -1, -1, -1);
                }
                int romPage = RomPages.Length - 1;
                MapRead0000 = RomPages[romPage];
                MapRead4000 = RomPages[romPage];
                MapRead8000 = RomPages[romPage];
                MapReadC000 = RomPages[romPage];

                MapWrite0000 = ROMRW ? MapRead0000 : m_trashPage;
                MapWrite4000 = ROMRW ? MapRead4000 : m_trashPage;
                MapWrite8000 = ROMRW ? MapRead8000 : m_trashPage;
                MapWriteC000 = ROMRW ? MapReadC000 : m_trashPage;

                Map48[0] = -1;
                Map48[1] = -1;
                Map48[2] = -1;
                Map48[3] = -1;
            }
            else
            {
                int videoPage = (CMR0 & 0x08) == 0 ? 5 : 7;

                var index = (CMR0 & 0x10) >> 2;

                // high 2 bits of ram page stored 
                // in the high byte of m_ru2[wnd]
                var w0 = m_ru2[index + 0] ^ 0x3FF;
                var w1 = m_ru2[index + 1] ^ 0x3FF;
                var w2 = m_ru2[index + 2] ^ 0x3FF;
                var w3 = m_ru2[index + 3] ^ 0x3FF;
                var isRam0 = (w0 & 0x40) == 0;
                var isRam1 = (w1 & 0x40) == 0;
                var isRam2 = (w2 & 0x40) == 0;
                var isRam3 = (w3 & 0x40) == 0;
                var isDos0 = (w0 & 0x80) == 0;
                var isDos1 = (w1 & 0x80) == 0;
                var isDos2 = (w2 & 0x80) == 0;
                var isDos3 = (w3 & 0x80) == 0;
                var kpa0 = CMR0 & 7;
                var kpa0mask = 0x38;
                if (!ZX128)
                {
                    var sega = (CMR0 & 0xE0) >> 5; // BaseConf: D7,D6,D5,D2,D1,D0
                    kpa0 |= sega << 3;
                    //kpa0 = (byte)((CMR0 & 0xE0) >> 2 | (CMR0 & 0x7));
                    kpa0mask = 0xC0;
                }
                var kpa8 = DOSEN ? 1 : 0;
                var romPage0 = (isDos0 ? kpa8 | (w0 & 0x3E) : w0 & 0x3F) & m_romMask;
                var romPage1 = (isDos1 ? kpa8 | (w1 & 0x3E) : w1 & 0x3F) & m_romMask;
                var romPage2 = (isDos2 ? kpa8 | (w2 & 0x3E) : w2 & 0x3F) & m_romMask;
                var romPage3 = (isDos3 ? kpa8 | (w3 & 0x3E) : w3 & 0x3F) & m_romMask;
                var ramPage0 = ((isDos0 ? (w0 & kpa0mask) | kpa0 : (w0 & 0x3F)) | ((w0 >> 2) & 0xC0)) & m_ramMask;
                var ramPage1 = ((isDos1 ? (w1 & kpa0mask) | kpa0 : (w1 & 0x3F)) | ((w1 >> 2) & 0xC0)) & m_ramMask;
                var ramPage2 = ((isDos2 ? (w2 & kpa0mask) | kpa0 : (w2 & 0x3F)) | ((w2 >> 2) & 0xC0)) & m_ramMask;
                var ramPage3 = ((isDos3 ? (w3 & kpa0mask) | kpa0 : (w3 & 0x3F)) | ((w3 >> 2) & 0xC0)) & m_ramMask;

                if (W0RAM0)
                {
                    isRam0 = true;
                    ramPage0 = 0;
                }

                if (m_ulaAtm != null)
                {
                    m_ulaAtm.SetPageMappingAtm(
                        VIDEO,
                        videoPage,
                        isRam0 ? ramPage0 : -1,
                        isRam1 ? ramPage1 : -1,
                        isRam2 ? ramPage2 : -1,
                        isRam3 ? ramPage3 : -1);
                }
                else
                {
                    m_ula.SetPageMapping(
                        videoPage,
                        isRam0 ? ramPage0 : -1,
                        isRam1 ? ramPage1 : -1,
                        isRam2 ? ramPage2 : -1,
                        isRam3 ? ramPage3 : -1);
                }

                MapRead0000 = isRam0 ? RamPages[ramPage0] : RomPages[romPage0];
                MapRead4000 = isRam1 ? RamPages[ramPage1] : RomPages[romPage1];
                MapRead8000 = isRam2 ? RamPages[ramPage2] : RomPages[romPage2];
                MapReadC000 = isRam3 ? RamPages[ramPage3] : RomPages[romPage3];

                MapWrite0000 = ((isRam0 && W0RAM0) ||
                    ((isRam0 || ROMRW) && ((m_writeDisable >> index) & 1) == 0)) ?
                    MapRead0000 : m_trashPage;
                MapWrite4000 = (isRam1 || ROMRW) && ((m_writeDisable >> index) & 2) == 0 ? MapRead4000 : m_trashPage;
                MapWrite8000 = (isRam2 || ROMRW) && ((m_writeDisable >> index) & 4) == 0 ? MapRead8000 : m_trashPage;
                MapWriteC000 = (isRam3 || ROMRW) && ((m_writeDisable >> index) & 8) == 0 ? MapReadC000 : m_trashPage;

                Map48[0] = isRam0 ? -1 : romPage0;
                Map48[1] = isRam1 ? ramPage1 : -1;
                Map48[2] = isRam2 ? ramPage2 : -1;
                Map48[3] = isRam3 ? ramPage3 : -1;
            }

            if (m_fddIoRamActive)
            {
                MapRead0000 = RamPages[FddIoRamPage];
                MapWrite0000 = m_fddIoWriteDisabled ? m_trashPage : MapRead0000;
                Map48[0] = -1;
            }

            // base_trdemu atm_pager.v gives in_nmi priority over in_trdemu:
            // page #FF temporarily replaces page #FE.  pager_off (PEN) keeps
            // its documented all-ROM mapping and therefore stays dominant.
            if (m_inNmi && !PEN)
            {
                MapRead0000 = RamPages[0xFF];
                MapWrite0000 = MapRead0000;
                Map48[0] = -1;
            }
        }

        /// <summary>
        /// Temporarily maps the PentEvo FDD I/O handler over the DOS ROM.
        /// The CPU continues at the instruction following IN/OUT, now in the
        /// handler page, exactly as on the FPGA and in UnrealSpeccy.
        /// </summary>
        public bool TryEnterFddIoRam()
        {
            // base_trdemu/zdos.v: trdemu_on requires
            // dos && romnram && !atm_pen2.  In ATM palette mode a shadow
            // #FF access must not replace window #0000 with RAM page #FE.
            if (m_fddIoRamActive ||
                !DOSEN ||
                PEN2 ||
                Array.IndexOf(RomPages, MapRead0000) < 0)
            {
                return false;
            }

            m_fddIoRamActive = true;
            m_fddIoWriteDisabled = true;
            m_fddIoTraceM1Budget = FddIoTrace ? 16 : 0;
            UpdateMapping();
            return true;
        }

        public bool FddIoRamActive
        {
            get { return m_fddIoRamActive; }
        }

        public bool IsDosRomMappedAt0000
        {
            get
            {
                return DOSEN && Array.IndexOf(RomPages, MapRead0000) >= 0;
            }
        }

        public bool FddIoTrace { get; set; }

        private void ExitFddIoRam()
        {
            if (!m_fddIoRamActive)
            {
                return;
            }

            m_fddIoRamActive = false;
            m_fddIoWriteDisabled = false;
            m_fddIoTraceM1Budget = 0;
            UpdateMapping();
        }

        protected override void Init(int romPageCount, int ramPageCount)
        {
            base.Init(romPageCount, ramPageCount);
            m_romMask = RomPages.Length - 1;
            if (m_romMask > 0x1F)
            {
                m_romMask = 0x1F;
            }
            m_ramMask = RamPages.Length - 1;
            if (m_ramMask > 0xFF)
            {
                m_ramMask = 0xFF;
            }
        }

        #endregion

        // BaseConf dram/arbiter.v: cend decisions, eight-cycle blocks.
        private sealed class EvoDramArbiter
        {
            private int blockRemaining, videoRemaining;
            private bool stallBlock;
            public bool CanUseCpu(bool go, int bandwidth)
            {
                return blockRemaining == 0 ? !go || bandwidth != 3 :
                    !stallBlock && videoRemaining != blockRemaining;
            }
            // 0=VIDEO, 1=CPU, 2=FREE, exactly as in RTL.
            public int Step(bool go, int bandwidth, bool cpuRequest)
            {
                int next = blockRemaining == 0 ?
                    (!go ? (cpuRequest ? 1 : 2) : bandwidth == 3 ? 0 : cpuRequest ? 1 : 0) :
                    stallBlock || videoRemaining == blockRemaining ? 0 :
                    cpuRequest ? 1 : videoRemaining != 0 ? 0 : 2;
                int nextVideo = videoRemaining;
                if (go && blockRemaining == 0)
                {
                    int max = (1 << bandwidth) & 7;
                    nextVideo = cpuRequest ? max : (max - 1) & 7;
                }
                else if (next == 0 && nextVideo != 0)
                    nextVideo--;
                if (blockRemaining == 0)
                    stallBlock = go && bandwidth == 3;
                blockRemaining = blockRemaining == 0 ? (go ? 7 : 0) : blockRemaining - 1;
                videoRemaining = nextVideo;
                return next;
            }
        }

        private EvoDramArbiter m_dramArbiter;
        private long m_dramNextCycle, m_dramLastTact = -1;
        private int m_dramProfileMode, m_dramProfilePent;
        private EvoRasterTiming m_dramRaster;
        private long m_dramRasterOrigin;

        internal void ActivateRaster(EvoRasterTiming timing, long masterOrigin)
        {
            if (timing == null || masterOrigin < 0 || (masterOrigin & 3) != 0)
                throw new ArgumentException("Invalid BaseConf raster epoch.");
            // Settle the old profile up to the boundary where possible. Keep
            // in-flight block quota and the monotonically advancing DRAM cursor.
            // Instruction-overrun edges are not an exact FPGA transition model.
            if (masterOrigin / 4 < m_dramRasterOrigin)
            {
                m_dramArbiter = null;
                m_dramLastTact = -1;
                m_dramNextCycle = 0;
                InvalidateMemoryBuffer();
            }
            else if (m_dramArbiter != null)
                AdvanceDramIdle(masterOrigin);
            m_dramRaster = timing;
            m_dramRasterOrigin = masterOrigin / 4;
        }

        private void SetDramProfile()
        {
            m_dramProfileMode = m_pFF77 & 7;
            // top.v: pent_vmode = {peff7[0],peff7[5]}.
            m_dramProfilePent = ((m_pEFF7 & 1) << 1) | ((m_pEFF7 >> 5) & 1);
        }
        private void GetVideoFetch(long cycle, out bool go, out int bandwidth)
        {
            if (cycle < 0) throw new ArgumentOutOfRangeException("cycle");
            var raster = m_dramRaster ?? EvoRasterTiming.Normal;
            long position = (cycle - m_dramRasterOrigin) % raster.FrameCycles;
            if (position < 0) position += raster.FrameCycles;
            raster.GetVideoFetch(position, m_dramProfileMode,
                m_dramProfilePent, out go, out bandwidth);
        }
        private void AdvanceDramIdle(long masterTact)
        {
            while (m_dramNextCycle * 4 + 3 < masterTact)
            {
                bool go; int bandwidth;
                GetVideoFetch(m_dramNextCycle, out go, out bandwidth);
                m_dramArbiter.Step(go, bandwidth, false);
                m_dramNextCycle++;
            }
        }
        private void PrepareDram(long masterTact)
        {
            if (masterTact < 0)
                throw new InvalidOperationException("Negative BaseConf master timestamp.");
            if (m_dramArbiter == null || masterTact < m_dramLastTact)
            {
                m_dramArbiter = new EvoDramArbiter();
                // Replay more than one line to reconstruct idle block
                // history on reset/snapshot or a backwards timestamp.
                m_dramNextCycle = Math.Max(0L, masterTact / 4 - 512);
                SetDramProfile();
                InvalidateMemoryBuffer();
            }
            AdvanceDramIdle(masterTact);
            SetDramProfile();
            m_dramLastTact = masterTact;
        }
        private int ReserveDram(long masterTact, CpuMemoryAccess access)
        {
            bool fast = CpuClockMultiplier == 4;
            long begin = masterTact + (fast ? 1 : 0), resumed = begin;
            AdvanceDramIdle(begin);
            bool denied = false;
            for (;;)
            {
                bool go; int bandwidth;
                GetVideoFetch(m_dramNextCycle, out go, out bandwidth);
                bool available = m_dramArbiter.CanUseCpu(go, bandwidth);
                long cycleBegin = m_dramNextCycle * 4;
                m_dramArbiter.Step(go, bandwidth, true);
                m_dramNextCycle++;
                if (available)
                {
                    if (denied) resumed = cycleBegin;
                    break;
                }
                denied = true;
            }
            int delay = checked((int)(resumed - begin));
            if (fast && access != CpuMemoryAccess.Write)
                delay += (access == CpuMemoryAccess.Opcode ? 6 : 5) - (int)(resumed & 3);
            // Master units; no second scaling by BusManager.
            return delay;
        }

        private bool m_dramBufferValid;
        private ushort m_dramBufferAddress;
        private ushort m_dramBufferWord;

        public int GetPortWait(ushort address)
        {
            // zports.v external_port: exact low FD with A15=1 (AY), or
            // low 1F/3F/5F/7F while dos || shadow_en_reg (VG93).
            if (CpuClockMultiplier != 4)
                return 0;
            int low = address & 255;
            bool external = (low == 0xFD && (address & 0x8000) != 0) ||
                ((DOSEN || SHADOW) &&
                (low == 0x1F || low == 0x3F || low == 0x5F || low == 0x7F));
            // zclock.v io_wait_cnt 8..F: 1,1,1,1,1,0,1,0.
            // Six stopped FPGA clock phases, aggregated in master units.
            // RD/WR are equivalent; interrupt acknowledgment uses another
            // callback and never reaches this ordinary-IO method.
            // Exact placement of the separated stalls at Z80 pin edges is
            // a later stage; this implements their total wait budget.
            return external ? 6 : 0;
        }

        public void InvalidateMemoryBuffer()
        {
            m_dramBufferValid = false;
        }

        public int CompleteMemoryAccess(ushort address, CpuMemoryAccess access,
            long masterTact, ref byte value)
        {
            PrepareDram(masterTact);
            var window = address >> 14;
            var read = window == 0 ? MapRead0000 : window == 1 ? MapRead4000 :
                window == 2 ? MapRead8000 : MapReadC000;
            // Use the final mapping, after the memory/M1 handlers ran.
            // ROM does not use the DRAM arbiter, and invalidates the buffer.
            if (Array.IndexOf(RomPages, read) >= 0)
            {
                InvalidateMemoryBuffer();
                return CompleteNmiM1(address, access, 0, ref value);
            }

            if (access == CpuMemoryAccess.Write)
            {
                var write = window == 0 ? MapWrite0000 : window == 1 ? MapWrite4000 :
                    window == 2 ? MapWrite8000 : MapWriteC000;
                // RTL memwr is gated by wrdisable. A protected write hit
                // retains the buffer. A miss still raises dram_beg with
                // cpu_rnw=1: model its read refill in the free-DRAM baseline.
                int writeDelay = 0;
                if (ReferenceEquals(write, read))
                {
                    writeDelay = ReserveDram(masterTact, CpuMemoryAccess.Write);
                    InvalidateMemoryBuffer();
                }
                else if (CpuClockMultiplier == 4 &&
                    (!m_dramBufferValid || m_dramBufferAddress != (address & 0xFFFE)))
                {
                    writeDelay = ReserveDram(masterTact, CpuMemoryAccess.Write);
                    var refill = address & 0x3FFE;
                    m_dramBufferWord = (ushort)((read[refill] << 8) | read[refill + 1]);
                    m_dramBufferAddress = (ushort)(address & 0xFFFE);
                    m_dramBufferValid = true;
                }
                return CompleteNmiM1(address, access, writeDelay, ref value);
            }

            var wordAddress = (ushort)(address & 0xFFFE);
            var fast = CpuClockMultiplier == 4;
            if (fast && m_dramBufferValid && m_dramBufferAddress == wordAddress)
            {
                value = (byte)((address & 1) == 0 ?
                    m_dramBufferWord >> 8 : m_dramBufferWord & 255);
                return CompleteNmiM1(address, access, 0, ref value);
            }

            int readDelay = ReserveDram(masterTact, access);
            var offset = address & 0x3FFE;
            m_dramBufferWord = (ushort)((read[offset] << 8) | read[offset + 1]);
            m_dramBufferAddress = wordAddress;
            m_dramBufferValid = true;
            value = (byte)((address & 1) == 0 ?
                m_dramBufferWord >> 8 : m_dramBufferWord & 255);
            return CompleteNmiM1(address, access, readDelay, ref value);
        }

        private int CompleteNmiM1(ushort address, CpuMemoryAccess access,
            int delay, ref byte value)
        {
            if (access != CpuMemoryAccess.Opcode)
                return delay;

            if (m_nmiSwitchAfterM1 && address == 0x0066)
            {
                // Complete the current read against its old mapping, then
                // override the bus with NOP and expose page #FF for #0067.
                value = 0x00;
                m_nmiSwitchAfterM1 = false;
                m_nmiEntryActive = false;
                m_inNmi = true;
                m_nmiClearM1Remaining = 0;
                InvalidateMemoryBuffer();
                UpdateMapping();
            }

            if (m_nmiClearAfterM1)
            {
                // The second post-#BE opcode has already been read from #FF.
                // Restore the underlying page before the remainder of that
                // instruction, exactly at the following refresh boundary.
                m_nmiClearAfterM1 = false;
                m_inNmi = false;
                InvalidateMemoryBuffer();
                UpdateMapping();
            }
            return delay;
        }

        #region Hardware Values

        private int m_activeCpuClockMultiplier = 2;

        public int CpuClockMultiplier
        {
            // zclock.v int_turbo: applied mode, separate from port request.
            get { return m_activeCpuClockMultiplier; }
        }

        public int RequestedCpuClockMultiplier
        {
            get
            {
                if ((m_pFF77 & 0x08) != 0)
                    return 4;
                return (m_pEFF7 & 0x10) != 0 ? 1 : 2;
            }
        }

        public void LatchClockAtRefresh()
        {
            // zclock.v: int_turbo <= turbo on sampled /RFSH assertion.
            // Memory and IO waits must consult this applied mode too.
            m_activeCpuClockMultiplier = RequestedCpuClockMultiplier;
        }

        public int MaxCpuClockMultiplier
        {
            // BaseConf FPGA master clock: 28 MHz = 8 * 3.5 MHz.
            // CPU rate selection remains in CpuClockMultiplier.
            get { return 8; }
        }

        [HardwareValue("PEN", Description = "Disable memory manager")]
        public bool PEN
        {
            get { return (m_aFF77 & 0x100) == 0; }
            set { m_aFF77 = (m_aFF77 & ~0x100) | (value ? 0x0000 : 0x0100); UpdateMapping(); }
        }

        [HardwareValue("CPM", Description = "Enable continous access for extended ports and TRDOS ROM")]
        public bool CPM
        {
            get { return (m_aFF77 & 0x200) == 0; }
            set { m_aFF77 = (m_aFF77 & ~0x200) | (value ? 0x0000 : 0x0200); if (value) DOSEN = true; UpdateMapping(); }
        }

        [HardwareValue("PEN2", Description = "Enable palette change through port #FF")]
        public bool PEN2
        {
            get { return (m_aFF77 & 0x4000) == 0; }
            set { m_aFF77 = (m_aFF77 & ~0x4000) | (value ? 0x0000 : 0x4000); UpdateMapping(); }
        }

        [HardwareValue("RG", Description = "Low bits of video mode")]
        public int RG
        {
            get { return m_pFF77 & 7; }
            set { m_pFF77 = (m_pFF77 & 0xF8) | (value & 7); UpdateMapping(); }
        }

        [HardwareValue("Z_I", Description = "Enable HSYNC interrupts")]
        public bool Z_I
        {
            get { return (m_pFF77 & 0x20) == 0; }
            set { m_pFF77 = (m_pFF77 & ~0x20) | (value ? 0x20 : 0x00); UpdateMapping(); }
        }

        [HardwareValue("SHADOW", Description = "Enable shadow ports")]
        public bool SHADOW
        {
            get { return (m_pXXBF & 1) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~1) | (value ? 1 : 0)); }
        }

        [HardwareValue("FNTWR", Description = "Allow write to font RAM")]
        public bool FNTWR
        {
            get { return (m_pXXBF & 4) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~4) | (value ? 4 : 0)); }
        }

        [HardwareValue("ROMRW", Description = "Allow writes to mapped ROM pages through BF.D1")]
        public bool ROMRW
        {
            get { return (m_pXXBF & 2) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~2) | (value ? 2 : 0)); UpdateMapping(); }
        }

        [HardwareValue("NMIREQ", Description = "Latched BF.D3 frame-aligned NMI request source")]
        public bool NMIREQ
        {
            get { return (m_pXXBF & 8) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~8) | (value ? 8 : 0)); }
        }

        [HardwareValue("BRKENA", Description = "Latched BF.D4 immediate breakpoint-NMI enable")]
        public bool BreakpointEnabled
        {
            get { return (m_pXXBF & 0x10) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~0x10) | (value ? 0x10 : 0)); }
        }

        [HardwareValue("PAL444", Description = "Latched BF.D5 palette mode; renderer activation pending B38")]
        public bool Palette444Enabled
        {
            get { return (m_pXXBF & 0x20) != 0; }
            set { m_pXXBF = (byte)((m_pXXBF & ~0x20) | (value ? 0x20 : 0)); }
        }

        [HardwareValue("BRKADDR", Description = "BaseConf breakpoint address latch")]
        public ushort BreakpointAddress
        {
            get { return m_breakpointAddress; }
            set { m_breakpointAddress = value; }
        }

        [HardwareReadOnly(true)]
        [HardwareValue("NMIPEND", Description = "Frame-aligned BaseConf NMI request pending")]
        public bool NmiPending { get { return m_nmiPending; } }

        [HardwareReadOnly(true)]
        [HardwareValue("NMIENTRY", Description = "NMI acknowledged; waiting for the #0066 M1 transition")]
        public bool NmiEntryActive { get { return m_nmiEntryActive; } }

        [HardwareReadOnly(true)]
        [HardwareValue("INNMI", Description = "RAM page #FF is selected in window #0000")]
        public bool InNmi { get { return m_inNmi; } }

        [HardwareReadOnly(true)]
        [HardwareValue("NMICLR", Description = "M1 fetches remaining before delayed #BE exit")]
        public int NmiClearM1Remaining { get { return m_nmiClearM1Remaining; } }

        [HardwareValue("CMOSEN", Description = "Enable CMOS ports shadow independent")]
        public bool CMOSEN
        {
            get { return (m_pEFF7 & 0x80) != 0; }
            set { m_pEFF7 = (byte)((m_pEFF7 & 0x7F) | (value ? 0x80 : 0)); }
        }

        [HardwareValue("W0RAM0", Description = "Map RAM#00 to window #0000..#3FFF (max priority)")]
        public bool W0RAM0
        {
            get { return (m_pEFF7 & 0x08) != 0; }
            set { m_pEFF7 = (byte)((m_pEFF7 & 0xF7) | (value ? 0x08 : 0)); }
        }

        [HardwareValue("ZX128", Description = "Enable ZX Spectrum 128 compatible #7FFD port (otherwise Pentagon 1024)")]
        public bool ZX128
        {
            get { return (m_pEFF7 & 0x04) != 0; }
            set { m_pEFF7 = (byte)((m_pEFF7 & 0xFB) | (value ? 0x04 : 0)); }
        }

        [HardwareValue("TURBOOFF", Description = "EFF7.D4: request 3.5 MHz; xx77.D3 has priority")]
        public bool TURBO
        {
            get { return (m_pEFF7 & 0x10) != 0; }
            set { m_pEFF7 = (byte)((m_pEFF7 & 0xEF) | (value ? 0x10 : 0)); }
        }

        [HardwareValue("RGEX", Description = "High bits of video mode (when RG=3)")]
        public int RGEX
        {
            get { return (m_pEFF7 & 0x01) | ((m_pEFF7 & 0x20) >> 4); }
            set { m_pEFF7 = (byte)((m_pEFF7 & 0xDE) | (value & 0x01) | ((value & 2) << 4)); }
        }

        [HardwareValue("VIDEO", Description = "Video Mode")]
        public AtmVideoMode VIDEO
        {
            get { return (AtmVideoMode)(RG | (RGEX << 3)); }
            set { RG = (int)value & 7; RGEX = (int)value >> 3; }
        }

        [HardwareReadOnly(true)]
        [HardwareValue("RU2", Description = "RU2 RAM memory manager content")]
        public int[] RU2
        {
            get { return m_ru2; }
        }

        [HardwareValue("DOSEN", Description = "TRDOS flag")]
        public override bool DOSEN
        {
            get { return base.DOSEN; }
            set { base.DOSEN = value | CPM; }
        }

        public override bool SYSEN
        {
            get { return SHADOW; }
            set { SHADOW = value; }
        }

        #endregion

        #region Bus Handlers

        protected virtual void BusWritePortXXFF_PAL(ushort addr, byte value, ref bool handled)
        {
            if ((DOSEN || SHADOW) && PEN2 && m_ulaAtm != null)
            {
                m_ulaAtm.SetPaletteAtm2(value);
            }
        }

        protected virtual void BusWritePortXX77_SYS(ushort addr, byte value, ref bool handled) // ATM2
        {
            if (DOSEN || SHADOW)
            {
                // Advance old profile to the IO timestamp, retaining
                // current block quotas across this mode change.
                if (m_cpu != null)
                    PrepareDram(m_cpu.Tact);
                m_pFF77 = value;
                SetDramProfile();
                m_aFF77 = addr;
                if (CPM) DOSEN = true;
                UpdateMapping();
                //cpu.int_gate = (comp.pFF77 & 0x20) != false;
                //set_banks();
            }
        }

        protected virtual void BusWritePortXFF7_WND(ushort addr, byte value, ref bool handled) // ATM2
        {
            if (DOSEN || SHADOW)
            {
                var wnd = ((CMR0 & 0x10) >> 2) | ((addr >> 14) & 3);
                if ((addr & 0x0800) != 0)
                {
                    m_ru2[wnd] = value | 0x0300;
                }
                else
                {
                    // store high 2 bits in the high byte of m_ru2[wnd]
                    m_ru2[wnd] &= 0x80; // save dos
                    m_ru2[wnd] |= 0x40; // always ram
                    m_ru2[wnd] |= (value & 0x3F) | ((value << 2) & 0x300);
                }
                UpdateMapping();
            }
        }

        protected virtual void BusWritePortXBF7_WPROT(ushort addr, byte value, ref bool handled)
        {
            if (!(DOSEN || SHADOW))
                return;
            var wnd = ((CMR0 & 0x10) >> 2) | ((addr >> 14) & 3);
            var mask = 1 << wnd;
            m_writeDisable = (byte)((m_writeDisable & ~mask) | ((value & 1) << wnd));
            UpdateMapping();
        }

        protected virtual void BusWritePort7FFD_128(ushort addr, byte value, ref bool handled)
        {
            if (m_lock)
            {
                return;
            }
            CMR0 = value;
        }

        protected virtual void BusWritePortXXBF_EVO(ushort addr, byte value, ref bool handled)
        {
            // znmi.v detects the falling edge of set_nmi.  Merely setting D3
            // arms the latch; clearing it requests NMI at the next int_start.
            var requestNmi = (m_pXXBF & 0x08) != 0 && (value & 0x08) == 0;
            m_pXXBF = value;
            if (requestNmi)
                m_nmiPending = true;
            UpdateMapping();
        }

        protected virtual void BusReadPortXXBF_EVO(ushort addr, ref byte value, ref bool handled)
        {
            if (handled)
            {
                return;
            }
            handled = true;
            // base_trdemu zports.v: D7:D6 read as zero; D5:D0 are the
            // complete configuration latch.  NMI/breakpoint/4:4:4 effects
            // are activated by their dedicated later stages.
            value = (byte)(m_pXXBF & 0x3F);
        }

        protected virtual void BusWritePortEFF7_MOD(ushort addr, byte value, ref bool handled)
        {
            // The EFF7 latch is writable only outside DOS/Shadow.
            // Other F7 handlers keep their own independent decoding.
            if (DOSEN || SHADOW)
            {
                return;
            }
            // Advance old profile to the IO timestamp, retaining
            // current block quotas across this mode change.
            if (m_cpu != null)
                PrepareDram(m_cpu.Tact);
            m_pEFF7 = value;
            SetDramProfile();
            UpdateMapping();
        }

        protected virtual void BusReadPortXXBD_BE_CFG(ushort addr, ref byte value, ref bool handled)
        {
            if (handled)
            {
                return;
            }
            var index = (addr >> 8) & 0x1F;
            // #13BD belongs to the base_trdemu FDD-mask device.  Leave it
            // available to FddPentEvo regardless of subscription order.
            if (index == 0x13 && (addr & 0xFF) == 0xBD)
            {
                return;
            }
            handled = true;
            switch (index)
            {
                case 0x00:
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                case 0x05:
                case 0x06:
                case 0x07:
                    value = (byte)(GetCfgPage((addr >> 8) & 7) ^ 0xFF);
                    break;
                case 0x08:
                    value = (byte)GetCfgIsRam();
                    break;
                case 0x09:
                    value = (byte)GetCfgIsDos();
                    break;
                case 0x0A:
                    value = CMR0;
                    break;
                case 0x0B:
                    value = m_pEFF7;
                    break;
                case 0x0C:
                    value = (byte)((m_pFF77 & 0x0F) |
                        ((m_aFF77 & 0x4000) >> 7) |
                        ((m_aFF77 & 0x0200) >> 3) |
                        ((m_aFF77 & 0x0100) >> 3) |
                        (DOSEN ? 0x10 : 0));
                    break;
                case 0x0D:
                    value = m_ulaAtm != null ? m_ulaAtm.ReadConfigPalette() : (byte)0xFF;
                    break;
                case 0x0E:
                    value = m_ulaAtm != null ? m_ulaAtm.ReadConfigFont() : (byte)0xFF;
                    break;
                case 0x0F:
                    // RTL leaves the high nibble undefined.
                    value = (byte)((value & 0xF0) |
                        (m_ulaAtm != null ? m_ulaAtm.ReadConfigBorder() : 0));
                    break;
                case 0x10:
                    value = (byte)m_breakpointAddress;
                    break;
                case 0x11:
                    value = (byte)(m_breakpointAddress >> 8);
                    break;
                case 0x12:
                    value = m_writeDisable;
                    break;
            }
        }

        protected virtual void BusWritePortXXBD_CFG(
            ushort addr,
            byte value,
            ref bool handled)
        {
            if (handled)
            {
                return;
            }
            switch ((addr >> 8) & 0x1F)
            {
                case 0x10:
                    m_breakpointAddress = (ushort)((m_breakpointAddress & 0xFF00) | value);
                    handled = true;
                    break;
                case 0x11:
                    m_breakpointAddress = (ushort)((m_breakpointAddress & 0x00FF) | (value << 8));
                    handled = true;
                    break;
            }
        }

        protected virtual void BusWritePortXXBE_FDD_EXIT(
            ushort addr,
            byte value,
            ref bool handled)
        {
            if (handled)
            {
                return;
            }

            // OUT (#BE),A is the documented return path from the page #FE
            // virtual-drive handler and the common NMI clear strobe.  NMI
            // keeps page #FF for two more M1 cycles; when NMI is active the
            // underlying page-#FE state is deliberately retained.
            handled = true;
            if (m_inNmi)
                m_nmiClearM1Remaining = 2;
            else
                ExitFddIoRam();
        }

        private int GetCfgIsDos()
        {
            var value = 0;
            var mask = 0x01;
            for (var index = 0; index < 8; index++)
            {
                // high 2 bits of ram page stored 
                // in the high byte of m_ru2[wnd]
                var w = m_ru2[index] ^ 0x3FF;
                var isDos = (w & 0x80) == 0;
                if (isDos) value |= mask;
                mask <<= 1;
            }
            return value;
        }

        private int GetCfgIsRam()
        {
            var value = 0;
            var mask = 0x01;
            for (var index = 0; index < 8; index++)
            {
                // high 2 bits of ram page stored 
                // in the high byte of m_ru2[wnd]
                var w = m_ru2[index] ^ 0x3FF;
                var isRam = (w & 0x40) == 0;
                if (isRam) value |= mask;
                mask <<= 1;
            }
            return value;
        }

        private int GetCfgPage(int index)
        {
            // Ports #xxBD/#xxBE return the raw low byte of the PentEvo
            // pFFF7 descriptor (the caller inverts it), not the currently
            // mapped physical page.  m_ru2 stores the same descriptor in a
            // compact layout: mode bits are in w[7:6], page bits 7:6 in w[9:8].
            var w = m_ru2[index] ^ 0x3FF;
            return (w & 0x3F) | ((w >> 2) & 0xC0);
        }

        protected virtual void BusReset()
        {
            // Retain the existing emulator reset baseline (7 MHz).
            m_activeCpuClockMultiplier = 2;
            m_dramArbiter = null;
            m_dramLastTact = -1;
            InvalidateMemoryBuffer();
            m_fddIoRamActive = false;
            m_fddIoWriteDisabled = false;
            m_writeDisable = 0;
            m_aFF77 = 0x4000;   // RESET: A14=1, A9=0, A8=0
            m_pFF77 = 3;        // RESET: D3=0, D2..D0=011
            m_pXXBF = 0;        // RESET=0
            m_pEFF7 = 0;        // RESET=0
            m_breakpointAddress = 0;
            m_nmiPending = false;
            m_externalNmiPending = false;
            m_nmiSignal = false;
            m_nmiEntryActive = false;
            m_inNmi = false;
            m_nmiClearM1Remaining = 0;
            m_nmiSwitchAfterM1 = false;
            m_nmiClearAfterM1 = false;
            DOSEN = CPM;

            CMR0 = 0;
            CMR1 = 0;
            UpdateMapping();
        }

        public override void ResetState()
        {
            base.ResetState();
            BusReset();

            // Init 128K
            m_aFF77 = 0xFF77;
            m_pFF77 = 0x00AB;
            Array.Copy(
                new int[] { 0x381, 0x37a, 0x37d, 0x3ff, 0x383, 0x37a, 0x37d, 0x3ff },
                m_ru2,
                m_ru2.Length);
            DOSEN = CPM;
        }

        #endregion
    }
}
