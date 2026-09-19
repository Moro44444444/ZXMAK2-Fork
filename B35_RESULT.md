# B35 result — BaseConf configuration ports `#BF/#BD/#BE`

Date: 2026-09-19

## Scope

B35 implements the configuration-port stage already defined by the B32 audit. It does not implement the next INT/NMI/breakpoint state-machine stage and does not enable 4:4:4 rendering. The accepted B34 media lifecycle remains unchanged.

## Change

- `#BF` now reads back the complete six-bit BaseConf latch: D0 shadow, D1 ROM write, D2 font write, D3 NMI request, D4 breakpoint enable and D5 4:4:4 enable; D7:D6 read as zero.
- BF.D1 now gates writes to physically mapped ROM pages and remains subordinate to the existing per-window write-disable mask.
- `#BD` indices `0D..12` now expose palette, font, border, breakpoint address and write-disable readback. Indices `10/11` also write the low/high breakpoint address bytes.
- Exact `#13BD` ownership remains with `FddPentEvo`; the B29/B31 virtual-FDD route is not intercepted by the general configuration handler.
- `#BE` is decoded as the common clear/return strobe and continues to leave the page-`#FE` virtual-FDD handler. The older `#xxBE` read alias remains for firmware compatibility.
- Font readback observes the retained output of the currently active ATM/BaseConf text renderer without changing rendering state.

## Verification

- Full Release solution: PASS; only the two pre-existing missing-ruleset warnings remain.
- `ConfigPortProbe-B35`: 539 PASS.
- B34 media swap: 33; IDE media: 10; Nemo IDE ports: 786; FDD trap: 14; Rage SCL: 113 — PASS.
- B30 video timing: 12748; BaseConf video controller: 321; palette: 1807; TRD shell: 655922 — PASS.
- Audio path baseline: 19; DirectSound buffer: 16 — PASS.

## Artifacts

- Before checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-before-20260919-170627`.
- Runtime package: `K:\Download\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-20260919-172210\release`.
- After checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-after-20260919-172210`.
- Portable ZIP: `K:\Download\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-20260919-172210\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35.zip`, SHA-256 `2D9D45D7570FD9C30E77E7E6181C15A9B5C06847056CADB3A29E699A3D07E2D8`.
- ZIP verification: 89/89 files byte-identical; the package contains the canonical 924-byte profile and no `.cmos`, `.nvram`, `.vmide` or `.log` state.

## Required runtime test

Runtime acceptance is not claimed. Smoke-test NedoOS, Rage with one short Enter, the accepted B30 border/multicolor fragment, Bad Apple, sound, and repeated SD/HDD replacement. The new latches mainly prepare the next NMI/breakpoint stage; no program-specific behavior was added.
