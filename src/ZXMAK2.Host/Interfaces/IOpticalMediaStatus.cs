namespace ZXMAK2.Host.Interfaces
{
    /// <summary>
    /// Host-side state of an optional optical drive. It is separate from the
    /// ATA protocol so the toolbar can distinguish disconnected, empty and
    /// ready states without peeking into a concrete hardware class.
    /// </summary>
    public interface IOpticalMediaStatus
    {
        bool IsOpticalDeviceConnected { get; }
        bool IsOpticalMediaReady { get; }
        string OpticalDriveName { get; }
    }
}
