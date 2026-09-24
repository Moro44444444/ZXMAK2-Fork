ZXMAK2 v45 — ZX-Evo BaseConf Alpha 8 MultiSound SAA bus fix test
Date: 2026-09-24

Purpose
-------
This is a separate runtime-test build based on v44. Stable v42 and the v43/v44
test folders remain untouched. The change is restricted to the SAA1099 bus and
external-clock model of ZX-MultiSound Rev.A2 plus its regression test.

Fixed after v44 runtime testing
-------------------------------
- The real Rev.A2 CPLD accepts SAA register writes at #1FF/#FF even while the
  SAA 8 MHz clock is stopped. Software may preload the complete chip and then
  start it by writing #F7 to an #FFFD alias.
- v44 incorrectly discarded every stopped-clock write, so players using that
  valid startup order lost part of their channel, mixer and envelope setup.
- Register access and clock gating are now independent, as in official top.v.
  A stopped clock freezes the generators; it no longer disconnects registers.

Automated verification
----------------------
- Test.exe /multisound preloads a complete SAA voice while the clock is off,
  starts the clock afterwards, and requires real audio. This sequence fails on
  the old adapter and passes on this build.
- Existing GS waveform, SounDrive waveform, YM aliases, ROM-M1 guard and
  conflict-policy checks remain enabled.
- Test.exe /tsfm verifies both YM2203 SSG paths, FM, all six SAA tone channels,
  noise and envelope, guarding the accepted TurboSound FM Pro behaviour.

Runtime acceptance requested
----------------------------
Please run the same trusted SAA1099 composition that was noisy in v44. General
Sound, SounDrive, AY/YM and TSFM/FM may be spot-checked for regression, but no
code in those paths was intentionally changed.

This is a local test build, not a GitHub Alpha 8 release.
