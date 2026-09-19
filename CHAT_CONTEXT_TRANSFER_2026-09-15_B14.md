# ZXMAK2 Fork — перенос рабочего контекста после B14

Дата: 15 сентября 2026. Пользователь: Павел Иванович, Moro.
Это продолжение действующего проекта. Актуальная область работы: ТОЛЬКО ZX-Evo BaseConf.

## 1. Точка продолжения

Последняя успешно собранная версия:

`ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507`

Запуск отдельного релиза:

`K:\Download\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507\release\ZXMAK2.exe`

Backup:

`L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507`

Автоматические проверки B14 прошли: переходы частоты в RFSH, IO, DRAM/кадр, CPU, порты частоты, банки и защита записи. Пользователь прислал BUILD READY, затем три скриншота теста частоты. Сохранить эти результаты как пользовательское испытание B14:

| Выбранный режим | FRAME LOOP COUNT, HEX | Десятичный счётчик |
|---|---:|---:|
| 3,5 МГц | 07FE | 2046 |
| 7 МГц | 0FFE | 4094 |
| 14 МГц | 1970 | 6512 |

Переключение влияет на реальную скорость выполнения тестового цикла. 7/3,5 ≈ 2,001; 14/7 ≈ 1,591. Это не универсальное измерение физической частоты или доказательство точности всех WAIT. Надпись MEASURED SPEED в старом тесте является результатом классификации счётчика. Она не измеряет фронты Z80.

На предыдущей B13 пользователь отдельно подтвердил: «Пока всё работает, ничего не поломалось». На B14 подтверждено исполнение теста во всех трёх режимах; полную повторную приёмку всех программ не заявлять.

Следующий выбранный этап: **B15 — альтернативные растры BaseConf**. B15 ещё НЕ написана, НЕ применена и НЕ собрана. Сначала изучить управление modes_raster в RTL/AVR, сверить с документацией и текущими классами; затем показать одну конкретную правку. Не обещать реализовать сразу весь растр, все порты и всё видео одним узлом.

## 2. Обязательные правила работы

1. Сначала прочитать этот файл, затем АКТУАЛЬНЫЙ PROJECT_JOURNAL.md, затем FORK_CHANGELOG.md. Старые прикреплённые журналы от 14 сентября не содержат всех B01–B14.
2. Истина о Windows-дереве — реальные исходники пользователя, backup принятой сборки и её RESULT.md. Перед правкой проверять git status и совпадение ключевых файлов с принятой базой.
3. Перед любой правкой показать: выбранную базу, номер следующей сборки и один логический узел; объяснить, что изменится в поведении.
4. Работать только с ZX-Evo BaseConf. ATM Turbo 2+, TSConf, Scorpion, Profi не менять. Не расширять список машин в этом этапе. В интерфейсе пока историческое имя готовой машины **ZX-Evo BSconf**.
5. Каждую сборку сохранять отдельно, с номером и временной меткой. Не выдавать перезаписанный _binrelease за отдельный откаточный релиз.
6. До изменения — полный backup исходников и предыдущего релиза; после изменения — сборка, существенные автоматические проверки, записи в обоих журналах, backup результата и отдельный релиз в K:\Download.
7. При ошибке не объявлять сборку готовой. Записать фазу, применена ли правка, FAILED.txt и текущее состояние. Не делать автоматический destructive rollback грязного рабочего дерева.
8. Давать готовый скачиваемый PowerShell .ps1 и точные команды его запуска. Пользователь не хочет больших ручных замен текста и многократных подтверждений.
9. Никаких скрытых автозапусков. Пользователь закрывает и запускает эмулятор сам. Скрипт должен остановиться, если ZXMAK2 ещё запущен.
10. Не заявлять, что код на L: изменён или сборка прошла, пока пользователь не запустил скрипт и не сообщил результат. У Codex в этом сеансе НЕ было прямого доступа к дискам L:, K:, C: пользователя.
11. Только подтверждённые факты. Не отмечать «все порты/все видеорежимы/все тайминги соответствуют» на основании одной демки или собственных тестов частичной модели.
12. Общие изменения Engine/CPU допустимы лишь с необязательными интерфейсами/хуками и сохранением прежнего поведения прочих провайдеров. В B14 такие изменения уже существуют; не откатывать их по одному имени папки.
13. Не выполнять git reset --hard, git checkout --, массовое удаление, push или замену пользовательских настроек без соответствующего поручения.
14. Общаться по-русски, обращаться «Павел Иванович». Команды пользователю — по возможности PowerShell. Промежуточные сообщения краткие и регулярные.

## 3. Все постоянные пути пользователя

