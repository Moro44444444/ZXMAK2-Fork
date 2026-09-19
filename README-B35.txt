ZXMAK2 — ZX Evo BaseConf, B35 test build

What changed:
- completed the six-bit #BF configuration latch and official readback;
- enabled documented ROM writes through BF.D1 while preserving write-disable protection;
- completed #BD palette/font/border/write-disable and breakpoint-address registers;
- preserved exact #13BD virtual-FDD ownership and the accepted B34 SD/HDD lifecycle.

Что изменено:
- завершена шестибитная защёлка конфигурации #BF и её документированное чтение;
- реализована запись ROM через BF.D1 с сохранением аппаратной защиты окон;
- завершены регистры #BD палитры, шрифта, бордюра, защиты записи и адреса breakpoint;
- сохранены точный #13BD virtual FDD и принятая смена SD/HDD из B34.

Please smoke-test NedoOS, Rage, B30 border/multicolor, Bad Apple, sound and SD/HDD replacement.
Проверьте NedoOS, Rage, border/multicolor B30, Bad Apple, звук и повторную смену SD/HDD.

This is a test build. Runtime acceptance is pending.
Это тестовая сборка; runtime-приёмка ожидает пользовательской проверки.
