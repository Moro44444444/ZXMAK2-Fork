namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>
    /// Host-only mount state used by the toolbar. It deliberately describes
    /// file attachment only; it has no role in the emulated I/O protocol.
    /// </summary>
    public interface IMediaStatusDevice
    {
        MediaStatusKind MediaStatusKind { get; }

        bool IsMediaMounted { get; }
    }

    public enum MediaStatusKind
    {
        Floppy,
        HardDisk,
        SecureDigital,
    }
}
