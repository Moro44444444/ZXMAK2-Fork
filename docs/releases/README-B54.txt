ZXMAK2 Fork — ZX Evolution BaseConf — v54 local test, 2026-09-26

Based on v53. v53 remains unchanged for rollback.
Only ZXMAK2.Host.WinForms.dll has changed; sound and expansion boards are unchanged.

- Alt+Enter: toggle fullscreen without passing Enter to ZX Evolution.
  Both regular Enter and keypad Enter are filtered during the shortcut.
- Maximum Speed: Ctrl+S instead of the old Scroll shortcut. The menu and
  toolbar tooltip show Ctrl+S. S is not passed to the guest during Ctrl+S.
- Alt+P: Machine Settings, unchanged from v53.
- Ordinary Enter, S and P remain usable by the guest.

Automated WinForms command-dispatch and DirectInput shortcut-filter checks
passed. Please verify actual keypresses with the emulator focused and confirm
that its menu does not accept Enter or S while invoking these host shortcuts.

No GitHub publication was made. Local firmware/native library distribution
restrictions are unchanged from v53.
