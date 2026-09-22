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


## Current public test release

### ZX-Evo BaseConf Alpha 7 — B40

The current prerelease is **Alpha 7**, based on the accepted B40 BaseConf
emulation baseline.

[**Download Alpha 7 portable ZIP**](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.7-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-7.zip)
or read the [Alpha 7 release notes](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.7-alpha).

- `View → Scale Mode → No Border` removes only the hardware border; all four
  existing scale modes continue to work in windowed and Full Screen display.
- The diagnostic `Test.exe` benchmark is included and no longer inherits the
  ZX-Evo default profile. Its renderer benchmark is back to 339–397 ms for
  500 frames; the Alpha 6 result around 4.5 seconds was a test-profile issue,
  not an emulation slowdown.
- The ZIP has one root folder and excludes personal machine state, media
  images, logs and PDB files. Extract it and run `ZXMAK2.exe`.

Older versions are available in the [Releases archive](https://github.com/Moro44444444/ZXMAK2-Fork/releases).

## Текущая тестовая версия

### ZX-Evo BaseConf Alpha 7 — B40

Текущая тестовая версия — **Alpha 7** на принятой базе эмуляции BaseConf B40.

[**Скачать portable ZIP Alpha 7**](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.7-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-7.zip)
или открыть [описание Alpha 7](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.7-alpha).

- `View → Scale Mode → No Border` убирает только аппаратный бордюр; все четыре
  режима масштаба продолжают работать в окне и Full Screen.
- В архив включён диагностический `Test.exe`. Он больше не наследует стартовый
  профиль ZX-Evo: renderer benchmark снова занимает 339–397 мс на 500 кадров.
  Результат около 4,5 с в Alpha 6 был проблемой тестового профиля, а не
  замедлением эмуляции.
- В ZIP одна корневая папка; личные состояния машины, образы носителей, логи и
  PDB исключены. Распакуйте архив и запустите `ZXMAK2.exe`.

Предыдущие версии сохранены в [архиве релизов](https://github.com/Moro44444444/ZXMAK2-Fork/releases).