| Назначение | Путь |
|---|---|
| Рабочая копия | L:\Work_two\ZX\ZXMAK2-Fork |
| Главный журнал | L:\Work_two\ZX\ZXMAK2-Fork\PROJECT_JOURNAL.md |
| Журнал изменений | L:\Work_two\ZX\ZXMAK2-Fork\FORK_CHANGELOG.md |
| Решение | L:\Work_two\ZX\ZXMAK2-Fork\src\ZXVM.sln |
| Конфигурация машин | L:\Work_two\ZX\ZXMAK2-Fork\src\ZXMAK2\machines.config |
| Рабочий Release-вывод | L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease |
| Отдельные готовые сборки | K:\Download\<BuildId>\release |
| Отчёт каждой сборки | K:\Download\<BuildId>\RESULT.md |
| Transcript сборки | K:\Download\<BuildId>.log |
| Скрипты для запуска | K:\Download\ZXMAK2-BaseConf-<NODE>-Bxx.ps1 |
| Backup | L:\Work_two\ZX\ZXMAK2-Fork\backup\<BuildId> |
| Лог эмулятора | C:\Logs\ZXMAK2.log |
| Исходный ROM | L:\Work_two\ZX\ZXMAK2-Fork\src\ZXMAK2\roms\EVO\zxevo.rom |
| Исходный пакет ROM | L:\Work_two\ZX\ZXMAK2-Fork\src\ZXMAK2\ROMS.PAK |
| ROM в релизе | L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease\roms\EVO\zxevo.rom |
| Пакет ROM в релизе | L:\Work_two\ZX\ZXMAK2-Fork\src\_binrelease\ROMS.PAK |

GitHub: https://github.com/Moro44444444/ZXMAK2-Fork
Remote: https://github.com/Moro44444444/ZXMAK2-Fork.git ; ранее использовалась main. Текущий HEAD брать из git-state.txt либо git, не из памяти. Старый известный коммит 8b0e89a не считать автоматически текущим.

Грязное дерево содержит много более ранних изменений в Engine, Hardware, SD/VHD, ATM/Evo, WinForms, проектах и ROM. Метка v13 в BuildId описывает принятую линию происхождения; полное независимое совпадение с исходным архивом v13 не было доказано.

## 4. Сборочные инструменты — исправление устаревших сведений

В ЭТОМ чате пользователь фактически установил и использовал:

`C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe`

Этот путь подтверждён Get-Item и успешными сборками B01–B14. В старом контексте и общей памяти упоминался VS2022 — не подменять им проверенный путь VS2019 без фактического обнаружения.

PowerShell 7.6.6; установлены reference assemblies .NET Framework 4.0 и Client Profile. Для этих старых проектов использовать Framework MSBuild, а не dotnet build. CSC для проб:

`C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe`

Скрипт B14 пересобирает последовательно ZXMAK2.Engine.Cpu, ZXMAK2.Engine, ZXMAK2.Hardware, ZXMAK2.Host.WinForms, ZXMAK2; параметры Release, AnyCPU, /t:Rebuild, /p:UseSharedCompilation=false, /m:1, /nologo. Проверяется обновление CPU/Engine/Hardware/WinForms DLL и EXE.

Предупреждение MSB3884 про AllRules.ruleset известно и не делало сборку неуспешной. Возможные блокировки VBCSCompiler учитывать по факту, не убивать процессы заранее.

В Linux-сеансе Codex не было pwsh/dotnet/mono/csc. Проводились статические проверки и Python-эталоны; реальную компиляцию и выполнение compiled probes делал скрипт на Windows. Не смешивать эти уровни проверки.

## 5. Принятый ROM — не вернуть старый

Пользователь прислал zxevo_v0.61.01_FE.rom. В работающем ERS показывалось **EVO Reset Service v0.61.01 FE**.

Размер принятого ROM: **524288 байт**.

SHA-256 принятого ROM:

`620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca`

Этот hash использовали и проверяли успешные установщики B01–B14: свободный исходный/релизный файл и ZIP-элемент **EVO/zxevo.rom** в обоих ROMS.PAK. Не путать hash элемента ROM с hash всего PAK.

Ранее обнаруженные 83A5D956... (свободный старый ROM) и f2e94dfc... (старый элемент PAK) были частью диагностики ДО синхронизации. Устаревшая память о F2E94 как «последнем ROM» неверна для актуальных Bxx. Старый ERS 0.59.12 FE также уже заменён.

Не менять ROM в B15. Сверять все четыре представления с принятой контрольной суммой.

## 6. Документация и RTL

Пользователь дал источники:

