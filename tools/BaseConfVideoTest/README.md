# BaseConf Video Test TRD

`BCVIDTEST.TRD` is the incremental ZX-Evo BaseConf video-conformance disk.

B23 contains the bootable shell and a format-aware high-contrast pattern
for every r1364 mode slot. It prepares the required physical RAM pages for
ZX/HWM, packed 16-colour, 320/640-wide and both text layouts, loads the
Spectrum ROM font into the BaseConf character generator, selects the documented
RG/RGEX route and waits for the next frame boundary with interrupts disabled.
This prevents the Spectrum ROM interrupt handler from writing its `#5Cxx`
system variables into physical page 5, where ATM 640x200 HWM would display
them near line 179. Physical pages are mapped
through the documented `#FFF7/#F7F7` BaseConf manager path; the active map and
the original `C000` descriptor read through `#xxBE` are preserved and restored.
Every screen carries its own format label; visual acceptance remains a user
runtime test.

Controls:

- `Q` / `A`: move selection;
- `Enter`: open selected slot;
- `1`..`7`: open a slot directly;
- `Space`: return from a slot to the menu.

Build with the pinned sjasmplus binary kept in the B21 backup:

```powershell
New-Item -ItemType Directory -Path tools\BaseConfVideoTest\build -Force
& <sjasmplus.exe> --nologo tools\BaseConfVideoTest\BcVideoMenu.asm
```

The assembler source creates a 640 KiB TR-DOS image containing an autostart
`boot.B` BASIC loader and `BCMENU.C` at address 32768. The loader explicitly
enters TR-DOS with `USR 15619` before issuing the disk `LOAD`; a plain BASIC
`LOAD` would address the tape loader after the boot program resumes in ROM.
