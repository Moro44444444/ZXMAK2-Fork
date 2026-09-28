ZXMAK2 — ZX Evo BaseConf, B36 test build

What changed:
- implemented early frame-INT release on Z80 interrupt acknowledge;
- implemented the official frame-aligned and breakpoint-triggered NMI paths;
- implemented the #0066 NOP transition, RAM page #FF and delayed #BE exit;
- preserved the accepted virtual-FDD page #FE path and all B35 configuration registers.

Что изменено:
- реализовано досрочное снятие кадрового INT по циклу подтверждения Z80;
- реализованы документированные кадровый NMI и немедленный NMI от breakpoint;
- реализованы NOP на #0066, страница RAM #FF и задержанный выход через #BE;
- сохранены принятый virtual FDD на странице #FE и все регистры B35.

Please smoke-test NedoOS, Rage with one short Enter, B30 border/multicolor,
Bad Apple, sound, SD/HDD replacement, IDE file access and the manual NMI command.

Проверьте NedoOS, Rage одним коротким Enter, border/multicolor B30,
Bad Apple, звук, смену SD/HDD, файлы IDE и ручную команду NMI.

The established runtime scenarios passed the user's regression smoke test.
Direct INT/NMI behavior remains probe-verified rather than application-tested.

Прежние runtime-сценарии прошли пользовательский регрессионный smoke-тест.
Сам INT/NMI подтверждён probe, но не отдельным прикладным тестом.
