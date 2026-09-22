# B40 result — BaseConf timing/video completion

Date: 2026-09-22

## Scope

B40 continues from the user-accepted B39A baseline. It completes only the
planned BaseConf timing/video work: documented 48K/128K contention, live
picture-mode and palette transitions, seven renderer golden vectors, and a
read-only review of the existing WD1793 trace. ULAplus, media lifecycle,
CD/ATAPI, ZX-BUS and new peripherals are not part of this build.

## Verification

- Release solution: PASS, 0 errors; only the two pre-existing missing-ruleset
  warnings remain.
- B30 video timing: 12748 PASS.
- B39A palette: 132867 PASS.
- B40 contention/open bus: 46 PASS.
- B40 live mode/palette: 9 PASS.
- B40 seven renderer golden vectors: 7 PASS.
- Tape clock probe: TAP and TZX standard pulse streams PASS at both 1×
  (ordinary Spectrum) and 8× (BaseConf master-clock) timing.

## Focused tape follow-up

The clean BaseConf profile now has the documented tape device, and the tape
player correction converts TAP/TZX pulse lengths from their fixed 3.5 MHz
format timebase to BaseConf's 28 MHz master tact counter. The ROM-loader
autoplay detector uses the same conversion. WAV/CSW playback, video, disks,
IDE and audio output are intentionally untouched.

## Runtime acceptance

Pending the user's test. Please check normal BaseConf startup, B30 border and
multicolor, ATM 16-color software, Rage, NedoOS, Bad Apple, normal sound,
SD/HDD replacement, IDE file browsing, and a TAP/TZX load from the BaseConf
ROM menu (cassette icon plus data loading). This is a local test package, not
a public GitHub release.
