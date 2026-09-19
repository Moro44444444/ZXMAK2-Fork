# B31 result — NedoOS / BaseConf virtual-FDD PEN2 gate

Статус: принято после пользовательской runtime-проверки.

- Найдено точное расхождение B29 с официальным `base_trdemu` r1364: вход в RAM handler page `#FE` должен требовать `dos && romnram && !atm_pen2`.
- В `MemoryPentEvo.TryEnterFddIoRam()` добавлен только отсутствовавший `!atm_pen2`, представленный свойством `!PEN2`.
- B29 Rage/FDD не откатывался: `FddPentEvo.cs` не изменён, page `#FE` и write-protect до первого M1 сохранены.
- B30 border/multicolor не менялся: `UlaPentEvo.cs` побайтно сохранён.

Проверки:

- Release solution: PASS, только два прежних ruleset warning;
- `FddTrapProbe-B31`: 14 PASS;
- `VideoTimingProbe-B30`: 12748 PASS;
- SCL Rage: 113 PASS;
- B21 controller: 321 PASS;
- palette: 1807 PASS;
- TRD: 655922 PASS;
- audio baseline: 19 PASS;
- DirectSound: 16 PASS.

Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-NEDOOS-B31-20260919-015150\release`.

Пользовательская проверка: `sd_boot.$C` → NedoOS; Rage из SD/Ramdisk одним коротким Enter; принятый B30 border/multicolor; при возможности Bad Apple `$C`.

Результат пользовательской проверки:

- NedoOS загружается;
- Rage работает;
- border/multicolor B30 сохранился;
- Bad Apple работает при запуске из File browser, меню и NedoOS;
- других сбоев при проверке не выявлено.
