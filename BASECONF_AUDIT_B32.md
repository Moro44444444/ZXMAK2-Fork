# ZX-Evolution BaseConf conformance audit — B32

Date: 2026-09-19

## Scope and authority

This is a static audit. B32 does not change production emulator code and does not claim runtime acceptance.

Normative source is the exact NedoPC FPGA Subversion snapshot `pentevo-fpga.r1364.tar.gz`:

- size: `5,556,455` bytes;
- SHA-256: `7A509FBCEF3AF85380EC475AD622A0682E714AF54625851FCB9AA678DA0BBB82`;
- BaseConf baseline: `baseconf/trunk`;
- official virtual-FDD extension used by this fork: `base_trdemu/trunk`.

The current GitHub HEAD is not mixed into the normative map. The `base_trdemu` differences are treated as an official layer over BaseConf because the accepted B31 configuration uses page `#FE`, mask register `#13BD`, and ERS virtual-FDD handling.

## Baseline integrity

The current production sources `MemoryPentEvo.cs`, `FddPentEvo.cs`, and `UlaPentEvo.cs` match the accepted B31 checkpoint byte-for-byte. Common files in `src/_binrelease` also match the B31 release. The only expected difference is the user's local `ZXMAK2.vmz`; it was not overwritten.

The unchanged accepted B31 binaries were rechecked with the existing compiled contracts: FDD trap `14` PASS, B30 video timing `12748` PASS, and BaseConf video controller `321` PASS.

Accepted B31 runtime observations remain the regression floor:

- NedoOS loads;
- Rage loads and runs;
- B30 border/multicolor remains correct;
- Bad Apple starts from the BaseConf browser, menu, and NedoOS.

## Port and device map

| Area | r1364 contract | Current state | Audit result |
|---|---|---|---|
| Keyboard | reads on low byte `#FE` and `#F6`; `#FC` is not a keyboard read | keyboard mask catches `#FE/#F6`; no `#FC` keyboard read | conforms structurally |
| Border | writes on `#FE/#F6/#FC`; four-bit color `{~A3,D2..D0}` | all three aliases and extended border are implemented | accepted by B30/B31 runtime |
| AY | `#FFFD/#BFFD` families, low byte `#FD` | AY device uses mask `#C0FF` | conforms structurally |
| `#7FFD` | low `#FD` or `#FC`, `A15=0`, with lock | both aliases present | covered by previous memory probes |
| `#EFF7` | normal/shadow address gating, `A12=0` | implemented | covered structurally; edge timing remains open |
| SD | normal `xx77/xx57`, shadow split of `xx57` by `A15` | implemented, including buffered reads | previously tested; exact SPI edge timing not certified |
| VG93 | `#1F/#3F/#5F/#7F/#FF` in shadow; `base_trdemu` drive mask | implemented; B29/B31 virtual-FDD path accepted | preserve as regression floor |
| `#13BD` | low four bits select drives handled by virtual FDD | implemented | accepted by Rage/NedoOS tests |
| Nemo IDE | even families `x10` and `x08`, high data byte only at `#11`, alternate status `#C8` | mask `(addr & #1E)==#10` misses the `x08` family except `#C8` and accepts invalid odd aliases | **confirmed decode defect** |
| Gluclock/CMOS | `#DFF7/#DEF7` address, `#BFF7/#BEF7` data; transaction holds WAIT until AVR reply | functional address/data subset exists | port function partial; WAIT handshake absent |
| COM/AVR | `#F8EF..#FFEF`, WAIT until AVR reply | no device/handler | missing |
| `#BF` system | D0 shadow, D1 ROM write, D2 font write, D3 NMI request, D4 breakpoint enable; `base_trdemu` adds D5 4:4:4 palette | only D0 and D2 have behavior; readback is truncated to low nibble | partial |
| `#BD` config | indices `00..13`: pages, flags, latches, palette/font/border, breakpoint, write-disable, FDD mask | `00..0C`, `12`, and FDD `13` exist | `0D..11` missing; breakpoint writes missing |
| `#BE` | in `base_trdemu`: write clears NMI/virtual-FDD state; no config read | write exits virtual-FDD state; compatibility config read also accepted | NMI behavior partial; extra read alias is non-normative |
| ULAplus | low byte `#3B`, register/data selected by `A14` | absent from PentEvo machine | missing documented built-in function |
| Kempston mouse | `#FADF/#FBDF/#FFDF` | implemented | conforms structurally |
| Kempston joystick | eight bits in `base_trdemu` | no joystick device in machine profile | missing |
| Beeper/tape/Covox | `#FE` beeper or tape-out selected by AVR; tape-in on keyboard read; `#FB` Covox; hardware selects a source | beeper and Covox exist, but no tape device/mux | partial; do not disturb accepted B25–B27 audio path without focused tests |

The emulator-only `#2F/#4F/#6F/#8F` handlers are the host/ERS communication mechanism. They are not presented as physical BaseConf ports and must remain isolated from the physical-port conformance map.

## Memory and paging

