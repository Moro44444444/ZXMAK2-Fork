ZXMAK2 v49 — ZXM-MoonSound Rev.01 test build
=============================================

This is a separate local test build based on the frozen v48 SAASound build.
It does not replace v48 and is not yet a GitHub Alpha release.

What is added
-------------

ZX Evolution BaseConf now offers `ZXM-MoonSound Rev.01` in either physical
ZXBUS slot.  The emulation follows CPLD 1.00 and contains:

* Yamaha YMF278B/OPL4 at 33.8688 MHz;
* OPL3 FM plus 24-channel wavetable/PCM playback;
* 2 MiB YRW801-M instrument ROM and physical 1 MiB SRAM;
* low-byte ports #7E/#7F and #C4-#C7;
* the real PentEvo TR-DOS port gate.

Both connectors are equivalent.  NeoGS, ZX-MultiSound and MoonSound can be
chosen in either slot, subject to the two-slot limit.  The same board type
cannot be installed twice.  MoonSound needs no additional switches.

How to enable it
----------------

1. Open Machine Settings.
2. Select the PentEvo ZXBUS page.
3. Enable Slot 1 or Slot 2.
4. Choose `ZXM-MoonSound Rev.01` and Apply.

Required files and 32-bit mode
------------------------------

`ymfm_opl4.dll` is the BSD-licensed YMF278B engine.  Its license is included as
`ymfm-opl4.LICENSE.txt`.  Do not move either native sound DLL away from the
program folder.

This build remains 32-bit because v48 uses the supplied 32-bit SAASound.dll and
v49 adds a 32-bit ymfm bridge.  ZXMAK2.exe and Test.exe are intentionally
marked 32-bit.

MoonSound also requires an exact 2 MiB Yamaha YRW801-M ROM named either:

* YRW801-M - Yamaha - 1993.rom
* yrw801.rom

The ROM may be beside ZXMAK2.exe or in the `roms` folder.  It is a local user
file and must not be redistributed on GitHub without permission.

Checks before delivery
----------------------

* Test.exe /moonsound
* Test.exe /tsfm
* Test.exe /multisound
* Test.exe (all ULA sanity and renderer benchmarks)
* BaseConf two-slot configuration and XML round-trip probe

Hardware notes and sources are recorded in
`docs/ZXM-MOONSOUND-REV01-IMPLEMENTATION.md`.
