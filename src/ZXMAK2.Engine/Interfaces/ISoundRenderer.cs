

namespace ZXMAK2.Engine.Interfaces
{
    #region Comment
    /// <summary>
    /// Provide way to transfer audio data to sound card. AudioBuffer will be taken between BusFrameEnd and BusFrameBegin events.
    /// </summary>
    #endregion
    public interface ISoundRenderer
    {
        int SampleRate { get; set; }
        uint[] AudioBuffer { get; }
		int Volume { get; set; }
    }

    /// <summary>
    /// Optional machine-level policy for the final PCM mixer.
    /// </summary>
    public interface ISoundMixerConfiguration
    {
        bool RejectDc { get; }
    }

    /// <summary>
    /// Gain for a device and its additional renderers, applied with wide
    /// accumulators in the final mixer, never clipped at individual sources.
    /// </summary>
    public interface ISoundSourceGainConfiguration
    {
        int OutputGainPercent { get; }
    }
}