- http://nedopc.com/zxevo/zxevo.php
- http://svn.nedopc.com/listing.php?repname=pentevo&
- http://nedoos.ru/svn/listing.php?repname=NedoOS

Скачанные/прикреплённые документы:

- zxevo_base_configuration.pdf — главный документ BaseConf, 55 страниц в использованной копии;
- zxevo_user_manual.pdf — пользовательское руководство;
- zxevo_sch_revb.pdf — принципиальная схема Rev B;
- Описание сервис-прошивки ''EVO Reset Service''.pdf;
- test_n_service.pdf;
- ts_flasher__quickmanual.pdf — инструкция прошивки; НЕ спецификация TSConf для этого этапа.

Основной RTL: **zxevo-05819.FywvAa/fpga/baseconf/trunk**. В старом сеансе использовалась также копия zxevo-05820-work; не смешивать ревизии незаметно.

Ключевые файлы относительно trunk:

- z80/zports.v — декодирование портов;
- z80/zmem.v — память, буфер слова и запросы DRAM;
- z80/zclock.v — частоты, RFSH, WAIT внешних портов;
- dram/arbiter.v, dram/dram.v — CPU/видео арбитраж и фазы cbeg/cend;
- video/video_sync_h.v, video/video_sync_v.v — растры;
- video/video_fetch.v, video/video_modedecode.v — выборки и режимы;
- top.v — соединение и порядок битов;
- slave/spi_fmt.txt, spihub и AVR-часть — внутренний обмен и настройки;
- texts/video_modes.txt, texts/dram_access.txt — дополнительные пояснения.

Внутренние SPI-регистры AVR↔FPGA не являются Z80-портами! Ориентиры старого аудита: $10/$11 keyboard, $20..$23 mouse/joystick, $30 reset, $40..$42 WAIT/RTC/RS232, $50/$51 common config, $60/$61 SD data/control. Для нового узла проверять конкретные биты по коду, а не только эту памятку.

B14 дополнительно сверяли с официальным Zilog Z80 CPU User Manual UM008011-0816:
https://www.zilog.com/docs/z80/um0080.pdf
Instruction Fetch: T3/T4 служат refresh; interrupt diagrams описывают acknowledge. В B14 реализована логическая граница; задержка возврата zclk через инвертор FPGA ещё не откалибрована.

### Временные Linux-пути прежних сеансов

Это ориентиры для поиска, а не гарантия сохранения или свежести:

- /workspace/scratch/b949d03cdbef/zxmak2-fork.c5TyIg — историческое зеркало, БЕЗ актуальных пользовательских B01–B14;
- /workspace/scratch/b949d03cdbef/upstream-zxmak2;
- /workspace/scratch/b949d03cdbef/zxevo-05819.FywvAa/fpga/baseconf/trunk;
- /workspace/scratch/b949d03cdbef/zxevo-05820-work/fpga/baseconf/trunk;
- /workspace/scratch/b949d03cdbef/zxevo-upstream.KTQT6g — дополнительные материалы/Unreal;
- /workspace/scratch/991b0717f2eb — сеанс Bxx, установщики и qa-b12.py;
- /workspace/scratch/991b0717f2eb/upload — прикреплённые PDF, журналы, ROM и ZX-Evo-Clock-Test.scl.

Извлечение основного PDF называлось baseconf-manual-layout.txt. Если пути не доступны, получить документы заново из сохранённых файлов/прикреплений. Не симулировать чтение исчезнувшего файла.

Для правок брать свежие исходники из пакета переноса пользователя либо из B14 backup: before/src содержит базу B13, а after содержит четыре изменённых B14 файла. Нельзя читать историческое зеркало как будто это код B14.

## 7. История этапов и фактическая приёмка

Все BuildId ниже начинаются с ZXMAK2-v13-ZXEVO-BC-. К ним применяются общие пути backup и K:\Download.

