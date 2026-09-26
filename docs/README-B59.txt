ZXMAK2-Fork / ZX Evolution BaseConf — v59 test
2026-09-26

Machine Settings (Alt+P) -> Joystick:
- Game profile: Standard or named profiles (e.g. Elite).
- New / Copy; Import / Export; delete with confirmation.
- Spectrum key assignments: controller button/axis/POV -> one of 40
  Spectrum keys, optionally Caps Shift / Symbol Shift.
- Kempston Extended (8-bit): configurable Fire 2/3/4 on bits 5/6/7.
- Existing DInput/XInput, Refresh, dead zone, D-pad/stick and auto-fire retained.

Select controller -> select profile -> Apply -> play.
For a new profile, use Spectrum key assignments -> Add; select the Spectrum
key and press the controller assignment to learn the physical input.
Assignments press the Spectrum keyboard, not Windows shortcuts.

Profiles: joystick-profiles.xml next to the emulator, backup .bak.
The selected profile is also embedded in the machine configuration.
Copy this XML or use Export / Import to move profiles to another release.
Apply commits changes; Cancel leaves prior settings and profile library intact.

User testing requested: real DInput/XInput gamepad / HOTAS, especially Elite.
No predefined Elite bindings supplied: choose keys for the exact game version.
Previous v58 and stable v54 folders remain available for rollback.
No new global hotkeys. See JOYSTICKS.md and HOTKEYS.md.
