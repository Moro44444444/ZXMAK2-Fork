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
| Nemo IDE | even families `x10` and `x08`, high data byte only at `#11`, alternate status `#C8` | exact decoder implemented in B33; media UI completed in B33-R1 | implemented; HDD/FAT runtime accepted |
| Gluclock/CMOS | `#DFF7/#DEF7` address, `#BFF7/#BEF7` data; transaction holds WAIT until AVR reply | functional address/data subset exists | port function partial; WAIT handshake absent |
| COM/AVR | `#F8EF..#FFEF`, WAIT until AVR reply | no device/handler | missing |
| `#BF` system | D0 shadow, D1 ROM write, D2 font write, D3 NMI request, D4 breakpoint enable; `base_trdemu` adds D5 4:4:4 palette | full latch/readback and D1/D3/D4 behavior implemented in B35/B36; D5 is latched | partial only until D5 renderer work |
| `#BD` config | indices `00..13`: pages, flags, latches, palette/font/border, breakpoint, write-disable, FDD mask | complete through B35/B36, including breakpoint address and existing FDD `13` | implemented; compiled probes pass |
| `#BE` | in `base_trdemu`: write clears NMI/virtual-FDD state; no config read | B36 implements delayed NMI clear and preserves accepted virtual-FDD behavior; compatibility read alias retained | implemented with documented compatibility alias |
| ULAplus | low byte `#3B`, register/data selected by `A14` | absent from PentEvo machine | missing documented built-in function |
| Kempston mouse | `#FADF/#FBDF/#FFDF` | implemented | conforms structurally |
| Kempston joystick | eight bits in `base_trdemu`; VG93 owns the overlapping family in Shadow/DOS | exact low-byte `#1F`, eight-bit host state and VG93-first arbitration implemented in B38 | implemented; compiled arbitration probe passes |
| Beeper/tape/Covox | `#FE` beeper or tape-out selected by AVR; tape-in on keyboard read; `#FB` Covox; hardware selects a source | B38 adds `xxFE/xxF6` tape-in and persistent AVR D3 mux between FE.D4/FE.D3; accepted AY/Covox/RejectDC paths are unchanged | implemented; runtime sound/tape check pending |

The emulator-only `#2F/#4F/#6F/#8F` handlers are the host/ERS communication mechanism. They are not presented as physical BaseConf ports and must remain isolated from the physical-port conformance map.

## Memory and paging

The main ATM/BaseConf paging equations, two maps, `#7FFD`, `#EFF7`, four 16 KiB windows, up to 4 MiB RAM, 512 KiB ROM limit, refresh-latched CPU clock selection, page-`#FE` virtual-FDD entry, and B31 write-protection lifecycle are implemented and have compiled probes.

Status after B36:

1. `#BF.D1` ROM-write enable was implemented in B35.
2. B36 implements the official `#0066` injected NOP, RAM page `#FF`,
   breakpoint address/M1 match and delayed `#BE` exit as one state machine.
3. Page `#FE` virtual FDD priority and restoration remain covered by regression
   probes and the accepted Rage/NedoOS path.
4. The remaining memory-map timing item is the short DOS-entry settling stall;
   its exact edge placement belongs to B37.

## Interrupts, reset, WAIT, and timing

### INT

The official frame INT starts at `int_start`, lasts up to 256 master clocks (32 base CPU tacts), pauses its counter while WAIT is active, and is released early on CPU interrupt acknowledge (`!IORQ && !M1` at the negative CPU edge).

Since B36 the ULA models the nominal 256-master-clock window, early interrupt
acknowledge release and a pause input for the official external WAIT source.
B37 connected the AVR/COM WAIT producers and their compiled coupling checks
pass; application-visible runtime acceptance remains with the user.

### NMI and breakpoint

B36 implements deferred frame-aligned NMI, immediate breakpoint NMI, the
`#0066` transition, RAM page `#FF` and delayed `#BE` exit as one hardware state
machine. It passed its compiled probe; no dedicated application-visible
runtime test has been performed.