| Этап | Хвост BuildId | Что изменили / результат |
|---|---|---|
| B01 | KBD-FEF6-B01-20260915-180143 | KeyboardDevice mask FF→F7, port FE, noDos=false в BaseConf; FE/F6. Пользователь подтвердил ERS, клавиши и мышь. |
| B02 | BORDER-B02-20260915-181825 | Точный low-byte FE/F6/FC для border в UlaPentEvo; A3 расширяет бордюр. Проверка всех 65536 адресов. |
| B03 | SDREAD-B03-20260915-184041 | ZsdPentEvo.RdXX57 больше не возвращает 0 при Shadow+A15; чтение CardRd. Compiled SD/портовые проверки. |
| B04 | BEEPER-B04-20260915-184910 | BaseConf beeper: exact FE, noDos=false, Ear=4, Mic=-1. |
| B05 | COVOX-B05-20260915-185529 | BaseConf Covox: exact FB, noDos=false, весь байт. |
| B06 | BANKS-B06-20260915-190522 | Порядок старших битов Pentagon1024: D7,D6,D5 вместо D5,D7,D6. Банки проверены чтением/записью по всем 4MB. Пользователь: FAT error исчезла, SD init OK, BadAppleColor.bin найден, демка запускается и расширенная графика отображается. |
| B07 failed | WPROT-B07-20260915-191932 | Правка защиты применена, сборка и банки прошли; проба защиты упала из-за неправильного тестового Shadow-setup. Не считать принятой сборкой. |
| B07R1 | WPROT-B07R1-20260915-193301 | Исправлен setup пробы, без новой правки эмулятора. Защита и банки прошли. |
| B08 | CLOCKPORT-B08-20260915-194633 | Декодер EFF7 по mask 11FF/value 01F7: 64 алиаса, вне DOS/Shadow; raw 0BBD, D4 slow, xx77.D3 приоритет 14. Принят только декодер управления. |
| B09 | CLOCKBUS-B09-20260915-195707 | Необязательный ICpuClockBusSync, синхронизация master timestamps на CPU bus callbacks. Старые провайдеры сохранены. |
| B10 | FCLK28-B10-20260915-200412 | 28 MHz master: MaxCpuClockMultiplier=8, Evo ULA FrameMultiplier=8, WD1793 clock Evo=28 MHz. Ещё без реального memory WAIT. |
| B11 | MEMWAIT-B11-20260915-205608 | Номинальный CPU14 multiplier4, память WAIT/буфер слова, free-DRAM baseline. Не включал видеоарбитраж. |
| B12 | DRAMARB-B12-20260915-212704 | CPU/video DRAM арбитраж нормального растра и общий 320-строчный frame во всех Evo renderers. Compiled проверки прошли; пользователь проверил нужные функции, явной регрессии не сообщил. |
| B13 | IOWAIT-B13-20260915-214741 | Бюджет WAIT внешних I/O: 6 master ticks на14, optional ICpuPortTiming, 786432 decoder cases +144 реальных IN/OUT. Пользователь: всё работает, ничего не поломалось. |
| B14 | RFSH-B14-20260915-220507 | Запрос/применённая частота разделены, latch в refresh hook; compiled переходы и регрессии прошли. Пользовательский тест частоты 07FE/0FFE/1970. |

Прежний B12 PowerShell сначала имел ParserError: закрывающий here-string и Append-Journals оказались на одной строке. Тот запуск НЕ исполнялся и НЕ менял исходники. Исправили тот же установщик (revision B12R1, номер сборки оставили B12), затем пользователь успешно собрал. Всегда проверять here-string terminators на отдельной строке!

Первый B01 запуск остановился в preflight из-за отсутствия главного журнала в корне. PROJECT_JOURNAL.md тогда находился в K:\Download; пользователь скопировал его в корень и повторил запуск успешно. Не искать главный журнал только в текущем PowerShell cwd.

## 8. Текущая реализация: узлы, которые нельзя потерять

### Банки и защита

- MemoryPentEvo.UpdateMapping: `var sega = (CMR0 & 0xE0) >> 5;` — D7,D6,D5,D2,D1,D0.
- Проверены 64 банка в каждой 1MB группе, обе карты, четыре окна, фактические read/write, ZX128 low-three-bit substitution, D5 lock/release, direct x7F7 и выбор экрана.
- xBF7 write-disable ports: SubscribeWrIo mask 0DFF/value09F7, 32 aliases, DOS||SHADOW gate. m_writeDisable имеет 8 независимых флагов двух карт.
- Бит защиты: ((CMR0 & 10hex)>>2)|(addr>>14). D0 задаёт отдельный флаг. 12BD читает flags; reset очищает.
- Защита перенаправляет MapWriteRAM на trash. Принудительный W0RAM0 обходит защиту, как в проверенном узле. ROM запись сама по себе не стала записью во FLASH.
- Полная запись 12BD/NMI/FLASH-протокол остаются отдельными задачами.

### CPU/master time

- ICpuClock — исторический clock provider. ICpuClockBusSync — необязательный маркер для синхронизации до callbacks.
- BusManager масштабирует только raw CPU delta, ведёт m_cpuClockBusActive, m_cpuClockBusLastTact, m_cpuClockRemainder, последние ratio/max. Добавленный delay уже в master units и исключается из повторного масштабирования.
- EventManager синхронизирует memory, IO, NoMreq, RESET, INTACK, NMIACK, SCANSIG. Memory completion вызывается ПОСЛЕ memory handlers, включая M1 mapping; port delay — ПЕРЕД port handlers.
- Старые ICpuClock-only провайдеры сохраняют instruction-end scaling и старые callback timestamps. Диагностические вызовы вне ExecCycle не продвигают CPU.
- 28 MHz master =8 относительно3,5. Применённые CPU ratios1/2/4. ULA normal frame573440 master ticks =71680 базовых тиков.

