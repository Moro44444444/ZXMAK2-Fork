# ZXMAK2 Fork — полный перенос контекста 0006 — 2026-09-18

Дата фиксации: 18 сентября 2026, после пользовательского отклонения runtime B24.

Этот документ является файловой точкой передачи длинного чата. Он не заменяет `PROJECT_JOURNAL.md`, `FORK_CHANGELOG.md`, backup и исходники, а задаёт правильный порядок их чтения и фиксирует результаты, появившиеся после последней сборки. Область работы — только **ZX-Evo BSconf / BaseConf**.

## 1. С чего должен начать новый чат

До любых изменений полностью прочитать в таком порядке:

1. `CHAT_CONTEXT_TRANSFER_2026-09-18_B24.md` — этот документ.
2. `PROJECT_JOURNAL.md`.
3. `FORK_CHANGELOG.md`.
4. `BASECONF_VIDEO_MODES_B19.md` перед любыми изменениями видео.
5. `backup\ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541\RESULT.md` и фактические текущие исходники перед продолжением звука.

Затем выполнить только read-only сверку `git status`, текущего дерева, B24 backup и B24 runtime-пакета. Нельзя начинать повторно уже выполненные B01–B24 или принимать B24 по compiled probes: пользовательская runtime-проверка B24 отрицательная.

## 2. Точная точка продолжения

Последняя **runtime-принятая функциональная точка**:

`ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642`

- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642`.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642`.
- Пользователь подтвердил правильную палитру в сравнении с UnrealSpeccy, отсутствие прежнего мусора в ATM 640×200 HWM и прохождение остальных тестов `BCVIDTEST.TRD`.

Последняя **собранная и compiled-проверенная, но runtime-отклонённая точка**:

`ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`

- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`.
- B24 очистила DirectSound ring buffer до `Play` и заменила удержание последнего сэмпла при underrun на стереорампу к нулю.
- Build и compiled probes прошли, но пользовательский дефект остался. B24 нельзя называть принятой и нельзя автоматически повторять или откатывать без анализа.

Следующий объединённый этап: **B25+B26 — измерительная локализация аудиофона плюс минимальное исправление DC/цифровой тишины**. Две задачи разрешено выполнять в одном цикле ради экономии лимита, но диагностика должна предшествовать выбору исправления внутри этого цикла.

## 3. Фактический результат пользовательской проверки B24

Пользователь проверял в наушниках и сравнивал ту же программу/игру с UnrealSpeccy:

- небольшой фон появляется сразу после чистого запуска ZXMAK2, без других действий;
- при работе дисковода, синхронно с миганием FDD-индикатора, появляются скрежет/щелчки;
- в игре звук ZXMAK2 остаётся нечистым и содержит небольшие артефакты;
- в UnrealSpeccy на том же материале фон отсутствует и звук чище.

Вывод: дефект B24 в host buffer был реальным защитным недостатком, но не являлся основной причиной наблюдаемого фона. Runtime B24 отклонён.

## 4. Новая сверка с UnrealSpeccy и BaseConf после B24

Пользователь дал точную ветку:

`http://svn.nedopc.com/listing.php?repname=pentevo&path=%2Ftools%2Funreal_fix%2F0.39.0%2FUnreal_NS%2F`

Установленные факты:

1. В актуальном `sound.cpp` UnrealSpeccy PentEvo финальный стереомикс проходит через `RejectDC`; параметр включён по умолчанию. После выдачи буфера его ring-ячейки обнуляются.
2. В `src\ZXMAK2.Host\Entities\FrameSound.cs` ZXMAK2 суммирует независимые источники и делит результат на их количество. Финального DC reject там нет.
3. `src\ZXMAK2.Hardware\SoundDeviceBase.cs` хранит внутренний unsigned DAC с начальным midpoint `0x8000`, но функция `GetSigned` переводит unsigned zero в signed `-32768`.
4. AY после reset имеет нулевые регистры и выдаёт unipolar zero; Beeper и Covox также являются unipolar источниками. Их нулевые/низкие уровни нельзя автоматически считать PCM-нулём без проверки всего тракта.
5. В `src\ZXMAK2\machines.config` у ZX-Evo BSconf одновременно подключены отдельные `AYCHRV`, `BeeperDevice #FE` и `CovoxMono #FB`.
6. По документации BaseConf `#FE.D4` управляет beeper, а `#FB` — unsigned Covox; это общая физическая линия: запись `#FB` передаёт её Covox, следующая запись `#FE` возвращает обычный beeper. Текущая независимая сумма двух устройств аппаратному переключению не соответствует.
7. `AYCHRV` не задаёт собственную частоту и наследует `1773400` Гц из `PsgChip`; актуальная ветка UnrealSpeccy PentEvo задаёт `1750000` Гц. Это может менять тон/темп, но само по себе не объясняет фон на чистом старте.
8. В текущем `Wd1793.cs` нет реализованного проигрывания механического звука FDD; есть лишь исторический TODO. Значит слышимый «дисковод» — паразитный эффект или корреляция портовой активности, а не заявленная функция.
9. VG93 не подаёт звук непосредственно. Условия VG93 Force Interrupt/index/ready остаются отдельной задачей и не должны смешиваться с первым исправлением аудиотракта.

