namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>
    /// Exposes the current TR-DOS working session independently from the
    /// instantaneous DOS ROM paging signal.
    /// </summary>
    public interface ITrDosSessionDevice
    {
        bool IsTrDosSessionActive { get; }
    }
}
