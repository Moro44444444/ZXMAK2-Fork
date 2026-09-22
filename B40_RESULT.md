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
- Tape clock probe: TAP and TZX retain native 3.5 MHz periods in storage and
  dynamically produce both 1× (ordinary Spectrum) and 8× (BaseConf
  master-clock) playback timing.

## Focused tape follow-up

The clean BaseConf profile now has the documented tape device with autoplay
enabled, and the tape player correction keeps TAP/TZX pulse lengths in their
fixed 3.5 MHz format timebase and converts each pulse only when it is played.
The ROM-loader autoplay detector uses the same current-machine conversion.
The multiplier is derived from the active machine: `ZX-Evo BSconf` is 8 and
every other machine is 1. Thus a loaded tape may be retained while switching
machines. WAV/CSW playback, video, disks, IDE and audio output are
intentionally untouched.

Saved VM profiles are also guarded: any obsolete `pulseClockMultiplier` field
is ignored. The actual machine name selects the scale at playback time, so
switching machines cannot carry BaseConf ×8 into another machine or vice
versa.

## Runtime acceptance

Accepted by the user as the stable B40 checkpoint on 2026-09-22. In addition
to the earlier BaseConf runtime checks, the retained-tape scenario is accepted:
the same TAP/TZX image may be used after BaseConf↔Pentagon/Spectrum machine
switches without reopening it. This remains a local test package, not a public
GitHub release.