Это пока анализ, не реализованные исправления. После B24 код звука, AY, VG93 и конфигурация `#FB/#FE` не менялись.

## 5. Объединённый план с экономией циклов

### B25+B26 — диагностика PCM и исправление DC/тишины

В одном рабочем цикле:

1. Создать compiled probe/диагностический захват, который измеряет mean, RMS, peak и межбуферный скачок отдельно для AY, Beeper, Covox и итогового микса.
2. Проверить холодный reset, устойчивый idle, одиночные записи `#FE/#FB` и последовательность во время VG93 activity.
3. По измерению выбрать минимальный уровень исправления: корректное представление тишины/инициализация источника и/или финальный DC blocker по модели UnrealSpeccy. Не делать глобальную замену `SoundDeviceBase` вслепую: она затрагивает другие машины.
4. Зафиксировать сохранение полезного AY/Beeper/Covox сигнала, независимость каналов, отсутствие повторного transient на каждой границе кадра и корректный Reset.
5. Build и compiled probes после исходных правок; отдельные backup/runtime/журналы.
6. Пользователь проверяет чистый запуск, минутный idle, FDD load и ту же игру. Runtime-приёмку до его результата не заявлять.

### B27+B28 — общая линия `#FB/#FE` и параметры AY

Только после пользовательского результата B25+B26:

1. Реализовать документированное владение общей линией: `#FB` выбирает/обновляет Covox, следующая `#FE` возвращает Beeper; сохранить точный tact записи и Reset-состояние.
2. Одновременно в этом цикле задать для ZX-Evo документированную/сверенную частоту AY `1750000` и проверить CHRV, порты, reset, envelope/noise, amp/pan tables.
3. Не менять AY других машин без отдельного основания.
4. Build/probes, отдельные runtime/backup/журналы и пользовательское сравнение.

### B29+B30 — VG93 и итоговая звуковая приёмка

Только после очистки звукового тракта:

1. Сверить команды VG93, DRQ/INTRQ, Force Interrupt index/ready, head step, track/sector и задержки по документации.
2. Доказать, какие операции во время дисковой загрузки затрагивают `#FE` или создают underrun/скачок PCM.
3. Выполнить общий runtime-набор: cold start, idle, TR-DOS, FDD load, Beeper, Covox, AY и проблемная игра.

## 6. Что уже сделано — не повторять

