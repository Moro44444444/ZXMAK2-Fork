ZXMAK2 v46 — ZX-Evo BaseConf Alpha 8 MultiSound runtime audio fix test
Date: 2026-09-24

Purpose
-------
This is a separate runtime-test build based on v45. Stable v42 and all v43-v45
test folders remain untouched. It corrects the SAA audio regression found with
real music and strengthens the two-chip TurboSound check.

Fixed after v45 runtime testing
-------------------------------
- The experimental SAA1099 generator rewrite from v44/v45 passed synthetic
  vectors but produced garbled and incomplete real music. It has been removed.
- The accepted v42 SAA generator/mixer is restored exactly.
- The verified Rev.A2 bus correction remains: SAA registers can be preloaded
  while the external clock is stopped; only generator progress is frozen.
- Test.exe /multisound now programs D1 and D2 with different tones and requires
  audio from both physical YM2203 SSG paths. The older register-only check could
  not prove that the second half of TurboSound was audible.

Automated verification
----------------------
- Test.exe /multisound: D1, D2, SAA preload, GS waveform, SounDrive waveform,
  ROM-M1 lock and automatic/manual conflict policy all PASS.
- Test.exe /tsfm: D1, D2, FM, all six SAA tones, noise and envelope PASS.
- Full Test.exe: all ULA sanity tests PASS; performance remains in the normal
  hundreds-of-milliseconds range, with no multi-second rendering regression.

Runtime acceptance requested
----------------------------
Please replay the same trusted SAA1099 composition and the same TurboSound
material used to expose v45. The first result checks the restored v42 renderer;
the second checks real musical use of both independently verified YM paths.

This is a local test build, not a GitHub Alpha 8 release.
