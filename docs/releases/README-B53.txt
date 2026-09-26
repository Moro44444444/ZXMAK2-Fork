ZXMAK2 Fork — ZX Evolution BaseConf, local v53 test build
2026-09-26

This folder is a separate test copy based on the v51 Beta 1 candidate.
The v51 and v52 folders have not been changed. Nothing from this build has
been published to GitHub.

Changes since v51:
- Open, Save and Warm Reset toolbar buttons use the supplied 32x32 artwork.
- The magenta edge around tape, floppy, HDD, SD and CD toolbar images is gone.
- Machine Settings > ULA has an Audio Output section with Master Volume
  (0–200%). The setting is saved in the machine profile; 100% is unchanged.
- Alt+P opens Machine Settings through the same command as the toolbar/menu.
  DirectInput no longer forwards P to the emulated ZX Evolution during Alt+P.
  Plain P and Ctrl+P are unchanged.

Please test Alt+P with the emulator window focused, including when the
emulated ZX Evolution menu is visible. Machine Settings should open without
selecting Service in the emulated menu. Also check left and right Alt.

Native SAASound/OPL4 libraries and machine firmware are present only in this
local test copy. Redistribute them only after checking their respective terms.
