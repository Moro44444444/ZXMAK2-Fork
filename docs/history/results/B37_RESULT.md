# B37 result — BaseConf AVR/COM WAIT and DOS settling

Date: 2026-09-20

## Scope

B37 is the isolated WAIT/remaining-port stage fixed by the canonical B32
audit. It preserves the accepted B01–B36 implementation and does not add
Kempston joystick, tape/beeper mux, ULAplus, 4:4:4 rendering, contention,
floating bus, ZX-BUS or CD/ATAPI.

## Official contract implemented

- The AVR/gluclock `F7` family now follows the r1364 `zports.v` equations:
  low byte `F7`, A8 selected by normal versus Shadow mode, A13 selecting the
  address strobe and A14 selecting the data strobe. This includes the official
  aliases around `#DFF7/#DEF7` and `#BFF7/#BEF7` without intercepting `#EFF7`.
- Only an AVR data read/write starts external WAIT. Address-only writes do not.
  The response is completed synchronously before the port handler returns;
  because the FPGA/AVR sources define no fixed firmware-service latency, the
  emulator records the shortest observable one-master-clock handshake rather
  than inventing a constant MCU delay.
- COM is decoded by low byte `EF`, with A10:A8 selecting the documented
  `#F8EF..#FFEF` Kondratyev/16550-compatible registers. Read/write direction,
  DLAB, IER/MCR masks, FIFO reset/status, MSR side effect, scratch register and
  official AVR reset values are implemented from `pentevo/avr/current/rs232.c`.
  B37 models the board-visible registers; an optional physical host serial
  endpoint is not added.
- AVR/gluclock and COM waits use the B36 external-WAIT hook and therefore pause
  a still-active frame INT. Existing DRAM/video and 14-MHz ordinary I/O stalls
  do not pause INT. Concurrent timing delays compose by the RTL OR/max rule,
  not by summing independent stall budgets.
- Entering the DOS map on the documented `#3Dxx` M1 transition now applies the
  minimum three 28-MHz-master-clock settling stall from `atm_pager.v`. It is
  consumed by that opcode transaction and overlaps any longer DRAM delay.

## Verification

- Visual Studio 2022 MSBuild 17.14.51, full Release solution: PASS, 0 errors;
  only the two pre-existing missing-ruleset warnings remain.
- `WaitPortProbe-B37`: 197981 PASS — exhaustive WAIT/F7/COM aliases, F7
  read/write equations, UART register behavior, INT pause/acknowledge and DOS
  stall/overlap vectors.
- B36 INT/NMI: 41; B35 configuration ports: 539; B34 media swap: 33; IDE
  media/ports: 10/786; FDD/Rage: 14/113 — PASS.
- B30 video timing/controller/palette/TRD: 12748/321/1807/655922 — PASS.
- Audio path/DirectSound: 43/16 — PASS.
- Superseded historical probes whose assumptions were intentionally replaced
  by B21/B30/B31 or the expanded B37 F7 decoder are not used as acceptance
  gates; their current replacements above pass.

## Artifacts

- Before checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-before-20260920-212148\snapshot`.
- Runtime package:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-20260920-214709\release`.
- Portable ZIP:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-20260920-214709\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37.zip`.
- ZIP: 124 files, 5312578 bytes, SHA-256
  `2BE84C7B9824D52DA332BD2036022EAC6FFBF682C785AD183242A975430EFB28`;
  clean extraction verified 124/124 files byte-identical. This is the private
  runtime-test package and intentionally retains the current `.cmos/.nvram`
  and `.vmide` state; public GitHub portable-cleaning rules are unchanged.
- After checkpoint:
  `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-after-20260920-214709\snapshot`.

## Runtime acceptance

Pending the user's test. Automated build/probe success does not claim runtime
acceptance. Required smoke test: NedoOS, Rage with one short Enter, B30
border/multicolor, Bad Apple, normal sound, SD/HDD replacement, IDE file
access, and ordinary BaseConf startup. Dedicated external serial hardware is
not part of this test package.
