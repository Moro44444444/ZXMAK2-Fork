ZXMAK2 Fork — ZX Evolution BaseConf — v55 ULAplus TEST, 2026-09-26

Based on the accepted stable v54. The v54 folder remains untouched.
Separate branch: codex/v55-ulaplus-compatibility. Not published to GitHub.

RU
В Machine Settings (Alt+P) -> ULA -> PENTEVO добавлен переключатель
Enable ULAplus (automatic). По умолчанию он выключен.
Галочка разрешает расширение: программа сама задаёт палитру и включает
режим через порты. Обычные программы остаются в обычных цветах.
Снятая галочка запрещает активацию, в том числе из SCR/SZX.
Сброс выключает программный режим, но сохраняет разрешение в настройках.

Для проверки картинки: включить галочку, выбрать стандартный графический
режим 256x192, поставить эмулятор на паузу и открыть через Open файл
tests/ULAplus-v55-test.scr. Ожидаемый вид — tests/ULAplus-v55-test.png.
SCR с палитрой имеет размер 6976 байт; обычный SCR — 6912 байт.
Загрузка обычного SCR возвращает стандартную палитру.
SZX сохраняет и восстанавливает блок палитры PLTT.

Это опциональная программная совместимость, не точная копия ограничений
FPGA BaseConf r1364: порты разрешены при всех TV/VGA-таймингах, а не только
при растре 128K. Чтение соответствует выбранному регистру ULAplus.
Timex-режимы и необязательный grayscale не реализованы.
ULAplus действует только в стандартном режиме 256x192; расширенные
ATM/EVO-режимы и прежняя палитра 4:4:4 не изменены.
Автоматическое включение означает команду программы, не распознавание
игр по имени; выход из программы сам по себе не сбрасывает её регистры.

EN
Machine Settings (Alt+P) -> ULA -> PENTEVO now has an unchecked-by-default
Enable ULAplus (automatic) checkbox. It permits software-controlled palette
and mode activation; it does not recolour ordinary programs automatically.
Unchecking blocks activation, including SCR/SZX restore. Reset disables
the software mode while preserving the host permission setting.
The optional compatibility implementation works at every TV/VGA timing,
unlike the physical BaseConf r1364 128K-only port restriction. Readback
returns the selected ULAplus register. Only standard 256x192 graphics use
the 64-entry GRB palette; extended ATM/EVO modes remain unchanged.
Timex and optional grayscale modes are not implemented.
For the sample SCR, use standard 256x192 graphics, pause the emulator,
then open tests/ULAplus-v55-test.scr; the neighbouring PNG is the reference.

Verification: 33,579 ULAplus checks; seven accepted renderer hashes;
mid-frame mode/palette, 4:4:4 palette, contention/open-bus probes; settings,
hotkey and media UI probe; master volume, EVO display, MultiSound/Max,
TurboSound FM Pro, MoonSound and ZXNetUSB tests passed.
Real-game/demo acceptance is still pending user testing.

Specification: https://zxdesign.itch.io/ulaplus
Hardware comparison: NedoPC BaseConf RTL r1364, z80/zports.v.
Local-only firmware/native-library distribution restrictions remain unchanged.