### Memory WAIT B11/B12

- CpuMemoryAccess enum Opcode/Read/Write; ICpuMemoryTiming.CompleteMemoryAccess(addr,access,masterTact,refvalue), InvalidateMemoryBuffer().
- m_dramBufferValid, logical-even m_dramBufferAddress и word; even byte = старший байт слова, odd = младший.
- Fast14 cache hit возвращает удержанное слово; miss резервирует DRAM. Slow3,5/7 читает заново. ROM не ждёт DRAM, оставляет handler value и сбрасывает RAM buffer.
- Free-DRAM convention: первый falling edge соответствует masterTact+1. Фаза modulo4: cbeg0/post1/pre2/cend3. M1 delay6/5/4/3; обычное чтение5/4/3/2. Это baseline-конвенция, НЕ законченная pin-edge калибровка.
- Unprotected write сбрасывает буфер; free-DRAM write delay0, при видеоарбитраже возможен wait. Protected hit удерживает буфер; protected miss запрашивает refill, в callback-модели синхронный. Асинхронное перекрытие refill ещё открыто.
- Bus учитывает prefix continuation: первый RDMEM в сегменте — opcode, кроме indexed CB displacement/opcode. RDMEM_M1 сохраняет прежнюю роль в DOS-переключении.

### Normal raster / DRAM arbitration B12

- MemoryPentEvo private nested EvoDramArbiter: blockRemaining, videoRemaining, stallBlock; Step→VIDEO0/CPU1/FREE2; quota 1<<bw за8 DRAM cycles, хвост квоты сохраняется после изменения go/bw.
- Проверенные RTL fixtures: bw2/noCPU VVVVFFFF; bw2/01010101 VCVCVCVC; bw2/01111111 VCCCCVVV; bw0/allCPU CCCCCCCV; bw1/allCPU CCCCCCVV; bw3/allCPU VVVVVVVV. Все1024 mask/quota combinations проверены.
- PrepareDram/AdvanceDramIdle/ReserveDram ведут idle историю в master ticks; полный номер метода читать в актуальном файле. cend=4n+3. При назад идущем времени/snapshot арбитр переинициализируется с ограниченным replay.
- Hardware pent bits: ((EFF7&1)<<1)|((EFF7>>5)&1), из top.v {D0,D5}. Не перепутать с программным RGEX.
- Реализован только normal raster448 DRAM cycles/line ×320 lines. Начало отсчёта h0v0 принято конвенцией; взаимное положение INT/изображения ещё не полностью подтверждено.
- GetVideoFetch: standard go lines80..271,h123..378; wide graphic lines76..275,h91..410; wide text lines76..275,h87..410. go зарегистрирован с фазой cend. Wide RG0/2/6/7; text6/7. bw=0 при RG3 и pent!=2, иначе1. Generic bw2/3 есть в тестах арбитра, не надо приписывать их всем текущим видеорежимам.
- UlaPentEvo.OnRendererInit переопределяет extended renderer FrameTactCount71680: Atm320, Atm640, AtmTxt, EvoTxt. Исправлены прежние69888/312 lines только в Evo subclass. Адреса пикселей/атрибутов и палитра этим узлом не переписывались.
- Все7 текущих renderer selections прошли normal frame573440. Это не приёмка всех пиксельных адресов/режимов.

### IO WAIT B13

- ICpuPortTiming : ICpuMemoryTiming, GetPortWait(addr).
- external_port RTL: exact lowFD+A15=1 (AY,128 aliases независимо Shadow), либо low1F/3F/5F/7F при DOS||SHADOW (VG93,1024 дополнительных aliases). Total1152 при включённом gate.
- При applied14 возвращается6 master ticks, иначе0. zclock io_wait_cnt8..F waveform1,1,1,1,1,0,1,0 суммарно6.
- Это суммарный бюджет остановленных FPGA ticks. Точное раздельное расположение и перекрытие с другими stalls ещё не реализованы. Не описывать как равномерное замедление всего I/O M-cycle до7MHz.
- Interrupt/NMI acknowledgment не проходят ordinary port hook. Проверены адреса/байт IN/OUT, no-clock/legacy/diagnostics/acks.

### Refresh clock B14 — последний узел

Изменены ровно четыре исходника:

