ZXMAK2 v42 — ZX-Evo BaseConf Alpha 8 AY/YM test
Date: 2026-09-24

Base
----
This is a separate test build based strictly on v41.  The accepted v41
portable folder was not modified.  All v41 user-interface, toolbar, media,
ZXBUS, NeoGS and TurboSound FM Pro features are retained.

AY/YM correction
----------------
The v41 +50% post-render gain is no longer applied to the two YM2203 SSG
(AY-compatible) renderers.  Their logarithmic AY/YM output already uses the
full signed range; applying another gain stage clipped every stereo sample in
the regression vector and audibly damaged ordinary AY music.  The accepted
+50% gain remains enabled for the SAA1099 and FM sections.

Verification
------------
- Full Release rebuild: 0 errors; only the two known missing-ruleset warnings.
- Test.exe /tsfm: D1, D2, FM and SAA audio PASS.
- AY/YM SSG native-headroom regression check PASS.
- The same updated test fails against the original v41 Hardware DLL, proving
  that it detects the repaired clipping path.
- PentEvoProfileProbe PASS: v41 ULA/Music/ZXBUS navigation and AY<->TSFM
  round-trip are preserved.
- NeoGsProbe PASS: ROM 1.11, DMA, microSD and VS1011 are preserved.
- Full Test.exe ULA sanity and performance suite PASS.

Runtime acceptance
------------------
Please check ordinary AY music through TurboSound FM Pro first.  Also make a
short control pass over SAA1099, FM, NeoGS, both SD cards, FDD, tape and CD.
This is a local test build, not a GitHub Alpha 8 release yet.
