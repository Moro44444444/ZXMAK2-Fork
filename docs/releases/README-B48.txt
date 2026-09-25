ZXMAK2 v48 — SAA1099 SAASound.dll evaluation build
===================================================

This is a local, experimental build based on the frozen Alpha 8 candidate
(v47).  It does not replace Alpha 8 and must not be published or used as a
GitHub release.

What is different
-----------------

Both SAA1099 renderers use the supplied native SAASound.dll backend:

* TurboSound FM Pro Rev. C SAA1099;
* ZX-MultiSound Rev.A2 SAA1099.

All board port decoding, the optional cross-board compatibility switches and
the existing mixer timing remain unchanged.  The DLL receives writes at their
original position in a video frame and produces 16-bit stereo at the current
host mixer sample rate.

Important
---------

SAASound.dll is a 32-bit native library.  Therefore ZXMAK2.exe and Test.exe
in this folder are intentionally 32-bit.  Do not replace them with the normal
AnyCPU EXE and do not move SAASound.dll away from this folder.

This trial has not established redistribution rights for the DLL.  Keep it
local until its upstream license and terms are verified.

How to compare
--------------

1. Test TurboSound FM Pro and MultiSound separately, as before.
2. For SAA1099, compare both provided media:
   * Arcane 2 with the relevant port-compatibility checkbox;
   * 1099 Test with the other board and its compatibility checkbox.
3. Judge three things independently: absence of crackle, fullness of the
   arrangement, and stereo balance.  AY, FM, GS and SoundDrive are not
   intentionally changed by this trial.

Built-in checks already passed from this folder:

* Test.exe — all ULA sanity checks and the 500-frame renderer benchmark;
* Test.exe /tsfm
* Test.exe /multisound
