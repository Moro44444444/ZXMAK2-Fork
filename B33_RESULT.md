# B33 result — exact Nemo IDE port decode

Date: 2026-09-19

## Change

Only `IdePentEvo.BusInit()` was changed. The former subscription mask `(low & #1E)==#10` was replaced by the exact BaseConf r1364 decode:

- `(low & #1F)==#10`: `#10,#30,#50,#70,#90,#B0,#D0,#F0`;
- `(low & #1F)==#08`: `#08,#28,#48,#68,#88,#A8,#C8,#E8`;
- exact `#11` for the high data byte;
- exact `#C8` alternate-status/control handler registered before the generic `x08` family.

This directly implements `IS_NIDE_REGS(x) = x[2:0]==000 && x[3]!=x[4]` and `IS_NIDE_HIGH(x) = #11` from `base_trdemu/trunk/z80/zports.v` r1364. ATA register selection remains `A7..A5`; data sequencing and the ATA core were not changed.

## Verification

- Full Release solution build: PASS; only the two pre-existing missing-ruleset warnings remain.
- `IdePortDecodeProbe-B33`: 786 PASS across all 256 low-byte values, `#11`, `#C8` priority, and `A7..A5` selection.
- B31 FDD trap: 14 PASS.
- B30 video timing: 12748 PASS.
- B21 video controller: 321 PASS.
- B23 palette: 1807 PASS.
- B27 audio baseline: 19 PASS.
- B27 DirectSound: 16 PASS.
- Packaged `ZXMAK2.Hardware.dll` is byte-identical to the newly built Release output; the runtime contains the canonical B31 `ZXMAK2.vmz` and no `.cmos`, `.nvram`, `.vmide`, or `.log` state files.

No FDD, memory, NedoOS/Rage, video, audio, keyboard, SD, ROM, or ATA command/core code was modified.

## Artifacts

- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEPORTS-B33-20260919-084445`.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-IDEPORTS-B33-20260919-084445\release`.

## Runtime acceptance

Not claimed. The user should check an IDE/HDD image through normal BaseConf boot and at least one read/write operation. A short NedoOS, Rage, and B30 border/multicolor smoke check is useful as regression confirmation, but B33 does not intentionally touch those paths.
