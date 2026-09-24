ZXMAK2 v47 — ZX-Evo BaseConf Alpha 8 SAA port compatibility test
Date: 2026-09-24

Purpose
-------
This is a separate test build based on the accepted v46 audio implementation.
The v46 folder remains untouched. It adds optional cross-board SAA1099 port
compatibility without changing either board's native default behaviour.

Why two modes are needed
------------------------
- arcane2.trd uses the TurboSound FM Pro SAA protocol: selector #FFFD and
  register/data cycles through #FFFD/#BFFD.
- 1099test.trd uses the ZX-MultiSound protocol: direct #01FF/#00FF writes.
- Both contain SAA1099 music, but the host boards expose the chip differently.

Settings
--------
1. ZX-MultiSound in ZXBUS Slot 1 or Slot 2:
   enable SAA1099, then enable "TSFM SAA port compatibility" directly beneath
   the occupied slot. This is the mode to try with arcane2.trd.
2. TurboSound FM Pro selected in Music:
   enable "MultiSound SAA port compatibility". This is the mode to try with
   1099test.trd.

Both options default to off and are saved in the machine configuration. No
automatic protocol guessing is performed. Native software should be tested
first with the compatibility option left off.

Automated verification
----------------------
- Test.exe /multisound: native D1/D2, SAA preload, GS, SounDrive, ROM lock and
  conflict policy PASS; optional TSFM #FFFD/#BFFD SAA ports PASS.
- Test.exe /tsfm: native D1/D2/FM/SAA and SAA channel coverage PASS; optional
  MultiSound #01FF/#00FF SAA ports PASS.
- Full Test.exe: all ULA sanity tests PASS and renderer performance remains in
  the normal hundreds-of-milliseconds range.

This is a local test build, not a GitHub Alpha 8 release.