- src\ZXMAK2.Hardware\Evo\MemoryPentEvo.cs
- src\ZXMAK2.Engine\BusManager.cs
- src\ZXMAK2.Engine\Interfaces\ICpuClock.cs
- src\ZXMAK2.Engine.Cpu\Processor\Z80Cpu.cs

EventManager.cs в B14 НЕ меняли, он сохраняет B13 hooks.

- MemoryPentEvo теперь implements ICpuRefreshClock : ICpuPortTiming.
- m_activeCpuClockMultiplier=2; CpuClockMultiplier сообщает активный ratio. RequestedCpuClockMultiplier отдельно читает raw xx77.D3 приоритет4, иначе EFF7.D4→1 или2. LatchClockAtRefresh применяет запрос.
- Портовые записи и raw0BBD не применяют частоту мгновенно. Memory/IO WAIT смотрят активный getter.
- Bus constructor связывает m_cpu.REFRESH=RefreshCpuClock. Hook действует только в активном CPU bus cycle и при optional ICpuRefreshClock; Sync завершает старые CPU deltas до latch, затем ratio/max обновляются. Для Evo дробного remainder нет, так как1/2/4 делят8.
- Z80Cpu имеет необязательный public Action REFRESH и NotifyRefresh(). Ordinary/prefix fetch: +2 rawT, NotifyRefresh, +1 rawT, затем прежний +1 refreshT в ветке команды. Остальные провайдеры не получают изменения времени.
- Indexed DD/FD CB displacement/final-opcode segment не вызывает NotifyRefresh. DD,FD,ED,CB M1 сами вызывают его. HALT repeated fetch тоже.
- NMI acknowledge: +2T, hook, +2T, прежний +1T; INT acknowledge: +4T включая automatic waits, hook, +2T, прежний +1T. RESET synthetic R increment не создаёт hook.
- Сохранён прежний emulator reset baseline7MHz. В RTL-блоке int_turbo latch нет rst_n reset branch; не утверждать, будто B14 доказала совпадение полного аппаратного reset-состояния.
- 36 old/new×master-phase cases,4 real ED OUT(C),A clock sequences, prefixes/indexed-CB/HALT/latest-request/diagnostics/realNMI/INT/RESET checks прошли. Старые steady-state probes явно применяют setup mode перед измерением; это подготовка теста, не обход переходов в эмуляторе.
- Реальный программный цикл пользователь подтвердил на трёх частотах. Физическое выравнивание zclk/FPGA при смене режима остаётся открытым.

## 9. Принцип упаковки и проверки следующей сборки

Установщик последней сборки:

`K:\Download\ZXMAK2-BaseConf-RFSH-B14.ps1`

Его точная копия:

`L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507\installer.ps1`

В предыдущем сеансе скачиваемый файл находился:

`/workspace/scratch/991b0717f2eb/ZXMAK2-BaseConf-RFSH-B14.ps1`

Для B15 брать scaffold B14, но НЕ просто менять номер: обновить accepted-parent BuildId, exact receipt title/decision, списки изменяемых/неизменяемых исходников, пути parent before/after и compiled probes под новый узел.

Backup layout:

- before\src — полный исходный src без bin,obj,_binrelease,_bindebug,.vs;
- before\release — полный предыдущий релиз;
- before\PROJECT_JOURNAL.md и FORK_CHANGELOG.md;
- after\release — полный новый релиз;
- after\MemoryPentEvo.cs, BusManager.cs, ICpuClock.cs, Z80Cpu.cs — в B14 имена flattened, не вложенные src-пути;
- after\оба журнала;
- base.txt, git-state.txt, working-tree.patch (binary), installer.ps1, RefreshProbe.cs/exe;
- при ошибке FAILED.txt и failed-state.

Для проверки родителя B14:

- ParentBuildId = ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507;
- RESULT.md содержит `## <ParentBuildId> - BaseConf RFSH clock latch`;
- Decision содержит `accept compiled logical RFSH clock latch only`;
- отсутствует FAILED.txt;
- четыре изменённых файла сверять с after\flattened name;
- EventManager/Ula/Fdd/прочие неизменённые B14 файлы — с before\src\<relative>;
- DLL/EXE/config/PAK — с after\release;
- журналы должны содержать receipt B14;
- конфигурация и ROM проверяются до/после; исправления B01–B06 должны остаться.

Обычные команды для каждого нового установщика:

```powershell
Unblock-File -LiteralPath 'K:\Download\ZXMAK2-BaseConf-<NODE>-B15.ps1'
& 'K:\Download\ZXMAK2-BaseConf-<NODE>-B15.ps1'
```

