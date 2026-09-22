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

### ZX-Evo BaseConf Alpha 6 — B40

The current downloadable prerelease is **Alpha 6**. It is based on the
accepted B40 BaseConf emulation baseline and is intended for testing.

- Clean first launch starts directly in **ZX-Evo BaseConf**.
- Toolbar controls are available for FDD, HDD and SD: Load/Eject menus and
  clear red/green connection indicators.
- SD settings use the same safe apply flow as HDD, so pressing Apply in
  Machine Settings preserves an already mounted card.
- F12 performs Warm Reset; Ctrl+F12 resets CMOS; Ctrl+Alt+F12 resets the full
  machine state. F12 retains its debugger function inside the debugger.

[**Download Alpha 6 portable ZIP**](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.6-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-6.zip)
or read the [Alpha 6 release notes](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.6-alpha).

Extract the ZIP to any folder and run `ZXMAK2.exe`. It contains one root
folder and no personal machine state, media images, logs, PDB files or
development probes.

Older Alpha versions remain available in the
[Releases archive](https://github.com/Moro44444444/ZXMAK2-Fork/releases).

## Что скачивать

### ZX Evo BaseConf Alpha 6 — B40

Текущая тестовая версия — **Alpha 6**. Она основана на принятой стабильной
базе эмуляции BaseConf B40 и предназначена для проверки.

- При первом чистом запуске сразу включается **ZX-Evo BaseConf**.
- Для FDD, HDD и SD добавлены меню Load/Eject и понятные красные/зелёные
  индикаторы подключения.
- Настройки SD применяются по той же безопасной схеме, что HDD: нажатие Apply
  в Machine Settings не извлекает уже подключённую карту.
- F12 — Warm Reset; Ctrl+F12 — сброс CMOS; Ctrl+Alt+F12 — полный сброс
  состояния машины. В debugger клавиша F12 сохраняет прежнюю функцию.

[**Скачать portable ZIP Alpha 6**](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.6-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-6.zip)
или открыть [описание Alpha 6](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.6-alpha).

Распакуйте ZIP в любую папку и запустите `ZXMAK2.exe`. В архиве одна корневая
папка; личные состояния эмулятора, образы носителей, логи, PDB и отладочные
файлы не включены.

Предыдущие Alpha сохранены в [архиве релизов](https://github.com/Moro44444444/ZXMAK2-Fork/releases).
