using System;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Attributes;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZX-Evo BaseConf one-bit sound output.  The AVR config0.D3 mux selects
    /// port-FE D4 (beeper) or D3 (tape-out), exactly as sound/sound.v r1364.
    /// </summary>
    public sealed class BeeperPentEvo : SoundDeviceBase
    {
        private CmosPentEvo m_cmos;
        private byte m_lastData;
        private bool m_outputHigh;
        private ushort m_dacHigh;

        public BeeperPentEvo()
        {
            Name = "BEEPER PENTEVO";
            Description = "ZX-Evo BaseConf beeper/tape-out mux";
            Volume = 40;
        }

        [HardwareValue("DATA", Description = "Last value written to low byte FE")]
        public byte LastData { get { return m_lastData; } }

        [HardwareValue("OUT", Description = "Selected D4/D3 one-bit output")]
        public bool OutputHigh { get { return m_outputHigh; } }

        [HardwareValue("SOURCE", Description = "Selected port-FE source bit")]
        public int SelectedBit
        {
            get { return m_cmos != null && m_cmos.BeeperTapeOutSelected ? 3 : 4; }
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            m_cmos = bmgr.FindDevice<CmosPentEvo>();
            if (m_cmos != null)
                m_cmos.BeeperMuxChanged += RefreshOutput;
            bmgr.Events.SubscribeWrIo(0x00FF, 0x00FE, WritePortFe);
            bmgr.Events.SubscribeReset(Reset);
        }

        public override void BusDisconnect()
        {
            if (m_cmos != null)
                m_cmos.BeeperMuxChanged -= RefreshOutput;
            base.BusDisconnect();
        }

        protected override void OnProcessConfigChange()
        {
            base.OnProcessConfigChange();
            var coefficient = Math.Max(0D, Math.Min(1D, Volume / 100D));
            m_dacHigh = (ushort)Math.Floor(ushort.MaxValue * coefficient);
        }

        private void Reset()
        {
            m_lastData = 0;
            SetOutput(false);
        }

        private void WritePortFe(ushort address, byte value, ref bool handled)
        {
            m_lastData = value;
            RefreshOutput();
        }

        private void RefreshOutput()
        {
            SetOutput((m_lastData & (1 << SelectedBit)) != 0);
        }

        private void SetOutput(bool high)
        {
            if (high == m_outputHigh)
                return;
            m_outputHigh = high;
            UpdateDac(high ? m_dacHigh : (ushort)0,
                high ? m_dacHigh : (ushort)0);
        }
    }
}
