ZXMAK2 v43 — ZX-Evo BaseConf Alpha 8 ZX-MultiSound Rev.A2 test
Date: 2026-09-24

Base
----
This is a separate experimental build based strictly on the accepted stable
v42 source.  The v42 portable folder was not modified and remains the rollback
build.  All accepted v42 media, toolbar, NeoGS, TurboSound FM Pro and AY/YM
behaviour is retained.

ZX-MultiSound Rev.A2
--------------------
- New single ZXBUS board based on the latest public Rev.A2 schematic and CPLD
  RTL (UzixLS/zx-multisound, commit d7f3ac2).
- Two YM2203 (TurboSound FM), SAA1099, General Sound 16 MHz/1 MiB with official
  ROM 1.05b, and four-channel SounDrive are implemented as parts of one board.
- YM partial address decoding, SAA #FF/#1FF addressing, #FFFD configuration,
  SounDrive aliases, GS #B3/#BB protocol, ROM/RAM map, DAC volumes, reset state,
  exact 321-clock GS interrupt counter and the ROM-M1 port lock follow top.v.
- Either physical ZXBUS slot can contain NeoGS or MultiSound.  One of each may
  be installed simultaneously, in either order; duplicate boards are rejected.
- Automatic conflict protection is the default: NeoGS owns #B3/#BB and disables
  only the MultiSound GS block; MultiSound YM selects Music: None and owns the
  AY/TSFM ports.  Manual mode exposes the four real switches and rejects a
  conflicting combination before changing the running profile.
- PentEvo internal Music now offers None, AY/YM and TurboSound FM Pro.  ZXBUS
  slot/switch controls are on their own Machine Settings page, not under ULA.

Known boundary
--------------
The physical board's external SAM2695 is a proprietary wavetable synthesizer.
The public board source provides its clock/wiring but no executable synthesizer
core, firmware or waveform ROM.  SAM2695/MIDI audio is therefore not emulated
in this candidate and is not claimed as complete.

Verification
------------
- Full Release rebuild: 0 errors; only the two known missing-ruleset warnings.
- Test.exe /multisound: YM aliases, SAA, four DACs, GS 1.05b handshake,
  ROM-M1 lock, automatic policy and manual guard PASS.
- PentEvoProfileProbe: both slot orders, duplicate protection, pre-mutation
  conflict rejection and XML settings round-trip PASS.
- NeoGsProbe and Test.exe /tsfm PASS.
- Full Test.exe ULA sanity and performance suite PASS; video vectors remain
  approximately 339-417 ms for 500 frames on the build host.

Runtime acceptance
------------------
Please test this folder independently.  Check MultiSound AY/YM, SAA1099,
General Sound software, SounDrive, then NeoGS + MultiSound together in both
slot orders.  This is a local test build, not a GitHub Alpha 8 release.
