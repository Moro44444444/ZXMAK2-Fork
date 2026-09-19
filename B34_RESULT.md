# B34 result — safe repeated SD/HDD media replacement

Date: 2026-09-19

## Scope

B34 changes only the host-side media replacement transaction and the PentEvo IDE reset state. Accepted B01–B33-R1 timing, video, audio, FDD/Rage, memory, ROM and port-decode paths were not redesigned.

## Change

- The SD image command now keeps the VM stopped through image replacement and the complete cold power-cycle. The emulation thread resumes only after firmware media state in RAM has been cleared.
- Selecting a different PentEvo HDD image automatically replaces the current image on `Apply`; a separate manual `Eject` is not required.
- Machine Settings compares the active and pending HDD path, read-only mode and geometry. Insert, replacement, eject or media-mode change performs a cold power-cycle while the VM remains stopped.
- The PentEvo/Nemo IDE reset now clears all 8/16-bit adapter latches and hard-resets both ATA devices. This prevents a half-word phase or pending ATA command from crossing into the next image.
- The existing safe open/validation, writable-by-default policy, automatic `.inf` geometry and persisted HDD configuration remain unchanged.

## Verification

- Full Release solution: PASS; only the two pre-existing missing-ruleset warnings remain.
- `MediaSwapProbe-B34`: 33 PASS, covering transaction order, HDD change/eject detection, and all Nemo IDE latches after reset.
- IDE media: 10 PASS; Nemo IDE ports: 786 PASS.
- FDD trap: 14 PASS; B30 video timing: 12748 PASS; palette: 1807 PASS.
- TRD shell: 655922 PASS; Rage SCL: 113 PASS.
- Audio path baseline: 19 PASS; DirectSound buffer: 16 PASS.
- Confirmed byte-unchanged against HEAD: PentEvo memory, ULA, SD controller implementation, AY and Engine bus code. `ZsdPentEvo` already had close/open/rollback and was not modified.

## Artifacts

- Before checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-before-20260919-135901`.
- After checkpoint with source snapshot and runtime: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-after-20260919-140643`.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-20260919-140643\release`.
- Portable ZIP: `K:\Download\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-20260919-140643\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34.zip`, SHA-256 `FB96133BCDB4451B5DA2912DD7D1A2BF62F4D5738AF15CF7CD726A5BC9E3F245`.
- ZIP verification: 86/86 files byte-identical after extraction; package contains the canonical 924-byte profile and no `.cmos`, `.nvram`, `.vmide`, `.log` or HDD image.

## Required runtime test

Runtime acceptance is not claimed. Test in one emulator process:

1. SD: insert image A, then select image B without manual eject, then A again.
2. HDD: select image A and Apply, then select image B directly and Apply, then A again; verify File Browser access after each cold start.
3. Test explicit HDD `Eject` followed by a new insert.
4. Smoke-check NedoOS file launch, Rage, B30 border/multicolor, Bad Apple and audio.

Automatic NedoOS `B.HDD boot` remains postponed and is not a B34 acceptance criterion.