### CPU/DRAM/IO timing

The 28 MHz master model, 3.5/7/14 MHz choices, refresh-boundary clock changes, 14 MHz DRAM arbitration/cache behavior, and the total external-I/O wait budget were added and have structural probes.

Remaining precision work:

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
- contention coupling around the already implemented B36 INT acknowledge;
- floating-bus behavior.

## Ordered correction plan after B32

Each item is a separate checkpoint with compiled/static probes plus user runtime testing. A later item must not be folded into an earlier one.

1. **B33 — Nemo IDE decode only — completed.** Exact r1364
   `x10/x08/#11/#C8` set; media UI completed in B33-R1.
2. **B35 — configuration-port contract — completed.** The separate accepted
   B34 media-lifecycle stage shifted the remaining build numbers by one.
3. **B36 — INT/NMI/breakpoint state machines — implemented.** General runtime
   regression accepted; dedicated visible INT/NMI test remains open.
4. **B37 — WAIT transactions and remaining built-in ports — implemented,
   runtime pending.** AVR gluclock WAIT, `#F8EF..#FFEF`, DOS settling stall and
   focused transaction probes are complete.
5. **B38 — built-in input/audio completion — implemented, runtime pending.**
   Kempston joystick and tape/mux behavior preserve the accepted sound path.
6. **B39 — ULAplus and 4:4:4 palette.** Implement as PentEvo overlays, not by
   attaching a second generic ULA; add renderer vectors and mid-frame tests.
7. **B40 — final timing/video certification.** Contention, floating bus, raster
   transitions, all seven renderer golden vectors, and focused WD1793
   command/status traces only if a mismatch is confirmed.
8. **After built-in conformance:** audit the two official ZX-BUS slots and only then expose documented pluggable peripheral cards.

### Execution status on 2026-09-20

The technical order above remains canonical. Build numbers moved by one after a separate B34 media-lifecycle checkpoint was inserted without changing the hardware sequence:

- Nemo IDE decode: completed in B33/B33-R1;
- safe repeated SD/HDD replacement: inserted and accepted as B34;
- configuration ports `#BF/#BD/#BE`: completed and runtime-smoked in B35;
- INT/NMI/breakpoint state machines: implemented in B36; general regression smoke accepted, dedicated visible INT/NMI test not performed;
- WAIT transactions, AVR/COM registers and DOS settling: implemented and
  compiled-regression checked in B37; user runtime smoke remains pending;
- built-in input/audio: implemented and compiled-regression checked in B38;
  user runtime smoke remains pending;
- next hardware stage after B38 acceptance: ULAplus and 4:4:4 palette (B39).

## Fixed implementation contract for B37–B39

The order and boundaries below are mandatory. Each build gets its own before
checkpoint, focused compiled probe, full B01–B36 regression run, runtime
package and journal entry. A successful build or probe does not constitute user
runtime acceptance.

### B37 — WAIT transactions and remaining built-in ports

Scope:

1. Implement the official AVR/gluclock transactions for the documented
   `#DFF7/#DEF7` address and `#BFF7/#BEF7` data families. A transaction must
   assert WAIT until the AVR-side reply at the same bus-cycle boundary used by
   the r1364 RTL.
2. Implement the documented COM/RS232 AVR family `#F8EF..#FFEF`, including its
   read/write selection, returned data and WAIT-until-reply lifecycle.
3. Add the short DOS-map settling stall at the documented transition edge;
   do not replace it with an instruction-level `CPU.Tact` correction.
4. Resolve overlap and release order of the existing memory/video/external-I/O
   WAIT sources at Z80 pin edges. Only the official external WAIT source may
   pause the B36 frame-INT counter.
5. Complete any remaining built-in port aliases that are inseparable from
   these transactions, based only on r1364 decode equations.

Required checks:

- exhaustive port-alias/read-write probe for gluclock and COM;
- WAIT assert/release and overlapping-source edge vectors;
- DOS-entry stall and B36 INT pause/acknowledge vectors;
- full established memory, FDD/Rage/NedoOS, IDE/media, video and audio
  regression suite;
- user runtime smoke before B37 can be called accepted.

Explicitly excluded from B37: Kempston joystick, tape/beeper mux, ULAplus,
4:4:4 palette, contention/floating bus, ZX-BUS and CD/ATAPI.

### B38 — built-in input and audio completion

Scope:

1. Add the BaseConf Kempston joystick with the official bit width and decode;
   preserve VG93 ownership of the overlapping low-byte family in Shadow/DOS.
2. Complete tape-in on the documented keyboard reads `xxFE/xxF6`.
3. Complete tape-out and the hardware-controlled beeper/tape source mux without
   changing the accepted AY, Covox, DirectSound and RejectDC paths.
4. Verify reset/default state and machine-profile wiring for these built-in
   devices. Do not introduce a ZX-BUS card as a substitute.

Required checks:

- exhaustive normal-versus-Shadow port arbitration probe;
- keyboard+tape and joystick bit vectors, reset and mux-transition vectors;
- audio baseline/DirectSound regression and silence/DC measurements;
- runtime checks for joystick, tape where a suitable image exists, normal
  music/sound, Rage/NedoOS and the accepted B30 border/multicolor scene.

Explicitly excluded from B38: ULAplus, 4:4:4 palette, final contention/video
certification, ZX-BUS and CD/ATAPI.

### B39 — ULAplus and official 4:4:4 palette

Scope:

1. Implement BaseConf ULAplus on low byte `#3B`, with register/data selection,
   read/write behavior and address/control gating taken from r1364.
2. Activate the already latched `#BF.D5` official `base_trdemu` 4:4:4 palette
   extension, including the documented address-derived low color bits.
3. Implement both features as overlays in `UlaPentEvo`; do not attach a second
   generic ULA and do not alter unrelated machine profiles.
4. Preserve palette state/readback, border behavior and deterministic
   mid-frame changes across all seven existing BaseConf renderer routes.

Required checks:

- register, palette RAM, reset and readback vectors;
- golden color vectors for ULAplus and 4:4:4 in all affected renderers;
- mid-frame palette/mode transition vectors;
- complete B30 border/multicolor and B23 palette regression plus the full
  B01–B38 suite;
- separate user visual runtime acceptance. Probe output alone is insufficient.

Explicitly excluded from B39: final contention/floating-bus certification,
unconfirmed VG93 changes, ZX-BUS and CD/ATAPI. Those remain B40 or later.

## B32 conclusion

B36 remains the last explicitly user-accepted conformance continuation point.
B37 and B38 are implemented and packaged, but neither is called runtime-
accepted until the user completes the corresponding smoke tests. The next
minimal hardware stage after B38 acceptance is B39: ULAplus and the official
4:4:4 palette extension.

B32 audit checkpoint: `backup/ZXMAK2-v13-ZXEVO-BC-AUDIT-B32-20260919-083728`.

### B39 correction status — 2026-09-21

- The first combined B39 attempt (ULAplus plus 4:4:4) was rejected by runtime
  testing because it regressed FDD sound and ATM 16-color software while its
  ULAplus path did not work.  It is not a continuation point.
- B39A restarts from accepted B38 / Alpha 0.4 and implements only the official
  `#BF.D5` 4:4:4 extension.  The D5=0 route is unchanged and ULAplus is
  explicitly deferred to a separate later checkpoint.
- B39A Release and the full compiled B23-B38 regression set pass. Пользователь
  принял runtime-проверку: Rage доходит до конца с чистыми SD/IDE-образами,
  B23 проходит все семь слотов с 4-ГБ образом, border/multicolor сохранены и
  3.5/7/14 МГц распознаются корректно. B39A — текущая принятая локальная база;
  ULAplus по-прежнему не входит в неё и должен быть отдельным будущим этапом.