| Этап | Результат и статус |
|---|---|
| B01 | Keyboard FE/F6: mask/port/noDos исправлены; ERS, клавиши и мышь пользователь подтвердил. |
| B02 | Точный BaseConf border decode FE/F6/FC, compiled полный адресный тест. |
| B03 | SD read decode `xx57` в Shadow+A15 исправлен. |
| B04 | Beeper exact FE, D4, без MIC, доступ вне DOS gate. Это портовый decode, не общий аудиотракт. |
| B05 | Covox exact FB, весь байт, DOS-доступ. Это decode, не переключение общей линии. |
| B06 | Исправлен порядок старших RAM bank bits; FAT первой SD заработал, демо запускается. |
| B07/B07R1 | Защита записи проверена; исходный B07 probe был ошибочен, R1 исправил тест, не production-код. |
| B08 | CPU clock control ports EFF7/xx77 decode. |
| B09 | Синхронизация master timestamps на CPU bus callbacks. |
| B10 | 28 MHz master timebase. |
| B11 | Memory WAIT/read buffer baseline. |
| B12 | CPU/video DRAM arbitration normal raster; пользователь не сообщил регрессии. |
| B13 | External I/O WAIT budget; пользователь подтвердил работу. |
| B14 | RFSH clock latch; пользовательские counts 3.5=`07FE`, 7=`0FFE`, 14=`1970`. Не физическая частотная сертификация. |
| B15 | Общая immutable steady-raster geometry/fetch table, compiled checkpoint. |
| B16 | AVR RDCFG protocol baseline, compiled checkpoint. |
| B17 | Исходный этап failed; не использовать. |
| B17-R1 | Исправленная сборка steady raster control, compiled checkpoint. |
| B18 | OSD diagnostic Scroll/AVR/TV-VGA/requested/active; пользователь подтвердил восемь состояний и видимую смену развёртки. |
| B19 | Паспорт семи реальных renderer paths r1364; production-код не менялся. |
| B20 | Общая raster/picture-window/border/INT основа; пользователь качественно подтвердил переключение без артефактов. |
| B21 | Первый TRD shell не читался; не принят. |
| B21-R1 | Исправлен TR-DOS вход в `boot.B`; shell/menu принят и стал базой B22. |
| B22 | Общий Spectrum pattern не соответствовал физической раскладке режимов; runtime отклонён. |
| B22-R1 | Format-aware patterns, но BOOT портил BaseConf mapping; runtime отклонён. |
| B22-R2 | Безопасный BaseConf physical page mapper; чистый BOOT/menu принят. |
| B23 | Исправлены глобальные palette caches и IRQ-мусор теста ATM 640; палитра, ATM 640 и остальные семь тестов пользователь подтвердил. Runtime принят. |
| B24 | DirectSound startup clear и underrun ramp compiled PASS, но фон/FDD-correlated/game artifacts остались. Runtime отклонён. |

Подробности, точные файлы, probes, hashes и решения находятся в двух журналах. Не пересобирать и не «исправлять» старые этапы без нового доказанного расхождения.

## 7. Видео: принятое и ограничения

Исполняемый RTL r1364 содержит семь подтверждённых путей:

1. ZX 256×192 attributes.
2. Pentagon hardware multicolor.
3. Pentagon packed 16c.
4. ATM 320×200 16c.
5. ATM 640×200 hardware multicolor.
6. ATM text 80×25.
7. BaseConf one-page text 80×25.

`256c` и `16+16c` присутствуют в общей проектной матрице, но отсутствуют в исполняемом decoder/address/fetch/render r1364; не добавлять их как обязательные режимы текущей ревизии.

Последний принятый TRD:

`K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642\BCVIDTEST.TRD`

Размер 655360 байт, SHA-256:

`14423989A5819D9C2A74D11D54CE2205D947145BB716752B5B42A84419B89CCA`

Не путать визуальную runtime-приёмку с осциллографической сертификацией точных pixel clock, INT phase, border phase, contention или физического TV/VGA.

## 8. Постоянные пути и инструменты

| Назначение | Путь |
|---|---|
| Рабочий репозиторий | `L:\Work_two\ZX\ZXMAK2-Fork` |
| Журнал | `L:\Work_two\ZX\ZXMAK2-Fork\PROJECT_JOURNAL.md` |
| Changelog | `L:\Work_two\ZX\ZXMAK2-Fork\FORK_CHANGELOG.md` |
| Solution | `L:\Work_two\ZX\ZXMAK2-Fork\src\ZXVM.sln` |
| Machine config | `L:\Work_two\ZX\ZXMAK2-Fork\src\ZXMAK2\machines.config` |
| Рабочий release | `L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease` |
| Отдельные runtime | `K:\Download\<BuildId>` |
| Backup | `L:\Work_two\ZX\ZXMAK2-Fork\backup\<BuildId>` |
| Лог | `C:\Logs\ZXMAK2.log` |
| MSBuild | `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe` |
| C# compiler probes | `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` |

Принятый ROM: ERS v0.61.01 FE, 524288 байт, SHA-256:

`620146534DF8A49C6B9042DF45812D1E7F90683DD8F7B813CA2C5ECAC96DC1CA`

Не возвращать старый ROM/PAK. Известное предупреждение отсутствующего `AllRules.ruleset` не является ошибкой сборки.

## 9. Состояние git на момент переноса

