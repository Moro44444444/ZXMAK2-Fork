namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// Independent ZX-MultiSound Max board. Its decoder follows the supplied
    /// fw_ms_opl3_cpld_saa_fix/rtl/top.v, not the older Rev.A2 profile.
    /// </summary>
    public sealed class ZxMultiSoundMaxDevice : ZxMultiSoundCore
    {
        public ZxMultiSoundMaxDevice() : base(true) { }
    }
}
