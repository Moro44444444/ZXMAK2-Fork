# B33-R1 result — IDE media UI and writable media defaults

Date: 2026-09-19

## Scope

B33-R1 adds only media selection and persistence around the already corrected B33 PentEvo/Nemo IDE implementation. The B33 I/O decode, ATA register/data path, FDD/Rage/NedoOS logic, memory, video and audio were not redesigned.

## HDD configuration

- `IDE PentEvo` now has its own Machine Settings panel with `HDD connected`, image path, `...`, `Eject`, and `Read only`.
- A newly selected HDD is read/write by default. `Read only` is enabled only by an explicit user choice or when the host file cannot safely be opened for writing.
- `Apply` transfers the settings through the normal machine reconnect; the controller remains installed when no HDD image is connected.
- The selected path, CHS, exact LBA and read-only state are stored in the machine configuration. The legacy `.vmide` descriptor remains compatible but is maintained automatically and no longer needs hand editing.
- Raw image length must be a positive multiple of 512 bytes. Exact LBA is always `file length / 512`; partitions and file contents are never changed.
- A same-name `.inf` is read automatically when it contains a valid matching `Cylinders`/`Cilinders`, `Heads`, `Sectors` and optional `LBA`. Without a valid `.inf`, deterministic compatible CHS is calculated while exact LBA remains authoritative.

For `L:\Work_two\ZX\ZX_IMG\ATM_HDD.hdd`, automatic detection is `CHS 400/16/63`, `LBA 403200`, matching both its 206438400-byte length and `ATM_HDD.inf`.

## Writable defaults

- `File -> Open` now presents `Read only` unchecked.
- FDD A-D browse now presents `Write Protect` unchecked.
- Empty FDD slots display write protection unchecked; an already mounted/saved disk preserves its explicit protection state.
- ZIP media keeps the pre-existing forced write protection.

## Verification

- Full Release solution: PASS; only the two pre-existing missing-ruleset warnings remain.
- New `IdeMediaProbe-B33-R1`: 10 PASS, including ATM_HDD geometry/LBA, writable/read-only selection, disconnect, and invalid non-512-byte image rejection.
- Machine Settings discovery contract: PASS; the packaged WinForms assembly exposes the exact `IdePentEvo` configuration control signature expected by `FormMachineSettings`.
- B33 IDE port decode: 786 PASS.
- B31 FDD trap: 14 PASS.
- B30 video timing: 12748 PASS.
- B21 video controller: 321 PASS.
- B23 palette: 1807 PASS.
- Rage SCL: 113 PASS.
- B27 audio baseline: 19 PASS.
- B27 DirectSound: 16 PASS.
- Packaged production DLL hashes match the current Release build. The package starts with the canonical 924-byte B31 profile and contains no `.cmos`, `.nvram`, `.vmide`, or `.log` state files.

## Artifacts

- Before checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-before-20260919-090000`.
- Core checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-core-20260919-091500`.
- UI checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-ui-20260919-093000`.
- Final checkpoint with the packaged runtime: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-20260919-093500`.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-20260919-093000\release`.

## Runtime acceptance

Not claimed. The user should select `ATM_HDD.hdd`, confirm `400/16/63` and `403200`, boot NEM, perform a write/read across restart, test `Eject`, and verify writable-by-default FDD loading through both File Open and Machine Settings. NedoOS, Rage, B30 border/multicolor and audio remain smoke regressions for the same run.
