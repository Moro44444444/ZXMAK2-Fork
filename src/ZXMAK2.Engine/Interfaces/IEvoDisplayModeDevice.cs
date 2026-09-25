namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>AVR video-output and raster selector of ZX Evolution BaseConf.</summary>
    public interface IEvoDisplayModeDevice
    {
        // 0..3: TV normal/60 Hz/48K/128K; 4..7: VGA equivalents.
        int DisplayMode { get; set; }
    }
}
