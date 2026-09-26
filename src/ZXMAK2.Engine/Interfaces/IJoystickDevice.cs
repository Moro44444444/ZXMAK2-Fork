using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Engine.Interfaces
{
    public interface IJoystickDevice
    {
        string HostId { get; set; }
        IJoystickState JoystickState { get; set; }
    }

    // Pressed contacts, not host key codes: combine with the real keyboard
    // at read time so auto-fire is independent of host/frame polling.
    public interface IJoystickKeyboardDevice : IJoystickDevice
    {
        byte GetKeyboardMask(ushort address);
    }

    public interface IKeyboardJoystickProvider
    {
        byte GetKeyboardJoystickMask(ushort address);
    }

    // Matrix keyboards merge the provider after their physical contacts.
    public interface IKeyboardJoystickSink { }
}
