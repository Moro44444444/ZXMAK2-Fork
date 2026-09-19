using System.IO;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Engine.Interfaces
{
    // Optional per-frame diagnostic text exposed by a machine device.
    // Hosts that do not display diagnostics can ignore this contract.
    public interface IFrameDiagnosticProvider
    {
        string FrameDiagnosticText { get; }
    }

    // Optional frame epoch for devices with variable raster periods.
    // Existing ULA implementations retain the cached/modulo engine path.
    public interface IUlaFrameTiming
    {
        int GetFrameTact(long masterTact);
        bool IsFrameComplete(long masterTact);
        void BeginFrameTiming(long masterTact);
    }

	public interface IUlaDevice
	{
        IFrameVideo VideoData { get; }

        bool IsEarlyTimings { get; }

		void LoadScreenData(Stream stream);
		void SaveScreenData(Stream stream);
		void ForceRedrawFrame();

		void Flush();

		int FrameTactCount { get; }
		bool CheckInt(int frameTact);

		byte PortFE { get; set; }
	}
}
