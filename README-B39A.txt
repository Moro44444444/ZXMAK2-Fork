ZXMAK2 — ZX Evo BaseConf, B39A test build

What changed:
- added only the official #BF.D5 BaseConf 4:4:4 palette extension;
- kept the accepted D5=0 palette path unchanged;
- preserved all seven BaseConf renderer routes and official palette readback;
- did not add ULAplus or alter FDD, IDE, SD, sound, INT/NMI or WAIT logic.

Что изменено:
- добавлено только официальное расширение палитры BaseConf 4:4:4 по #BF.D5;
- принятый путь палитры при D5=0 оставлен без изменений;
- сохранены все семь видеорежимов BaseConf и официальный readback палитры;
- ULAplus не добавлялась; FDD, IDE, SD, звук, INT/NMI и WAIT не менялись.

Please smoke-test normal startup, B30 border/multicolor, ATM 16-color,
Rage, NedoOS, Bad Apple, sound, SD/HDD replacement and IDE file access.

Проверьте обычный запуск, border/multicolor B30, ATM 16-color, Rage,
NedoOS, Bad Apple, звук, смену SD/HDD и чтение файлов IDE.

Automated checks passed; runtime acceptance remains a user test.
Автоматические проверки пройдены; runtime-приёмку выполняет пользователь.
