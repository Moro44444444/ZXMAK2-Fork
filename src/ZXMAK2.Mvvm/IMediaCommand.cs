namespace ZXMAK2.Mvvm
{
    /// <summary>
    /// Stable identity for a command that mounts or ejects emulated media.
    /// The host must use this identity instead of the localized display text.
    /// </summary>
    public interface IMediaCommand : ISuccessCommand
    {
        MediaCommandKind MediaKind { get; }
        MediaCommandAction MediaAction { get; }
        int DriveIndex { get; }
    }

    public enum MediaCommandKind
    {
        Floppy,
        HardDisk,
        SecureDigital,
        OpticalDisc,
    }

    public enum MediaCommandAction
    {
        Load,
        Eject,
    }
}