- Репозиторий: `L:\Work_two\ZX\ZXMAK2-Fork`.
- Branch: `main` tracking `origin/main`.
- HEAD: `d86c4ce426582d5e8227ed33c54be498f3aa4ea3`.
- Дерево намеренно грязное: оно содержит всю накопленную линию форка, новые tools, ROM, backups и изменения B01–B24. Нельзя использовать `git reset --hard`, `git checkout --` или считать весь diff изменениями последнего этапа.
- Перед каждым этапом сравнивать конкретные pinned-файлы с `after` предыдущего backup, а не с исходным git HEAD.
- Создание этого документа и запись отрицательного runtime B24 в два журнала — единственные изменения после аналитической сверки; production-код и бинарники после B24 не менялись и не собирались.

## 10. Обязательный рабочий порядок

1. Перед исходной правкой назвать выбранную базу, BuildId и ограниченный узел.
2. Проверить checkpoint против предыдущего backup.
3. Сохранить отдельный before/after backup.
4. После каждой исходной правки выполнить Release build и относящиеся compiled probes; не полагаться только на статический поиск.
5. Не менять другие машины и не расширять scope без отдельного поручения.
6. Обновлять `PROJECT_JOURNAL.md` и `FORK_CHANGELOG.md`.
7. Делать отдельный runtime-пакет в `K:\Download`.
8. Не запускать эмулятор за пользователя и не заявлять runtime-приёмку без его проверки.
9. При неудаче сохранять failed state и причину; не делать destructive rollback грязного дерева.
10. Разрешено объединять по две тесно связанные задачи для экономии лимита, но probes и выводы по каждой причине должны оставаться различимыми.

## 11. Открытые задачи, которые нельзя забыть

Кроме ближайшего звука:

- точные pin-edge/stall overlap/refill и физическая смена CPU clock;
- полная точность raster/INT/border/contention сверх визуально принятой модели;
- FLASH/NMI/системный `12BD`;
- зависание при замене первой SD-карты второй — после B06 первая FAT исправлена, второй image lifecycle не закрыт;
- physical FPS/audio/tape scheduler: исторически отмечено расхождение normal frame с фиксированными 50 FPS;
- полная карта оставшихся портов и collision/gate audit;
- VG93 Force Interrupt index/ready и прочие точные условия;
- итоговая комплексная runtime-приёмка ERS/NedoOS/SD/FDD/video/audio.

Не начинать эти пункты вместо B25+B26 и не объявлять уже закрытыми по одной демонстрации.

## 12. Стартовая фраза для нового чата

> Продолжаем ZXMAK2 Fork, только ZX-Evo BSconf. Полностью прочитай `CHAT_CONTEXT_TRANSFER_2026-09-18_B24.md`, затем актуальные `PROJECT_JOURNAL.md`, `FORK_CHANGELOG.md` и B24 `RESULT.md`. Сверь текущий репозиторий с backup `ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`, ничего не меняя. B23 video runtime принят; B24 compiled PASS, но runtime отклонён: фон на чистом старте, звук при FDD activity и нечистая игра остались. Не повторяй B01–B24. Следующий объединённый узел B25+B26: сначала измерить AY/Beeper/Covox/final PCM mean/RMS/peak/discontinuity, затем внести одно минимальное DC/тишина-исправление по результату. После исходных правок build/probes, журналы, отдельные backup/runtime; runtime-приёмку не заявлять до моего теста.

## 13. Контроль восстановления нити

Новый чат должен правильно назвать без подсказки:

- B23 как последнюю runtime-принятую точку;
- B24 как последнюю собранную, но runtime-отклонённую;
- точные симптомы B24;
- наличие `RejectDC` в UnrealSpeccy и его отсутствие в ZXMAK2;
- риск unsigned zero → signed `-32768`, независимого микса AY/Beeper/Covox и аппаратной общей линии `#FB/#FE`;
- частоты AY `1773400` в текущем ZXMAK2 и `1750000` в сверенной PentEvo ветке;
- отсутствие реализованного механического FDD sound generator;
- B25+B26 как следующий объединённый этап, а не повторное исправление DirectSound ring;
- принятые семь video paths и B23 TRD hash;
- открытый второй SD-image lifecycle и остальные незакрытые аппаратные фазы;
- запрет runtime-приёмки без пользовательского теста.

Если хотя бы один пункт не подтверждается файлами, новый чат обязан остановиться на read-only сверке и назвать конкретное расхождение, а не восстанавливать код по памяти.
