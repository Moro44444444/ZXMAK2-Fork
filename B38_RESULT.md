# B38 result — BaseConf built-in input and audio

Date: 2026-09-21

## Scope

B38 is the isolated built-in input/audio stage fixed by the canonical B32
audit. It preserves the accepted B01–B36 behavior and the implemented B37
WAIT/port work. It does not add ULAplus, 4:4:4 palette rendering, final
contention/floating-bus work, ZX-BUS cards or CD/ATAPI.

## Official contract implemented

- The BaseConf Kempston joystick is decoded only on low byte `#1F` and returns
  all eight documented bits. Host buttons map to B/fire, C, A and Start on
  D4..D7; existing five-bit machine profiles keep their legacy behavior.
- In the machine profile VG93 is ordered before Kempston. Therefore normal
  mode reads `#1F` as joystick, while Shadow/DOS leaves the overlapping
  `#1F` to VG93 exactly as `zports.v` requires.
- Tape input is supplied on D6 of the documented `xxFE/xxF6` keyboard-read
  family. Generic FE output loopback and duplicate tape monitoring are
  disabled for ZX-Evo only; other machine profiles retain their old defaults.
- A PentEvo-specific one-bit sound device implements the r1364 hardware mux:
  AVR config0.D3 selects FE.D4 beeper or FE.D3 tape-out. Num Lock invokes the
  official AVR toggle, changes are combinational against the last FE value,
  and the selection persists in the existing CMOS state.
- AY, Covox, final RejectDC, DirectSound underrun handling and unrelated
  profiles were not changed.

## Verification

- Visual Studio 2022 MSBuild 17.14.51, full Release solution: PASS, 0 errors;
  only the two pre-existing missing-ruleset warnings remain.
- `InputAudioProbe-B38`: 196641 PASS — profile wiring, exhaustive `#1F` and
  `xxFE/xxF6` decode equations, normal/Shadow/DOS VG93 arbitration, eight- and
  five-bit joystick vectors, tape D6/loopback, persistent mux, combinational
  transitions and Num Lock edge behavior.
- B37 WAIT/ports: 197981; B36 INT/NMI: 41; B35 configuration ports: 539; B34
  media swap: 33; IDE media/ports: 10/786; FDD/Rage: 14/113 — PASS.
- B30 video timing/palette/TRD: 12748/1807/655922 — PASS. The B30 probe was
  updated only so config0.D3 is now recognized as the documented B38 bit.
- Audio path/DirectSound: 43/16 PASS, including cold/steady idle silence and
  RejectDC measurements.

## Checkpoints and package

- Before checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-before-20260921-045812\snapshot`.
- Runtime package and after checkpoint are recorded in the final project
  journal entry after archive verification.
- Runtime package:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-20260921-051523\release`.
- Portable ZIP:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-20260921-051523\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38.zip`.
- ZIP: 126 files, 5324858 bytes, SHA-256
  `7A1F87CFFE1DB43A0F081710C8999350FA68EF3A27166D0932255A0B4F88315A`;
  clean extraction was verified 126/126 files byte-identical.
- After checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-after-20260921-051523\snapshot`
  (834/834 source files verified).
- This is a private runtime-test package retaining current `.cmos/.nvram` and
  `.vmide` state. Public GitHub portable-cleaning rules are unchanged.

## Runtime acceptance

Pending the user's test. Automated build/probe success does not claim runtime
acceptance. Required smoke test: normal BaseConf startup; Rage with one short
Enter; NedoOS and Bad Apple; B30 border/multicolor; ordinary AY/Covox/beeper
sound and absence of new idle noise; SD/HDD replacement and IDE file access.
If suitable software is available, also test Kempston and tape input. Num Lock
must switch the FE.D4/FE.D3 source without affecting the video Scroll Lock.