Имя NODE определить после аудита. Это шаблон, не готовая существующая команда B15. Давать пользователю ссылку с реальным именем файла и полным sandbox-путём; пользователь скачивает в K:\Download. Новые reusable artifacts сохранять надёжно, не полагаться лишь на временный scratch.

## 10. Что ещё остаётся привести к реальной машине

Приоритет следующего узла — растры. Остальные пункты не потерять:

1. **Альтернативные растры**: modes_raster, источник управляющих битов AVR/FPGA, выбор normal/60Hz/48K/128K, длины строки/кадра, fetch windows, режим переключения, INT/video phase. В раннем аудите фигурировали320/262/312/311 lines и448/456 DRAM cycles/line — перед реализацией подтвердить точные сочетания и исключения в RTL данной ревизии. Не добавлять выдуманный Z80-порт только ради выбора режима.
2. **Точные FPGA/Z80 фазы**: возвращённый zclk, cbeg/cend, memory RD/refill overlap, protected-write asynchronous refill, pin-edge alignment смены частоты. B11–B14 — callback-based approximation со явно принятыми границами.
3. **Видео**: полный аудит адресации пикселей/атрибутов, экранных страниц, палитр/DAC, text/hires/multicolor/256-color modes и корректного переключения. Успех одной демки не покрывает все режимы.
4. **Оставшиеся порты**: полная таблица masks/aliases, DOS/Shadow gates, чтение статусов, коллизии обработчиков, keyboard/tape, mouse/joystick, AY/TurboSound, RTC/RS232/IDE/SD/VG93, ULAplus — наличие/отсутствие и поведение проверить по текущему коду. B01–B08 не исправляли всю карту.
5. **Системные регистры**: полная запись12BD, NMI/reset, FLASH protocol. Не превращать FLASH в безусловно writable ROM-array; сначала сверить чип и datasheet.
6. **Смена второго SD-образа**: остаётся зависание при замене первой карты второй. Это отдельная регрессия жизненного цикла VM/SD, не проблема FAT чтения первой карты.
7. **FPS/audio/tape**: унаследованное Sound/Tape FrameTacts×50 и normal71680 base tacts даёт28,672MHz при28MHz hardware clock, тогда как физический normal frame≈48,828Hz. Около2,4% расхождения — отдельный аудит scheduler/FPS/tape/audio. Не замалчивать и не менять коэффициент без проверки всех потребителей.
8. **Итоговая runtime приёмка**: ERS, NedoOS, клавиши/мышь на3,5/7/14, SD init/FAT/demo, несколько классических демо, все реализованные videomodes, clocks/raster/INT, второй/третий image, cancel/bad-image. Пользователь просил основные runtime испытания после приведения портов/видео ближе к спецификации, но добровольные проверки B06/B12/B13/B14 уже есть.

### Не потерять историю SD

Старые v11/v12 не использовать как исправную базу. V13 раньше дала нормальную смену SD-образов. В старом журнале описан безопасный detach/close старой карты, подключение новой, восстановление старой при ошибке и общий Warm Reset. Это описание прежней реализации, не доказательство корректности нынешней B14.

Пользователь помнит, что при первой карте был cold reset, а для второй мог потребоваться полный перезапуск VM. Это воспоминание пользователя; точный прежний алгоритм проверить по журналам и исходникам MainViewModel/MainView/VirtualMachine/BusManager/ZsdPentEvo/SdCard/VhdStream. Не объявлять доказанным «полный restart» только на основании воспоминания.

Поддержка raw img/ima, fixed/dynamic VHD и NedoOS RAW VHD уже была добавлена. Не ломать её при исправлении reset. Пользователь сообщил, что тот же SD-образ и BIOS работают в Unreal; это сравнительный результат, не разрешение скопировать другую machine-модель.

**FAT первой карты уже исправилась на B06.** Не повторять старое утверждение, что VideoPlayer всё ещё не видит FAT. Второй SD-image hang ещё не перепроверен.

## 11. Пакет свежих исходников для нового чата

Следующий чат может не иметь прежних временных файлов или прикреплений. Надёжный пакет — этот контекст, два АКТУАЛЬНЫХ журнала, RESULT B14, installer B14 и актуальный src. Ниже read-only экспорт Windows-дерева; не выполняет новую сборку, не меняет исходники или журналы и не запускает эмулятор.

Сначала скачать этот MD в K:\Download под его точным именем. Затем при необходимости выполнить:

