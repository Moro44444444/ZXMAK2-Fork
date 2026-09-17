# ZXMAK2 Fork — журнал изменений

Этот файл фиксирует только изменения форка и состояние их проверки. Исходная
история ZXMAK2 остаётся в `src/history.txt`.

## 2026-09-13 — ATM Turbo 2+, Quick Boot и Kempston Mouse

### Коррекция после проверки Test v11

- Test v11 признана нерабочей и не должна использоваться для дальнейшей
  проверки: ATM по-прежнему стартовал с `TURBO ON`, после использования
  общего окна открытия выбор SD-образа мог блокироваться, а Quick Boot не
  запускал оболочку.
- Причина старта ATM найдена в штатном `ATM/bios_1_07_13.rom`: байт настройки
  по адресу файла `#4D43` содержал `1`, из него ROM формировал бит D3 порта
  `#FF77` и надпись `TURBO ON`. Байт изменён на `0`, поэтому ROM сам запускает
  ATM Turbo 2+ в `TURBO OFF`; Warm Reset повторяет то же штатное начальное
  состояние.
- Причина зависания окна SD найдена в повторно используемом диалоге Windows:
  проверка файлов обычной команды Open оставалась подписанной после закрытия
  окна и затем отклоняла `.img`, `.ima` и `.vhd`. Обработчик теперь всегда
  снимается в `finally`; сам SD-контроллер и безопасная замена карты не
  изменялись.
- Для Quick Boot введён отдельный признак текущего сеанса TR-DOS. Он
  устанавливается при фактическом обращении TR-DOS к WD1793 и очищается при
  Reset, поэтому не зависит от кратковременного переключения сигнала DOSEN во
  время исполнения кода из RAM.
- Для ZX-Evolution отдельно учитывается путь Nedo Emul/виртуального привода:
  перенаправленный вызов через RAM-страницу `#FE` также подтверждает активный
  сеанс TR-DOS, даже если обращение не дошло до физического WD1793.
- Быстрая загрузка теперь применяет флаг `B128 PAGED` из штатного `boot.szx`.
  Этот блок включает доступ к TR-DOS ROM, но не заменяет подключённые образы и
  не меняет состояние приводов; без него загруженная оболочка не могла
  обратиться к уже вставленному диску.
- Исправленный ATM ROM включён и в исходники, и в переносимую тестовую сборку;
  установщик принудительно обновляет `ROMS.PAK` в каталоге Release.

### Подтверждено запуском перед исправлением

- После реализации динамической частоты ZX-Evolution BaseConf тест показал
  разные реальные скорости: `3.5`, `7` и `14 MHz`. Переключатель `W.CPU
  frequency` теперь влияет не только на надпись в EVO Reset Service.
- На ATM Turbo 2+ тест подтвердил обе реальные ступени `3.5` и `7 MHz`.
- Обнаружено неверное исходное состояние ATM Turbo 2+: после выбора машины она
  открывалась с `TURBO ON` и фактически работала на `7 MHz`.
- Обнаружена регрессия Kempston Mouse в NedoOS: захват указателя Windows
  выполнялся, но игра «Чёрный ворон» не получала координаты ни на `3.5`, ни на
  `7 MHz`. Причина — устройство мыши из готовой конфигурации ZX-Evo BSconf
  блокировало свои порты при активном признаке DOSEN.

### Реализовано, ожидает проверки запуском

- В готовой конфигурации `ZX-Evo BSconf` Kempston Mouse разрешена при активном
  DOSEN (`noDos=false`). Клавиатурная правка NedoOS и частоты 3.5/7/14 MHz не
  изменялись.
- ATM Turbo 2+ теперь получает из штатного ROM очищенный бит D3 порта `#FF77`:
  начальный режим — `TURBO OFF`, фактическая частота — `3.5 MHz`.
- Ручное включение `TURBO ON` сохраняется во время обычной работы, а аппаратный
  Warm Reset снова очищает D3 и возвращает `TURBO OFF`, как на реальной машине.
- Quick Boot больше не загружает `boot.szx` как полный снимок другой машины и
  не выполняет скрытый переход в TR-DOS. Он доступен только тогда, когда в
  текущей конфигурации есть Beta Disk/TR-DOS и уже начался фактический сеанс
  TR-DOS.
- Специальная загрузка Quick Boot переносит из штатного `boot.szx` только
  программное состояние оболочки: CPU, RAM, экран и видимое программе
  банковое состояние. Она не сбрасывает устройства и не восстанавливает из
  снимка AY или состояние Beta Disk, поэтому сохраняются выбранная машина,
  ATM/PentEvo-регистры, турбо-режим, звуковая конфигурация и носители.
- Подключённые дискеты во всех имеющихся приводах A-D остаются теми же,
  включая несохранённые изменения в памяти. После запуска оболочки текущая
  дорожка каждого реально вставленного носителя перечитывается контроллером
  без закрытия и повторного открытия файла.
- Кнопка Quick Boot на панели остаётся прежней, но становится серой вне
  активного TR-DOS. Пункт `Quick Boot` в меню `Tools` появляется только при
  активном TR-DOS и исчезает после выхода из него.
- Доступность Quick Boot определяется устройствами текущей конфигурации, а не
  названием готовой машины. Поэтому поддерживается и пользовательская машина,
  в которую вручную добавлен контроллер TR-DOS.
- Список готовых машин теперь сортируется по алфавиту в общем источнике данных,
  поэтому одинаковый порядок используется и в быстром выборе, и в окне
  настроек машины.

Проверить в тестовой сборке: старт и Warm Reset ATM Turbo 2+, Kempston Mouse в
NedoOS/«Чёрном вороне» на всех трёх частотах ZX-Evo, появление Quick Boot только
в активном TR-DOS, сохранение образов и изменений приводов A-D, а также запуск
штатной Quick Boot-оболочки на готовой и пользовательской конфигурациях.

## 2026-09-13 — ввод, SD и тест частоты

### Подтверждено запуском

- Повторная смена SD-образов работает, включая смену из запущенной NedoOS.
- После замены карты выполняется безопасный перезапуск, новый образ доступен.
- PS/2-клавиатура работает в NedoOS; проверены Enter, Backspace и Shift.
- Kempston Mouse работает в адаптированной для NedoOS игре «Чёрный ворон».
- Добавлен отдельный `ZX-Evo-Clock-Test.scl` для измерения фактической работы
  Z80 между двумя кадровыми прерываниями. Тест показывает сырой счётчик и
  распознанную частоту `3.5`, `7` или `14 MHz`.
- Запуск теста подтвердил дефект переключения частоты ZX-Evolution BaseConf:
  при выбранных в EVO Reset Service значениях `3.5`, `7` и `14 MHz` во всех
  трёх случаях получен одинаковый счётчик `#07FE`, соответствующий `3.5 MHz`.
  Пункт `W.CPU frequency` меняет отображаемое значение, но фактическую скорость
  выполнения Z80 в текущей реализации ZXMAK2 не изменяет.

### Реализовано, ожидает проверки запуском

- Добавлен общий контроллер динамической частоты Z80. Внутреннее время шины
  работает в тактах максимальной частоты машины, а при пониженной частоте между
  командами Z80 добавляются аппаратные такты ожидания. Переключение регистра
  начинает действовать со следующей команды и не меняет длительность кадра.
- Для ZX-Evolution BaseConf реализованы три режима из официальной схемы
  UnrealSpeccy: `3.5 MHz` (1x), `7 MHz` (2x) и номинальные `14 MHz` с
  эффективной производительностью около 11 MHz (3x из-за ожиданий памяти).
- Для ATM Turbo 2+ реализованы документированные режимы `3.5 MHz` и `7 MHz`;
  режим выбирается битом D3 системного порта `#FF77`.
- Видеоразвёртка и кадровые прерывания отделены от частоты процессора, поэтому
  переключение турбо не должно менять 50 Hz, положение бордюра или видеорежим.