### Следующая сборка: B40 — финальная conformance-проверка timing/video

В B40 входят только: (1) evidence-based phase refinement contention и
floating bus в пределах r1364, (2) mid-frame transitions raster/mode/palette
и их связь с INT/video phase, (3) golden-векторы всех семи renderer-путей,
(4) command/status trace VG93 без изменения его поведения до подтверждённого
расхождения. B40 не включает ULAplus, медиа-утилиту, SD/HDD lifecycle,
ZX-BUS, CD/ATAPI или новую периферию.

## Continuation: post-Alpha 6 static code review — 2026-09-22

### Scope and non-mutation guarantee

This is a read-only review of the Alpha 6 tree after B40 / Alpha 5.  No source
file, resource, configuration, binary, package, tag or release was changed;
no build or emulator/test executable was run.  `git diff --check
B40-stable..HEAD` produced no whitespace or merge-conflict diagnostics.  The
reviewed baseline was commit `3eafc0e` (`Fix test benchmark profile
isolation`), following `5479c7d` (`Add no-border display mode`) and `8ac7681`
(`Prepare Alpha 6 portable release`).

The untracked `src/_binverify/` directory was observed but deliberately left
outside the review and untouched.

### Confirmed architectural risk: machine-profile state inheritance (P1)

`BusManager.LoadConfigXml` retains old device instances and reuses an instance
when the incoming XML declares the same device type.  Device configuration is
then loaded into that existing instance.  `BusDeviceBase.LoadConfigXml` does
not first reset the instance to device defaults.  Several device loaders use
the current property value as the fallback when an XML attribute is absent.

This is observable in the profile set: a number of TapeDevice and Keyboard
nodes omit attributes that other profiles declare explicitly.  Thus, after a
machine switch, a missing target-profile attribute can retain a value from the
previous machine.  This is the principal static risk for cross-machine
behaviour, including tape parameters, keyboard mapping and mouse parameters.

It must not be "fixed" by recreating every device indiscriminately: retaining
the inserted/open tape while switching machines is a desired user workflow.
The correct future change is to separate session state (for example the loaded
tape) from hardware/profile configuration, then always apply the latter from
the target profile.  It requires a focused machine-switch regression matrix.

Relevant implementation:

- `src/ZXMAK2.Engine/BusManager.cs` (`LoadConfigXml`);
- `src/ZXMAK2.Engine/Entities/BusDeviceBase.cs` (`LoadConfigXml`);
- `src/ZXMAK2.Hardware/General/KeyboardDevice.cs` (`OnConfigLoad`);
- `src/ZXMAK2.Hardware/General/TapeDevice.cs` (`OnConfigLoad`);
- `src/ZXMAK2.Hardware/General/KempstonMouseDevice.cs` (`OnConfigLoad`).

### Code-quality findings

1. **Tracked stale DirectX renderer duplicate (P2).**
   `src/ZXMAK2.Host.WinForms/Mdx/Renderers.mdx/` contains old renderer source
   copies, including `VideoRenderer.cs`.  The WinForms project compiles only
   `Mdx/Renderers/`, not `Renderers.mdx`; therefore the duplicate has no
   runtime effect, but differs from the compiled No Border implementation and
   is a maintenance hazard.  Remove or archive it later in a standalone
   cleanup commit, not mixed with hardware work.
2. **Toolbar binds by display text (P2).**
   `MainView.RegisterMediaCommand` finds FDD/HDD/SD commands by captions such
   as `Open SD Card image...` and `Eject SD Card`.  The present captions work,
   but a rename or localization would silently break the binding.  A later
   cleanup should bind a stable command ID/tag rather than UI text.
3. **Legacy visual workarounds (P3).**
   `FormMachineSettings` contains an old PortFE preservation/reset workaround
   and a zero-height context-menu workaround.  They are host-UI technical debt
   only; this review found no path from them to PentEvo ports, timing or media
   state.