```powershell
$ErrorActionPreference = 'Stop'
if (Test-Path Variable:PSNativeCommandUseErrorActionPreference) {
    $PSNativeCommandUseErrorActionPreference = $false
}
$zxRoot = 'L:\Work_two\ZX\ZXMAK2-Fork'
$zxBuild = 'ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507'
$zxTag = Get-Date -Format 'yyyyMMdd-HHmmss'
$zxPack = "K:\Download\ZXMAK2-Handoff-B14-$zxTag"
$zxContext = 'K:\Download\CHAT_CONTEXT_TRANSFER_2026-09-15_B14.md'
if (-not (Test-Path -LiteralPath $zxContext -PathType Leaf)) {
    throw "Сначала скачайте файл контекста: $zxContext"
}
New-Item -ItemType Directory -Path $zxPack | Out-Null
Copy-Item -LiteralPath $zxContext -Destination (Join-Path $zxRoot 'CHAT_CONTEXT_TRANSFER_2026-09-15_B14.md')
Copy-Item -LiteralPath $zxContext -Destination $zxPack
foreach ($zxName in @('PROJECT_JOURNAL.md', 'FORK_CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $zxRoot $zxName) -Destination $zxPack
}
Copy-Item -LiteralPath "K:\Download\$zxBuild\RESULT.md" -Destination $zxPack
Copy-Item -LiteralPath "$zxRoot\backup\$zxBuild\installer.ps1" `
    -Destination (Join-Path $zxPack 'ZXMAK2-BaseConf-RFSH-B14.ps1')
& robocopy.exe (Join-Path $zxRoot 'src') (Join-Path $zxPack 'src') `
    /E /XJ /R:1 /W:1 /NFL /NDL /NP `
    /XD bin obj _binrelease _bindebug .vs
if ($LASTEXITCODE -ge 8) { throw "Ошибка копирования исходников: $LASTEXITCODE" }
Compress-Archive -LiteralPath $zxPack -DestinationPath "$zxPack.zip"
Get-Item -LiteralPath "$zxPack.zip" | Select-Object FullName, Length, LastWriteTime
```

Архив содержит исходники, включая ROM source/PAK, но исключает build outputs. PDF и RTL надо приложить/дать доступ дополнительно, если следующий чат не найдёт сохранённые материалы. Локальный Windows-путь папки RTL не установлен — не придумывать его. Можно дать пользователю точную команду поиска после появления такой необходимости.

Пакет фиксирует текущие файлы; он не заменяет полноценный backup B14 на диске L:. Для сверки состояния можно отдельно приложить git-state.txt из B14 backup. Пользовательские runtime результаты07FE/0FFE/1970 находятся в ЭТОМ контексте; не утверждать, что после скриншотов оба Windows-журнала уже были дополнены. Результаты требуется добавить при ближайшем этапе либо отдельной документальной записи.

## 12. Стартовая фраза для нового чата

> Продолжаем ZXMAK2 Fork, только ZX-Evo BaseConf. Сначала прочитай CHAT_CONTEXT_TRANSFER_2026-09-15_B14.md, затем актуальные PROJECT_JOURNAL.md и FORK_CHANGELOG.md из приложенного пакета. Последняя собранная база — ZXMAK2-v13-ZXEVO-BC-RFSH-B14-20260915-220507. Пользовательский clock test: 3,5=07FE, 7=0FFE, 14=1970; на B13 явных поломок не было. Следующий этап B15 — аудит и реализация альтернативных растров по RTL BaseConf; B15 ещё не внесена. Перед правкой покажи базу, номер и один логический узел, объясни поведение. Каждая правка — готовый PowerShell, backup до/после, сборка, проверки, оба журнала и отдельный релиз K:\Download\<BuildId>\release. Не смешивай ATM/TSConf/Scorpion. Сначала кратко подтверди точку продолжения и открытые ограничения; не начинай новую сборку до моего «начинай».

## 13. Проверка восстановления нити

Новый помощник должен правильно назвать:

- B14-220507, а не исходную v13 или B13, как текущую базу;
- следующий номерB15 и узел растров, не начинать B14 заново;
- ROM0.61.01FE/hash620146..., не F2E94...;
- фактический MSBuild VS2019, не неподтверждённый2022;
- FAT первой карты исправилась B06, второй SD-image hang открыт;
- RFSH latch реализован на логической границе; точные pin edges ещё открыты;
- normal448×320 реализован; альтернативные растры и полный video/port audit ещё открыты;
- наличие изменённого Z80Cpu.cs B14 и необязательного ICpuRefreshClock;
- пути L:\Work_two\ZX\ZXMAK2-Fork и K:\Download, отдельный EXE в release каждой сборки;
- различие временного исторического зеркала и свежих исходников Windows;
- требование дать скачиваемый .ps1 и команды, а не только план.

Если какой-то исходник/документ отсутствует, помощник должен назвать конкретный недостающий файл и получить его, а не воспроизводить правку по предполагаемому состоянию.
