namespace ZXMAK2.Host.Interfaces
{
    /// <summary>
    /// Host-only identity for multiple SD sockets sharing one toolbar button.
    /// Index 0 is the machine Z-controller, index 1 is NeoGS.
    /// </summary>
    public interface ISecureDigitalMediaStatus
    {
        int SecureDigitalIndex { get; }
    }
}
