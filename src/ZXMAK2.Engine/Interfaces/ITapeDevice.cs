using System;
using System.Collections.Generic;
using ZXMAK2.Model.Tape.Interfaces;


namespace ZXMAK2.Engine.Interfaces
{
	public interface ITapeDevice
	{
		void Play();
		void Stop();
		void Rewind();
		void Reset();       // loaded new image
		bool IsPlay { get; }
		int TactsPerSecond { get; }
		// TAP/TZX pulse lengths use the original 3.5 MHz Z80 timebase.  A
		// machine which exposes Cpu.Tact in a faster master-clock domain
		// returns the factor needed to convert those pulse lengths.
		int TapePulseClockMultiplier { get; }
		List<ITapeBlock> Blocks { get; }
		event EventHandler TapeStateChanged;
		int CurrentBlock { get; set; }
		int Position { get; }
		bool UseTraps { get; set; }
		bool UseAutoPlay { get; set; }
	}
}
