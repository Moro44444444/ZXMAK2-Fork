using System;
using ZXMAK2.Hardware.General;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    public class FddPentEvo : FddController
    {
        private byte m_p2F, m_p4F, m_p6F, m_p8F;
        private byte m_p13BD;
        private byte m_lastSystem;
        private int m_selectedDrive;
        private MemoryPentEvo m_memoryPentEvo;

        
        public FddPentEvo()
            : base(8)
        {
            Name = "FDD PentEvo";
            Description = "FDD WD1793 + specific PentEvo ports";
        }


        protected override void OnSubscribeIo(IBusManager bmgr)
        {
            bmgr.Events.SubscribeWrIo(0x009F, 0x001F, BusWriteFdc);
            bmgr.Events.SubscribeRdIo(0x009F, 0x001F, BusReadFdc);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00FF, BusWriteSys);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x00FF, BusReadSys);

            bmgr.Events.SubscribeWrIo(0x00FF, 0x002F, WritePortEmu);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x004F, WritePortEmu);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x006F, WritePortEmu);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x008F, WritePortEmu);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x002F, ReadPortEmu);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x004F, ReadPortEmu);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x006F, ReadPortEmu);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x008F, ReadPortEmu);

            // PentEvo BaseConf exposes the virtual-drive selector at #13BD.
            // ERS FE also uses write/read-back on this register to verify that
            // the matching FPGA firmware is present.
            // zports.v decodes A12..A8 and the low byte; A15..A13 are aliases.
            bmgr.Events.SubscribeWrIo(0x1FFF, 0x13BD, WritePort13BD);
            bmgr.Events.SubscribeRdIo(0x1FFF, 0x13BD, ReadPort13BD);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00BE, TracePortBe);
            bmgr.Events.SubscribeReset(ResetPort13BD);
        }

        public override void BusInit(IBusManager bmgr)
        {
            m_memoryPentEvo = bmgr.FindDevice<MemoryPentEvo>();
            if (m_memoryPentEvo != null)
            {
                m_memoryPentEvo.FddIoTrace = LogIo;
            }
            base.BusInit(bmgr);
        }

        protected override void BusWriteFdc(ushort addr, byte value, ref bool handled)
        {
            if (handled || !IsActive)
            {
                return;
            }
            if (RedirectVirtualDriveAccess("WR", addr, value))
            {
                handled = true;
                return;
            }
            base.BusWriteFdc(addr, value, ref handled);
            if (LogIo && ((addr & 0x60) >> 5) == 0)
            {
                Trace("WD-CMD-AFTER addr=#{0:X4} value=#{1:X2} {2}",
                    addr, value, CompactWdState());
            }
        }

        protected override void BusReadFdc(ushort addr, ref byte value, ref bool handled)
        {
            if (handled || !IsActive)
            {
                return;
            }
            if (RedirectVirtualDriveAccess("RD", addr, null))
            {
                value = 0xFF;
                handled = true;
                return;
            }
            base.BusReadFdc(addr, ref value, ref handled);
        }

        protected override void BusWriteSys(ushort addr, byte value, ref bool handled)
        {
            if (handled || !IsActive)
            {
                return;
            }

            // zdos.v masks the physical VG93 chip select using the drive that
            // was selected before this I/O cycle.  A masked system write is
            // handled by page #FE and must not leak into the physical WD1793.
            if (RedirectVirtualDriveAccess("SYS-WR", addr, value))
            {
                handled = true;
                return;
            }

            base.BusWriteSys(addr, value, ref handled);
            m_lastSystem = (byte)(value & 0x1F);
            m_selectedDrive = value & 0x03;
            Trace("SYS-WR addr=#{0:X4} value=#{1:X2} drive={2} mask=#{3:X2} {4}",
                addr, value, m_selectedDrive, m_p13BD, CompactWdState());
        }

        protected override void BusReadSys(ushort addr, ref byte value, ref bool handled)
        {
            if (handled || !IsActive)
            {
                return;
            }
            if (RedirectVirtualDriveAccess("SYS-RD", addr, null))
            {
                value = 0xFF;
                handled = true;
                return;
            }

            base.BusReadSys(addr, ref value, ref handled);
            // UnrealSpeccy preserves the low WD system bits while FDD I/O to
            // RAM support is enabled.  ERS relies on the selected drive bits.
            value = (byte)((value & 0xE0) | m_lastSystem);
        }

        private bool RedirectVirtualDriveAccess(
            string operation,
            ushort addr,
            byte? value)
        {
            var selected = (m_p13BD & (1 << m_selectedDrive)) != 0;
            if (!selected || m_memoryPentEvo == null)
            {
                return false;
            }

            var wasActive = m_memoryPentEvo.FddIoRamActive;
            var dosRomMapped = m_memoryPentEvo.IsDosRomMappedAt0000;
            var entered = m_memoryPentEvo.TryEnterFddIoRam();
            Trace("VIRTUAL-{0} addr=#{1:X4} value={2} drive={3} mask=#{4:X2} " +
                "wasActive={5} dosRom={6} entered={7} PC=#{8:X4} T={9}",
                operation,
                addr,
                value.HasValue ? string.Format("#{0:X2}", value.Value) : "--",
                m_selectedDrive,
                m_p13BD,
                wasActive,
                dosRomMapped,
                entered,
                m_cpu.regs.PC,
                m_cpu.Tact);
            if (entered)
            {
                // Nedo Emul/TR-DOS can access a PentEvo virtual drive through
                // RAM page #FE without reaching the physical WD1793 path.
                MarkTrDosSessionActive();
            }
            // The real VG93 is disabled for a masked drive even when page #FE
            // cannot be entered (for example, while its handler is active).
            return true;
        }

        protected virtual void WritePort13BD(ushort addr, byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;
            m_p13BD = val;
            Trace("P13BD-WR value=#{0:X2} drive={1} PC=#{2:X4} T={3}",
                val, m_selectedDrive, m_cpu.regs.PC, m_cpu.Tact);
        }

        protected virtual void ReadPort13BD(ushort addr, ref byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;
            val = m_p13BD;
            Trace("P13BD-RD value=#{0:X2} drive={1} PC=#{2:X4} T={3}",
                val, m_selectedDrive, m_cpu.regs.PC, m_cpu.Tact);
        }

        private void TracePortBe(ushort addr, byte val, ref bool handled)
        {
            if (!LogIo || m_memoryPentEvo == null ||
                !m_memoryPentEvo.FddIoRamActive)
            {
                return;
            }

            Trace("PBE-EXIT value=#{0:X2} activeBefore=True PC=#{1:X4} T={2}",
                val, m_cpu.regs.PC, m_cpu.Tact);
        }

        private string CompactWdState()
        {
            return m_wd.DumpState()
                .Replace("\r", string.Empty)
                .Replace("\n", "; ");
        }

        private void Trace(string format, params object[] args)
        {
            if (LogIo)
            {
                Logger.Debug("EVO-FDD " + format, args);
            }
        }

        private void ResetPort13BD()
        {
            m_p13BD = 0;
            m_lastSystem = 0;
            m_selectedDrive = 0;
            Trace("RESET P13BD=#00 drive=0 PC=#{0:X4} T={1}",
                m_cpu != null ? m_cpu.regs.PC : 0,
                m_cpu != null ? m_cpu.Tact : 0);
        }

        protected virtual void WritePortEmu(ushort addr, byte val, ref bool handled)
        {
            if (IsActive)
            {
                switch ((addr & 0x00FF) >> 4)
                {
                    case 0x02:
                        m_p2F = val;
                        break;
                    case 0x04:
                        m_p4F = val;
                        break;
                    case 0x06:
                        m_p6F = val;
                        break;
                    case 0x08:
                        m_p8F = val;
                        break;
                }
            }
        }

        protected virtual void ReadPortEmu(ushort addr, ref byte val, ref bool handled)
        {
            if (IsActive)
            {
                switch ((addr & 0x00FF) >> 4)
                {
                    case 0x02:
                        val = m_p2F;
                        break;
                    case 0x04:
                        val = m_p4F;
                        break;
                    case 0x06:
                        val = m_p6F;
                        break;
                    case 0x08:
                        val = m_p8F;
                        break;
                }
            }
        }
    }
}