- Время ленты и WD1793 пересчитано в такты максимальной частоты. Скорость
  воспроизведения ленты, обороты диска и задержки головки не должны ускоряться
  вместе с Z80.
- Сигнал `Z` ATM Turbo 2+ оставлен привязанным к положению видеолуча и не
  меняет период при переключении 3.5/7 MHz.
- Добавлен отдельный `ATM-Turbo-2-Clock-Test.scl`. Ожидаемые приблизительные
  результаты: ATM — `#07CD`/`#0F9A`; ZX-Evo —
  `#0800`/`#1000`/`#1800`.

Проверить в тестовой сборке: все ступени обоих компьютеров, неизменные 50 FPS,
звук AY/бипера, загрузку с TR-DOS и отсутствие новых бордюрных эффектов.

### Отложено

- Эмуляция ZXNETUSB и сетевой мост Windows перенесены в последнюю очередь.
- Платы ZXBUS и дополнительные звуковые устройства будут добавляться после
  исправления и проверки переключения частоты процессора.

## 2026-09-12 — текущий этап

### Подтверждено запуском

- Добавлена готовая конфигурация `Santaka 002` с оригинальным 48K ROM.
  Проверены запуск и встроенный русский знакогенератор.
- Добавлена готовая конфигурация `ZX-Evo BSconf` с ERS 0.59.12 FE.
- Исправлено декодирование портов памяти PentEvo для новых ERS.
- Добавлен регистр `#13BD`, необходимый проверке FPGA в ERS 0.59.12 FE.
  После исправления сервисное меню ERS запускается без сообщения
  `Incorrect FPGA zxevo_fw.bin`.
- На панель инструментов добавлены кнопки выбора готовой машины и SD-образа.
  Кнопка SD доступна только для конфигурации, содержащей SD-контроллер.
- Список готовых машин на панели сортируется по алфавиту.
- В тестовой сборке v6 подтверждено аппаратное перенаправление FDD I/O
  PentEvo в RAM-страницу `#FE`: `SATISFAC.SCL` загружается с SD в
  `Ramdisk A`, затем виден в `TR-DOS boot`, и демо запускается.
- В тестовой сборке v7 подтверждено исправление ULA PentEvo: при загрузке
  `SATISFAC` с виртуального диска бордюр остаётся чёрным, как на реальной
  машине и в UnrealSpeccy.

### Реализовано, ожидает проверки сборкой и запуском

- Безопасная смена SD-образа во время работы: прежний файл закрывается до
  открытия нового, а при ошибке открытия автоматически подключается обратно.
- При каждой успешной смене создаётся новый объект SD-карты, поэтому состояние
  SPI-команд, счётчики передачи и буфер предыдущего образа не переходят в новый.
  Это устраняет зависимость второй смены карты от незавершённого состояния первой.
- На время выбора и смены SD-образа виртуальная машина полностью
  останавливается. Состояние теперь считывается непосредственно из VM, а не из
  асинхронно обновляемого свойства главного окна.
- По журналу v7 подтверждено, что работал ровно один процесс ZXMAK2 и поток VM
  не завершался с исключением. Ошибка повторной смены возникала при открытии
  ранее использованного IMG: Windows сообщала, что файл занят другим процессом.
- Порядок замены изменён на физически корректный: сначала старая карта
  отсоединяется, её поток принудительно сбрасывается и закрывается, затем
  открывается новый образ. Если новый образ открыть нельзя, прежний файл
  подключается обратно. Повторный `BusConnect` также закрывает старую карту.
- Автоматический Warm Reset теперь выполняется после возобновления VM тем же
  методом, что и нажатие кнопки Warm Reset. Это исключает отдельный путь v7,
  где Reset выполнялся внутри уже остановленной VM и мог оставлять PentEvo в
  искажённом видеорежиме («матрас»).
- Поддерживаются RAW-образы `.img`/`.ima`, стандартные fixed/dynamic VHD и
  совместимый RAW-режим для официальных образов NedoOS с расширением `.vhd`,
  которые UnrealSpeccy читает как посекторный файл.
- Добавлена минимальная проверка SD-образа: размер кратен 512 байтам,
  присутствует корректная MBR/FAT-сигнатура и правдоподобная таблица разделов
  либо FAT boot sector.
- Максимальная поддерживаемая ёмкость ограничена 8 GiB.
- Ёмкость карты формируется в CSD из фактического размера образа.
- Для образов свыше 2 GiB включается режим SDHC с блочной адресацией, что
  устраняет старое ограничение контроллера, всегда представлявшегося картой
  на 1 GiB.

Причина проблемы с образами на 4 GiB: прежний контроллер возвращал один и тот
же CSD для любого файла. Этот CSD сообщал прошивке ёмкость 1 GiB, хотя сам
`FileStream` умел обращаться к большим смещениям. Новые ERS используют данные
CSD/OCR и поэтому считали такую карту отсутствующей или несовместимой.

### Проверка текущего этапа

- Подтверждено в v6: на чистом запуске SD-образ открывается, `SATISFAC.SCL`
  загружается в `Ramdisk A`, в `TR-DOS boot` появляются `SATISFAC` и `boot`,
  демо запускается.
- Подтверждено в v7: во время загрузки с `Ramdisk A` бордюр остаётся чёрным.

- Собрать Release и убедиться, что `ZXMAK2.exe` и изменённые DLL обновились.
- Проверить загрузку ранее работавшего небольшого RAW-образа.
- Проверить `.ima` и `.img` от карты 4 GiB: ERS должна увидеть файловую систему.
- На работающей `ZX-Evo BSconf` сменить образ A на образ B. Старый файл должен
  освободиться, новый — подключиться, а Warm Reset — выполниться автоматически.
- Отменить выбор файла и попробовать заведомо некорректный образ. Reset не
  должен происходить; ранее подключённая карта должна остаться доступной.
- Проверить официальный RAW-образ NedoOS с расширением `.vhd` и обычный VHD с
  корректным footer.
- Убедиться, что файл свыше 8 GiB отклоняется понятным сообщением.

Тестовый пакет этого этапа создаёт отдельную переносимую папку и запускает её
с новой конфигурацией `ZX-Evo BSconf`: физические диски A-D и SD-образ не
подключены. Сохранённый `ZXMAK2.vmz` основной сборки не копируется и не
изменяется, поэтому результаты прежних запусков не влияют на проверку.

### Отложено

- `ZX-Evo TSconf` и расширенные видеорежимы TSconf.
- Дополнительные звуковые карты.

## ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143 - baseline before keyboard change
- Base: Current user working tree; v13 lineage not independently verified; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- No source change in this baseline build; three Release projects rebuilt successfully.
- Source/release free ROM and PAK entry verified: v0.61.01 FE, SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Backup of original sources and release: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143\before.
- Baseline compiled release: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143\baseline-release.
- Runtime keyboard/mouse/TR-DOS/SD tests: not performed by this script.

## ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143 - ZX-Evo BaseConf keyboard FE/F6
- Base: Current user working tree; v13 lineage not independently verified; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One source change: src/ZXMAK2/machines.config, machine ZX-Evo BSconf, KeyboardDevice mask 0xFF -> 0xF7.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Source/release machines.config byte hashes agree; all 65536 addresses checked: exactly xxFE/xxF6.
- Accepted ROM unchanged and verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143.log.
- Runtime ERS/NedoOS keyboard and mouse tests: pending user verification.
- Decision: build and configuration checks passed; functional acceptance pending. No other port/timing/video compliance claimed.

## ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143 - user runtime report before B02
- User confirmed ROM present and working mouse/keyboard. Exact DOS/NedoOS test mode remains unspecified.
- B02 preflight verified parent release hashes, B01 configuration and the three involved ULA source hashes.
- No claim of complete BaseConf compliance or direct runtime F6 test.

## ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825 - ZX-Evo BaseConf border port decoding
- Base: ZXMAK2-v13-ZXEVO-BC-KBD-FEF6-B01-20260915-180143; current user working tree; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One source change: src/ZXMAK2.Hardware/Evo/UlaPentEvo.cs, WritePortFE accepts only xxFE/xxF6/xxFC.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled ULA tested at all 65536 addresses: 768 accepted, others preserve border/port/extension state.
- Colors verified against RTL: border = {~A3,D2:D0}; FE/FC 0..7, F6 8..15.
- machines.config/B01 keyboard and shared ULA base sources unchanged; source/release config hashes agree.
- Accepted ROM unchanged and verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825.log.
- Runtime ERS, virtual disk load, keyboard/mouse regression and visual border tests: pending user verification.
- Decision: build and compiled port behavior checks passed; functional acceptance pending. No raster/WAIT/video timing compliance claimed.

## ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825 - reported issues before B03
- B02 build and compiled border checks passed; overall runtime acceptance remains pending.
- User reports VideoPlayer SD init error (13), no valid FAT volume; same image/program/BIOS works in UnrealSpeccy.
- User also reports emulator hangs when replacing the second SD image; root cause not established.
- Actual Windows RdXX57 confirmed to return zero in Shadow when A15=1, contradicting BaseConf zports.v and UnrealSpeccy SD read decoding.
- B03 addresses only RdXX57. SD hot swap, filesystem format and video renderer are separate concerns.

## ZXMAK2-v13-ZXEVO-BC-SDREAD-B03-20260915-184041 - ZX-Evo BaseConf SD port read decoding
- Base: ZXMAK2-v13-ZXEVO-BC-BORDER-B02-20260915-181825; current user working tree; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One source change: src/ZXMAK2.Hardware/Evo/ZsdPentEvo.cs, RdXX57 always reads SPI data if not already handled.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled RdXX57 tested for all 256 high bytes in normal/Shadow modes using a synthetic CMD17 sector in memory.
- Response/token/512-byte payload/CRC pipeline checked; handled reads preserve data/SPI state; absent card returns FF.
- B01 keyboard/config and B02 border source retained; SD card protocol, memory, media-change/reset code unchanged.
- Accepted ROM unchanged and verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-SDREAD-B03-20260915-184041\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-SDREAD-B03-20260915-184041. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-SDREAD-B03-20260915-184041.log.
- Runtime VideoPlayer/FAT retest: deferred until the planned runtime test stage; error (13) not yet declared resolved.
- Second-image hang: still unresolved; this build does not claim to repair SD hot swap.
- Decision: build and compiled SD read checks passed. No complete SD/filesystem/video compliance claimed.

## ZXMAK2-v13-ZXEVO-BC-BEEPER-B04-20260915-184910 - ZX-Evo BaseConf beeper port decoding
- Base: ZXMAK2-v13-ZXEVO-BC-SDREAD-B03-20260915-184041; current user working tree; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One changed element: BaseConf BeeperDevice in src/ZXMAK2/machines.config.
- Exact low byte FE (all upper bytes), noDos=false; D4 retained, D3 disabled as before; default volume retained.
- Reference: NedoPC pentevo/fpga/baseconf/trunk/z80/zports.v, beeper_wr=(loa==PORTFE)&&portfe_wr_fclk, without Shadow/DOS gating.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled BeeperDevice loaded from release config; real BusInit subscription and EventManager WRPORT tested.
- All 65536 addresses plus all 256 data bytes at every xxFE port checked in four DOS/Shadow combinations.
- No edits to other machine blocks, beeper implementation, CPU clock, video renderer or SD reset/media path.
- B01 keyboard, B02 border and B03 SD read retained; accepted ROM verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-BEEPER-B04-20260915-184910\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-BEEPER-B04-20260915-184910. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-BEEPER-B04-20260915-184910.log.
- Beeper AVR-controlled D3/D4 mux, Covox arbitration and analog sound behavior remain separate pending work.
- Runtime program tests deferred at user request; FAT error (13) and second SD image hang not declared resolved.
- Decision: build and compiled beeper port checks passed; full machine conformity is not yet complete.

## ZXMAK2-v13-ZXEVO-BC-COVOX-B05-20260915-185529 - ZX-Evo BaseConf Covox DOS access
- Base: ZXMAK2-v13-ZXEVO-BC-BEEPER-B04-20260915-184910; current user working tree; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One changed attribute: BaseConf CovoxMono noDos=true -> false in src/ZXMAK2/machines.config.
- Port xxFB and mask FF already matched RTL and are retained; all 8 data bits and default volume retained.
- Reference: NedoPC pentevo/fpga/baseconf/trunk/z80/zports.v, COVOX=8'hFB; covox_wr=(loa==COVOX)&&port_wr_fclk, without Shadow/DOS gating.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled CovoxMono loaded from release config; real BusInit subscription and EventManager WRPORT tested.
- All 65536 addresses plus all 256 data bytes at every xxFB port checked in four DOS/Shadow combinations.
- Actual DAC samples checked for full 8-bit mono data, including 0 and 255; handled writes preserve DAC state.
- B01 keyboard, B02 border, B03 SD reads and B04 beeper configuration retained; other machine blocks unchanged.
- No edits to Covox implementation, CPU clock, video renderer or SD media/reset path.
- Accepted ROM verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-COVOX-B05-20260915-185529\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-COVOX-B05-20260915-185529. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-COVOX-B05-20260915-185529.log.
- Shared beeper/Covox output arbitration, AVR beeper D3/D4 mux and analog sound behavior remain pending work.
- Runtime program tests deferred at user request; FAT error (13) and second SD image hang not declared resolved.
- Decision: build and compiled Covox port/data checks passed; full machine conformity is not yet complete.

## ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522 - ZX-Evo BaseConf 7FFD extended RAM bank order
- Base: ZXMAK2-v13-ZXEVO-BC-COVOX-B05-20260915-185529; current user working tree; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One source line changed: MemoryPentEvo.UpdateMapping, high bank bits are (CMR0 & 0xE0) >> 5, in order D7,D6,D5.
- RAM bank selector in extended mode is D7,D6,D5,D2,D1,D0; previously D5,D7,D6,D2,D1,D0.
- Examples with other selector bits clear: D5 -> bank 8 (was 32); D6 -> bank 16 (was 8); D7 -> bank 32 (was 16).
- References: uploaded zxevo_base_configuration.pdf section 6, page 14; BaseConf zports.v pent1m_page={p7ffd_int[7:5],p7ffd_int[2:0]}; atm_pager.v six-bit substitution.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled physical bank probe uses real MemoryPentEvo BusInit and EventManager WRPORT/RDMEM/WRMEM, with synthetic 4MB RAM and 512KB ROM.
- All 64 selector banks in all four 1MB groups, both memory maps and all four 16KB windows checked; read/write identity and inactive descriptor preservation verified.
- ZX128 low-three-bit substitution, D5 lock/release, direct x7F7 RAM pages and D3 screen selection retained.
- Total: 2128 physical window mappings checked.
- B01-B05 configuration and source fixes retained; machines.config unchanged.
- Accepted ROM verified in source/release free files and PAK entries: SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Separate build: K:\Download\ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522\release. Saved VMZ files excluded from this test copy.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522.log.

### Memory audit: observed implementation, remaining work
| Node | Documentation / BaseConf RTL | Current finding |
| --- | --- | --- |
| 7FFD extended banks | D7,D6,D5,D2,D1,D0 | Corrected by B06; compiled RAM mapping checks passed |
| Two maps / four windows | D4 chooses map; xFF7/x7F7 select pages per window | Exercised in B06 for RAM; exhaustive port aliases and ROM behavior remain separate |
| xBF7 write protection | Per-window, per-map read-only flag; clear on reset | Not implemented in inspected MemoryPentEvo; pending |
| xxBF bit 1 | Enables writes to ROM, subject to write protection | Not applied to ROM write mappings in inspected source; pending |
| M1 / DOS entry | Active map D4 and per-window dos7ffd/ramnrom conditions | Current BusReadM1 does not enforce all required conditions; pending |
| xxBD state reads | Includes page descriptors and write-disable state | Read-only state missing; read aliases and masks require separate audit |

