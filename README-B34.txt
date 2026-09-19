ZXMAK2 — ZX Evo BaseConf, B34 test build

What changed:
- repeated SD-card replacement now completes a cold power-cycle before emulation resumes;
- a new HDD image automatically replaces the old one on Apply, without mandatory manual Eject;
- HDD insert/replace/eject resets the PentEvo/Nemo IDE byte latches and ATA state;
- accepted B33-R1 HDD selection, writable defaults and automatic geometry remain intact.

Please test SD A -> B -> A and HDD A -> B -> A in one emulator process.
Also smoke-test NedoOS file launch, Rage, border/multicolor, Bad Apple and sound.

This is a test build. Runtime acceptance is pending.
Automatic NedoOS HDD boot is not part of B34.
