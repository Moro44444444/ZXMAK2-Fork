ZXMAK2 — ZX Evo BaseConf, v41 integrated media and TSFM test build

This is a separate Alpha 8 candidate test build based on the accepted v40.
The v40 folder remains unchanged and is the rollback point.

Changes:
- TurboSound FM Pro Rev. C output raised by 50% for both YM2203 SSGs,
  YM2203 FM and SAA1099, with signed 16-bit saturation;
- TurboSound FM Pro moved to the normal Music device list and offered only
  on machine profiles that already have a compatible AY/YM device;
- PENTEVO uses the standard ULA selector; its two ZXBUS slots are on a
  separate "ZXBUS PentEvo" page;
- NeoGS remains a ZXBUS board; its microSD page is enabled only while the
  board is installed in Slot 1 or Slot 2;
- one unified toolbar state model: gray = unavailable, red = available and
  empty, green = media inserted/ready;
- cassette button opens the Tape Player and provides Load, Eject, Play,
  Stop, Rewind, Quick Load and Auto Play controls;
- CD/DVD button is the rightmost toolbar item and connects a Windows optical
  drive as BaseConf IDE Slave; Eject disconnects and clears it;
- FDD, HDD, both SD cards, tape and optical media use matching indicators.

Проверки:
- полная Release-пересборка;
- Test.exe /tsfm: два SSG, FM, SAA и усиление с насыщением — PASS;
- NeoGS ROM 1.11, DMA, microSD, MP3 — PASS;
- PENTEVO ULA/Music/ZXBUS, обе SD и панель инструментов — PASS;
- ATAPI на физическом Windows CD/DVD-приводе — PASS;
- полный Test.exe: все ULA sanity-векторы и benchmark — PASS.

Стартовая машина в чистой папке — ZX-Evo BSconf. Пользовательские файлы
ZXMAK2.cmos, ZXMAK2.vmide/ZXMAK2.vide, ZXMAK2.vmz и образы не включены.