The main ATM/BaseConf paging equations, two maps, `#7FFD`, `#EFF7`, four 16 KiB windows, up to 4 MiB RAM, 512 KiB ROM limit, refresh-latched CPU clock selection, page-`#FE` virtual-FDD entry, and B31 write-protection lifecycle are implemented and have compiled probes.

Open differences:

1. `#BF.D1` ROM-write enable is not implemented.
2. The official NMI path temporarily executes `#0066` from ROM, injects a zero byte there, then maps RAM page `#FF` into `0000-3FFF`; this state machine is absent.
3. Breakpoint address registers and M1-match immediate NMI are absent.
4. `#BE` clear in RTL is delayed across refresh edges; the current virtual-FDD exit is a narrower software approximation.
5. DOS-entry switching includes a short hardware stall while the ROM mapping settles; exact edge placement is not certified.

## Interrupts, reset, WAIT, and timing

### INT

The official frame INT starts at `int_start`, lasts up to 256 master clocks (32 base CPU tacts), pauses its counter while WAIT is active, and is released early on CPU interrupt acknowledge (`!IORQ && !M1` at the negative CPU edge).

Current ULA uses the correct nominal 32-base-tact window and documented raster origin, but does not model early acknowledge release or WAIT-paused pulse length. Status: **partial**.

### NMI and breakpoint

The official deferred frame-aligned NMI request, immediate breakpoint NMI, `#0066` transition, RAM page `#FF`, and delayed `#BE` exit are not represented as one hardware state machine. Status: **missing**.

### CPU/DRAM/IO timing

The 28 MHz master model, 3.5/7/14 MHz choices, refresh-boundary clock changes, 14 MHz DRAM arbitration/cache behavior, and the total external-I/O wait budget were added and have structural probes.

Remaining precision work:

- exact separated Z80 pin-edge placement of the external-port wait pattern;
- overlap between WAIT sources;
- DOS-map settling stall;
- gluclock/COM WAIT-until-AVR transactions;
- exact contention phase and floating bus;
- mid-frame activation edge of raster/mode changes.

The official `zclock.v` itself labels exact contention synchronization as unfinished, so any refinement must follow observable r1364 logic and documented timing, not invent a new model.

### WD1793

The core has a Force Interrupt (`#D0` family) implementation and DRQ/INTRQ state handling. This audit found no basis for changing it before a focused command/status trace. It remains a verification item rather than a confirmed defect.

## Video modes

All seven renderer routes present in r1364 are implemented:

1. ZX attribute mode;
2. Pentagon hardware multicolor;
3. Pentagon 16-color mode;
4. ATM 320x200 16-color mode;
5. ATM 640x200 hardware multicolor;
6. ATM 80x25 text;
7. BaseConf one-page 80x25 text.

The generic-document modes `256c` and `16+16c` are not present in r1364 BaseConf and must not be added as BaseConf modes.

B30/B31 user testing accepts border/multicolor output. Remaining video work is conformance rather than a rollback:

- golden vectors for every renderer;
- exact mid-frame mode/raster transitions;
- ULAplus;
- `base_trdemu` 4:4:4 palette extension (`#BF.D5` and address-derived low color bits);
- INT acknowledge and contention coupling;
- floating-bus behavior.

## Ordered correction plan after B32

Each item is a separate checkpoint with compiled/static probes plus user runtime testing. A later item must not be folded into an earlier one.

1. **B33 — Nemo IDE decode only.** Replace the broad/wrong mask with the exact r1364 `x10/x08/#11/#C8` set. No memory, video, FDD, or timing changes.
2. **B34 — configuration-port contract.** Complete `#BF/#BD/#BE` read/write decoding and readback without yet activating NMI or ULAplus behavior. Remove or explicitly quarantine the non-normative `#BE` config-read alias only after boot compatibility tests.
3. **B35 — INT/NMI/breakpoint state machines.** Add early INT acknowledge, WAIT-paused INT duration, frame-aligned/immediate NMI, `#0066`, page `#FF`, breakpoint registers, and delayed clear as one documented unit.
4. **B36 — WAIT transactions and remaining built-in ports.** AVR gluclock WAIT, `#F8EF..#FFEF`, DOS settling stall, then exact I/O pin-edge tests.
5. **B37 — built-in input/audio completion.** Kempston joystick and tape/mux behavior, preserving the already accepted sound path.
6. **B38 — ULAplus and 4:4:4 palette.** Implement as PentEvo overlays, not by attaching a second generic ULA; add renderer vectors and mid-frame tests.
7. **B39 — final timing/video certification.** Contention, floating bus, raster transitions, all seven renderer golden vectors, and focused WD1793 command/status traces.
8. **After built-in conformance:** audit the two official ZX-BUS slots and only then expose documented pluggable peripheral cards.

## B32 conclusion

B31 is a valid continuation point and must not be rolled back. The first minimal, high-confidence correction is the isolated Nemo IDE address decoder. Runtime acceptance of any future build remains the user's decision.

B32 audit checkpoint: `backup/ZXMAK2-v13-ZXEVO-BC-AUDIT-B32-20260919-083728`.
