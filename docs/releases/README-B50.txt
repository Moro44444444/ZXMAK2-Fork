ZXMAK2 — ZX Evo BaseConf, v50 ZXNetUSB Rev.C test build

This is a separate test build based on the accepted v49 MoonSound build.
The v49 folder remains unchanged and is the rollback point.

Changes:
- ZXNetUSB Rev.C (Ethernet) can be installed in either PentEvo ZXBUS slot;
- the documented Rev.C CPLD I/O ports and WIZnet W5300 register/socket model
  are implemented for the current NedoOS I/O driver;
- TCP and UDP use ordinary Windows host sockets; no TAP driver, raw adapter
  access or administrator rights are needed;
- built-in guest network configuration is 10.0.2.15, gateway 10.0.2.2 and
  DNS proxy 10.0.2.3;
- USB/SL811HS and W5300 memory-mapped mode are intentionally outside this
  Ethernet-only test build;
- VM menu now lists Warm Reset (F12), CMOS Reset (Ctrl+F12) and Factory Reset
  (Ctrl+Alt+F12); the existing keyboard handling is unchanged;
- the Warm Reset toolbar tooltip is now simply F12.

Checks:
- Release build completed with no code errors;
- Test.exe /zxnetusb: Z80 I/O decode, W5300 ID, TCP transmit/receive and
  DHCP — PASS;
- Test.exe /tsfm, /multisound and /moonsound — PASS.
- the complete ULA sanity and rendering benchmark suite — PASS.

This build has not been published to GitHub. It is intended for NedoOS
runtime testing before any stable release decision.