- Video address mapping and mode switching, exact turbo wait states and SD media reset remain separate stages.
- Shared beeper/Covox output arbitration and AVR-controlled D3/D4 beeper mux remain pending.
- Runtime program tests deferred at user request; FAT error (13) and second SD image hang not declared resolved.
- Decision: build and compiled scoped RAM bank checks passed; complete memory/video conformity is not claimed.

## ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522 - user runtime result before B07
- User reports the previously observed FAT error is gone in the B06 test.
- Demo starts and the extended graphical modes used by it work, according to user.
- Screenshot shows a rendered demo frame; exact demo name and mode IDs were not provided.
- This is scoped runtime confirmation, not complete acceptance of all video modes.
- Second SD image replacement hang has not been reported as retested or fixed.

## ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932 - failed stage
- FAILED: ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932; phase: B07 build and compiled write-protection / bank regression checks; write-protection patch applied: True; error: Compiled write protection test failed.
- Original backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932\before; do not treat this build as accepted.

## ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932 - failure diagnosis / B07R1 verification
- The B07 source patch was applied and projects built. B06 bank regression checks passed.
- The protection-mask test failed because Program() disables Shadow and the next xBF7 writes ran with both DOS and Shadow OFF.
- This is a confirmed setup defect in the probe; the complete B07 source behavior still requires the corrected compiled checks.
- Current memory source verified byte-for-byte as B06 plus the six known B07 source fragments.
- B07R1 changes the diagnostic setup only: enable Shadow before programming all protection masks and assert the test mode.
- No emulator source is edited in this recovery script.
- User B06 runtime report retained: FAT error gone, demo starts, used extended graphics work; second SD image hang not yet retested.

## ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-192249 - B07 write-protection verification, corrected probe
- Base: ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522 plus verified source patch from ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932; corrected probe only; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- Recovery from ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932; current memory source verified as exact B06 plus the known B07 protection patch.
- No emulator source edited; the corrected compiled probe enables Shadow after Program() and asserts DOS OFF / Shadow ON.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- All 65536 addresses inspected for xBF7 subscription; all 32 aliases and four D0/data cases in four DOS/Shadow combinations passed.
- All 256 protection masks tested over both maps and four windows using real WRPORT/RDPORT/RDMEM/WRMEM and synthetic memory.
- Read bank preserved, RAM writes blocked or allowed according to flags; page changes retain flags; reset clears flags; 12BD returns them.
- Forced RAM0 exception and existing virtual FDD RAM FE override / BE return passed.
- B06 regression over all 4MB, both maps, four windows, ZX128 lock and screen selection passed.
- B01-B06 fixes and machines.config retained; ROM SHA256 verified in source/release free files and PAK entries: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Before/after backup of the recovery state: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-192249. Original pre-patch B06 source/release also retained in L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932\before.
- Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-192249\release. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-192249.log.
- User B06 runtime result: FAT error gone, demo starts, used extended graphics work; full video acceptance remains pending.
- Second SD image hang has not been reported as retested or fixed.
- ROM write enable, full NMI behavior, 12BD writes and remaining port aliases stay separate pending work.
- Decision: B07 source accepted for this scoped stage only after corrected compiled checks passed; original failed B07 remains unaccepted.

## ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932 - failure diagnosis / B07R1 verification
- The B07 source patch was applied and projects built. B06 bank regression checks passed.
- The protection-mask test failed because Program() disables Shadow and the next xBF7 writes ran with both DOS and Shadow OFF.
- This is a confirmed setup defect in the probe; the complete B07 source behavior still requires the corrected compiled checks.
- Current memory source verified byte-for-byte as B06 plus the six known B07 source fragments.
- B07R1 changes the diagnostic setup only: enable Shadow before programming all protection masks and assert the test mode.
- No emulator source is edited in this recovery script.
- User B06 runtime report retained: FAT error gone, demo starts, used extended graphics work; second SD image hang not yet retested.

## ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-193301 - B07 write-protection verification, corrected probe
- Base: ZXMAK2-v13-ZXEVO-BC-BANKS-B06-20260915-190522 plus verified source patch from ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932; corrected probe only; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- Recovery from ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932; current memory source verified as exact B06 plus the known B07 protection patch.
- No emulator source edited; the corrected compiled probe enables Shadow after Program() and asserts DOS OFF / Shadow ON.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- All 65536 addresses inspected for xBF7 subscription; all 32 aliases and four D0/data cases in four DOS/Shadow combinations passed.
- All 256 protection masks tested over both maps and four windows using real WRPORT/RDPORT/RDMEM/WRMEM and synthetic memory.
- Read bank preserved, RAM writes blocked or allowed according to flags; page changes retain flags; reset clears flags; 12BD returns them.
- Forced RAM0 exception and existing virtual FDD RAM FE override / BE return passed.
- B06 regression over all 4MB, both maps, four windows, ZX128 lock and screen selection passed.
- B01-B06 fixes and machines.config retained; ROM SHA256 verified in source/release free files and PAK entries: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Before/after backup of the recovery state: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-193301. Original pre-patch B06 source/release also retained in L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-WPROT-B07-20260915-191932\before.
- Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-193301\release. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-193301.log.
- User B06 runtime result: FAT error gone, demo starts, used extended graphics work; full video acceptance remains pending.
- Second SD image hang has not been reported as retested or fixed.
- ROM write enable, full NMI behavior, 12BD writes and remaining port aliases stay separate pending work.
- Decision: B07 source accepted for this scoped stage only after corrected compiled checks passed; original failed B07 remains unaccepted.

## ZXMAK2-v13-ZXEVO-BC-CLOCKPORT-B08-20260915-194633 - BaseConf CPU control ports
- Base: ZXMAK2-v13-ZXEVO-BC-WPROT-B07R1-20260915-193301; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One production node/file: src/ZXMAK2.Hardware/Evo/MemoryPentEvo.cs.
- EFF7 write registration changed from exact EFF7 to F7 low byte, A8=1, A12=0. All 64 hardware aliases are now registered.
- DOS/Shadow gate retained; xx77 write decoding retained; F7 pager/protection handlers remain independent.
- Hardware register label TURBOOFF now describes EFF7.D4 as a 3.5 MHz request; public property retained for compatibility.
- Removed the unsupported comment claiming that uniform x3 reproduces hardware WAIT.
- References: user BaseConf manual section 6 (page 15); BaseConf RTL zports.v portf7_wr / peff7_int and zclock.v.
- Three Release projects rebuilt; executable and two checked DLLs regenerated.
- Compiled checks inspect all 65536 addresses; all 64 F7 and 256 xx77 aliases, all F7 data bytes in four DOS/Shadow modes, xx77 address/data latch and D3 priority passed.
- Compiled checks passed: EFF7.D4 selection, xx77.D3 priority, raw 0BBD readback and hardware reset selecting 7 MHz.
- B06 bank regression and B07R1 write-protection regression passed using actual bus callbacks and synthetic memory.
- Source configurations and selected dependencies unchanged; accepted ROM verified in source/release free files and PAK entries: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-CLOCKPORT-B08-20260915-194633. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-CLOCKPORT-B08-20260915-194633\release. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-CLOCKPORT-B08-20260915-194633.log.
- User runtime before B08: B06 SD/FAT success, demo starts and used extended graphics work; B07R1 compiled protection and bank tests passed.
- User clock SCL results before B08: 3.5 MHz=07FE (2046), 7 MHz=0FFE (4094), 14 MHz=17FC (6140). Test counts loop iterations between frame interrupts; results approximately x1/x2/x3.
- Important limit: nominal 14 MHz still uses uniform x3 approximation. Per-access WAIT, cache, video arbitration, I/O waits and refresh-phase clock switching have not been implemented or accepted as conformant by B08.
- Further open work: remaining ports, full video mode/address/timing audit, INT timings, flash ROM protocol, full NMI, 12BD writes and second-SD-image replacement hang.
- Runtime B08 acceptance pending; a standard EFF7 clock test may retain the same numbers because its original exact address already worked.
- Decision: accept the CPU control port decoder only after compiled checks passed; do not mark all CPU/video timing as complete.

