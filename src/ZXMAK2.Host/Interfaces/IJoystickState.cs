

namespace ZXMAK2.Host.Interfaces
{
    public interface IJoystickState
    {
        bool IsLeft { get; }
        bool IsRight { get; }
        bool IsUp { get; }
        bool IsDown { get; }
        bool IsFire { get; }
    }

    /// <summary>
    /// Optional extended Kempston state used by machines whose hardware
    /// exposes all eight joystick bits.  Legacy devices continue to consume
    /// IJoystickState and therefore retain their original five-bit behavior.
    /// </summary>
    public interface IJoystickState8 : IJoystickState
    {
        byte KempstonState { get; }
    }
}
