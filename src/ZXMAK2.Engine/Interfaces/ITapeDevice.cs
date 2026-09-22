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
		void Eject();
		void Reset();       // loaded new image
		bool IsPlay { get; }
		int TactsPerSecond { get; }
		// TAP/TZX pulse lengths use the original 3.5 MHz Z80 timebase.  The
		// active machine profile supplies a factor when it advances Cpu.Tact
		// in another domain (BaseConf is explicitly 8).
		int TapePulseClockMultiplier { get; }
		List<ITapeBlock> Blocks { get; }
		event EventHandler TapeStateChanged;
		int CurrentBlock { get; set; }
		int Position { get; }
		bool UseTraps { get; set; }
		bool UseAutoPlay { get; set; }
	}
}
