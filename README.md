# ZXMAK2

## ZX Spectrum Emulator - Virtual Machine

The project written in C# and needs only .NET framework 4 and DirectX 9.
Managed DirectX was removed and replaced with direct calls to native DirectX.
This is why it can run on any windows from Windows XP to Windows 10.
You can run it even on Linux under the Mono. But the graphics will be not so good as on DirectX.

This emulator is designed in the way to be Virtual Machine style. You can change emulated hardware just on the fly.

Full support for Windows XP/Vista/7/8/10 x86/x64.
Don't forgot to install DirectX 9!


![ZX Evo BaseConf — EVO Reset Service](docs/images/zx-evo-baseconf.png)


## Supported ZX Spectrum models

The following ZX Spectrum clones are supported:
* ZX Spectrum 48 (contended memory)
* ZX Spectrum 128 (contended memory)
* ZX Spectrum +3 (contended memory, but currently without FDD)
* Pentagon 128/512/1024
* SCORPION 256/1024, PROF-ROM 256/1024
* ATM 4.50
* ATM 7.10
* ZX Evo BaseConf
* Santaka 002
* PROFI 3.xx
* PROFI 5.xx
* SPRINTER (except spectrum config)
* QUORUM 64
* QUORUM 256
* Leningrad 1
* BYTE 48K
* LEC 48/528
* Other (custom configuration and plugins, for example LEC mod)

## History

The project was previously hosted on CodePlex. 
Here is the old link: https://archive.codeplex.com/?p=zxmak2

You can also visit ZXMAK2 discussions:
- English: https://www.worldofspectrum.org/forums/discussion/39647/
- Russian: http://zx-pk.ru/threads/16830-zxmak2-virtualnaya-mashina-zx-spectrum.html

You may also be interested about this emulator history:
- ZXMAK.NET - released in 2005-2008: https://sourceforge.net/projects/zxmak-dotnet/files/zxmak-dotnet/
- ZXMAK - first ZXMAK emulator written in C++, released in 2001-2003: http://zxmak.narod.ru/


## What's new

### ZX-Evo BaseConf Alpha 0.1 — B33-R1

- Reworked ZX-Evolution BaseConf emulation against the official r1364 documentation: memory paging, hardware ports, timing, interrupts, palette and all seven documented video modes.
- Fixed border/multicolor phase, mid-frame video switching and several audio timing/DC issues.
- Fixed the PentEvo virtual FDD path used by ERS: Rage now starts with one short Enter, while NedoOS and Bad Apple compatibility is preserved.
- Corrected Nemo IDE port decoding and added convenient HDD image selection, persistence and ejection in Machine Settings. HDD access, FAT browsing and file launching have been runtime-tested.
- Disk images opened through the normal UI are writable by default; write protection remains available as an explicit option.
- Added a clean portable ZX-Evo BaseConf package for testing: [download the Alpha 0.1 portable ZIP](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.1-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-0.1.zip) or [view the release notes](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.1-alpha).

Known Alpha limitation: replacing an SD or HDD image for a second time in the same emulator process may still require restarting the emulator. Automatic OS boot from HDD will be handled separately.
