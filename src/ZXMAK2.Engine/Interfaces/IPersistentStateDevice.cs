namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>
    /// A device with user-resettable persistent state, such as PentEvo CMOS.
    /// The host uses this small contract so it does not need a dependency on
    /// a concrete hardware implementation.
    /// </summary>
    public interface IPersistentStateDevice
    {
        string PersistentStateFileName { get; }
        void ResetPersistentState();
    }
}
