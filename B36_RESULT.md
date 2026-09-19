# B36 result — BaseConf INT/NMI/breakpoint state machines

Date: 2026-09-19

## Scope

B36 is the isolated INT/NMI/breakpoint stage from the canonical B32 audit, renumbered after the inserted B34 media-lifecycle checkpoint. It preserves the accepted B35 configuration ports and does not add AVR/COM transactions, new peripherals, ULAplus or 4:4:4 rendering.

## Official contract implemented

- Frame INT begins at `int_start`, remains active for 256 master clocks and is released early by the Z80 interrupt-acknowledge cycle.
- The INT counter can be paused by the physical external-WAIT duration. Only the documented AVR/COM WAIT sources may use this hook; ordinary DRAM/turbo stalls do not. Those WAIT transactions remain the next separate stage.
- A falling edge of `#BF.D3` latches a deferred NMI and starts it at the following frame `int_start`.
- An enabled breakpoint compares the address of every M1 opcode fetch and starts NMI immediately on a match. The breakpoint remains enabled.
- The first NMI-handler opcode at `#0066` is forced to `NOP` while the old mapping is still visible. RAM page `#FF` is selected only after that M1, so the next byte at `#0067` comes from page `#FF`.
- Active NMI page `#FF` has priority over the virtual-FDD page `#FE`. `OUT (#BE),A` retains the underlying FDD state and removes page `#FF` only after two following M1 fetches.
- The normal inactive-NMI `#BE` path remains the accepted B31/B35 virtual-FDD exit.

## Verification

- Visual Studio 2022 MSBuild 17.14.51, full Release solution: PASS, 0 errors; only the two pre-existing missing-ruleset warnings remain.
- `InterruptNmiProbe-B36`: 41 PASS.
- B35 configuration ports: 539; B34 media swap: 33; IDE media/ports: 10/786; FDD/Rage: 14/113 — PASS.
- B30 video timing/controller/palette/TRD: 12748/321/1807/655922 — PASS.
- Audio path/DirectSound: 19/16 — PASS.

## Artifacts

- Before checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-before-20260919-175343\snapshot`.
- Runtime package: `K:\Download\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-20260919-181135\release`.
- After checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-after-20260919-181135`.
- Portable ZIP: `K:\Download\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-20260919-181135\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36.zip`.
- ZIP SHA-256: `B87C7FDC415E5F77DB35EFCA5D9143B9EEA3326A78E9600EB1323DC4EE332656`; clean extraction verified 92/92 files byte-identical, with the canonical 924-byte profile and no user state files.

## Runtime acceptance

The user completed a general B36 regression smoke test and reported that the previously working scenarios still behave as before, with no visible regressions. B36 is accepted as regression-safe in this observed scope. The new INT/NMI/breakpoint transitions were not exercised by a dedicated visible application test and remain verified by the 41-check compiled probe rather than claimed as direct runtime observation.
