ZXMAK2 v44 — ZX-Evo BaseConf Alpha 8 ZX-MultiSound audio fix test
Date: 2026-09-24

Purpose
-------
This is a separate runtime-test build based on the v43 ZX-MultiSound Rev.A2
candidate.  Stable v42 and the original v43 test folder remain untouched.
All accepted BaseConf, toolbar/media, NeoGS and internal AY behaviour is kept.

Fixed after v43 runtime testing
-------------------------------
- General Sound and SounDrive now preserve every DAC transition on the shared
  four-channel converter.  GS samples are timestamped from the private 16 MHz
  Z80 timeline instead of collapsing a whole execution batch onto one host-Z80
  timestamp.
- GS interrupt low time is taken literally from the Rev.A2 CPLD: 33 clocks at
  12 MHz (reload edge and counter values 0..31).
- The managed SAA1099 core was reconciled with current SAASound 3.5 source:
  correct tone/noise logic, buffered frequency and octave updates, tone-clocked
  noise mode, verified 18-bit LFSR, envelope buffering/resolution, PDM
  amplitude behaviour, sync/reset state and 64x internal oversampling.
- No external SAASound.dll is required or included.

Automated verification
----------------------
- Full Release solution build: 0 errors; only the two known missing-ruleset
  warnings.
- Test.exe /multisound: YM aliases, official GS 1.05b boot handshake,
  ROM-M1 guard and conflict policy PASS; GS waveform 301 transitions and
  SounDrive waveform 101 transitions PASS.
- Test.exe /tsfm: two YM2203 SSG paths, FM and SAA PASS; every one of the six
  SAA tone channels plus noise and envelope vectors produces audio.
- NeoGsProbe PASS.  The complete ULA/performance Test.exe suite also passes.

Runtime acceptance requested
----------------------------
Please recheck the same trusted software that exposed v43:
1. SAA1099 track on ZX-MultiSound.
2. A known General Sound module/player.
3. A trusted SounDrive program.
4. NeoGS together with MultiSound, to confirm the automatic #B3/#BB guard.

This remains a local test build, not the GitHub Alpha 8 release.  MIDI through
the external proprietary SAM2695 is still outside the implemented boundary.
