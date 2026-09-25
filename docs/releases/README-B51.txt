ZXMAK2 v51 — ZX Evolution BaseConf / ZX-MultiSound Max test build
=================================================================

This is a separate local test build based on Beta 1 (v50). It does not
replace Beta 1 or the original ZX-MultiSound Rev.A2. It has not been
published on GitHub.

What changed
------------

* ZX-MultiSound Max is a separate ZXBUS card choice for either slot.
  The existing ZX-MultiSound Rev.A2 remains selectable.
* Each MultiSound card has its own optional TSFM-compatible SAA1099 port
  switch. Both use the included SAASound.dll; their chip states are separate.
* Max has 2 MiB GS RAM, external GS firmware and OPL3 FM at #C4-#C7.
  If MoonSound is installed, it owns those shared OPL ports instead.
* Slot order resolves shared YM/SAA/GS/SoundDrive port ownership when both
  MultiSound cards are installed. Invalid manual port conflicts are rejected.
* View > EVO Display Mode shows the eight PentEvo TV/VGA raster choices and
  checks the active mode. The menu is disabled for other machines.
* VM > Warm Reset now shows only F12. The old Alt+Ctrl+Insert binding was
  removed. Ctrl+F12 and Ctrl+Alt+F12 remain CMOS and Factory Reset.

Firmware and limitations
------------------------

ZX-MultiSound Max requires the original firmware file
`roms\ZX-MultiSound-Max-GS.bin` (512 KiB). The local test package contains
the copy supplied in ZxMultiSoundMax.zip. The firmware is not committed to
the source repository; anyone building from source must supply their own
lawfully obtained copy.

The external SAM2695 MIDI module on the physical Max card is not emulated.
This build implements the board's host-visible sound paths and OPL3, not a
complete model of every external component. Yamaha MoonSound ROM distribution
rights must likewise be checked before publishing any portable package.

The program and tests are 32-bit because the native SAASound and ymfm sound
libraries in this build are 32-bit.

Checks
------

Run `Test.exe /multisoundmax`, `/multisound`, `/tsfm`, `/moonsound`,
`/evo-display` and `/zxnetusb`. Sound still needs listening tests on the
user's actual music collection before this build can be called stable.
