# B39A result — isolated BaseConf 4:4:4 palette

Date: 2026-09-21

## Scope

B39A starts again from the user-accepted B38 / Alpha 0.4 baseline.  It adds
only the official `base_trdemu` 4:4:4 palette extension selected by `#BF.D5`.
The rejected combined B39 implementation was not reused.  ULAplus, final
contention/floating-bus work, VG93, ZX-BUS and CD/ATAPI are not changed.

## Official contract

- With D5 clear, the accepted B01-B38 `SetPaletteAtm2` path is unchanged.
- With D5 set, the upper RGB pairs retain the ATM2 active-low data encoding;
  the lower pairs are decoded from address lines A8/A9/A12-A15 exactly as in
  `video_palframe.v` r1364.
- `#0DBD/#0DBE` returns the lower RGB pairs while D5 is set and the legacy
  upper pairs while D5 is clear, following `BD_COLORRD` in `zports.v`.
- D5 changes only write/read interpretation.  It does not clear or rewrite
  palette RAM.  A 4:4:4 write updates all seven existing BaseConf renderers
  after flushing the already rendered part of the frame.

## Verification

- Visual Studio 2022 MSBuild 17.14.51, full Release solution: PASS, 0 errors;
  only the two pre-existing missing-ruleset warnings remain.
- `Palette444Probe-B39A`: 132867 PASS — unchanged D5=0 route, all 256 data
  bytes × all 64 relevant address combinations, seven renderer routes,
  D5=1 readback and D5 retention.
- Established regressions: B23 palette 1807; B30 video 12748; B35 config 539;
  B36 INT/NMI 41; B37 WAIT 197981; B38 input/audio 196641; B31 FDD 14;
  Rage SCL 113; TRD 655922; media swap 33; IDE media/ports 10/786;
  audio 19 and DirectSound underrun 14 — PASS.

## Checkpoints and package

- Before checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-before-20260921-152615\snapshot`
  (826/826 files).
- Isolated implementation checkpoint: commit `8f7d9a7` on branch
  `codex/b39a-palette444-only`.
- Runtime package:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-20260921-154533\release`.
- ZIP: 123 files, 5050610 bytes, SHA-256
  `518B1CB99EF204EE501545D7C552B16B9959DED828785E5487427EB4C23E50B6`;
  clean extraction was verified 123/123 files byte-identical and remained
  alive through a four-second startup smoke test.
- After checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-after-20260921-154923\snapshot`
  (829/829 source files).

## Runtime acceptance

Pending the user's visual and application smoke test.  Automated checks do
not claim runtime acceptance.  Required checks: normal BaseConf startup;
B30 border/multicolor; ATM 320x200 16-color software; Rage with one short
Enter; NedoOS and Bad Apple; normal sound without FDD scraping; SD/HDD swap
and IDE browsing.  If a documented BaseConf 4:4:4 test is available, compare
its colors and mid-frame transitions.  ULAplus is intentionally absent.
