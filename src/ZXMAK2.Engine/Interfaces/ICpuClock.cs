namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>
    /// Describes a machine-controlled CPU clock relative to its base clock.
    /// MaxCpuClockMultiplier defines master-tact units relative to the base
    /// CPU clock. CpuClockMultiplier selects the execution rate. The master
    /// clock may be faster than the CPU clock to represent wait phases.
    /// </summary>
    public interface ICpuClock
    {
        int CpuClockMultiplier { get; }
        int MaxCpuClockMultiplier { get; }
    }

    /// <summary>
    /// Optional clock provider requesting master-clock synchronization before
    /// CPU bus callbacks. Existing ICpuClock providers keep legacy timing.
    /// This interface does not itself implement hardware WAIT states.
    /// </summary>
    public interface ICpuClockBusSync : ICpuClock
    {
    }

    public enum CpuMemoryAccess
    {
        Opcode,
        Read,
        Write
    }

    /// <summary>
    /// Optional per-access timing in master tacts. Called after device memory
    /// callbacks with their final read value. Returned delay must not be
    /// converted to CPU tacts or scaled a second time.
    /// </summary>
    public interface ICpuMemoryTiming : ICpuClockBusSync
    {
        int CompleteMemoryAccess(ushort address, CpuMemoryAccess access,
            long masterTact, ref byte value);
        void InvalidateMemoryBuffer();
    }

    /// <summary>
    /// Optional ordinary IN/OUT timing. Returned delays use master units;
    /// interrupt and NMI acknowledgment are excluded from this callback.
    /// </summary>
    public interface ICpuPortTiming : ICpuMemoryTiming
    {
        int GetPortWait(ushort address);
    }

    /// <summary>
    /// Optional machine clock latch at the CPU refresh boundary.
    /// CpuClockMultiplier reports the applied rate, not the port request.
    /// Providers without this interface retain existing clock behavior.
    /// </summary>
    public interface ICpuRefreshClock : ICpuPortTiming
    {
        void LatchClockAtRefresh();
    }
}