### Alpha 6 No Border review

No Border is host-side source-rectangle cropping only.  `IFrameVideo.ActiveArea`
defines the active picture; both DirectX and GDI renderers select that area
before the existing scale modes are applied.  `FrameVideo` clips the active
rectangle to the actual frame.  Consequently No Border does not change memory
mapping, ULA port handling, INT timing, contention or the emulated machine.

The DirectX clone is recreated if ActiveArea changes, avoiding stale source
dimensions.  The toolbar artwork is refreshed only when media mount state
changes, not allocated afresh on every status tick.

Small scope gap (P2): `SprinterRenderer` does not supply an ActiveArea, so No
Border has no crop to apply on that machine.  BaseConf/Evo/ATM renderer paths
do supply active rectangles and are not affected by this gap.

### BaseConf hardware conformance review

The implementation was structurally compared with the frozen official NedoPC
r1364 RTL snapshot recorded above (`fpga.r1364/baseconf` and
`base_trdemu`).  The comparison covered the decode/mapping equations and
state transitions; it is not a claim of oscilloscope-level proof for every
possible raster-phase transition.

The following current implementation groups match the relevant RTL structure:

- **Memory:** 256 RAM pages of 16 KiB (4 MiB), 32 ROM pages (512 KiB), normal
  and Shadow/DOS map selection, `#7FFD/#7FFC`, `#EFF7`, `#xx7F/#xxFF`,
  `#xxBF` and `#xxBE` families.
- **IDE:** normal/shadow C8 alternate-status and the x10/x08 IDE decoder
  families.
- **SD:** normal/shadow `#xx57` data and normal `#xx77` control family.
- **CMOS / GLUClock / COM:** `#DFF7/#DEF7` address, `#BFF7/#BEF7` data and
  the `#F8EF..#FFEF` COM family.
- **FDD:** `#13BD` virtual-drive selection and physical WD93 suppression.
- **INT and fast I/O:** active-low INT, a 256-master-clock base window,
  extension through external WAIT, release on INT acknowledge, and the
  six-master-clock external 14 MHz I/O duration.

One documented compatibility divergence remains intentional: the emulator
also accepts configuration reads through `#xxBD` for ERS compatibility,
although the physical configuration-multiplex read is `#xxBE` and `#xxBD`
normally has breakpoint-address meaning.  This alias is not a random fallback
and must not be removed without an ERS compatibility regression test.

### Accuracy boundaries, not newly confirmed regressions

1. Raster/display routing is committed at a software frame boundary in places
   where hardware may respond to a current horizontal/vertical phase.
2. The AVR response delay uses the shortest deterministic host-side model;
   actual firmware service latency is not a fixed FPGA constant.
3. `UlaPentEvo` retains an old `c_ulaFlashPeriod = 25` "TODO: check" note;
   it concerns flash-phase fidelity, not the accepted media/port/INT paths.

The official RTL itself includes a TODO around exact contention signal
synchronisation.  Therefore, if a particular demo exposes a timing mismatch,
the appropriate next step is a narrow port/tact trace and golden vector for
that case, not a broad timing rewrite.

### Test-program and release conclusions

The recent `Test` change explicitly loads its embedded test-machine profile
after reset, isolating the benchmark from the portable release's default
ZX-Evo BaseConf profile.  Static review found no error in that correction.
`Test.exe` still raises its process/thread priority, so it is a benchmark and
diagnostic tool rather than a passive end-user launcher.

No new source-side failure was found in the accepted SD/HDD/FDD lifecycle,
media status indicators, reset logic, or the BaseConf port/memory/INT mapping.
Alpha 6 remains an appropriate stable rollback point.  The safest later work
order is: (1) profile-state/configuration separation with switch tests,
(2) stale-renderer cleanup, (3) stable media command IDs, and only then
(4) evidence-driven raster/WAIT refinement if a reproducible program needs
it.