## ZXMAK2-v13-ZXEVO-BC-CLOCKBUS-B09-20260915-195707 - BaseConf CPU bus clock synchronization
- Base: ZXMAK2-v13-ZXEVO-BC-CLOCKPORT-B08-20260915-194633; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One logical node: CPU clock synchronization at bus callbacks. Four production files: MemoryPentEvo.cs (optional marker only), BusManager.cs, EventManager.cs, Interfaces/ICpuClock.cs.
- Existing instruction-end scaling made slow-mode memory/port callback timestamps too early. BaseConf now synchronizes elapsed master tacts before memory, IO, internal no-MREQ, interrupt/reset and signal-scan callbacks, with a final synchronization at cycle end.
- ICpuClockBusSync is optional and enabled only by MemoryPentEvo. Existing ICpuClock providers retain original scaling/timestamps; no ATM, Scorpion, TSConf device implementation changed.
- Existing requested clock selection and uniform x3 nominal-14-MHz approximation remain unchanged. This stage does not claim hardware WAIT/cache/video-arbitration conformance.
- References: BaseConf manual section 6; z80/zclock.v and z80/zmem.v show waits applied to individual accesses; Zilog Z80 instruction cycle timing.
- Release rebuild passed. Compiled real-Z80 tests passed: LD A,(nn), LD (nn),A, IN A,(n), OUT (n),A, 257 consecutive executions each in five provider modes (5140 instructions).
- Tests verify bus callback phases, unchanged instruction totals, fractional remainder without drift, null-clock and legacy-only providers, and inactive diagnostic calls.
- B08 clock decoder/gating/priority checks and B06/B07R1 bank/write-protection regression checks passed using actual bus and synthetic memory.
- machines.config and selected unrelated source dependencies unchanged. Accepted ROM verified in source/release free files and PAK entries: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-CLOCKBUS-B09-20260915-195707. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-CLOCKBUS-B09-20260915-195707\release. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-CLOCKBUS-B09-20260915-195707.log.
- User previous results retained: SD/FAT read OK, demo starts and used extended graphics work; B08 compiled clock-port/bank/protection tests passed.
- Clock SCL before B09: 3.5=07FE, 7=0FFE, 14=17FC. Re-run may differ slightly in interrupt-boundary counts; counts alone cannot establish real hardware WAIT accuracy.
- Pending: per-access 14 MHz WAIT, cache, DRAM/video arbitration, external IO waits, refresh-phase frequency latch, full video/port/INT audit, flash ROM, NMI/12BD writes, second SD image replacement hang.
- Runtime B09 acceptance pending.
- Decision: accept BaseConf bus timestamp synchronization only after compiled checks passed; full hardware clock/timing conformance remains pending.

## ZXMAK2-v13-ZXEVO-BC-FCLK28-B10-20260915-200412 - BaseConf 28 MHz master timebase
- Base: ZXMAK2-v13-ZXEVO-BC-CLOCKBUS-B09-20260915-195707; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One logical node: CPU, ULA and FDD master clock units. Four files: MemoryPentEvo.cs, UlaPentEvo.cs, FddPentEvo.cs and Interfaces/ICpuClock.cs (documentation only).
- Master multiplier 4->8 in BaseConf memory and ULA; FddPentEvo constructor clock scale 4->8. No other machine device implementation changed.
- CPU requested rate multipliers retained at 1/2/3; nominal 14 MHz still uses the old uniform effective-x3 approximation.
- CPU master tacts now represent FPGA 28 MHz phases, matching BaseConf z80/zclock.v, z80/zmem.v and dram/arbiter.v. A DRAM cycle consists of four FPGA clocks; exact odd FPGA-clock delays require this finer master resolution.
- Spectrum frame has 573440 master tacts instead of 286720; renderer geometry and frame duration in base tacts unchanged.
- Real FDD model verified at 28000000 master tacts/second; physical disk timing retained under the new unit scale.
- Compiled checks passed: ULA master/frame units, renderer tact conversion and preserved inherited INT duration. This is a unit-conversion check, not full video/INT conformance.
- Compiled real-Z80 memory/IN/OUT phase and total tests passed over 5140 instructions, including fractional remainder, legacy/no-clock providers and failure-state cleanup.
- B08 port decoder and B06/B07R1 bank/write-protection regression checks passed using real bus callbacks and synthetic RAM/ROM.
- Source configuration and selected unrelated dependencies unchanged; ROM verified in source/release free files and PAK entries: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Four Release projects rebuilt; executable and checked engine/hardware/host DLLs regenerated.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-FCLK28-B10-20260915-200412. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-FCLK28-B10-20260915-200412\release. Build log: K:\Download\ZXMAK2-v13-ZXEVO-BC-FCLK28-B10-20260915-200412.log.
- User previous acceptance retained: B09 compiled tests passed; SD/FAT works, demo starts and used extended graphics work.
- User old clock SCL results: 3.5=07FE, 7=0FFE, 14=17FC. Current rates retained; minor frame-boundary rounding differences remain possible.
- Pending: dynamic 14 MHz WAIT and cache/DRAM/video arbitration, external IO waits, refresh-phase switching, full video/port/INT audit, flash ROM, NMI/12BD writes, second SD image replacement hang.
- Runtime B10 acceptance pending; please check ERS, SD demo/video/sound, keyboard/mouse and the same clock test.
- Decision: accept the 28 MHz master-domain conversion only after compiled checks passed; do not mark hardware WAIT complete.

## ZXMAK2-v13-ZXEVO-BC-MEMWAIT-B11-20260915-205608 - BaseConf free-DRAM memory WAIT and read buffer
- Base: ZXMAK2-v13-ZXEVO-BC-FCLK28-B10-20260915-200412; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One logical node: per-access CPU memory timing. Four files: MemoryPentEvo.cs, BusManager.cs, EventManager.cs and Interfaces/ICpuClock.cs.
- Nominal 14 MHz CPU execution multiplier 3->4; added per-access master-tact delays replace the old uniform x3 approximation. Modes 3.5/7 MHz and 28 MHz CPU/ULA/FDD master units retained.
- Source: supplied BaseConf RTL z80/zmem.v free-DRAM WAIT table and cached_addr/rd_buf invalidation; dram/arbiter.v defines four FPGA clocks per DRAM cycle. This stage assumes cpu_next=1 and adds no video-denial model.
- DRAM phases cbeg/post_cbeg/pre_cend/cend: opcode miss delays 6/5/4/3 master tacts, ordinary read miss delays 5/4/3/2; cached reads and unprotected writes have zero additional delay under the free-DRAM assumption.
- Callback-to-dram_beg baseline convention: first 14 MHz falling edge, callback master timestamp +1. Exact FPGA/Z80 pin-edge alignment is pending; these results do not certify machine-wide 14 MHz timing accuracy.
- One 16-bit read word retained, logical address comparison A15..A1, correct even/odd byte selection. ROM access, unprotected RAM write, CPU IO/reset/INT/NMI acknowledgment invalidate the buffer. Protected RAM write hit retains it; a protected-write miss requests a read refill. Slow-mode reads reload DRAM without extra baseline delay.
- Baseline refill is synchronous in the callback; overlapping read-strobe/refill behavior, including protected-write misses, needs the later FPGA pin-edge/arbiter model.
- Timing hook runs after existing memory handlers so the final mapped page is used, including M1 mapping changes. Added master-tact delays excluded from subsequent CPU scaling.
- Prefix continuation fetches routed by the legacy core through RDMEM are classified for timing only; indexed-CB displacement reads remain ordinary reads. Core implementation and memory handler dispatch unchanged.
- Compiled checks passed: all four M1/read WAIT table phases, actual buffered byte data and invalidation, ROM exclusion, protected/unprotected writes and slow modes.
- Real Z80 bus regression passed over 5140 instructions with an independent literal WAIT/cache reference, including nominal 14 MHz, fractional scaling, no-clock and legacy providers. Three real ED/CB/DD prefix examples passed; injected callback failure cleanup and inactive diagnostics checked.
- B08 clock-port, B06 bank identity and B07R1 write-protection checks passed. Existing 28 MHz ULA/renderer/INT scaling and real FDD model clock checks passed; video/INT geometry not certified by these checks.
- Four Release projects rebuilt; checked executable/engine/hardware/host outputs regenerated.
- Configuration, selected unrelated dependencies and accepted source/release ROM unchanged: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Before/after backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-MEMWAIT-B11-20260915-205608. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-MEMWAIT-B11-20260915-205608\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-MEMWAIT-B11-20260915-205608.log.
- User acceptance retained from B06: SD/FAT works, demo starts and the extended graphics used by it display. Second SD image hang still open.
- Pending: DRAM/video arbitration, external-port waits, RFSH-only frequency switching, FPGA pin-edge calibration, full video/port/INT audit, flash ROM, NMI/12BD writes, second SD image replacement.
- B11 runtime acceptance pending. Clock SCL counts may change from old 07FE/0FFE/17FC; do not use a preset 14 MHz count as a correctness target.
- Decision: accept the compiled free-DRAM WAIT/read-buffer baseline only; do not mark full hardware WAIT or video arbitration complete.

