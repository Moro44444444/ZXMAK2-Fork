using ZXMAK2.Hardware.General;


namespace ZXMAK2.Hardware.Atm
{
    /// <summary>
    /// ATM Turbo 2+ WD1793 using the 7 MHz master-tact time base.
    /// </summary>
    public class FddAtm710 : FddController
    {
        public FddAtm710()
            : base(2)
        {
            Name = "FDD ATM710";
            Description = "FDD WD1793 with ATM Turbo 2+ timing";
        }
    }
}
