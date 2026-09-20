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


## What's new

### Current development build — B37

- Implemented the official AVR/gluclock `F7` and COM `#F8EF..#FFEF` register/decode families, including the external WAIT lifecycle and B36 frame-INT pause.
- Added the three-master-clock DOS-entry settling stall at the `#3Dxx` M1 transition, composed with existing memory delays by the RTL OR/max rule.
- Full automated regression passes. B37 still requires the user's runtime smoke test; build/probe success is not runtime acceptance.

### ZX-Evo BaseConf Alpha 0.3 — B36

- Reworked ZX-Evolution BaseConf emulation against the official r1364 documentation: memory paging, hardware ports, timing, interrupts, palette and all seven documented video modes.
- Fixed border/multicolor phase, mid-frame video switching and several audio timing/DC issues.
- Corrected Nemo IDE port decoding and added convenient HDD image selection, persistence and ejection in Machine Settings. HDD access, FAT browsing and file launching have been runtime-tested.
- Disk images opened through the normal UI are writable by default; write protection remains available as an explicit option.
- Replacing or ejecting SD/HDD media now performs a controlled cold power-cycle, so repeated media changes do not require a manual emulator restart.
- Added a clean portable ZX-Evo BaseConf package for testing: [download the Alpha 0.3 portable ZIP](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.3-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-0.3.zip) or [view the release notes](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.3-alpha).

Known Alpha limitations: automatic OS boot from HDD is still being investigated; the SDHC/Rage case with one specific 4-GB image is under diagnosis.

Next development stage after B37 runtime acceptance: B38 — the documented
built-in Kempston joystick, tape-in/tape-out and beeper/tape mux. Peripheral
expansion remains deferred until the built-in BaseConf audit is complete.

Development records: [current project journal](PROJECT_JOURNAL.md),
[concise changelog](FORK_CHANGELOG.md), [BaseConf r1364 audit and plan](BASECONF_AUDIT_B32.md),
and [latest B37 report](B37_RESULT.md). Older detailed reports are retained in
[`docs/history/results`](docs/history/results/).

## Что нового

### Текущая тестовая сборка — B37

- Реализованы официальные семейства AVR/gluclock `F7` и COM `#F8EF..#FFEF`, включая внешний WAIT и паузу кадрового INT из B36.
- Добавлен трёхтактовый DOS settling stall на M1-переходе `#3Dxx`; он совмещается с задержками памяти по аппаратному правилу OR/max.
- Полная автоматическая регрессия проходит. B37 ожидает пользовательского runtime smoke; успешные сборка и probe не считаются runtime-приёмкой.

### ZX Evo BaseConf Alpha 0.3 — B36

- Эмуляция ZX-Evolution BaseConf сверена с официальной документацией r1364: память, порты, тайминги, прерывания, палитра и семь документированных видеорежимов.
- Исправлены фаза border/multicolor, переключение видеорежимов и проблемы синхронизации звука.
- Исправлено декодирование Nemo IDE; добавлены выбор и сохранение HDD-образа в Machine Settings. Подключение HDD, чтение FAT и запуск файлов проверены.
- Диски, открытые через обычный интерфейс, по умолчанию доступны для записи; защиту можно включить вручную.
- Повторная замена или извлечение SD/HDD теперь выполняет управляемый холодный перезапуск, поэтому ручной перезапуск эмулятора для смены носителя не требуется.
- [Скачать portable ZIP Alpha 0.3](https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.3-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-0.3.zip) или открыть [страницу релиза](https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.3-alpha).

Известные ограничения Alpha: автоматическая загрузка ОС с HDD ещё исследуется; диагностируется сценарий SDHC/Rage с одним конкретным образом 4 ГБ.

Следующий этап после runtime-приёмки B37: B38 — документированные встроенные
Kempston joystick, tape-in/tape-out и beeper/tape mux. Расширительная
периферия по-прежнему отложена до завершения аудита встроенного BaseConf.

Документы разработки: [постоянный журнал](PROJECT_JOURNAL.md),
[краткая хронология](FORK_CHANGELOG.md), [аудит и план BaseConf r1364](BASECONF_AUDIT_B32.md),
[последний отчёт B37](B37_RESULT.md). Более ранние подробные отчёты сохранены в
[`docs/history/results`](docs/history/results/).