## ZXMAK2-v13-ZXEVO-BC-DRAMARB-B12-20260915-212704 - BaseConf CPU/video DRAM arbitration
- Base: ZXMAK2-v13-ZXEVO-BC-MEMWAIT-B11-20260915-205608; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One logical node: CPU/video DRAM arbitration and its common raster timebase. Two files changed: MemoryPentEvo.cs and UlaPentEvo.cs. Shared CPU/Engine timing implementation unchanged from B11.
- BaseConf dram/arbiter.v cend decisions ported: eight-cycle blocks, mandatory video quotas, CPU priority until the quota deadline; block state retained until completion when go or bandwidth changes.
- Actual BaseConf video_modedecode selects 1/8 for RG=3 with pent_vmode other than 2, otherwise 1/4. Generic 1/2 and full-block denial supported by the arbiter but never assigned to a machine mode by this stage.
- top.v pent_vmode bit order preserved: EFF7 D0 is the high bit, D5 the low bit.
- video_sync_h/v and video_fetch normal 448x320 fetch windows represented in DRAM cycles, including registered go visibility at the arbiter. Standard fetch: lines 80..271, horizontal cycles 123..378. Wide graphics: lines 76..275, horizontal 91..410. Wide text: same lines, horizontal 87..410.
- CPU RAM misses/writes reserve DRAM cycles; cache hits and ROM accesses do not create a DRAM request. Delay combines denial until cpu_next and B11 read-miss phase delay at resumed cbeg.
- Ordinary frame duration in all seven supported renderer selections set to 71680 base tacts / 573440 master tacts; extended renderer inherited 69888-tact frames corrected inside UlaPentEvo. Other machine ULA implementations untouched.
- Idle cycles advance arbiter state. Port mode writes advance old profile to the IO timestamp before selecting the new profile; current block quota retained. Reset/backwards timestamps rebuild an idle history of more than one line.
- Compiled checks passed: six RTL arbiter traces, 1024 request masks with exact quotas, quota retention across go/bandwidth changes, 512 mode/fetch boundary checks and an integrated RAM-write denial/recovery case.
- All seven implemented renderer frame lengths passed. B11 free-DRAM WAIT/buffer, prefix, real-CPU bus trace, clock-port, bank identity, protection and existing 28 MHz ULA/FDD checks passed.
- Four Release projects rebuilt; checked outputs regenerated. Configuration, selected unrelated dependencies, Engine timing source and accepted ROM unchanged: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-DRAMARB-B12-20260915-212704. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-DRAMARB-B12-20260915-212704\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-DRAMARB-B12-20260915-212704.log.
- B06 user acceptance retained: SD/FAT works, demo starts and used extended graphics display. Second SD image replacement hang still open.
- Limits: normal raster only. AVR-controlled 60 Hz/48K/128K raster selection, exact INT/raster/Z80 pin-edge alignment, asynchronous refill overlap, external IO waits and RFSH-only frequency switching remain pending. This callback model is not a complete FPGA pin simulation.
- Full video address/palette/geometry and port/INT audit, flash ROM, NMI/12BD writes and SD replacement remain pending.
- Runtime B12 acceptance pending. Clock counts and extended-mode cadence may change; old preset counts are not a hardware correctness target.
- Decision: accept compiled normal-raster DRAM arbitration and common frame units only; do not mark full machine timing conformance.

## ZXMAK2-v13-ZXEVO-BC-IOWAIT-B13-20260915-214741 - BaseConf external-port IO wait budget
- Base: ZXMAK2-v13-ZXEVO-BC-DRAMARB-B12-20260915-212704; verified current source/release; ERS v0.61.01 FE; Git HEAD: aaf19da24ff5d333c0de531f44d815a128cd1eab.
- One logical node: ordinary IN/OUT external-port waits. Four files: MemoryPentEvo.cs, BusManager.cs, EventManager.cs and Interfaces/ICpuClock.cs.
- RTL zports.v external_port decoder: exact low FD with A15=1 for AY; exact low 1F/3F/5F/7F for VG93 while dos || shadow_en_reg. Both read/write aliases covered.
- At nominal 14 MHz, zclock.v io_wait_cnt states 8..F produce 1,1,1,1,1,0,1,0: six stalled 28 MHz master clocks. The hook accounts for this total before device IO callbacks; it does not slow every 4T IO machine cycle uniformly to 7 MHz.
- Modes 3.5/7 MHz, internal ports, interrupt/NMI acknowledgment and inactive diagnostics incur no added ordinary-port delay.
- Added master delay excluded from subsequent CPU scaling; read/write addresses and byte values retained. DRAM idle time includes elapsed IO wait naturally at the next memory access.
- Optional ICpuPortTiming extends the existing Evo memory timing contract; no-clock and legacy-only providers retain old timing.
- Compiled checks passed: 786432 address/speed/DOS/Shadow decoder cases, expected 128/1152 alias counts, six-master-tact budget, 144 real IN/OUT instructions with callback/total/data checks, legacy/no-clock, diagnostic and acknowledgment exclusions.
- B12 arbiter/fetch/frame and B11 WAIT/buffer/prefix/CPU bus tests passed, along with bank, protection, clock-port and 28 MHz ULA/FDD regressions.
- Four Release projects rebuilt. Existing DRAM and ULA code, configuration, selected unrelated dependencies and accepted source/release ROM unchanged: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IOWAIT-B13-20260915-214741. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-IOWAIT-B13-20260915-214741\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-IOWAIT-B13-20260915-214741.log.
- User B12 runtime acceptance: checked functions work; no reported obvious regression. B06 SD/FAT and demo acceptance retained. Second SD image hang not yet retested.
- Limits: IO stall waveform is aggregated as a total; its exact separated pin-edge placement and overlap with other stall signals remain pending. RFSH-only frequency switching, alternative rasters and complete INT/video phase calibration also remain pending.
- Full video/address/palette/port audit, flash ROM, NMI/12BD writes and SD replacement remain pending.
- Runtime B13 acceptance pending. This stage does not certify complete machine-wide IO/clock timing accuracy.
- Decision: accept compiled external-port decoder and master-tact wait budget only; do not mark exact FPGA pin timing complete.

## ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507 - BaseConf RFSH clock latch
- Base: ZXMAK2-v13-ZXEVO-BC-IOWAIT-B13-20260915-214741; verified current source/release; ERS v0.61.01 FE. Machine: ZX-Evo BSconf. One node: requested/applied clock separation and logical refresh-boundary latch.
- RTL reference: fpga/baseconf/trunk/z80/zclock.v; int_turbo <= turbo on sampled /RFSH assertion, not at a port write or instruction boundary.
- Zilog UM008011-0816, instruction-fetch section and interrupt diagrams: https://www.zilog.com/docs/z80/um0080.pdf
- MemoryPentEvo CpuClockMultiplier reports the applied mode; RequestedCpuClockMultiplier decodes raw EFF7.D4/xx77.D3 with existing priority and gates. Port writes/readback do not latch it.
- Optional ICpuRefreshClock: settle elapsed CPU time at the old mode, latch the request, account for subsequent CPU time at the new mode. Master ratio 8 and applied ratios 1/2/4 divide exactly.
- CPU hook at logical T3 refresh entry for ordinary/prefix/HALT M1; indexed DD/FD-CB displacement/final opcode are excluded. Real NMI/INT acknowledgment paths have refresh hooks; RESET's synthetic R increment does not.
- Existing reset baseline remains 7 MHz. This does not add a hardware reset to RTL int_turbo, which has no rst_n branch in the referenced clock-latch block.
- Memory/IO WAIT logic now uses the applied frequency. Existing DRAM arbitration and external IO budget retained.
- Compiled checks passed: 36 old/new clock/master-phase transitions, four real ED OUT(C),A clock-port sequences, prefix refresh, indexed-CB exclusions, HALT, last request, diagnostic exclusion, real NMI/INT and RESET.
- Previous steady-state tests explicitly latch their setup mode before testing: 786432 IO decoder cases, 144 IN/OUT instructions, 5140 CPU traces, DRAM quota/fetch/frame, WAIT/buffer, clock-port, 4MB bank, protection and 28 MHz ULA/FDD checks passed.
- Five Release projects rebuilt; CPU/Engine/Hardware/WinForms/EXE outputs regenerated.
- Config, ULA renderer, selected unrelated dependencies and accepted source/release ROM unchanged: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507.log.
- User B13 runtime acceptance: checked functions work; no reported breakage. B06 SD/FAT error gone and demo starts. Second SD image replacement hang not yet retested.
- Limits: logical sampled refresh-boundary model only. Exact returned Z80 clock propagation, phase alignment on speed changes, separated IO stalls and stall overlap remain pending. Alternative rasters and complete INT/video phase calibration remain pending.
- Full video/address/palette/port audit, flash ROM, NMI/12BD writes, SD replacement and FPS/audio/tape calibration remain pending.
- Runtime B14 acceptance pending. Do not mark complete machine timing or every port/video mode compliant.
- Decision: accept compiled logical RFSH clock latch only; exact FPGA pin timing remains open.

## ZXMAK2-v13-ZXEVO-BC-RASTERTABLE-B15-20260915-231652 - BaseConf shared raster table
- Base: ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507; verified current source/release; ERS v0.61.01 FE. Machine: ZX-Evo BSconf. One node: shared immutable steady-raster geometry/fetch table; runtime remains normal.
- Two changed production files only: Evo/MemoryPentEvo.cs and Evo/UlaPentEvo.cs. No project file, Engine/CPU, AVR-emulation or other machine implementation edit.
- References: user-supplied pentevo-fpga.r1364.tar.gz and pentevo-avr.r1364.tar.gz, BaseConf trunk.
- FPGA archive SHA256: 7a509fbcef3af85380ec475ad622a0682e714af54625851fcb9aa678da0bbb82; AVR archive SHA256: de70b7940b5d33a2698b498bd9f2320a16f9fd9c241ad23e21b3da356c4bde1e.
- RTL modes_raster 00/01/10/11: normal 448x320, 60Hz 448x262, 48K 448x312, 128K 456x311 DRAM cycles/lines.
- Base frame tacts: 71680/58688/69888/70908; master frame tacts: 573440/469504/559104/567264.
- ULA normal frame/line/vertical start and DRAM fetch now consume the same normal profile. Normal timings and all seven renderer selections retained.
- Alternate profiles verified as data only; none selectable or activated in the emulator by B15.
- Compiled raster checks: immutable descriptors, periods/unit conversion, raw RTL INT coordinates as data only, exhaustive steady fetch across each full raster/all RG/pent cases, periodic wrap and invalid inputs.
- Original B14 compiled RFSH / external IO / DRAM / WAIT / CPU / bank / protection / frame / FDD regression probe retained unchanged and passed.
- ROM unchanged; accepted ERS v0.61.01 FE SHA256: 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca, checked in all four source/release representations. User reports ROM originates from r1364; provenance not independently established by these probes.
- Config and all backed-up unrelated source/library files unchanged; five Release projects rebuilt.
- Audit correction: temporary folder 05819 was not a proven SVN revision number. Audit now explicitly uses SVN r1364.
- AVR r1364 main.h: MODES_RASTER=0x30, MODE_VIDEO_MASK=0x31. zx_set_config sends bits 5:4 through SPI config0 0x50; top.v connects them to modes_raster.
- AVR r1364 Scroll Lock cycles eight TV/VGA and raster states. Old Shift+ScrollLock behavior is commented out; the older manual is not authoritative for this current control path.
- AVR version.c extension type 3 reads modes_register at index 0, FF otherwise; current CmosPentEvo lacks this mode. Not repaired in B15.
- User B14 clock-loop runtime: 3.5=07FE (2046), 7=0FFE (4094), 14=1970 (6512). Loop counts are not physical clock/WAIT measurements; full B14 runtime acceptance remains open.
- B06 FAT first-card error gone, demo starts; B13 user reported no breakage. Second SD image hang not retested/fixed.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RASTERTABLE-B15-20260915-231652. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-RASTERTABLE-B15-20260915-231652\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-RASTERTABLE-B15-20260915-231652.log.
- Pending next node: AVR raster-controller/readback plus coordinated alternative-raster activation. Exact transition policy, INT/video phase, border sync and 48K/128K contention need separate verified work.
- Still pending: pin-edge/stall overlap/refill, full video/port audit, FLASH/NMI/12BD, SD replacement, FPS/audio/tape and comprehensive runtime acceptance.
- Runtime B15 acceptance pending. Do not mark alternate raster emulation or full machine timing complete.
- Decision: accept compiled shared raster table and unchanged normal runtime only; alternative raster activation remains pending.

## ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838 - BaseConf AVR RDCFG protocol baseline
- Base: ZXMAK2-v13-ZXEVO-BC-RASTERTABLE-B15-20260915-231652; verified current source/release; ERS v0.61.01 FE. Machine: ZX-Evo BSconf. One node: AVR extension selector and normal-only read-config protocol.
- Installer R1 corrects only the reviewed B15 ULA terminal-newline hash assumption. User's initial B16 attempt 20260915-233521 stopped in Preflight with patch applied False; Memory/CMOS hashes matched and ULA matched the reviewed payload without terminal LF. Backup comparisons and exact hash guards remain mandatory.
- One changed production file: Evo/CmosPentEvo.cs. ULA/DRAM/CPU/Engine/project/config/ROM implementation unchanged.
- Source: user-supplied pentevo-avr.r1364.tar.gz, baseconf/trunk/src/version.c GetVersionByte/SetVersionType and main.h EXT_TYPE_RDCFG=3.
- AVR archive SHA256: de70b7940b5d33a2698b498bd9f2320a16f9fd9c241ad23e21b3da356c4bde1e.
- Type 3 selected through an existing F0..FF CMOS data write; F0 reads 00, F1..FF read FF. Repeated reads do not consume data.
- IMPORTANT: 00 is the explicit emulator protocol baseline: normal raster, TV, auxiliary flags off. This is not a complete modes_register implementation or independently measured hardware configuration.
- No NVRAM restoration/persistence of the AVR configuration; no VGA, Scroll Lock, tape mux or LED control added. Do not infer implemented control from readback.
- Unknown selector bytes 4..255 retained and read FF; no longer silently choose BaseConf version. Types 0/1 version data, type 2 PS/2 FIFO, reset selection, existing visibility/aliases and legacy buffer-clear policy retained.
- Full CMOS GLUK reg C EEPROM-banking/LED/status semantics and physical RTC/NVRAM mapping are NOT certified by this stage.
- Before editing: accepted B15 compiled raster-table and unchanged B14 regression probes passed, plus compiled old CMOS baseline.
- After rebuilding: compiled 256-selector x 16-index readback, exact 65536-address subscription maps, A8/Shadow/CMOSEN gates, repeated config reads, reset, PS/2 FIFO and NVRAM regression checks passed.
- Original B15 RasterTableProbe and B14 RefreshProbe function bodies unchanged and passed again. Five Release projects rebuilt with fresh output checks.
- Accepted ROM unchanged: ERS v0.61.01 FE, 524288 bytes, SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca, checked in all four source/release representations. User reports ROM from r1364; provenance not independently proven.
- All backed-up unrelated source/library files and configuration unchanged. Complete before/after source/release/journals retained, with changed-source copies for parent checks.
- B15 user reports BUILD READY for 20260915-231652 with compiled probes passed; comprehensive B15 runtime acceptance remains open.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838.log.
- Next node: actual AVR raster control with coordinated ULA/DRAM/frame-counter activation. Transition phase, INT, border sync and 48K/128K contention require verified work.
- Still pending: FPGA pin-edge/stall overlap/refill, full video/port audit, FLASH/NMI/12BD, second SD-image replacement, FPS/audio/tape and comprehensive runtime acceptance.
- Runtime B16 acceptance pending. Do not mark complete AVR configuration or alternate-raster emulation done.
- Decision: accept compiled normal-only AVR RDCFG protocol baseline only; actual AVR control and alternate-raster activation remain pending.


## ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-20260916-003323 - failed stage
- FAILED: ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-20260916-003323; phase: B17 rebuild and compiled activation / B16 / B15 / B14 probes; AVR raster-control patch applied: True; error: Build failed: ZXMAK2.Hardware (exit 1)
- Original backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-20260916-003323\before; do not treat this build as accepted.

## ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-R1-20260916-004056 - BaseConf AVR steady raster control / build repair R1
- Base: ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838; verified current source/release; ERS v0.61.01 FE. Machine: ZX-Evo BSconf. Recovery from failed ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-20260916-003323. B16 handoff and failed-build before/src verified against all 806 pinned entries; current source verified as exact B17 payload plus unchanged B16 entries.
- R1 fixes the missing ZXMAK2.Engine.Attributes import in UlaPentEvo.cs and CmosPentEvo.cs only. Original B17 stopped with CS0246 and remains FAILED; no prior B17 success is claimed. R1 rebuilds all six projects and reruns all four probes.
- One logical node inherited from B17: supported AVR video bits 0/4/5 and coordinated steady-raster frame epochs. Ten original B17 source/resource files; R1 changes two imports only; no csproj, ROM, CPU clock-latch implementation, FDD or distinct non-Evo machine implementation edit.
- Source: user-supplied AVR and FPGA BaseConf trunk r1364. AVR archive SHA256 de70b7940b5d33a2698b498bd9f2320a16f9fd9c241ad23e21b3da356c4bde1e; FPGA archive SHA256 7a509fbcef3af85380ec475ad622a0682e714af54625851fcb9aa678da0bbb82.
- Host ScrollLock enum appended without shifting old key identities; WinForms and MDX embedded maps extended; PS/2 make 7E and break F0 7E logged.
- Scroll Lock rising edge cycles 00,01,10,11,20,21,30,31,00 independently of extension selector; held/release events don't repeat. Shift does not select the obsolete commented-out AVR behavior.
- RDCFG type 3 F0 reads supported video bits, F1..FF read FF. Other AVR modes_register flags (tape/LED/etc.) are NOT emulated by this field. Physical TV/VGA signal generation is NOT emulated by bit 0.
- Supported video bits restored from the previously inaccessible .cmos slot FE and persisted there; unsupported stored flags preserved but not advertised as emulated. This is a bounded mapping to the existing .cmos container, not full RTC/GLUK EEPROM/address equivalence. Z80 reset does not reset AVR video selection.
- Requested and active raster separated. IMPORTANT APPROXIMATION: activation occurs at the next software frame boundary (instruction-overrun handling included), not at the immediate FPGA register/hsync edge of r1364. No exact mid-frame transition claim.
- Active periods: normal 448x320, 60Hz profile 448x262, 48K 448x312, 128K 456x311 cycles/lines; frame master tacts 573440/469504/559104/567264. Bit 0 TV/VGA does not change the raster period.
- ULA, Engine and DRAM share the absolute frame origin; changing period does not remap CPU absolute master time through a different modulo. All seven supported renderer selections share the chosen frame period.
- Alternate renderer line/paper windows rebuilt; 60Hz bottom border clipped to the available frame lines. Legacy normal drawing phase restored on return to normal. Full border/pixel phase and palette/video-address audit remain pending.
- DRAM fetch follows the active steady profile and epoch; old idle time settled where possible and in-flight block quota/cursor retained. Backwards timestamp reanchors use reset/replay approximation. Edge overlap/refill and exact instruction-overrun transition remain pending.
- Optional IUlaFrameTiming contract added in existing interface file. Only UlaPentEvo implements it. Legacy engine cached/modulo and ULA behavior retained; existing clock interfaces/functions unaffected.
- SoundDeviceBase optional path adopts the new frame start/length while preserving FrameSound buffer identity and over-frame samples at the same sample rate. Host/resampler cadence remains inherited 50 FPS. Physical raster Hz / wall-clock CPU rate / audio / tape calibration NOT completed; transition filter/phase accuracy remains pending.
- Before R1 editing: original B14 RefreshProbe, B15 RasterTableProbe and B16 normal-only AvrReadConfigProbe passed using the preserved, verified B16 release in failed-build before/release. These function bodies retained unchanged and passed again after rebuilding.
- New compiled checks passed: supported NVRAM/readback mask, eight Scroll states/hold/release/Shift/reset/log, all 16 old/new profile transitions, master-phase overrun, latest request/rewind, seven renderer periods/lines/full draw, anchored fetch boundaries/wrap, in-flight quota preservation, real Engine ExecCycle/FrameReady/sound epoch/buffer, embedded keyboard resources and legacy cached path.
- Six Release projects rebuilt (Host, CPU, Engine, Hardware, WinForms, EXE); checked outputs fresh. Source machine config and every unrelated backed-up source/dependency file unchanged; exact changed-source byte hashes verified.
- ROM unchanged: ERS v0.61.01 FE, 524288 bytes, SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca; source/release/portable free files and PAK entries verified. User reports ROM from r1364; independent provenance not established.
- Recovery backup before/src and before/release capture the failed B17 state, NOT a runnable accepted B16 release. Accepted B16 remains in L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AVRRDCFG-B16-20260915-233838 and original B17 rollback base remains in L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-20260916-003323\before.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-R1-20260916-004056. Separate release: K:\Download\ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-R1-20260916-004056\release. Transcript: K:\Download\ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-R1-20260916-004056.log. Full before/after source/release/journals retained. Installer does not launch ZXMAK2 or reset the user's VM.
- User B16 confirmed BUILD READY 20260915-233838, not comprehensive runtime acceptance. B06 first-card FAT error gone/demo starts and B13 no reported breakage retained; second SD-image hang not retested/fixed.
- Runtime B17 acceptance pending: ERS boot, keyboard/mouse, first SD demo/video/sound; eight plain Scroll presses back to normal; return-to-normal clock test; persistence after closing/reopening. Do not use alternate profiles as certified 48K/128K timing yet.
- Still pending: exact immediate transition/horizontal-vcount/INT, border sync, 48K/128K contention, IO/WAIT pin-edge/stall overlap/refill, full video/port audit, FLASH/NMI/12BD, SD replacement, physical FPS/audio/tape and comprehensive runtime acceptance.
- Decision: accept compiled supported-AVR-video / steady-raster epoch model with explicit software frame-boundary approximation only; not complete hardware timing compliance.

