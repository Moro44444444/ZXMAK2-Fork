# ZXMAK2 — журнал проекта

Последнее обновление: 2026-09-22

Этот файл является постоянным рабочим источником истины проекта. Перед
изменением кода нужно сверяться с ним, а после каждой сборки дополнять его
фактическим результатом проверки. Решения из памяти или из предположений не
заменяют запись в журнале. Отдельные handoff-файлы больше не используются:
текущее состояние, история и backlog должны оставаться здесь и в Git.

## Правила работы

1. Сначала фиксируем исходную рабочую версию.
2. За один этап меняем только один логический узел.
3. Перед правкой проверяем затронутые зависимости.
4. После изменения делаем сборку, проверку и резервную точку.
5. Если появилась новая поломка, не добавляем следующие функции: возвращаемся к последней рабочей точке и записываем причину.
6. Нельзя смешивать ATM Turbo, ZX-Evo/BaseConf и Scorpion в одну задачу или описание.
7. Не удаляем ROM и файлы машин без проверки, что они не используются другими конфигурациями.

8. `PROJECT_JOURNAL.md` является постоянным журналом требований и результатов. После
   каждого изменения фиксируются цель, затронутые файлы, успешный или неуспешный
   результат, проверки, backup и незакрытые ограничения. Невыполненные пожелания
   записываются отдельно как backlog и не считаются реализованными. Перед каждым
   новым этапом журнал перечитывается; при переносе проекта достаточно взять этот
   файл из репозитория вместе с `FORK_CHANGELOG.md`.

## Текущая точка и утверждённый порядок

- Последняя пользовательски принятая точка: **B36**, commit `76c75d2`. Пользователь подтвердил
  отсутствие видимых регрессий относительно принятого поведения B01–B35.
- Новые INT/NMI/breakpoint-переходы B36 подтверждены compiled-probe, но не
  отдельным прикладным runtime-тестом; расширять эту приёмку нельзя.
- Текущая собранная тестовая точка: **B37**. AVR/gluclock, COM/RS232 WAIT и
  DOS settling stall прошли compiled-регрессию; пользовательская runtime-приёмка
  ещё не выполнена, поэтому принятой точкой остаётся B36.
- Канонический аудит и технический план: `BASECONF_AUDIT_B32.md`.
- Последний подробный отчёт: `B37_RESULT.md`. Отчёты завершённых прежних
  этапов находятся в `docs/history/results/`.
- Краткая хронология без дублирования технических протоколов ведётся в
  `FORK_CHANGELOG.md`.

Следующие этапы выполняются строго по одному логическому узлу:

1. **B38 — встроенный ввод и звук:** после runtime-приёмки B37 — Kempston joystick, tape-in/tape-out и
   документированный beeper/tape mux.
2. **B39 — палитра:** ULAplus и официальное расширение BaseConf 4:4:4.
3. **B40 — финальная временная сверка:** contention, floating bus,
   raster/INT/video phase и golden-векторы видеорежимов; отдельно trace VG93,
   только если появится подтверждённое расхождение.
4. Только после встроенного BaseConf — два официальных ZX-BUS слота и затем
   документированная периферия, включая отдельный этап CD/ATAPI.

Полный неизменяемый контракт границ, обязательных probes и критериев готовности
B37–B39 записан в разделе `Fixed implementation contract for B37–B39` файла
`BASECONF_AUDIT_B32.md`. Кратко: B37 закрывает только WAIT/AVR/COM/DOS stall;
B38 — только встроенные Kempston joystick и tape/beeper mux; B39 — только
ULAplus и официальную 4:4:4 palette. Ни один из этапов не получает runtime-статус
без отдельной пользовательской проверки.

Независимый backlog не подмешивается в эти этапы: SD New/Eject и persistence,
горячие клавиши reset/CMOS, автоматический HDD boot NedoOS и диагностика Rage
на конкретном 4-ГБ SDHC-образе. Подробные требования сохранены ниже по дате
2026-09-19.

## Архивный контекст до B01 — 2026-09-12—14

Раздел ниже сохранён только как история исходной базы. Он не является текущим
планом и не отменяет аудит B32 или принятую точку B36.

### База и контрольные версии

- v10 рассматривалась как возможная откаточная база после неудачного этапа v12.
- Исходной рабочей версией для следующего этапа выбрана `ZXMAK2-NedoOS-Input-v13-20260913`.
- В v13 подтверждена нормальная смена образов.
- Ограничения v13: частоты работают неправильно и в ATM Turbo, и в ZX-Evo; это неисправности исходной базы, которые нужно исправлять по отдельности.
- До отдельной проверки исходников v13 считать эталоном поведения со сменой образов, а не автоматически полностью исправной сборкой.
- Версия v12 признана непригодной для дальнейшего тестирования: ATM Turbo 2+ стартует с Turbo On; после смены второго образа наблюдалась потеря холодного рестарта; QuickBoot работал некорректно.
- Текущая рабочая копия исходников содержит незавершённые изменения. До начала следующего этапа нужно определить, какая копия является базовой, и не объявлять её стабильной без проверки.

### Целевой состав машин

Оставить и проверить:

- классические Spectrum;
- Santaka 002;
- Byte с TR-DOS;
- Pentagon — нужные поздние варианты;
- последний вариант Profi;
- Scorpion ZS-256 Turbo+ — создать/настроить отдельно по документации и ROM;
- ATM Turbo 2+;
- ZX Evolution BaseConf;
- позднее TSConf.

Кандидаты на исключение из основного списка:

- Sprinter;
- Quorum;
- Delta как дублирующий обычный Spectrum 48;
- старые и дублирующие варианты Profi;
- старые варианты Scorpion;
- редкие или недоделанные машины.

Удаление заменяем на скрытие до тех пор, пока не проверены зависимости ROM, устройств и конфигураций.

### ATM Turbo 2+

Требования:

- при загрузке машины стартовый режим должен быть `Turbo Off` / 3,5 МГц;
- переход в Turbo должен давать 7 МГц;
- после Warm Reset режим должен возвращаться к Turbo Off, согласно принятому решению по поведению оригинальной машины;
- смена образа должна корректно завершаться и выполнять требуемый сброс;
- смена второго и последующих образов должна работать так же, как первого;
- QuickBoot пока не считать исправленным до отдельной проверки.

### ZX-Evo / BaseConf / TSConf

- BaseConf и TSConf относятся к ZX-Evo; не переносить эти понятия на ATM Turbo.
- Для текущего исследования нужна именно ZX-Evo BaseConf; TSConf и ATM в этот раздел не добавлять.
- В исходниках BaseConf зафиксированы два класса видеовыхода: `lowres` с пиксельной частотой 7 МГц (256 или 320 пикселей на строку) и `hires` с 14 МГц (512 или 640 пикселей на строку).
- В BaseConf описаны видеорежимы `attr`, `text`, `2c`, `16c`, `256c` и `16+16c`. Документ `video_modes.txt` помечен авторами как начальное приближение с целью минимальной Pentagon-подобной работоспособности, поэтому его нельзя автоматически считать полной спецификацией всех режимов.
- Для совместимости нужно различать профили растра/таймингов `48k`, `128k`, `Pentagon` и режим `60Hz`; в формате конфигурационных регистров BaseConf эти варианты занимают отдельное поле режима растра. Это не просто переключение разрешения.
- Официальное руководство указывает частоты CPU 3,5 МГц (обычный режим), 7 МГц (turbo без WAIT) и 14 МГц (turbo+ с WAIT). В BaseConf WAIT зависит от сочетания скорости памяти, видеорежима и частоты CPU, поэтому проверка частоты должна включать и реальную длительность машинного цикла, и WAIT.
- Для VGA официально требуется монитор с поддержкой примерно 48,8 Гц; переключение VGA/TV в BaseConf выполняется Scroll Lock. Это нужно учитывать отдельно от профилей 48k/128k/Pentagon/60Hz.
- Перед реализацией и исправлением BaseConf составить сверенную карту Z80 I/O-портов по `z80/zports.v`, `z80/zkbdmus.v`, `vg93/vg93.v`, `sound/sound.v`, `video/*` и ROM-исходникам. Нельзя переносить адреса или маски из ATM Turbo.
- Обязательные группы карты: клавиатура и tape-in, Kempston joystick, Kempston mouse, AY, beeper/tape-out, Covox, ULAplus, 7FFD/EFF7/ATM-регистры, VG93/TR-DOS, SD, IDE, NMI/reset/breakpoint, RTC/RS232 wait-порты и ZXBUS.
- В карте для каждого порта фиксировать полный адресный шаблон, направление чтения/записи, используемые биты, зависимость от `A[15:8]`, `A[14]`, `A[12]`, `A[9]`, `A[8]` и от `shadow/dos`, а также побочный эффект и WAIT.

Подтверждённые ориентиры из RTL BaseConf, которые нужно сохранить при дальнейшей детализации:

| Функция | Порт/адресный шаблон | Уточнение |
|---|---|---|
| Клавиатура и tape-in | `xxFE` (также `xxF6` читается тем же входом) | Клавиатурная строка выбирается старшим байтом адреса; данные — 5 бит клавиш и tape-in. |
| Border/beeper | запись `xxFE`; border также декодируется для `xxF6` и `xxFC` | Beeper берёт бит 4 или tape-out бит 3 в зависимости от режима; border — биты цвета из записи и адресная фаза. |
| AY | `BFFD` и `FFFD` | `BFFD` — доступ к данным, `FFFD` — выбор регистра/доступ AY; декодирование идёт по `A[15:14]`. |
| Covox | запись `xxFB` | В звуковой модуль передаётся весь байт. |
| Kempston joystick | чтение `xx1F` в обычном режиме | Возвращаются 5 бит джойстика; в Shadow/TR-DOS этот же младший байт используется VG93. |
| Kempston mouse | `FADF` — кнопки, `FBDF` — X, `FFDF` — Y | Это отдельная адресация по старшему байту; не смешивать с `xx1F`. |
| VG93/TR-DOS | Shadow: `xx1F`, `xx3F`, `xx5F`, `xx7F`, `xxFF` | Команда, дорожка, сектор, данные, системный регистр; `#FF` дополнительно задаёт привод, сторону, reset и head-load. |
| SD через Z80 | обычный режим: `xx77` — CS/config, `xx57` — SPI data | В Shadow режимы `xx57` разделяются по `A15`; это не TRD/SCL-диск. |
| ZX-Evo config/NMI | `xxBF`, `xxBE`, `xxBD` | `#BF` — конфигурация, `#BE` — чтение состояния/завершение NMI, `#BD` — адрес breakpoint; селектор `A[12:8]` у `#BE` обязателен. |
| ULAplus | `xx3B` | Режим и данные различаются по `A14` и управляющим битам записи. |

#### Аудит реализации ZX-Evo BaseConf на 2026-09-13

Аудит выполнен сравнением текущих классов `UlaPentEvo`, `UlaAtm450`,
`MemoryPentEvo`, `ZsdPentEvo` и состава машины в `machines.config` с локальными
исходниками RTL BaseConf. Это проверка исходного кода; пункты, требующие запуска
ROM или прикладного теста, отдельно помечены как непроверенные практикой.

Что в основном соответствует RTL и пока не должно переписываться без теста:

- выбор основных экранных режимов: стандартный ZX, hardware multicolor,
  Pentagon 16 colors, ATM 320x200, ATM 640x200 и два текстовых режима;
- базовая раскладка видеопамяти hardware multicolor и Pentagon 16 colors;
- адреса Kempston mouse: `FADF` — кнопки, `FBDF` — X, `FFDF` — Y;
- основное Z80-декодирование SD: `xx77`, `xx57` и разделение доступа к `xx57`
  по `A15` в Shadow;
- базовое адресное декодирование AY через семейства `BFFD`/`FFFD`.

Подтверждённые расхождения текущей реализации с RTL BaseConf:

1. `UlaPentEvo` использует один постоянный Pentagon-растр: 71680 тактов кадра,
   224 такта в строке. Не реализован выбор четырёх профилей BaseConf:
   Pentagon 71680, 60 Hz, 48K 69888 и 128K 70908. Вместе с профилем должны
   изменяться длина строки, положение изображения, INT и contention.
2. Экспериментальная схема частот в текущей рабочей копии растягивает
   `CPU.Tact` после выполнения процессорного цикла. Она не воспроизводит
   переключение 3,5/7/14 МГц на `RFSH`, динамические задержки памяти,
   14-МГц WAIT внешних портов и contention. Эту схему нельзя считать готовой;
   она является главным подозреваемым в регрессии Kempston mouse.
3. Базовый ULA-обработчик подписан почти на все чётные порты. В RTL border
   изменяется только при записи в младшие адреса `FE`, `F6` и `FC`. Частное
   исключение `BE`, добавленное в `UlaPentEvo`, проблему полностью не решает.
4. Обычный `BeeperDevice` также декодирует почти все чётные порты. В BaseConf
   beeper записывается через `xxFE`, а источник выбирается между D4 и tape-out
   D3 сигналом `beeper_mux`.
5. Готовая конфигурация ZX-Evo читает клавиатуру только через `xxFE`; RTL
   возвращает ту же клавиатурную матрицу и tape-in также через `xxF6`.
   `TapeDevice` в составе готовой машины ZX-Evo отсутствует.
6. `KempstonJoystick` в составе готовой машины ZX-Evo отсутствует. По RTL он
   читается через `xx1F` вне Shadow; в Shadow тот же младший адрес принадлежит
   VG93/TR-DOS.
7. Covox имеет правильный младший адрес `xxFB`, но в `machines.config` задан
   `noDos=true`. В RTL запись Covox не блокируется состоянием Shadow/DOS.
8. RTL BaseConf содержит ULAplus на `xx3B`, но готовая конфигурация ZX-Evo не
   содержит соответствующего устройства.
9. Порты `BF/BE/BD` требуют отдельной доводки: RTL определяет `BF` как регистр
   конфигурации, `BE` как мультиплексированное чтение состояния/завершение NMI,
   `BD` как запись адреса аппаратной точки останова. Текущая экспериментальная
   обработка чтения `BD` как дополнительного порта конфигурации не подтверждена
   RTL и должна быть проверена по используемому ROM до изменения.

Вывод по видео: основные форматы пикселей и раскладка видеопамяти выглядят
правдоподобно и частично совпадают с RTL, но весь видеовывод нельзя считать
точным, пока отсутствуют переключаемые растры, правильный INT, contention и
связанные WAIT. Ошибку Bad Apple нельзя заранее приписывать одному renderer:
сначала необходимо исправить системные тайминги и Covox, затем повторить тест.

#### Архивный порядок исправления до B01

1. Сохранить текущую незавершённую рабочую копию отдельной резервной точкой и
   подготовить чистую копию `ZXMAK2-NedoOS-Input-v13-20260913`. Ничего из
   текущих наработок молча не удалять и не переносить в базу целым пакетом.
2. Собрать чистую v13 и выполнить минимальные контрольные тесты: загрузка
   BaseConf, клавиатура, Kempston mouse, TR-DOS, первый и повторный выбор
   образа. Результат записать до первой правки.
3. Исправлять портовую часть по одному узлу с отдельной сборкой и проверкой:
   точное декодирование border; beeper; клавиатура/tape; Kempston joystick;
   Covox; затем ULAplus.
4. После стабильной портовой базы отдельно заменить экспериментальную схему
   частот на модель 3,5/7/14 МГц с WAIT и contention на уровне машинных/шинных
   циклов. Не компенсировать скорость скачком `CPU.Tact` после инструкции.
5. Отдельным этапом реализовать переключение профилей растра Pentagon, 60 Hz,
   48K и 128K и проверить длительность кадра, строки и положение INT.
6. После исправления таймингов проверить каждый видеорежим тестовыми экранами.
   Менять renderer или адресацию видеопамяти только при подтверждённом
   расхождении с RTL или тестом.
7. Затем повторно проверить Black Raven, EVO Reset Service и Bad Apple.
   Для Bad Apple раздельно фиксировать запуск, видеорежим, скорость и Covox.
8. SD, IDE, VG93, QuickBoot, ATM Turbo и TSConf не смешивать с указанными
   этапами. Для каждого из них должен быть отдельный цикл правка-сборка-тест.

Отдельно от Z80-портов зафиксировать SPI-регистры AVR↔FPGA: `$10/$11` клавиатура, `$20..$23` мышь и Kempston joystick, `$30` reset, `$40..$42` WAIT/RTC/RS232 address, `$50/$51` общая конфигурация, `$60/$61` SD data/control. Это внутренний интерфейс платы и не список портов, доступных программе Z80.
- Настройки ZX-Evo должны сохраняться после Warm Reset и после перезапуска эмулятора.
- QuickBoot должен быть доступен только если в активной конфигурации присутствует и активен TR-DOS.
- Наличие TR-DOS должно определяться также для пользовательской конфигурации, где TR-DOS добавлен вручную.
- QuickBoot не должен переводить машину в TR-DOS. Он работает только в уже активном режиме TR-DOS.
- При применении QuickBoot нужно перечитать состояние образов всех доступных дисководов пользовательской конфигурации — до четырёх, а не только A:.
- В меню Tools пункт QuickBoot должен появляться только при активном TR-DOS. Иконка остаётся прежней, но становится серой/активной по состоянию.
- Оболочку QuickBoot использовать существующую, не переписывать без отдельной необходимости.

### Диски и образы

- Поддерживаемые образы: прежде всего TRD и SCL, а также образы SD-карт, используемые конкретной машиной.
- Плата ZX-Evolution имеет отдельный контроллер SD(HC); официальное руководство также отдельно перечисляет IDE и floppy-контроллер с поддержкой до четырёх дисководов.
- В BaseConf SD-интерфейс использует регистр данных `$60` и регистр управления `$61`: `lock` находится в бите 7, `CS_n` — в бите 0. Доступ к SD разделяется между Z80 и периферийным контроллером AVR. Это важная граница для эмуляции: SD-образ нельзя трактовать как обычный TRD/SCL-диск.
- SD-карта в EVO используется как FAT-носитель для загрузки/хранения файлов, конфигураций и ROM; TRD/SCL остаются файлами или образами дисководов внутри этой среды. Для эмулятора нужно отдельно проверить чтение/запись SD-образа, его замену, сохранение состояния после Warm Reset и работу с файлами TRD/SCL на нём.
- Смена образа во время работы не должна считаться бесшовной: если архитектура требует Warm Reset, это должно быть явно и одинаково реализовано.
- Нужно проверить cold/warm reset, смену первого и второго образа, наличие образа в каждом из четырёх дисководов и повторное чтение состояния после QuickBoot.

### NeoGS / General Sound

Проверенный вывод по текущим исходникам ZXMAK2:

- ZXMAK2 фактически не предоставляет пользователю эмуляцию NeoGS или General Sound: такого устройства нет ни в готовых конфигурациях, ни в списке добавляемых устройств рабочей сборки.
- В дереве исходников действительно существует файл `src/ZXMAK2.Hardware/GeneralSoundDevice.cs`, но он не включён в `src/ZXMAK2.Hardware/ZXMAK2.Hardware.csproj`, поэтому не компилируется и не попадает в исполняемую сборку.
- Найденный `GeneralSoundDevice.cs` считать старой незавершённой экспериментальной заготовкой, а не доказательством поддержки General Sound или NeoGS. Повторно объявлять устройство реализованным только на основании наличия этого файла нельзя.
- В заготовке отсутствует полноценная поддержка SD-карты NeoGS; необходимая прошивка `bootgs.rom` также не подключена к проекту.
- Пункты `Access SD NeoGS` и `Reset NeoGS`, отображаемые в EVO Reset Service, принадлежат ROM ZX-Evolution и рассчитаны на физическую плату NeoGS. Наличие этих пунктов в ROM не означает, что NeoGS поддерживается эмулятором.
- Если NeoGS когда-либо будет добавляться в ZXMAK2, это отдельная новая подсистема, требующая реализации и проверки процессора, памяти, прошивки, портов обмена, звуковых каналов, reset/NMI и собственной SD-карты. Не включать эту работу неявно в исправления BaseConf.

### Scorpion ZS-256 Turbo+

Это отдельная аппаратная конфигурация, а не просто ProfROM-вариант существующего Scorpion.

Подтверждённые ориентиры:

- выпуск семейства Turbo+ — примерно 1996–1998;
- Z80B: 3,5/7 МГц;
- 256 КБ ОЗУ;
- AY-3-8912/совместимый AY;
- встроенный контроллер дисковода;
- два ZX-BUS;
- PROF ROM;
- сервисный монитор.

Источники для дальнейшей сверки:

- репозиторий схемы и платы: `https://github.com/romychs/Scorpion256TPlus`;
- каталог ROM и документации: `https://speccy4ever.speccy.org/_SC.htm`.

До реализации нужно установить конкретный ROM Turbo+, карту ROM, портовую карту, WAIT, видеотайминги, работу Beta Disk, клавиатуры и Kempston-мыши.

### Известные неисправности на момент старта B01

- После правок частот Kempston-мышь перестала работать на 3,5 и 7 МГц; проверить также 14 МГц не удалось. Симптом: курсор активируется/гаснет, но управление в программе не работает.
- ATM Turbo 2+ ранее запускался с включённым турбо, хотя должен начинать с Turbo Off.
- При смене второго образа ранее пропадал требуемый сброс.
- QuickBoot ранее работал неполно или некорректно.
- Bad Apple для ZX-Evo/BaseConf не запускается даже на исходной версии Alex Makeev; требуется отдельное исследование видеорежима, Covox и частоты.

## ZX-Evo / BaseConf — источники технической сверки

- Локальные исходники BaseConf: `fpga/baseconf/trunk/texts/video_modes.txt`, `fpga/baseconf/trunk/texts/dram_access.txt`, `fpga/baseconf/trunk/slave/spi_fmt.txt`, Verilog-модули `video`, `dram`, `z80` и `spihub`.
- Локальное руководство пользователя ZX Evolution: `docs/revC/zxevo_user_manual.pdf`.
- Внешняя сверка: https://bruxy.regnet.cz/web/8bit/EN/zx-evolution/ и https://github.com/tslabs/zx-evo.

### Архивный порядок следующего рабочего сеанса до B01

1. Зафиксировать и проверить базовую копию v13.
2. Снять список изменений и определить, какие из них уже находятся в рабочей копии.
3. Отдельно провести инвентаризацию машин и зависимостей.
4. Убрать из основного списка только однозначно ненужные конфигурации, не удаляя спорные файлы.
5. Исправлять и проверять машины по одной.
6. После каждого этапа обновлять этот журнал: что изменено, какая сборка получена, что проверено, что осталось неисправным.

## Формат записи результата проверки

Для каждой сборки записывать:

- имя/дату сборки;
- исходную базу;
- изменённую задачу;
- фактические действия пользователя;
- ожидаемый результат;
- фактический результат;
- новые регрессии;
- решение: принять, исправить или откатить.

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

## ZXMAK2-v13-ZXEVO-BC-RASTERDIAG-B18-20260917-213256 - BaseConf raster diagnostic indicator
- Base: ZXMAK2-v13-ZXEVO-BC-RASTERCTRL-B17-R1-20260916-004056; checkpoint verified before editing. All 806 pinned source files and all 79 release files matched the accepted B17-R1 backup; the current and backup journals also matched. Machine: ZX-Evo BSconf.
- One diagnostic node only: expose the existing B17 Scroll Lock / AVR video state and requested-versus-active raster state in the existing `View -> Debug Info` OSD. No raster timing, INT, contention, clock, DRAM arbitration, sound, keyboard mapping, persistence, ROM, configuration or non-Evo machine behavior changed.
- Seven production files changed: `ZXMAK2.Engine/Interfaces/IUlaDevice.cs`, `ZXMAK2.Hardware/Evo/CmosPentEvo.cs`, `ZXMAK2.Host/Interfaces/IFrameInfo.cs`, `ZXMAK2.Host/Entities/FrameInfo.cs`, `ZXMAK2.Engine/VirtualMachine.cs`, `ZXMAK2.Host.WinForms/Controls/RenderVideo.cs` and `ZXMAK2.Host.WinForms/Mdx/Renderers/OsdRenderer.cs`. No project file changed.
- Added optional `IFrameDiagnosticProvider`; only `CmosPentEvo` implements it. The engine transports its text through `FrameInfo` and the optional host-side `IFrameDiagnosticInfo`, and WinForms appends it to the already optional Debug Info overlay. The original `IFrameInfo` contract and five-argument `FrameInfo` constructor remain compatible; legacy machines receive an empty string and retain their old overlay.
- Indicator format: `ZX-Evo B18: Scroll=N AVR=HH TV/VGA=B raster req=M:name active=M:name[ pending]`. `Scroll` counts rising-edge Scroll Lock presses during the process lifetime; `AVR` is the supported video mask; `TV/VGA` is bit 0; requested and active modes/names expose the B17 software-frame-boundary handoff, with `pending` only while they differ.
- Compiled B18 probe passed provider discovery, AVR=31 / TV-VGA=1 / requested 3:128K versus active 0:normal pending, next-frame activation, `FrameInfo` transport and OSD property wiring. Preserved B17 activation, B16 RDCFG, B15 raster-table and B14 full regression probes all passed against the rebuilt release.
- Six Release projects rebuilt: Host, CPU, Engine, Hardware, WinForms and EXE. Relative to B17-R1 exactly the seven expected source files differ; the other 799 pinned files are byte-identical.
- Accepted ROM remains unchanged: ERS v0.61.01 FE, SHA256 620146534df8a49c6b9042df45812d1e7f90683dd8f7b813ca2c5ecac96dc1ca.
- Backup: L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RASTERDIAG-B18-20260917-213256. Separate runtime package: K:\Download\ZXMAK2-v13-ZXEVO-BC-RASTERDIAG-B18-20260917-213256\release. Build and probe transcripts are retained in the backup.
- Runtime acceptance is explicitly pending the user's test. Check `View -> Debug Info`, eight released plain Scroll Lock presses, requested/active convergence and return to the initial AVR/raster state. Do not infer physical TV/VGA output from bit 0.
- Still pending and deliberately outside B18: exact immediate transition/horizontal-vcount/INT, border sync, 48K/128K contention, IO/WAIT pin-edge/stall overlap/refill, full video/port audit, FLASH/NMI/12BD, SD replacement, physical FPS/audio/tape and comprehensive runtime acceptance.
- Decision: accept the compiled diagnostic visibility node only; do not claim B17/B18 runtime acceptance until the user completes the runtime test.

### Пользовательская проверка B18 — 2026-09-17

- Пользователь проверил все восемь состояний Scroll Lock и предоставил восемь снимков экрана. Диагностическая строка меняет `AVR`, `TV/VGA`, requested и active raster в соответствии с переключением; визуально также меняется развёртка.
- Диагностический индикатор B18 принят как работающий в runtime.
- Эта проверка является качественным подтверждением переключения и видимого изменения кадра. Она не измеряет точные координаты пикселей, частоту строк/кадров, фазу INT, contention или физические TV/VGA-сигналы.

## ZXMAK2-v13-ZXEVO-BC-VIDEOCONTRACT-B19-20260917-231704

- База: принятый B18. Перед началом 806 закреплённых исходных файлов, runtime из 79 файлов и оба журнала сверены с B18 без расхождений. Производственный код в B19 не изменялся.
- Один логический узел: точный паспорт реализованных видеорежимов BaseConf r1364 и карта расхождений текущего эмулятора. Паспорт: `BASECONF_VIDEO_MODES_B19.md`.
- Нормативный источник: `pentevo-fpga.r1364.tar.gz`, SHA256 `7A509FBCEF3AF85380EC475AD622A0682E714AF54625851FCB9AA678DA0BBB82`. Выбранные неизменённые RTL-файлы сохранены в `backup\ZXMAK2-v13-ZXEVO-BC-VIDEOCONTRACT-B19-20260917-231704\reference`.
- Установлено по `video_modedecode.v`, `video_addrgen.v`, `video_fetch.v` и `video_render.v`: r1364 реализует семь селекторных путей — ZX attr 256×192; Pentagon hardware multicolor 256×192; Pentagon 16c 256×192; ATM 16c 320×200; ATM hardware multicolor 640×200; ATM text 80×25; BaseConf one-page text 80×25.
- Существенная коррекция терминологии: enum `Evo256x192` выбирает Pentagon hardware multicolor, а не 256-цветный framebuffer. `EvoAlco16c` соответствует Pentagon 16c.
- `texts/video_modes.txt` является общей матрицей форматов/полосы памяти. Указанные там `256c` и `16+16c` не имеют декодера, адресогенератора, fetch или render-пути в BaseConf r1364 и не входят в обязательную матрицу совместимости r1364.
- Текущие семь маршрутов рендереров и основные схемы страниц/пикселей структурно присутствуют, но точными пока не приняты: общий raster/picture-window/border/INT, mid-frame policy, contention/floating bus, физический TV/VGA и mode-specific golden vectors остаются открыты.
- Шесть Release-проектов перестроены. Новый compiled `VideoModeContractProbe` прошёл 46 проверок селекторов, семи маршрутов, классов кадров, 16-элементных палитр, packed 16c и идентичности HM-пути. Полный сохранённый каскад B18/B17/B16/B15/B14 прошёл без регрессий.
- Отдельный backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOCONTRACT-B19-20260917-231704`. Новый runtime-пакет не создаётся: бинарный производственный контракт не менялся, текущий runtime остаётся B18.
- Решение: B19 принять как документационную и compiled-контрольную точку, без runtime-приёмки видеорежимов. Следующий узел B20 — только общая основа raster/picture window/border/INT; отдельные форматы и TRD не смешивать с B20.

## ZXMAK2-v13-ZXEVO-BC-VIDEOBASE-B20-20260917-233050 — общая временная база видео

- База: B19; перед правкой текущие 806 закреплённых исходных файлов, 79 файлов release, журналы и `BASECONF_VIDEO_MODES_B19.md` сверены с checkpoint B19 без расхождений.
- Выполнен только B20: общая для семи путей геометрия raster/picture window, эпоха INT, длительность INT и фаза border. Форматы пикселей, палитры, mode controller и TRD не изменялись.
- По RTL r1364 выставлены начала picture: 140-й цикл (70-й renderer tact) для 256×192 и 108-й цикл (54-й tact) для широких 320/640/text; первые строки wide 76/42/60/59, standard на четыре строки позже.
- Общий viewport начинается на том же месте для всех путей: горизонтально с 54-го tact, вертикально за 28 строк до wide-picture. Для короткой 60 Hz развертки нижний border ограничен фактической длиной кадра.
- Renderer epoch привязан к аппаратному INT: normal/60 Hz — v=0,h=2; 48K — v=1,h=126; 128K — v=1,h=130. INT длится 32 базовых tact (256 master tact). DRAM video fetch переведён в ту же эпоху.
- Для 48K/128K включена аппаратная 4T-защёлка border с вычисленной фазой; normal/60 Hz сохраняют немедленное обновление. Изменение внесено одинаково в Spectrum, ATM 320, ATM 640, ATM text, Evo text и Evo A16 renderers.
- Изменены ровно шесть production-файлов: `UlaPentEvo.cs`, `Atm320Renderer.cs`, `Atm640Renderer.cs`, `AtmTxtRenderer.cs`, `EvoTxtRenderer.cs`, `EvoA16Renderer.cs`.
- После каждой правки выполнены шесть Release rebuild. Итоговый B20 `VideoTimingProbe` прошёл 12 512 проверок; B20 raster-table probe — 17 355 101 проверку; B19/B18/B16 и адаптированный к INT-эпохе полный B14 regression probe прошли. Старые B17/B15/B14 probes сохранены неизменными в прежних checkpoint; их физическая нулевая точка fetch закономерно заменена отдельными B20-копиями.
- Это compiled-приёмка общей временной основы, не runtime-приёмка изображения. Физический VGA/TV, точные пиксели каждого формата, mid-frame переключение, contention/floating bus и wall-clock FPS/audio остаются открыты.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOBASE-B20-20260917-233050`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOBASE-B20-20260917-233050\release`.
- Следующий отдельный узел после пользовательской проверки B20: B21 mode controller и bootable TRD-оболочка. В B20 к нему не переходили.

### Пользовательская runtime-проверка B20 — 2026-09-18

- Пользователь подтвердил, что название активной развёртки и геометрия кадра меняются, переключение работает, видимых артефактов нет.
- B20 принят качественно в пределах runtime-задачи: стабильное переключение и отсутствие видимых повреждений кадра. Проверка не измеряет координаты с точностью до tact, частоты, фазу INT или физический TV/VGA.

## ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-20260918-000040 — picture controller и bootable TRD shell

- База: B20 после пользовательской качественной runtime-приёмки. Перед началом 806 закреплённых исходных файлов и 79 файлов release совпали с after-снимком B20; журналы и паспорт B19 также совпали.
- Один production-файл изменён: `src/ZXMAK2.Hardware/Evo/UlaPentEvo.cs`. Добавлен явный `BaseConfVideoModeController` с именованными семью маршрутами r1364.
- Контроллер проверяет все 32 сырых сочетания RG/RGEX. Pentagon selector игнорируется для RG 0/2/6/7, как в RTL; RG 1/4/5 и Pentagon code 3 детерминированно дают ZX fallback.
- Запрос формата отделён от активного renderer. B21 применяет renderer route на следующей программной границе кадра, совместно с существующей политикой raster B17/B20; requested/active/pending доступны как hardware values.
- Это явная программная политика эмулятора, а не утверждение о точном mid-frame FPGA edge. Точная аппаратная семантика смены формата остаётся отдельной задачей.
- Создан `tools/BaseConfVideoTest/BcVideoMenu.asm` и воспроизводимый полный `BCVIDTEST.TRD` размером 655360 байт. В каталоге два файла: автозапускаемый `boot.B` и `BCMENU.C`, загружаемый по адресу 32768.
- B21 TRD содержит оболочку с семью корректно названными r1364 mode slots, управлением Q/A, Enter, 1..7 и Space. Слоты намеренно сообщают `FORMAT PATTERN: B22+`: B21 проверяет boot/menu/dispatch, но не выдаёт placeholder за тест пиксельного формата.
- Использован официальный Windows binary sjasmplus v1.24.0; архив `sjasmplus-1.24.0.win.zip`, SHA256 `7E1F8840842039BDB97E51A59ABDA2E5C25A9DA160097E8F79A62583AB72D0E0`, сохранён в reference B21.
- Проверки: B21 controller probe — 321; B21 timing probe — 12736; B21 TRD probe — 30. B19 video contract, B18 diagnostic, B16 RDCFG, B20 raster table и полный B14 regression cascade прошли.
- Старый B20 timing probe ожидаемо не соответствует новой frame-boundary picture policy и сохранён неизменным в B20. Его заменяет отдельный B21 timing probe; остальные старые probes не менялись.
- Runtime-приёмка B21 ожидает пользовательскую проверку автозапуска TRD, отображения семи пунктов, Q/A/Enter, прямых клавиш 1..7 и возврата Space. Форматные изображения ещё не тестируются.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-20260918-000040`. Runtime и TRD: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-20260918-000040`.
- Следующий изолированный узел после приёмки оболочки: B22, только ZX 256×192 attr golden vectors и визуальный pattern в первом слоте TRD.

## ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-R1-20260918-004015 — исправление boot.B

- Пользовательская runtime-проверка исходного B21 выявила отказ: другие TRD читаются, а `BCVIDTEST.TRD` пытается стартовать без видимого результата; `LIST` также ничего не показывал. Исходный B21 runtime не принят и сохранён без перезаписи.
- Причина локализована в BASIC-загрузчике: после автозапуска `boot.B` он выполнял обычный `LOAD "BCMENU" CODE` без повторного входа в TR-DOS, поэтому команда уходила в магнитофонный загрузчик BASIC ROM.
- Исправлен только этот узел. Строка 10 теперь выполняет `RANDOMIZE USR VAL "15619": REM: LOAD "BCMENU" CODE`; отдельная строка 20 запускает загруженный код через `RANDOMIZE USR VAL "32768"`. Контроллер видеорежимов, тайминги и menu machine code не менялись; B22 не начат.
- Добавлен постоянный `tools/BaseConfVideoTest/TrdShellProbe.cs`. Он монтирует образ через штатный `TrdSerializer` ZXMAK2, проверяет каталог и системный сектор, делает полный байтовый serialize round-trip, требует последовательность `USR 15619 / REM / LOAD`, autostart marker и семь menu slots.
- После каждой исходной правки шесть Release-проектов перестроены. Итоговый B21-R1 TRD probe прошёл 655905 проверок; B21 controller 321, B21 timing 12736, B19 contract 46, B18 diagnostic, B16 RDCFG 201030, B20 raster table 17355101 и полный B14 cascade прошли без регрессий.
- Новый `BCVIDTEST.TRD`: 655360 байт, SHA256 `8CD7E0B1EAE5F91077B8CBB841C4BEAD45940A205836C96CFA9C63DAB7516F4A`. Emulator release намеренно взят без изменений из B21; исправлен только TRD/README.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-R1-20260918-004015`. Новый runtime/TRD: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOMENU-B21-R1-20260918-004015`.
- Runtime-приёмка B21-R1 ожидает повторную пользовательскую проверку boot/menu. Не заявлять приёмку и не начинать B22 до этого теста.

## ZXMAK2-v13-ZXEVO-BC-VIDEOPATTERN-B22-20260918-041824 — общий pattern-suite семи видеомаршрутов

- База: B21-R1 после пользовательского подтверждения исправленного TRD boot/menu. В production-коде эмулятора B22 изменений не вносит; изменены только тестовая оболочка, TRD-probe и документация.
- В `tools/BaseConfVideoTest/BcVideoMenu.asm` добавлены общая физическая bitmap/attribute pattern-запись, выбор семи raw RG/RGEX селекторов r1364 (`3,19,11,0,2,6,7`) через `EFF7` и `FF77`, а также ожидание программной границы кадра после выбора.
- Семь слотов теперь показывают `B22 TEST SLOT` и `COMMON PATTERN / ROUTE ACTIVE`; названия режимов сохранены: ZX 256×192 attr, Pentagon 256 HWM, Pentagon 256 16c, ATM 320×200 16c, ATM 640×200 HWM, ATM text 80×25, BaseConf text 80×25.
- `TrdShellProbe-B22` проверяет полный 640 KiB TRD round-trip через `TrdSerializer`, каталог/автозагрузку, семь пунктов и B22 slot markers. Результат: 655906 PASS. Сохранённый compiled cascade B21/B20/B19/B18/B16/B14 прошёл.
- После правки выполнены sjasmplus assembly, шесть Release rebuild и все compiled probes. Физическая картинка, соответствие каждого pixel format и отсутствие артефактов остаются runtime-проверкой пользователя; приёмка не заявляется.
- Отдельный runtime/backup B22 создаётся после этой контрольной точки; следующий логический узел до пользовательского теста не начинать.

### Пользовательская runtime-проверка B22 — отклонено

- Получены семь последовательных снимков. ZX attr показывает подписанный pattern; Pentagon HWM, ATM 640 HWM и оба text-режима дают чёрный экран; Pentagon 16c и ATM 320 выводят полосы без читаемой подписи.
- Переключение семи renderer routes подтверждено различающимся выводом, но общий Spectrum-page pattern не соответствует физической раскладке остальных форматов. B22 runtime не принят.

## ZXMAK2-v13-ZXEVO-BC-VIDEOPATTERN-B22-R1-20260918-044100 — format-aware pattern suite

- Исправлен только тестовый TRD. Production-код эмулятора не изменён и следующий видеоузел не начат.
- Каждый слот теперь подготавливает собственную раскладку: ZX bitmap/attrs; Pentagon HWM bitmap и построчные attrs; Pentagon packed 16c страницы 4+5; ATM 320 страницы 1+5; ATM 640 bitmap page 5 + attrs page 1; ATM text symbols page 5 + attrs page 1; BaseConf one-page text page 8.
- Для обоих текстовых режимов Spectrum ROM font 32..127 загружается через `XXBF.D2` в 2 KiB character generator. Все семь экранов содержат собственную подпись B22-R1, pattern marker и приглашение Space.
- Стек теста перенесён из области HWM attrs в безопасную страницу по адресу `BFF0`. Физические страницы временно отображаются в окно `C000` через BaseConf/7FFD paging; после возврата в меню восстанавливается стандартный маршрут и страница.
- TRD: 655360 байт, SHA256 `8B8258D0B721E2B953AFB5C2E304A665EB9F0C805835A26C4270D215D2BBEDB4`. `TrdShellProbe-B22-R1`: 655916 PASS, включая штатный ZXMAK2 serialize round-trip, каталог, page mapper, font-write sequence, packed-pixel table и семь raw selectors.
- После каждой правки выполнены sjasmplus assembly, шесть Release rebuild и сохранённый B21/B20/B19/B18/B16/B14 cascade; все проверки PASS.
- Runtime-приёмка B22-R1 ожидает повторные семь пользовательских снимков. До неё следующий логический узел не начинать.

### Пользовательская runtime-проверка B22-R1 — отклонено уже на BOOT

- Пользователь уточнил, что искажённые символы и случайные цветные блоки появляются сразу после автозапуска `boot.B`, до выбора любого из семи тестов. Поэтому этот снимок не является результатом renderer route и не позволяет оценивать видеорежимы.
- Причина локализована в тестовом ASM: стартовая загрузка font RAM сначала выполняла одиночный `OUT #7FFD`, ошибочно считая его самостоятельным селектором физической страницы `C000`. После ERS/TR-DOS активной могла быть единичная карта памяти и произвольные дескрипторы окон; запись также меняла бит выбора карты и разрывала соответствие CPU-экрана и видеостраницы.
- B22-R1 runtime не принят. Production-код эмулятора по этому наблюдению не обвиняется и следующий логический узел не начат.

## ZXMAK2-v13-ZXEVO-BC-VIDEOPATTERN-B22-R2-20260918-051327 — безопасный BOOT/pager

- Сверено с официальным руководством BaseConf и RTL r1364: `#00BE..#07BE` читают инверсные номера страниц обеих карт, `#08BE/#09BE` — `ramnrom/dos7ffd`, `#0ABE` — последнее значение `#7FFD`; `#FFF7/#F7F7` являются документированными descriptor/direct-page портами окна `C000`.
- BOOT теперь сначала сохраняет активную карту и точный дескриптор `C000`, временно открывает только `XXBF.D0`, отключает `dos7ffd` для рабочего окна, выбирает линейную физическую страницу через `#F7F7`, загружает шрифт через `XXBF.D2` и до рисования меню восстанавливает исходный descriptor. Значение `#7FFD` не изменяется.
- BaseConf text page исправлена с 7FFD-кодированного `$20` на линейный физический номер `8`. Возврат Space также восстанавливает исходный `C000`, а не принудительно пишет страницу через `#7FFD`.
- `TrdShellProbe-B22-R2` требует чтения `#0ABE/#08BE`, записи `#FFF7/#F7F7`, state-preserving font enable и отсутствие старого standalone `#7FFD` mapper. Результат: 655920 PASS; TRD 655360 байт, SHA256 `2943B63888036DF094D3F5A402085C5F3FBD4C0ED038527FD192FC154576C52B`.
- Все 23 Release-проекта перестроены последовательно Visual Studio 2022 MSBuild. Сохранённые B21 controller/timing, B19 contract, B18 diagnostic, B16 RDCFG, B20 raster и полный B14 refresh cascade прошли.
- Runtime/backup: `ZXMAK2-v13-ZXEVO-BC-VIDEOPATTERN-B22-R2-20260918-051327`. Первая пользовательская проверка — только чистый BOOT/menu; runtime-приёмка и переход дальше не заявляются.

### Пользовательская runtime-проверка B22-R2 BOOT — принята

- Пользовательский снимок сразу после автозапуска подтверждает чистое стандартное меню `BASECONF VIDEO B22-R2`: заголовок, семь mode slots, курсор и обе строки управления читаются; прежних повреждённых глифов и случайных цветных блоков нет.
- Принят только BOOT/menu и исправление безопасного pager/font-init. Запуск пунктов, возврат Space и изображения семи renderer routes этим снимком не проверялись; B22 целиком ещё не принят.
- Следующий логический узел не начат.

## ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642 — глобальная BaseConf-палитра и чистый ATM 640 HWM test

- Основание: пользователь сравнил один и тот же ZX-Evo экран в ZXMAK2 и UnrealSpeccy. ZXMAK2 показывал только уровни каналов `00/AA/FF`, тогда как эталон использовал все четыре двухбитных уровня `00/55/AA/FF`. Отдельно на B22-R2 ATM 640×200 HWM были видны небольшие фрагменты под строкой `PRESS SPACE TO MENU`.
- Повторная сверка выполнена по официальному `zxevo_base_configuration.pdf` и актуальному RTL `tslabs/zx-evo`: палитра содержит 16 логических цветов из 64 аппаратных, запись выполняется через shadow `#FF`, вход PentEvo имеет раскладку `grbG--RB`, а RGB-выход — инверсный `GgRrBb`. Существующая перестановка входных битов, инверсия и декодирование порта в ZXMAK2 совпали со спецификацией.
- Найден production-дефект в `UlaAtm450.SetPaletteAtm`: все семь renderer-путей совместно использовали один массив палитры, но собственные таблицы ink/paper пересчитывал только активный renderer. После записи палитры в одном режиме и последующего переключения другой renderer использовал старый Spectrum cache. Теперь каждая аппаратная запись обновляет декодированные таблицы всех семи BaseConf render paths.
- Адресация ATM 640×200 в `Atm640Renderer` и `BcVideoMenu.asm` подтверждена по официальной формуле: 40 блоков 16×1; bitmap page 5/7 по смещениям `X+Y*40` и `#2000+X+Y*40`; attributes page 1/3 с теми же смещениями. Исправлять production renderer по этому наблюдению не потребовалось.
- Артефакты теста локализованы в стандартной области системных переменных Spectrum `#5Cxx`, которая физически находится в page 5 и в ATM 640 HWM видна примерно на строках 179–180. B22-R2 разрешал ROM interrupt, и обработчик записывал туда счётчики. B23 держит interrupts disabled и заменяет `EI/HALT` на достаточную busy-wait задержку, по-прежнему пересекающую программную границу кадра.
- Новые compiled probes: `VideoPaletteProbe-B23` — 1807 PASS, все 256 PentEvo значений и обновление кешей семи renderer-путей; `TrdShellProbe-B23` — 655922 PASS, включая штатный ZXMAK2 round-trip, безопасный BaseConf pager и отсутствие ROM `EI/HALT` wait. Сохранённые B21 controller/timing, B19 contract, B18 diagnostic, B16 RDCFG, B20 raster table и полный B14 cascade прошли без регрессий.
- Полная Release solution пересобрана после функциональной правки и повторно после документации. Сохранились только известные предупреждения об отсутствующих `AllRules.ruleset` и `MinimumRecommendedRules.ruleset`; ошибок сборки нет.
- `BCVIDTEST.TRD`: 655360 байт, SHA256 `14423989A5819D9C2A74D11D54CE2205D947145BB716752B5B42A84419B89CCA`.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642`. Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642`.
- Runtime-приёмка B23 не заявляется. Пользователю нужно повторить палитровый экран и слот 5 ATM 640×200 HWM; ожидаются четырёхуровневая палитра как в UnrealSpeccy и отсутствие фрагментов около строк 179–180. Остальные шесть слотов и возврат Space также остаются пользовательской проверкой.

### Пользовательская runtime-проверка B23 — принята

- Пользователь представил одновременный снимок ZXMAK2 и UnrealSpeccy с одной конфигурацией ZX-Evo: четырёхуровневая программируемая палитра в ZXMAK2 визуально соответствует эталону UnrealSpeccy; прежняя насыщенная reset Spectrum palette больше не наблюдается.
- Отдельно подтверждено, что в ATM 640×200 HWM исчезли фрагменты под `PRESS SPACE TO MENU`. Пользователь также подтвердил прохождение остальных тестов `BCVIDTEST.TRD`.
- B23 принимается по runtime: глобальная BaseConf palette, семь format-aware test slots, чистый ATM 640×200 HWM и возврат в меню работают в проверенном сценарии. Это не является измерительной сертификацией точной аналоговой цветопередачи, pixel clock, INT или аппаратной фазы растра.

## ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541 — чистый старт DirectSound и безопасный underrun

- Основание: пользователь в наушниках обнаружил слабый фон и мелкие щелчки сразу после чистого запуска ZXMAK2, а также усиление артефактов синхронно с обращениями к дисководу. На той же программе UnrealSpeccy/RealSpec воспроизводили чистый звук.
- Повторно сверены официальные разделы BaseConf о звуке и VG93 и актуальный RTL `zports.v`. В BaseConf нет генератора звука механики дисковода: `#FE` управляет beeper/tapeout, `#FB` — unsigned Covox, AY использует `#FFFD/#BFFD`; VG93 использует shadow-порты `#1F/#3F/#5F/#7F/#FF`. Текущие `FddPentEvo` decode и системный регистр соответствуют этому контракту и в B24 не менялись.
- Найдена независимая причина на стороне Windows-аудиовывода: DirectSound начинал циклическое воспроизведение до очистки вновь созданного аппаратного ring buffer. При нехватке очередного блока код заполнял весь пропуск последним ненулевым стереосэмплом, создавая DC-площадку и последующий слышимый скачок.
- В `src/ZXMAK2.Host.WinForms/Mdx/DirectSound.cs` все сегменты ring buffer теперь обнуляются до `Play`. При underrun оба signed 16-bit канала линейно сводятся от последнего сэмпла к цифровому нулю внутри одного блока, после чего сохранённое состояние также становится нулём.
- Добавлен `tools/BaseConfAudioTest/DirectSoundBufferProbe.cs`: он исполняет приватный underrun path, проверяет точную независимую стереорампу, достижение нуля, сброс retained sample и порядок initial-clear-before-play. Результат — 16 PASS.
- После функциональной правки и после добавления probe выполнена полная Release-сборка всех 23 проектов. Ошибок нет; остались только прежние предупреждения об отсутствующих `AllRules.ruleset` и `MinimumRecommendedRules.ruleset`. Повторно прошли `VideoPaletteProbe-B23` — 1807 PASS и `TrdShellProbe-B23` — 655922 PASS.
- В B24 намеренно не менялись `Wd1793`, `FddPentEvo`, AY, видеокод и TRD. Сверка выявила два следующих самостоятельных узла: точные условия VG93 Force Interrupt по index/ready и документированная общая линия BaseConf `#FB/#FE` с переключением beeper/Covox и Num Lock beeper/tapeout. Их нельзя смешивать с исправлением host buffer.
- Runtime-приёмка B24 не заявляется. Пользовательская проверка должна включать наушники: чистый запуск, неподвижный idle, загрузку с FDD при мигающей иконке и ту же игру/программу для сравнения с UnrealSpeccy или RealSpec.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`. Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AUDIOBUFFER-B24-20260918-113541`.

### Пользовательская runtime-проверка B24 — отклонено

- Пользователь проверил B24 в наушниках. Небольшой фон остаётся сразу после чистого запуска эмулятора; во время активности дисковода, совпадающей с миганием FDD-индикатора, остаются дополнительные скрежет/щелчки; в сравнительной игре звук ZXMAK2 остаётся менее чистым, чем в UnrealSpeccy.
- Следовательно, очистка DirectSound ring buffer и безопасная underrun-рампа B24 являются защитными исправлениями host-output, но не устраняют наблюдаемую первопричину. B24 runtime не принят; при этом правку нельзя автоматически откатывать как вредную без отдельного основания.
- По предоставленной пользователем актуальной ветке UnrealSpeccy PentEvo `tools/unreal_fix/0.39.0/Unreal_NS` подтверждено: финальный микс использует включённый по умолчанию `RejectDC`; текущий ZXMAK2 такого финального DC reject не имеет.
- Текущая архитектура ZXMAK2 требует отдельной проверки: `SoundDeviceBase` переводит unsigned DAC zero в signed `-32768`, `FrameSound` усредняет AY/Beeper/Covox, а конфигурация ZX-Evo держит Beeper `#FE` и Covox `#FB` независимыми источниками. По BaseConf это общая физическая линия, которую запись `#FB` передаёт Covox, а следующая запись `#FE` возвращает Beeper.
- В `Wd1793` нет реализованного генератора механического звука FDD; шум, коррелирующий с VG93, считать паразитным аудиоэффектом до доказательства обратного. Отдельно выявлено расхождение частоты AY: ZX-Evo наследует `1773400`, актуальная ветка UnrealSpeccy PentEvo задаёт `1750000`; это кандидат на различие тона, но не объяснение фонового шума само по себе.
- Следующий объединённый узел: B25+B26 — измерение mean/RMS/peak/межбуферных скачков отдельно по AY/Beeper/Covox и итоговому PCM, затем минимальное исправление DC/тишины на основании результата. До новой правки не повторять B24 и не начинать VG93 или общий `#FB/#FE` тракт.

## ZXMAK2-v13-ZXEVO-BC-AUDIODC-B25-B26-20260918-161532 — измерение и финальный RejectDC

- Основа этапа сверена с backup B24 без изменений production/release-файлов. Учтён runtime-результат B24: видеорежимы не поломались, но слабый постоянный фон слышен сразу после старта, при работе FDD возникает хруст, а звук демо остаётся менее чистым, чем в UnrealSpeccy.
- B25 локализовал дефект до DirectSound. При цифровом idle AY выдавал постоянные `-32768/-32768`, а итоговый микс AY/Beeper/Covox — `-10922/-10922` с тем же RMS и нулевой AC-составляющей. Запись unsigned zero в Beeper/Covox давала переход до `-32768` с максимальным шагом `21859`. Это измеряемая постоянная составляющая и источник чувствительности к переключениям, а не звук механики FDD.
- В B26 добавлен опциональный финальный stereo RejectDC после сведения всех источников: `y = 0.995 * (x - x1) + 0.99 * y1`, по схеме актуального UnrealSpeccy. Состояние фильтра сохраняется между host-кадрами; первый входной sample используется как начальная база и выдаётся цифровым нулём, поэтому холодный старт не создаёт отдельный DC-переход.
- Политика включена только устройством `AYCHRV`, которое в `machines.config` используется только машиной `ZX-Evo BSconf`. Старый двухаргументный `FrameSound` сохраняет прежнее поведение для остальных машин.
- Изменены production-файлы `ISoundRenderer.cs`, `BusManager.cs`, `FrameSound.cs`, `AYCHRV.cs`; добавлен диагностический `tools/BaseConfAudioTest/AudioPathProbe.cs`. DirectSound-исправление B24 сохранено.
- Финальный `AudioPathProbe`: 43 PASS; оба фильтрованных idle-кадра имеют mean/RMS/peak `0`, разрыва на границе кадров нет. Release solution пересобрана без ошибок; остались только прежние предупреждения об отсутствующих ruleset-файлах.
- Повторно прошли: DirectSound B24 — 16 PASS; palette B23 — 1807; TRD B23 — 655922; B21 controller — 321; B21 timing — 12736; B19 contract — 46; B18 diagnostic; B16 RDCFG — 201030; B20 raster table — 17355101; полный B14/B20 Refresh cascade — PASS.
- Не менялись частота AY, логика VG93/Force Interrupt, арбитраж общей линии `#FB/#FE`, видео, `BCVIDTEST.TRD`, ROM и machine configuration.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AUDIODC-B25-B26-20260918-161532`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-AUDIODC-B25-B26-20260918-161532\release`.
- Runtime-приёмка не заявляется. Требуется пользовательская проверка в наушниках: запуск, не менее минуты idle, FDD activity и та же демо/игра в прямом сравнении с UnrealSpeccy.

### Пользовательская runtime-проверка B25+B26 — частично принята

- Пользователь подтвердил в наушниках: после старта и в idle теперь полная тишина, прежний постоянный фон исчез. Видеорежимы не поломались. Для области startup/idle финальный RejectDC B25+B26 принят.
- При работе дисковода остался отдельный скрежет/хруст. Музыка стала, возможно, немного чище, но при прямом сравнении всё ещё звучит грязнее UnrealSpeccy. Полная audio runtime-приёмка B25+B26 поэтому не заявляется.
- Контрольный запуск неизменённого исходного ZXMAK2 с GitHub показал: скрежета FDD там нет, однако музыка и в оригинале менее чистая, чем в UnrealSpeccy. Тем самым дефекты разделены: FDD-скрежет является регрессией текущей fork/runtime-комплектации, а оставшаяся разница AY/music унаследована от исходного ZXMAK2 и должна исследоваться отдельно.

## ZXMAK2-v13-ZXEVO-BC-CLEANPROFILE-B27-20260918-170648 — чистый runtime-профиль ZX-Evo

- В runtime-пакете B25+B26 найден сохранённый `ZXMAK2.vmz` от 14.09.2026, который `MainViewModel` безусловно загружает поверх `machines.config`. Поэтому B04/B05 фактически не участвовали в пользовательской проверке: сохранённый Beeper имел `mask=7`, `bitMic=3`, Covox — `noDos=True`, Keyboard — `mask=255`.
- Широкая маска Beeper и включённый MIC D3 позволяют записям нестрого в low `#FE`, а также изменениям D3/бордюра во время загрузки, попадать в звуковой тракт. Это наиболее конкретный кандидат на fork-only скрежет. FDD-индикатор лишь читает `Wd1793.LedRd/LedWr` в конце кадра и не генерирует звук или прерывание.
- Добавлен канонический переносимый профиль `tools/ZXEvoBsconf.vmz`, совпадающий с текущим блоком `ZX-Evo BSconf`: Beeper exact low `#FE`, `D4`, `bitMic=-1`, `noDos=false`; Covox exact low `#FB`, `noDos=false`; Keyboard `mask=#F7/port=#FE`. В пакет он копируется как `release/ZXMAK2.vmz`.
- Production-код, частота AY, RejectDC, DirectSound, VG93, видео, ROM, TRD и `machines.config` в B27 не менялись. Из runtime-папки исключены старые `.cmos/.nvram/.vmide/.log` состояния.
- `CleanProfileProbe` прошёл 40 проверок структуры, атрибутов, порядка и разрешения типов. Полная Release solution собрана без ошибок и с двумя прежними ruleset warnings. Повторно прошли AudioPath 43, DirectSound 16, palette 1807, TRD 655922, B21 controller/timing 321/12736.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-CLEANPROFILE-B27-20260918-170648\release`. Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-CLEANPROFILE-B27-20260918-170648`.
- Runtime-приёмка B27 не заявляется. Первая проверка — та же загрузка с FDD в наушниках. Чистоту музыки против UnrealSpeccy этим этапом не оценивать как критерий B27: она выделена в следующий самостоятельный AY/mixer/cadence узел.

### Пользовательская runtime-проверка B27 — принята

- Пользователь подтвердил: постоянного фона нет, при работе дисковода наводок/скрежета нет. Регрессия fork/runtime-профиля устранена каноническим `ZXMAK2.vmz`.
- Первоначальное сравнение музыкальной чистоты с UnrealSpeccy признано некорректным: в выбранной демке UnrealSpeccy показывал много активных каналов и, вероятно, использовался многоканальный/multisound-путь, тогда как ZXMAK2 выводил обычный AY-путь.
- На демке с тем же обычным AY-звуком пользователь не подтвердил ухудшение ZXMAK2; субъективно звук оказался даже немного насыщеннее. Направление отдельной «очистки AY» закрыто без изменений кода.
- B27 принимается по runtime-критериям startup/idle и FDD activity. Это не осциллографическая сертификация аналогового тракта и не утверждение о внутреннем режиме UnrealSpeccy без отдельной инструментальной проверки.

### Новая пользовательская регрессия после B27 — Rage на SCL

- Пользователь сообщил отдельный сценарий только для некоторых программ: демка Rage на том же SCL-образе при однократном Enter показывает активность FDD, затем остаётся на чёрном экране; если повторить выбор и удерживать Enter, загрузка проходит. В UnrealSpeccy с тем же образом достаточно одного Enter.
- Это не связано с уже закрытым аудиоузлом: FDD-иконка здесь рассматривается только как признак активности контроллера.
- Первичные кандидаты разделены: (1) преобразование SCL в `DiskImage` (`SclSerializer`/каталог/сектора), затем виртуальный FDD-путь PentEvo через `#13BD` и временное отображение RAM-страницы `#FE` с выходом через `#BE`; (2) WD1793 timing/DRQ/INTRQ и завершение команд; (3) CPU/DRAM WAIT во время нестандартного загрузчика. Сам факт удержания Enter не доказывает дефект клавиатуры — он может менять только момент запуска/повторной попытки.
- В `Wd1793` условия Force Interrupt `index/ready` помечены незавершёнными, но в доступном коде UnrealSpeccy эти ветви также немедленно завершаются; исправлять их вслепую нельзя.

## Следующий узел B28 — Rage/SCL loader diagnostic

- Сначала воспроизвести два сценария на одном чистом B27-профиле и одном SCL: одиночный Enter до чёрного экрана и удержание Enter до успешного запуска.
- Проверить SCL-дескрипторы, размер файла, порядок/границы секторных данных и результат преобразования в `DiskImage` до запуска CPU.
- Затем снять для обоих проходов FDD-команды, PC/tact, DRQ/INTRQ, смену дорожки/сектора, состояние `#13BD`, вход/выход RAM-страницы `#FE` через `#BE` и момент остановки активности.
- Сопоставить результат с виртуальным FDD-путём и обычным VG93-путём; только после локализации менять один узел. Клавиатуру, аудио и видеорежимы в B28 не менять.
- Runtime-приёмка B28 не заявляется до повторного пользовательского теста Rage.

### B28 diagnostic package — Rage FDD trace

- `RAGE.SCL` проверен до запуска CPU: `SclSerializer` сформировал стандартный диск 80x2; каталог и данные всех 7 файлов/163 секторов совпали с исходным SCL, 113/113 проверок PASS. Формат SCL исключён как непосредственная причина.
- В `FddPentEvo` добавлена условная диагностическая трасса, активная только при `logIo=true`: операции `#13BD`, выбранный drive/mask, попытка и результат входа в виртуальный FDD RAM page `#FE`, запрос выхода через `#BE`, PC/tact и компактный снимок WD1793 после команды. В `MemoryPentEvo` добавлены только read-only диагностические свойства состояния отображения.
- Подготовлен отдельный профиль `tools/ZXEvoBsconf-RageTrace.vmz`; канонический `tools/ZXEvoBsconf.vmz` не изменён. Алгоритмы FDD/WD1793, audio, keyboard и video не менялись.
- Полная Release solution собрана без ошибок; остались два прежних ruleset warnings. Пакет проверен: обе папки имеют `logIo=True`, `noDelay=False`, одинаковые бинарники и `RAGE.SCL`, старых `.cmos/.nvram/.vmide/.log` нет.
- Повторные проверки после сборки: SCL Rage 113 PASS; B21 controller 321 PASS; B21 timing 12736 PASS; B23 TRD 655922 PASS.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-RAGETRACE-B28-20260918-211640`. Два независимых прохода: `single-enter` пишет `ZXMAK2-B28-single-enter.log`, `held-enter` пишет `ZXMAK2-B28-held-enter.log`.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-RAGETRACE-B28-20260918-211640`.
- Это диагностическая сборка, не исправление. Runtime-приёмка не заявляется; для локализации нужны оба пользовательских журнала.

### B28-R2 — разделение SD/Ramdisk и прямого SCL

- Пользователь установил решающее различие: один и тот же `RAGE.SCL` при прямом открытии в эмулируемый FDD запускается одним Enter, а после загрузки из SD-образа через EVO Service (`Ramdisk: RAGE`) воспроизводит чёрный экран до удержания Enter.
- Следовательно, первичный узел смещён с общего SCL/FDD к цепочке SD-образ → EVO Service/Ramdisk → виртуальный диск/boot handler. Это также объясняет наличие `Ramdisk: RAGE` при `Mount A–D: NONE`.
- Подготовлен уточнённый пакет `K:\Download\ZXMAK2-v13-ZXEVO-BC-RAGETRACE-B28-R2-20260918-222848` с двумя проходами: `service-sd` и `direct-scl`. Нужны оба лога для сравнения.
- До получения логов патч FDD не выполняется; runtime-приёмка не заявляется.

### B28-R3 — virtual handler M1 trace

- B28-R2 логи подтвердили: в `direct-scl` WD1793 реально читает 80-дорожечный образ; в `service-sd` физический FDD остаётся not-ready, а весь сценарий проходит через `#13BD` и RAM handler `#FE` с выходом `#BE`.
- Добавлена ещё только диагностическая запись: первые 16 M1 fetch из страницы `#FE` после каждого виртуального FDD-перехвата. Она ограничена `logIo=True`; логика отображения и обработки не менялась.
- Следующее сравнение: два исхода одного SD/EVO Service пути (`Ramdisk: RAGE`) — одиночный Enter против удержания Enter. Пакет: `K:\Download\ZXMAK2-v13-ZXEVO-BC-RAGETRACE-B28-R3-20260918-223928`.
- Release собрана без ошибок, SCL Rage 113 PASS. Runtime-приёмка не заявляется.

## ZXMAK2-v13-ZXEVO-BC-VFDDRTL-B29-20260918-231349 — virtual FDD по RTL BaseConf

- Парные B28-R3 журналы и повторный одиночный запуск локализовали сбой в обработчике виртуального диска: после Enter ERS зацикливался на `PC #0248/#024E/#02F4`, непрерывно снимая и возвращая маску дисковода. Прямой SCL через физический VG93 при этом работает.
- Причина сверена с официальными `zdos.v`, `atm_pager.v` и руководством BaseConf. При входе в `trdemu` RAM-страница `#FE` аппаратно защищена от записи до первого следующего M1. Это не даёт завершающей записи блочной I/O-инструкции (`INI/INIR`) испортить начало обработчика. В ZXMAK2 страница ранее становилась доступной для записи сразу.
- `MemoryPentEvo` теперь включает временную защиту записи страницы `#FE` на входе и снимает её на первом M1. Вход разрешён при активном DOS и любой ROM-странице в окне `#0000`, как задают сигналы `dos && romnram`, а не только при точном совпадении с ROM_DOS.
- `FddPentEvo` теперь повторяет аппаратное отключение physical VG93 для выбранного masked drive: замаскированные обращения поглощаются даже во время уже активного handler, а system write не успевает изменить физический WD/выбранный drive до перехвата.
- Порт маски FDD приведён к реальному decode: low `#BD` плюс `A12..A8=#13`; старшие `A15..A13` являются aliases (`mask #1FFF`). Специальных условий для Rage или клавиши Enter нет.
- Полная Release solution собрана без ошибок; остались только два прежних ruleset warnings. Проверки: SCL Rage — 113 PASS, B21 controller — 321 PASS, B21 timing — 12736 PASS, B23 TRD — 655922 PASS.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VFDDRTL-B29-20260918-232134\release`. Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VFDDRTL-B29-20260918-231349`.
- Runtime-приёмка B29 не заявляется. Нужен пользовательский повтор сценария SD image → EVO Service → `Ramdisk: RAGE` → один короткий Enter; затем контроль прямого открытия `RAGE.SCL` и ранее принятых звука/видеорежимов.

### Пользовательское наблюдение после B29 — отдельный video/INT sync узел

- Пользователь подтвердил: Rage и виртуальный FDD теперь работают с первого Enter; дисковый сценарий закрыт как практическая проблема.
- На визуальном эталоне B18 бордюрный эффект и мультиколор синхронны. В B24/B27 и последней версии эффект выполняется, но имеет заметный сдвиг фазы/тактов на полном экране.
- Зафиксирована рабочая гипотеза для следующего этапа: при аудиоизменениях B24–B26 могла проявиться или сохраниться ошибка общей кадровой/`INT`-синхронизации, `FrameReady` или raster epoch. Это гипотеза, не установленная причина; audio-код без отдельного подтверждения не менять.
- Следующий этап: сопоставить B18 с B24/B27 по `INT`, началу кадра, border timing и multicolor fetch, затем сделать минимальную video-only правку. Runtime-приёмка без нового пользовательского теста не заявляется.

### Постоянное правило для следующего этапа — официальное соответствие ZX Evolution

- Все дальнейшие исправления ZX-Evo/BaseConf должны в первую очередь соответствовать официальной документации ZX Evolution и исходному BaseConf RTL: `zclock`, `z80`, `video_sync`, `video_fetch`, `dram/arbiter`, `zports` и официальным описаниям видеорежимов.
- Визуальные снимки B18 используются только как runtime-контрольный эталон правильного результата. Нельзя подгонять тайминги по картинке в обход документированной аппаратной фазы.
- Для следующего video/INT этапа обязательны: ссылка на конкретный RTL/документ, описание аппаратного сигнала или состояния, минимальная правка и отдельная проверка регрессий Rage/FDD, звука и уже принятых видеорежимов.
- Это правило сохраняется при переносе проекта в новый чат или на другой аккаунт; перед началом работы нужно перечитать этот раздел и последние записи B27–B29.

### Пользовательская runtime-приёмка B29

- Пользователь подтвердил, что Rage из SD/EVO Service/Ramdisk после B29 запускается одним коротким Enter и работает. Практическая приёмка исправления virtual FDD завершена.
- Отдельно обнаруженная небольшая ошибка фазы border/multicolor не относится к FDD и вынесена в B30.

## ZXMAK2-v13-ZXEVO-BC-VIDEOPHASE-B30-20260919-004505 — фаза 256×192 относительно INT

- Сравнение B18 с B20–B29 локализовало регрессию: B20 одновременно заменил эффективную горизонтальную фазу трёх 256×192 renderer-путей с 65 на 69 tact относительно normal INT. Audio B24–B26 эту фазу не менял и причиной не является.
- Официальный `video_sync_h.v` r1364 сохранён источником аппаратной геометрии: raw `HPIX_BEG_PENT=140`, `HINT_BEG=2`; raster periods, vertical windows, INT epoch, DRAM fetch и 4T border logic B20 не откатывались.
- Исправлена граница между двумя системами координат: raw 7 MHz `hpix` остаётся 140, а для 256×192 action table `SpectrumRenderer` введён явный adapter `StandardRendererPhaseBaseTacts=4`. Поэтому параметр first-paper равен 66, а с normal `c_ulaIntBegin=1` эффективная фаза снова равна 65, как в принятой B18.
- Adapter применяется только к общему стандартному пути ZX 256×192 / Pentagon HWM / Pentagon 16c. Широкие ATM 320/640/text, frame/INT periods, audio, FDD, ROM, palette и mode controller не менялись.
- Release solution собрана без ошибок; сохранены два прежних ruleset warnings. Новый compiled `VideoTimingProbe-B30` прошёл 12 748 проверок. Регрессии: SCL Rage 113, B21 controller 321, palette 1807, TRD 655922, audio baseline 19 и DirectSound 16 — PASS.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-VIDEOPHASE-B30-20260919-004505`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOPHASE-B30-20260919-004715\release`.
- Runtime-приёмка B30 не заявляется. Пользователю нужно повторить тот же border/multicolor фрагмент, сравнить с B18 и проверить Rage одним Enter, звук и переключение уже принятых видеорежимов.

### Пользовательская runtime-приёмка B30 — принято

- Пользователь проверил B30 на том же полноэкранном border/multicolor-фрагменте и подтвердил результат: `B30 Good`.
- Приложенный снимок показывает согласованный полноэкранный цветовой эффект без наблюдавшегося в B24/B27/B29 фазового рассогласования.
- B30 принят по целевому runtime-критерию border/multicolor sync. Это не расширяет приёмку на ещё не проверенные аппаратные edge cases, альтернативные растры или mid-frame переключения.

## ZXMAK2-v13-ZXEVO-BC-NEDOOS-B31-20260919-015150 — запрет virtual-FDD trap в режиме PEN2

- Пользователь локализовал новую регрессию по сборкам: NedoOS через `sd_boot.$C` загружается в B28-R3, но после B29 и в B30 после запуска остаётся чёрный экран. При этом `Bad apple.$C` с отдельным файлом данных запускается, Rage после B29 работает одним коротким Enter, а border/multicolor B30 принят. Поэтому SD-чтение, ERS `$C`-загрузчик и видео в целом не откатываются.
- Повторная сверка B28-R3 → B29 подтвердила, что регрессию мог затронуть только virtual-FDD узел: в B29 условие входа было расширено до `DOSEN && любое ROM в #0000`, но из официального BaseConf `base_trdemu` r1364 не был перенесён третий gate.
- Официальный `fpga/base_trdemu/trunk/z80/zdos.v` задаёт `trdemu_on = vg_rdwr_fclk && fdd_mask[vg_a] && dos && romnram && !atm_pen2`. `atm_pager.v` затем отображает RAM page `#FE` и применяет `trdemu_wr_disable`. Локальные копии исходного RTL сохранены в backup B31 под `reference/fpga-r1364/base_trdemu`.
- В `MemoryPentEvo.TryEnterFddIoRam()` добавлен только отсутствовавший запрет при `PEN2=true`. Теперь palette-write на shadow `#FF` не может ошибочно подменить окно `#0000` страницей `#FE`; настоящий masked FDD access при `DOSEN + ROM + !PEN2` по-прежнему входит в handler.
- B29-механизм Rage не откатывался: `FddPentEvo.cs` побайтно совпадает с принятой B29 (`C89F0BDC...F0DE`), отображение page `#FE` и `trdemu_wr_disable` до первого M1 сохранены. B30-видео не менялось: `UlaPentEvo.cs` побайтно совпадает с принятой B30 (`7F16FE20...1537`). No-index diff `MemoryPentEvo.cs` относительно B31-before содержит только комментарий и `PEN2` gate.
- Release solution собрана без ошибок; остались два прежних предупреждения об отсутствующих ruleset-файлах. Новый compiled `FddTrapProbe-B31` прошёл 14 проверок: положительный `DOS+ROM+!PEN2`, три отрицательных gate, page `#FE`, защита записи и её снятие на первом M1.
- Регрессии после сборки: `VideoTimingProbe-B30` 12748, SCL Rage 113, B21 controller 321, palette 1807, TRD 655922 на закреплённом B23-образе, audio baseline 19 и DirectSound 16 — PASS.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-NEDOOS-B31-20260919-014522`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-NEDOOS-B31-20260919-015150\release`.
- Runtime-приёмка B31 не заявляется. Пользователю нужно проверить: (1) `sd_boot.$C` и появление NedoOS; (2) Rage из SD/EVO Service/Ramdisk одним коротким Enter; (3) принятый B30 border/multicolor-фрагмент; дополнительно — запуск Bad Apple `$C`.

### Пользовательская runtime-приёмка B31 — принято

- Пользователь подтвердил загрузку NedoOS через `sd_boot.$C`.
- Rage из SD/EVO Service/Ramdisk продолжает работать после исправления; регрессии принятого B29 virtual-FDD не обнаружено.
- Принятый в B30 border/multicolor-эффект сохранился без регрессии.
- Bad Apple успешно запускается тремя путями: из ERS File browser, из меню и из NedoOS.
- Других сбоев во время проверки не выявлено. B31 принята по целевым runtime-критериям NedoOS, Rage, border/multicolor и Bad Apple; это не считается исчерпывающей сертификацией всех программ и аппаратных edge cases.

## Backlog после встроенного BaseConf — ZX-BUS и расширительные платы

- Пользователь уточнил, что в модели ZX-Evolution BaseConf нужно представить два стандартных разъёма ZX-BUS, чтобы выбирать конфигурацию машины и устанавливать в слоты дополнительные звуковые или иные платы.
- В текущей конфигурации `ZX-Evo BSconf` таких слотов и отдельного ZX-BUS слоя нет: встроенные `AYCHRV`, Beeper и Covox подключены непосредственно в VM-профиле. Поиск исходников подтверждает отсутствие готовой реализации ZXBUS.
- Это должны быть два слота на общей системной шине, а не две независимые Z80-шины. Перед кодом требуется сверить официальный сигнал/портовый контракт: адресный decode, IRQ/NMI, WAIT, reset, конфликт портов и порядок подключения нескольких плат.
- Это направление добавлено в backlog и не заменяет утверждённый текущий план B32+; сначала продолжаем работу по нему. Обязательная последовательность: (1) полная ревизия соответствия BaseConf официальной документации — все порты, карты памяти, прерывания, тайминги, WAIT/RESET и все документированные видеорежимы; (2) исправление и проверка только встроенной периферии, которая прямо описана в документации; (3) после закрытия этой ревизии — аудит официального контракта ZX-BUS и двух слотов; (4) виртуальная инфраструктура слотов и только затем подключаемые документированные платы, включая звуковые устройства. Самовольное расширение перечня периферии не допускается.
- Встроенный AY/CHRV BaseConf, принятые B25–B27 звуковой тракт, B29/B31 virtual-FDD и B30 video не изменять при создании инфраструктуры слотов. Каждая плата получает отдельную compiled-проверку и отдельный runtime-тест.

## B32 — полная статическая ревизия BaseConf r1364

- Текущие production-файлы и общий release сверены с принятым B31: `MemoryPentEvo.cs`, `FddPentEvo.cs` и `UlaPentEvo.cs` совпадают побайтно. Локальный пользовательский `src/_binrelease/ZXMAK2.vmz` намеренно не заменялся каноническим профилем.
- Нормативная база закреплена точным архивом `pentevo-fpga.r1364.tar.gz`, SHA-256 `7A509FBCEF3AF85380EC475AD622A0682E714AF54625851FCB9AA678DA0BBB82`. Аудит ведётся в два слоя: `baseconf/trunk` и официальный `base_trdemu/trunk`, необходимый принятому ERS/Rage/NedoOS virtual-FDD пути. Текущий GitHub HEAD с r1364 не смешивается.
- Полный результат записан в `BASECONF_AUDIT_B32.md`: карта портов, memory/paging, INT/NMI, WAIT/reset/timing, WD1793 и все семь реально присутствующих в r1364 видеорежимов. Режимы `256c` и `16+16c` из общего описания не входят в BaseConf r1364 и добавляться не должны.
- Подтверждён первый изолированный дефект: `IdePentEvo` декодирует `(addr & #1E)==#10`, из-за чего теряет официальные Nemo IDE aliases `x08` (кроме отдельного `#C8`) и принимает лишние нечётные aliases. Это назначено единственной целью B33.
- Следующие подтверждённые неполные области: системный `#BF` (D1/D3/D4/D5), чтение/запись конфигурации `#BD` индексов `0D..11`, аппаратный NMI/брейкпоинт, раннее снятие INT по acknowledge и пауза INT во время WAIT, AVR/COM WAIT, Kempston joystick, tape/mux, ULAplus/4:4:4 palette. Они разделены на B34–B39 и не будут объединяться вслепую.
- B30/B31 не откатываются: семь renderer routes присутствуют, принятые border/multicolor, Rage, NedoOS и Bad Apple являются обязательным regression floor.
- На неизменённых B31 binaries повторно прошли compiled contracts: FDD trap 14, B30 video timing 12748 и BaseConf video controller 321 — PASS.
- В B32 production-код не изменялся, новая runtime-сборка не выпускалась и runtime-приёмка не заявляется. После встроенного соответствия остаётся отдельный аудит двух ZX-BUS слотов; периферия добавляется только затем и только по документации.
- Audit checkpoint: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-AUDIT-B32-20260919-083728`.

## ZXMAK2-v13-ZXEVO-BC-IDEPORTS-B33-20260919-084445 — точный Nemo IDE decode

- Исправлен только `IdePentEvo.BusInit()`: прежний mask `(low & #1E)==#10` заменён точной формулой BaseConf r1364 `low[2:0]=000 && low[3]!=low[4]`, то есть семьями `x10` и `x08`, плюс отдельный high-byte port `#11`.
- `#C8` по RTL входит в `x08`, но выбирает alternate-status/control block; его точный handler регистрируется раньше общей семьи и поглощает обращение через `handled`.
- ATA register select `A7..A5`, 16-bit data sequencing и сам ATA core не менялись. FDD/Rage/NedoOS, memory, SD, video, audio, keyboard и ROM не затрагивались.
- Release solution собрана без ошибок с двумя прежними ruleset warnings. Проверки: B33 IDE 786, B31 FDD 14, B30 timing 12748, B21 controller 321, B23 palette 1807, B27 audio 19 и DirectSound 16 — PASS.
- Упакованный `ZXMAK2.Hardware.dll` побайтно совпадает с новым Release; профиль `ZXMAK2.vmz` канонический, сохранённых `.cmos/.nvram/.vmide/.log` в runtime нет.
- Backup: `L:\Work_two\ZX\ZXMAK2-Fork\backup\ZXMAK2-v13-ZXEVO-BC-IDEPORTS-B33-20260919-084445`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-IDEPORTS-B33-20260919-084445\release`.
- Runtime-приёмка не заявляется. Основной тест — обычная загрузка BaseConf с IDE/HDD-образа и операция чтения/записи; затем короткий smoke NedoOS, Rage и B30 border/multicolor.

## Зафиксированная UI-доработка носителей — writable по умолчанию

- Пользователь просит, чтобы дисковые образы, открываемые через обычное `File -> Open`, по умолчанию подключались без защиты от записи. Причина практическая: сохранения игр и другие записи сейчас незаметно блокируются, особенно для нового пользователя эмулятора.
- Причина найдена точно: `MainViewModel.CommandFileOpen_OnExecute()` явно задаёт `ReadOnlyChecked = true`; отдельный browse-диалог четырёх FDD в `CtlSettingsBetaDisk.btnBrowse_Click()` делает то же самое.
- При реализации оба значения по умолчанию нужно заменить на writable (`false`), сохранив видимый флажок ручной защиты. Если файл или контейнер фактически недоступен для записи, загрузчик обязан оставить эффективную защиту и не повреждать данные.
- Та же политика относится к будущему UI выбора HDD-образа: новый образ подключается writable по умолчанию, имеется ручной `Read only`, путь и состояние сохраняются автоматически во внутреннем `.vmide`.
- Это пока записанное требование, а не изменение B33: production-код и runtime-пакет B33 не менялись.

### Контрольный HDD-образ для IDE UI и B33 runtime

- Пользователь предоставил пару `L:\Work_two\ZX\ZX_IMG\ATM_HDD.hdd` + `ATM_HDD.inf` для ATM/ZX-Evolution IDE-проверки.
- `.hdd` является raw sector image размером `206438400` байт, строго `403200` секторов по 512 байт.
- Сопутствующий текстовый `.inf` задаёт точную геометрию: `400` cylinders, `16` heads, `63` sectors, `403200` LBA; произведение CHS и длина файла совпадают.
- Будущий выбор HDD должен сначала искать одноимённый `.inf` и импортировать его CHS/LBA. При отсутствии `.inf` можно вычислить точный LBA по длине файла и применить отдельно документированную совместимую CHS-геометрию. Разметку/разделы внутри `.hdd` не изменять.
- Этот образ использовать для runtime-проверки B33: HDD boot, чтение и тестовая запись при выключенном по умолчанию `Read only`.

## ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-20260919-093000 — UI HDD и writable media defaults

- В Machine Settings для `IDE PentEvo` добавлен отдельный экран: `HDD connected`, путь, выбор `.hdd`, `Eject` и ручной `Read only`. Отключение образа не удаляет сам контроллер; применение выполняется штатным `Apply` с переподключением машины.
- Ручное редактирование `.vmide` больше не требуется. Путь, CHS, LBA и read-only сохраняются в конфигурации машины; старый `.vmide` по-прежнему читается для совместимости, после чего поддерживается автоматически.
- Exact LBA определяется только как длина raw-файла / 512. Невыровненный или пустой образ отклоняется. Одноимённый `.inf` автоматически импортирует только полную согласованную CHS/LBA; иначе выбирается детерминированная совместимая CHS при неизменном точном LBA. Содержимое, разделы и размер образа не меняются.
- На пользовательской паре `ATM_HDD.hdd/.inf` автоматически подтверждены `206438400` байт, `403200` LBA и `400/16/63` CHS. Новый HDD writable по умолчанию; host read-only/невозможность безопасного write-open переводит его в эффективный read-only.
- `File -> Open` теперь открывает флажок `Read only` снятым; browse для FDD A-D — `Write Protect` снятым. Явно сохранённая защита существующего образа сохраняется, ZIP остаётся принудительно защищённым.
- B33 exact port decode не менялся. B34 системные порты и остальной аппаратный backlog не начинались. FDD/Rage/NedoOS, memory, video и audio production-paths не правились, кроме двух UI default флажков носителей.
- Release solution PASS с двумя прежними ruleset warnings. Контракт обнаружения нового Machine Settings control — PASS. Проверки: новый IDE media 10, B33 ports 786, B31 FDD 14, B30 timing 12748, B21 controller 321, B23 palette 1807, Rage SCL 113, audio 19 и DirectSound 16 — PASS.
- Checkpoints: `backup/ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-before-20260919-090000`, `...-core-20260919-091500`, `...-ui-20260919-093000`, финальный с runtime `...-20260919-093500`. Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-IDEMEDIA-B33-R1-20260919-093000\release`. Подробности: `docs/history/results/B33_R1_RESULT.md`.
- Runtime-приёмка не заявляется. Обязательны пользовательские NEM boot/write/read-after-restart, Eject/no-HDD, writable FDD обоими путями и smoke NedoOS/Rage/B30 border-multicolor/audio.


## Промежуточная стабильная точка B33-R1 — 2026-09-19 13:09:06

- По просьбе пользователя текущее состояние зафиксировано перед следующим функциональным этапом. Исходный код не изменялся, новая сборка не выполнялась.
- Подтверждено пользователем в runtime: ранее принятые NedoOS, Rage, border/multicolor B30 и Bad Apple продолжают работать; HDD-образ подключается, определяется, а его FAT-содержимое видно в ERS File Browser; NedoOS запускается вручную с файла на HDD.
- Автоматическая загрузка NedoOS через `B.HDD boot` не считается принятой и отложена до завершения работы с носителями.
- Открытый дефект следующего этапа B34: первая установка SD/HDD проходит нормально, но повторная замена носителя в том же процессе эмулятора может приводить к зависанию. Требуется атомарная замена с корректным закрытием старого образа, сбросом состояния контроллера и внутренним cold power-cycle; явный Eject перед выбором нового файла не должен быть обязателен.
- Зафиксирован полный восстанавливаемый снимок рабочего дерева без `.git`, `backup`, `tmp`, `bin`, `obj` и `.vs`: `backup/ZXMAK2-v13-ZXEVO-BC-STABLE-B33-R1-20260919-130906/snapshot`.
- Эта запись не расширяет runtime-приёмку: отмечено только то, что уже проверил пользователь.

## GitHub prerelease — ZX-Evo BaseConf Alpha 0.1

- Из стабильного commit `14c249397122ba3978a5de3625ed3e7d12750de2` собрана чистая Release-версия в отдельном detached worktree. Теги исходной точки: `zxevo-b33-r1-stable` и `v0.1-alpha`.
- Visual Studio 2022 MSBuild 17.14.51 завершил полную последовательную сборку с нулём ошибок. Остались только два прежних предупреждения об отсутствующих `AllRules.ruleset` и `MinimumRecommendedRules.ruleset`.
- Portable-папка содержит 75 файлов. В неё установлен точный принятый профиль B33-R1 `ZXMAK2.vmz` — 924 байта, SHA-256 `43832C2F34A2F2B7AE0F12E4E216D4482A500536D47B8D4536EF230E8F923C5D`. Пользовательские `.cmos/.nvram/.vmide`, журналы и образы носителей исключены.
- ZIP полностью распакован во временную папку и сверен побайтово: 75/75 файлов, расхождений нет. Размер ZIP `4724772` байта; SHA-256 `D9A95F7D5A2143E84068C790F8C8273D9B5D02B0FC51ABF6D703FEA85E929DC0`.
- На распакованном пакете прошли проверки: IDE media 10, Nemo IDE ports 786, PentEvo FDD trap 14, video timing 12748, audio path 19 и DirectSound buffer 16.
- GitHub prerelease: `https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.1-alpha`. Прямая загрузка: `https://github.com/Moro44444444/ZXMAK2-Fork/releases/download/v0.1-alpha/ZXMAK2-ZXEvo-BaseConf-Alpha-0.1.zip`.
- Локальные копии: `K:\Download\ZXMAK2-ZXEvo-BaseConf-Alpha-0.1` и `K:\Download\ZXMAK2-ZXEvo-BaseConf-Alpha-0.1.zip`.
- В README релиза честно отмечены открытые ограничения: повторная замена SD/HDD в одном процессе может зависнуть до B34; автоматический `B.HDD boot` NedoOS не принят. Создание prerelease не заявляет новую пользовательскую runtime-приёмку.

### Уточнение пользовательской приёмки HDD

- Пользователь подтвердил работу HDD-контроллера в проверенном объёме: образ подключается, контроллер читает носитель, FAT-содержимое доступно в ERS File Browser, запуск файла с HDD работает.
- Настройка автоматической загрузки конкретной ОС через `B.HDD boot` вынесена на будущее и не является критерием приёмки самого HDD-контроллера.
- Открытым дефектом медиапути остаётся только повторная замена SD/HDD в одном процессе эмулятора; это отдельная задача B34.

## ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-20260919-140643 — безопасная повторная замена SD/HDD

- Причина повторной SD-замены локализована в `MainViewModel.ExecuteMediaChange`: после успешного close/open поток VM возобновлялся до reset, а затем выполнялся только warm reset. Теперь VM остаётся остановленной до завершения `DoPowerCycle`; после отмены или ошибки прежнее running/paused состояние сохраняется.
- HDD Machine Settings ранее штатно закрывал и заново открывал ATA image через `BusManager.LoadConfigXml`, но продолжал выполнение без cold-cycle, а пять 8/16-bit защёлок Nemo IDE могли пережить замену. Теперь активный и pending HDD сравниваются по пути, read-only и CHS/LBA; insert, A→B, Eject и смена режима завершаются cold power-cycle до единственного resume.
- `IdePentEvo.BusReset` теперь обнуляет `m_ide_write`, обе write-phase защёлки, read-phase и retained high byte, затем вызывает hard reset ATA master/slave. Декодирование официальных портов B33, ATA data path и геометрия B33-R1 не менялись.
- `ZsdPentEvo` не изменялся: в нём уже были stop-safe close-old/open-new, reset SPI state и rollback к прежнему образу при ошибке. B34 исправляет окружающий lifecycle VM.
- Полная Release solution собрана: 0 ошибок, два прежних missing-ruleset warnings. Новый `MediaSwapProbe-B34` — 33 PASS. Регрессии: IDE media 10, IDE ports 786, FDD 14, B30 timing 12748, palette 1807, TRD 655922, Rage SCL 113, audio 19, DirectSound 16 — PASS.
- Неизменность подтверждена для PentEvo memory/ULA/SD implementation, AY и Engine BusManager. Checkpoints: до правки `backup/ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-before-20260919-135901`; после правки с source snapshot и runtime `backup/ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-after-20260919-140643`.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-MEDIASWAP-B34-20260919-140643\release`; ZIP рядом, SHA-256 `FB96133BCDB4451B5DA2912DD7D1A2BF62F4D5738AF15CF7CD726A5BC9E3F245`. Распаковка сверена 86/86, state/media-файлов нет, профиль канонический 924 байта.
- Runtime-приёмка не заявляется. Пользователь должен проверить SD A→B→A и HDD A→B→A без ручного Eject в одном процессе, отдельный Eject→insert и smoke NedoOS/Rage/B30/Bad Apple/audio. Автоматический `B.HDD boot` NedoOS по-прежнему отложен.

## Постоянное требование журнала — 2026-09-19

- Пользователь потребовал вести постоянный журнал в самом репозитории, чтобы его можно было всегда найти и перенести на другой аккаунт или в новый чат. В журнале должны фиксироваться успешные и неудачные попытки, сделанные и несделанные функции, проверки, backup и открытые ограничения.
- Канонический файл такого журнала — `PROJECT_JOURNAL.md`; краткая хронология изменений — `FORK_CHANGELOG.md`. Оба файла обновляются после каждого фактического изменения или сборки и коммитятся вместе с исходниками. Runtime-приёмка записывается только по явному пользовательскому тесту.

## Backlog — постоянство SD-образа и управление картой — 2026-09-19

- Текущее состояние: SD можно открыть с панели инструментов, но отдельного `Eject SD card` в интерфейсе нет; при выборе нового образа старый уже заменяется безопасным close-old/open-new путём B34.
- Требование: добавить к кнопке SD меню `New Card…` и `Eject Card`. `Eject Card` должен закрывать образ, сбрасывать SD-контроллер и очищать сохранённое состояние присутствия карты.
- Требование: если карта подключена, её путь и состояние `present` сохраняются в профиле машины и автоматически восстанавливаются после перезапуска ZXMAK2. Если пользователь явно сделал Eject, после перезапуска слот остаётся пустым.
- Ошибка отсутствующего или повреждённого образа не должна зависать или блокировать запуск: эмулятор стартует без карты и пишет понятное предупреждение в журнал.
- Это только зафиксированный backlog следующего этапа. Production-код, B34, runtime-пакет и принятая периферия этим пунктом не изменялись; runtime-приёмка не заявляется.

## Backlog — CD/DVD через IDE/ATAPI — 2026-09-19

- Пользователь потребовал добавить возможность подключать и читать CD/DVD как физический привод, так и виртуальный образ диска. Носитель должен быть доступен как IDE master/slave с явным выбором устройства в настройках и с операциями подключения/извлечения.
- Минимальный технический объём: полноценный ATAPI packet path вместо текущей заготовки, идентификация CD-ROM, `TEST UNIT READY`, `REQUEST SENSE`, `INQUIRY`, `READ CAPACITY`, чтение секторов и необходимые команды для загрузочных программ/демо. Форматы образов и физический привод должны быть определены по проверяемым источникам, без самовольного расширения протокола.
- В UI нужен отдельный тип устройства CD/DVD, путь к образу или выбор физического привода, состояние подключено/извлечено и принудительная защита от записи для CD.
- Нормативное ограничение: официальная спецификация ZX-Evolution фиксирует IDE master/slave, но не описывает ATAPI/CD подробно; документация UnrealSpeccy описывает CD-ROM для IDE KAY/Scorpion/ATM-2. Поэтому поддержку для ZX-Evo BaseConf сначала сверяем с реальным ROM/OS и аппаратным контрактом, а затем реализуем как изолированное расширение без изменения принятых BaseConf/FDD/video/audio путей.
- Это только backlog. Production-код, B34, runtime-пакет и пользовательская runtime-приёмка этим пунктом не изменялись.

## CMOS/NVRAM persistence — существующая функция и контроль — 2026-09-19

- Пользователь уточнил, что файл `.cmos` уже существует и его сохранение состояния BIOS/ERS работает. Новую систему хранения добавлять не требуется; это принятая функция, которую нельзя ломать.
- Важно не смешивать уровни хранения: `.cmos` — постоянная память эмулируемой CMOS/RTC/NVRAM, которую читает и записывает BIOS/ERS; `.vmz` — профиль конфигурации ZXMAK2; `.vmide` — совместимый sidecar описания IDE-образа. ROM-файл сам по себе не изменяется.
- В текущем ZX-Evo `CmosPentEvo` уже загружает и сохраняет 256-байтный `.cmos`, включая поддержанный AVR video slot `FE`. Это нужно сохранить и отдельно проверить по официальной карте CMOS для ZX-Evolution.
- Для ATM Turbo 2/XT BIOS и других машин с CMOS при будущей ревизии проверяется только сохранность существующей функции: уникальный sidecar на конфигурацию, сохранение после нормального завершения, безопасное поведение при отсутствии/повреждении файла и отсутствие переноса настроек между разными машинами.
- Не считать автоматически, что размер памяти, частота CPU и список устройств хранятся в CMOS: часть этих параметров может принадлежать профилю ZXMAK2 или ROM/ERS. Перед изменением требуется разделить документированные CMOS-поля и host-конфигурацию.
- Это контрольное требование, а не новая runtime-приёмка. Production-код, B34 и CD/SD задачи этим пунктом не изменялись.

## Backlog — горячие клавиши Warm Reset и CMOS reset — 2026-09-19

- Пользователь сообщил, что на его клавиатуре нет физической клавиши Insert. Поэтому существующую комбинацию Warm Reset `Alt+Ctrl+Insert` нужно переназначить на `Alt+Ctrl+End`.
- Пользователь пожелал добавить `F12` как удобную дополнительную клавишу Warm Reset.
- `Ctrl+F12` назначается на сброс CMOS. Это должен быть host-level обработчик главного окна, чтобы комбинация не попадала в эмулируемую клавиатуру; F12 в CPU Debugger должна сохранить локальное назначение Stack/Breakpoints.
- Сброс CMOS нельзя реализовать простым удалением файла во время работы: содержимое уже загружено в память эмулируемого устройства. Нужен явный reset API для CMOS-устройств: создать резервную копию `.cmos`, восстановить документированные значения по умолчанию, сохранить файл и выполнить reset, чтобы BIOS заново прочитал настройки.
- Для устройств без поддержанного CMOS reset команда должна быть недоступна или явно сообщать об отсутствии поддержки. Перед очисткой нужна подтверждающая операция, так как настройки BIOS/ERS будут сброшены.
- Это пока backlog. Production-код, B34, runtime-пакет и runtime-приёмка не изменялись.

## Правило релизных заметок What's New — 2026-09-19

- После каждого фактического изменения следующей сборки обновляются `What's New`/release notes и краткая запись в `FORK_CHANGELOG.md`.
- В release notes попадают только реально реализованные изменения с указанием сборки и результата проверок. Незапущенный backlog (например, CD/ATAPI, SD Eject/persistence, F12/CMOS reset) не описывается как готовая функция.
- Для публичного релиза сохраняется двуязычная подача: английский и русский текст, без заявления runtime-приёмки, если пользователь её ещё не проводил.

## Правило состава публичного GitHub portable-релиза — 2026-09-19

- В публичный ZIP не включаются пользовательские sidecar-файлы `.cmos` и `.vmide`: они могут нести персональное состояние BIOS/ERS, пути к HDD и прочую локальную конфигурацию. После первого запуска ZXMAK2 создаёт собственные файлы пользователя.
- `.vmz` допускается только как специально подготовленный чистый профиль, необходимый для старта нужной машины (например, ZX-Evo BaseConf): без путей к SD/HDD/FDD-образам, без подключённых носителей, логов и персонального состояния.
- Перед публикацией portable-папка проверяется отдельно после чистой распаковки: нет `.cmos`, `.vmide`, media images, логов, временных файлов или абсолютных пользовательских путей; при этом BaseConf-профиль и требуемые ROM/PAK остаются доступны.
- Минимизация выполняется только над копией, предназначенной для GitHub ZIP, и только после успешной сборки/проверки полного runtime-output. Из public-копии допускается удалить `.pdb` (отладочные символы), XML-документацию сторонних библиотек и внутренние build/result/checksum-отчёты, если они не объявлены частью релиза. Рабочая локальная сборка, diagnostics-пакет и исходный build-output не очищаются.
- Нельзя удалять исполняемый файл, его `.config`, требуемые `.dll`, runtime-конфигурации, ROM/PAK, нужные ROM-файлы, чистый `.vmz`, пользовательскую документацию и лицензию. После очистки обязательно выполнить запуск из чисто распакованного ZIP и сверить состав с заранее определённым manifest/списком обязательных файлов.
- История последней папки стандартного Windows Open dialog хранится вне portable-папки в пользовательском профиле Windows и не должна ошибочно считаться частью релиза.

## Backlog — Rage из большого SDHC-образа — 2026-09-19

- Пользователь обнаружил новый отдельный сценарий: один и тот же `RAGE.SCL` из 4-ГБ `cf4gbAAA.ima` запускается, но затем детерминированно зависает примерно в первой трети/середине демки и на штатной скорости 3,5 МГц, и при максимальном ускорении. Из 500-МБ raw-образа `sd_nedo.vhd` демка полностью отрабатывает на обеих скоростях.
- Два извлечённых пользовательских файла `L:\Work_two\ZX\RAGE_AAA.SCL` и `L:\Work_two\ZX\RAGE_vhd.SCL` проверены побайтно: размер обоих `41839`, различий `0`, SHA-256 обоих `D530FF773125F4C4E8AEC786FBB8B8021BD67589C039A990B530EAC88D45306F`. Исходный SCL как причина исключён.
- `RAGE.SCL` содержит семь TR-DOS-файлов (`RAGE`, `file_id`, `rage1`–`rage4`, `boot`), поэтому успешный старт не доказывает корректность всех последующих подгрузок из созданного RAM-диска.
- Существенное различие носителей: `cf4gbAAA.ima` больше 2 ГиБ и в `SdCard` включает SDHC/CSD v2 с секторной адресацией; `sd_nedo.vhd` имеет 500 МБ, фактически является raw FAT32 без VHD-footer и работает как SDSC/CSD v1 с байтовой адресацией. Образ 4 ГБ находится ниже текущего внутреннего лимита 8 ГиБ; размер сам по себе не признан ошибочным.
- Следующая диагностическая задача: после импорта SCL сравнить RAM-диск/его секторные контрольные суммы для SDHC и SDSC. Если данные различаются — локализовать команду/сектор в SDHC-чтении или копировании; если совпадают — сравнить состояние RAM, страниц, регистров, INT и FDD перед запуском Rage. Затем сделать минимальное общее исправление без специальных условий под Rage.
- Пользователь отдельно проверит поведение того же 4-ГБ образа в UnrealSpeccy. Результат должен быть добавлен сюда как контроль реализации. Production-код и образы не изменялись; сборка и runtime-приёмка не заявляются.

## GitHub prerelease — ZX-Evo BaseConf Alpha 0.2 — 2026-09-19

- После явного подтверждения пользователя опубликован GitHub prerelease `ZX-Evo BaseConf Alpha 0.2`: `https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.2-alpha`. Тег `v0.2-alpha` указывает на `250191d05b092425836dfcf8eef7d4fb388b257a`.
- Архив `ZXMAK2-ZXEvo-BaseConf-Alpha-0.2.zip` содержит 45 файлов, размер `3386668` байта, SHA-256 `21A1E905FBF5DF5E4B980BBA3DCE9E8E7E27D777723C187E36146031AC26F6DD`.
- Public-копия построена из B34 отдельно от полного runtime. Исключены `.cmos`, `.vmide`, образы носителей, логи, PDB, внутренние reports и абсолютные локальные пути; `log4net.config` исправлен на относительный путь. До запуска список ZIP точно совпал со списком publish-copy. Чистая распаковка успешно запустила `ZXMAK2.exe`; созданный после запуска `.vmide` — штатное пользовательское состояние и в ZIP не входит.
- README обновлён на двуязычные ссылки Alpha 0.2 и отражает B34 повторную замену носителей. Публикация не расширяет runtime-приёмку: это отдельный проверенный пользователем факт для B34, а не новая проверка Alpha 0.2.

## Пользовательская runtime-приёмка B34 — принято — 2026-09-19

- Пользователь проверил повторную смену SD и HDD в разных направлениях, включая переход карта→HDD и HDD→карта. Носители после cold power-cycle определяются и читаются; зависание при второй замене не воспроизводится.
- ERS File Browser корректно предлагает Master HDD или SD Card, когда подключены оба носителя. Клавиша `D` и выбор обоих источников работают.
- Приёмка относится к исправлению lifecycle смены носителей B34. Она не означает приёмку автоматической загрузки NedoOS через `B.HDD boot`, будущего SD Eject/persistence или CD/ATAPI.

## ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-20260919-172210 — конфигурационные порты `#BF/#BD/#BE`

- Этап взят строго из утверждённого `BASECONF_AUDIT_B32.md`; из-за уже занятого номера B34 он выпущен как B35. Следующий INT/NMI/breakpoint этап сюда не смешивался.
- `#BF` теперь возвращает весь официальный шестибитный latch: D0 shadow, D1 ROM write, D2 font write, D3 set NMI, D4 breakpoint enable, D5 palette 4:4:4; D7:D6 равны нулю. D3/D4/D5 в B35 только сохраняются: NMI/breakpoint state machine и 4:4:4 renderer будут отдельными этапами.
- D1 подключён к карте записи ROM согласно `romwe_n`: запись разрешается только для реально отображённой ROM и блокируется существующим `wrdisable` соответствующего окна.
- В `#BD` завершены индексы `0D` palette readback, `0E` font output, `0F` border, `10/11` breakpoint low/high и сохранён `12` write-disable. `10/11` получили запись адреса breakpoint; само срабатывание breakpoint не включено.
- `#13BD` намеренно оставлен точному `FddPentEvo`, поэтому принятые B29/B31 Rage/NedoOS virtual-FDD path не перехватываются. `#BE` остаётся выходом из page `#FE` handler и декодируется также в неактивном состоянии как будущий общий clear; совместимый read alias `#xxBE` сохранён для старых ERS.
- Font readback реализован как сохранённый последний разрешённый выход font RAM активного ATM/BaseConf text renderer; состояние и фаза renderer при чтении не изменяются.
- Перед правкой создан полный checkpoint `backup/ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-before-20260919-170627`. Первая Release-сборка прошла без ошибок; два прежних missing-ruleset warning сохранены.
- Новый compiled `ConfigPortProbe-B35` прошёл 539 проверок. Регрессии: B34 media 33, IDE media 10, IDE ports 786, FDD 14, Rage SCL 113, B30 timing 12748, video controller 321, palette 1807, TRD 655922, audio 19, DirectSound 16 — PASS.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-20260919-172210\release`; after-checkpoint: `backup/ZXMAK2-v13-ZXEVO-BC-CFGPORTS-B35-after-20260919-172210`. Подробности: `docs/history/results/B35_RESULT.md`.
- Portable ZIP содержит 89 файлов и побайтово проверен 89/89; SHA-256 `2D9D45D7570FD9C30E77E7E6181C15A9B5C06847056CADB3A29E699A3D07E2D8`. Профиль канонический 924 байта, пользовательских `.cmos/.nvram/.vmide/.log` нет.
- Runtime-приёмка B35 не заявляется. Пользователю нужен smoke NedoOS, Rage одним коротким Enter, принятый B30 border/multicolor, Bad Apple, звук и повторная смена SD/HDD.

### Пользовательская runtime-приёмка B35 — принято

- Пользователь выполнил общий regression smoke и не обнаружил критических отклонений: multicolor и бордюрный эффект сохранены, дисковые носители меняются, IDE-диск определяется, файлы на нём видны и читаются без проблем.
- B35 принят в этом проверенном объёме. Отдельного прикладного теста новых служебных latch `#BF/#BD/#BE` не было; их compiled-контракт закрыт 539 автоматическими проверками.

## ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-20260919-181135 — INT/NMI/breakpoint

- Этап выполнен строго как следующий пункт утверждённого `BASECONF_AUDIT_B32.md`; номер сдвинут на B36 из-за ранее добавленного отдельного B34 media-lifecycle. Периферия, ULAplus, 4:4:4 и AVR/COM здесь не смешивались.
- По официальным `zint.v`, `znmi.v`, `zbreak.v`, `atm_pager.v`, `zdos.v` и документации BaseConf реализованы: INT 256 master clocks, досрочное снятие по interrupt acknowledge, интерфейс паузы счётчика только внешним WAIT, кадровый NMI по спаду BF.D3, немедленный breakpoint-NMI по M1, принудительный NOP на `#0066`, последующее отображение RAM `#FF` и задержанный на два M1 выход через `#BE`.
- Порядок M1 зафиксирован отдельно: opcode `#0066` полностью читается со старой картой и заменяется на `00`; `#FF` включается только после завершения этой транзакции. После `#BE` второй opcode ещё читается из `#FF`, и только затем восстанавливается нижележащая карта. При одновременном NMI и virtual FDD страница `#FF` имеет приоритет, а состояние `#FE` сохраняется до выхода из NMI.
- Текущие DRAM/turbo stalls намеренно не растягивают INT. Официальный `spiint_n` формируется только будущими gluclock/COM WAIT-транзакциями; они подключат уже проверенный B36 hook на следующем отдельном этапе.
- Before-checkpoint: `backup/ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-before-20260919-175343/snapshot`. Visual Studio 2022 MSBuild 17.14.51: 23 Release-проекта, 0 ошибок, два прежних missing-ruleset warning.
- Новый `InterruptNmiProbe-B36` прошёл 41 проверку. Регрессии: B35 config 539, B34 media 33, IDE 10/786, FDD/Rage 14/113, видео 12748/321/1807/655922, звук 19/16 — PASS.
- Runtime: `K:\Download\ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-20260919-181135\release`; after-checkpoint: `backup/ZXMAK2-v13-ZXEVO-BC-INTNMI-B36-after-20260919-181135`. ZIP проверен после чистой распаковки: 92/92 файла, SHA-256 `B87C7FDC415E5F77DB35EFCA5D9143B9EEA3326A78E9600EB1323DC4EE332656`. Подробности: `B36_RESULT.md`.

### Пользовательская runtime-приёмка B36 — регрессия принята

- Пользователь провёл общий визуальный smoke-тест и сообщил, что всё ранее работавшее продолжает работать как прежде; видимых регрессий не обнаружено.
- Это подтверждает сохранность принятого поведения B01–B35. Специального прикладного теста новых INT/NMI/breakpoint-переходов не было: они подтверждены compiled-probe на 41 проверку, но не объявляются отдельно наблюдавшимися в runtime.
- Следующий отдельный пункт утверждённого B32-плана выпускается как B37: WAIT-транзакции AVR/gluclock и COM, DOS settling stall и оставшиеся встроенные порты. B36 с ним не смешивается.

## Синхронизация документации и очистка репозитория — 2026-09-19 19:05

- Production-код и runtime B36 не изменялись. Перед работой создан полный
  checkpoint `backup/ZXMAK2-DOCSYNC-before-20260919-185806/snapshot`.
- `PROJECT_JOURNAL.md`, `BASECONF_AUDIT_B32.md`, `FORK_CHANGELOG.md` и
  `README.md` синхронизированы на одной текущей точке B36 и одном порядке
  B37→B40. Старый план до B01 явно помечен как архивный, а ошибочно
  продублированный B07-R1 удалён из журнала.
- Подробные завершённые отчёты B31 и B33–B35 перенесены в
  `docs/history/results/`; текущий `B36_RESULT.md` оставлен в корне. Два старых
  handoff-файла и README прежних тестовых пакетов удалены как полностью
  заменённые постоянным журналом, Git-историей и текущим README.
- Удалены не подключённые к проектам legacy `*.bak`, локальный `src/debug.txt`
  и генерируемый индекс справки `ZXMak2.chw`. Используемый старым
  `release.bat` файл `src/filelist.txt` намеренно сохранён.
- Краткий changelog перестроен в хронологическом порядке 2026-09-12→19 и
  больше не дублирует построчно 200-КБ журнал. Все относительные Markdown-ссылки
  проверены и разрешаются.
- Контрольная Release-сборка `ZXVM.sln` после удаления артефактов завершилась
  без ошибок; сохранились только два прежних предупреждения об отсутствующих
  `AllRules.ruleset` и `MinimumRecommendedRules.ruleset`.
- Итоговый checkpoint: `backup/ZXMAK2-DOCSYNC-after-20260919-191000/snapshot`.
- Эта операция не выпускает новую runtime-сборку и не расширяет
  пользовательскую runtime-приёмку B36.

## Зафиксированный контракт B37–B39 — 2026-09-19

- По требованию пользователя в `BASECONF_AUDIT_B32.md` закреплены точный scope,
  исключения и критерии готовности трёх следующих сборок.
- B37: AVR/gluclock и COM/RS232 WAIT, DOS settling stall, совмещение WAIT на
  границах Z80 и подключение уже подготовленной B36 паузы INT.
- B38: встроенный Kempston joystick, tape-in/tape-out и beeper/tape mux с
  сохранением принятого AY/Covox/DirectSound тракта.
- B39: ULAplus и официальное расширение 4:4:4 через `#BF.D5` во всех затронутых
  renderer-путях, с golden и mid-frame vectors.
- B40, ZX-BUS, CD/ATAPI и независимый backlog в B37–B39 не подмешиваются.
  Runtime-приёмка каждого этапа остаётся исключительно за пользователем.

## GitHub prerelease — ZX-Evo BaseConf Alpha 0.3 — B36 — 2026-09-20

- Опубликован prerelease `v0.3-alpha` из commit `26e7159`:
  `https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.3-alpha`.
- Portable ZIP `ZXMAK2-ZXEvo-BaseConf-Alpha-0.3.zip` содержит 45 файлов,
  вложены в один корневой каталог `ZXMAK2-ZXEvo-BaseConf-Alpha-0.3`,
  размер `3391464` байта, SHA-256
  `0A4B23BFF64F0415205460DDF8AEE9404A4303C719DCF8D144B0809C2BBA9455`.
- Чистая распаковка проверена: `ZXMAK2.exe` присутствует; пользовательские
  `.cmos/.nvram/.vmide`, образы носителей, логи и PDB в ZIP не входят.
- После проверки заменён первоначальный архив; в публичном списке оставлен
  только `v0.3-alpha`, старые релизы Alpha 0.1/0.2 удалены, их git-теги
  сохранены.
- Release notes сделаны на английском и русском языках. Публикация не меняет
  границу runtime-приёмки B36: отдельный прикладной тест INT/NMI по-прежнему
  ожидается от пользователя.

## ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-20260920-214709 — AVR/COM WAIT и DOS settling

- Этап выполнен строго в границах B37 из канонического
  `BASECONF_AUDIT_B32.md`. B38 input/audio, B39 palette, B40 contention/video,
  ZX-BUS, CD/ATAPI и независимый backlog не затрагивались.
- Перед изменениями создан полный checkpoint
  `backup/ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-before-20260920-212148/snapshot`.
- Официальные источники повторно сверены по `baseconf/trunk/z80/zports.v`,
  `zwait.v`, `zclock.v`, `mem/atm_pager.v`, `slave/slavespi.v` r1364 и
  `pentevo/avr/current/rs232.c`, `zx.c`.
- `CmosPentEvo` теперь декодирует семейство `F7` по аппаратным A8/A13/A14 и
  Shadow/CMOSEN-условиям. Адресные и data-алиасы сохранены, `#EFF7` не
  перехватывается. WAIT создаётся только data read/write, как в RTL.
- Добавлено семейство COM с младшим байтом `EF`, выбором регистра по A10:A8 и
  AVR-reset/register semantics для `#F8EF..#FFEF`: DLAB, DLL/DLM, IER/ISR,
  LCR/MCR, LSR/MSR и SCR. Физический host serial endpoint в B37 не добавляется;
  программно видимая регистровая сторона платы завершена.
- В `MemoryPentEvo` AVR/COM транзакция получает кратчайший наблюдаемый
  синхронный WAIT в один master clock. Фиксированное время выполнения прошивки
  AVR не выдумывается: официальная схема удерживает WAIT до SPI reply, но не
  задаёт постоянную длительность main-loop/ISR. Ответ и commit происходят в
  текущем I/O callback после внесения задержки.
- Только этот официальный внешний WAIT подключён к B36
  `PauseFrameInterruptForWait`; обычные DRAM/video/14-МГц I/O stalls INT не
  растягивают. После interrupt acknowledge внешний WAIT также не продлевает
  уже снятый INT.
- При реальном переходе DOS на M1 `#3Dxx` фиксируется минимум три master clocks
  из `atm_pager.v`. Stall потребляется той же opcode-транзакцией, не является
  инструкционной поправкой и совмещается с более длинной DRAM-задержкой через
  `max`, как аппаратный OR входов `zclock.v`.
- Visual Studio 2022 MSBuild 17.14.51: полная Release-сборка прошла без ошибок;
  остались два прежних missing-ruleset warning.
- Новый `WaitPortProbe-B37` прошёл 197981 проверку: исчерпывающие WAIT/F7/COM
  alias/decode, read/write, UART reset/DLAB/masks/status, INT pause/acknowledge
  и DOS stall/overlap.
- Актуальные регрессии прошли: B36 INT/NMI 41, B35 config 539, B34 media 33,
  IDE 10/786, FDD/Rage 14/113, B30 video 12748/321/1807/655922, audio 43/16.
  Старые B16/B20/B21 probes с намеренно заменёнными ожиданиями не являются
  gates; используются их актуальные B30/B31/B37 замены.
- Подробный отчёт: `B37_RESULT.md`. Runtime-пакет:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-20260920-214709\release`.
- ZIP содержит 124 файла, чистая распаковка проверена 124/124 побайтно;
  SHA-256 `2BE84C7B9824D52DA332BD2036022EAC6FFBF682C785AD183242A975430EFB28`.
  Это приватный runtime-test package с текущими `.cmos/.nvram/.vmide`, а не
  публичный GitHub portable; правила очистки публичного релиза не менялись.
- Итоговый checkpoint:
  `backup/ZXMAK2-v13-ZXEVO-BC-EXTWAIT-B37-after-20260920-214709/snapshot`.
- Пользовательская runtime-приёмка B37 не заявляется. Требуются NedoOS, Rage
  одним коротким Enter, B30 border/multicolor, Bad Apple, обычный звук,
  повторная смена SD/HDD, IDE file access и обычный запуск BaseConf.

## ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-20260921-051523 — встроенный ввод и audio mux

- Этап выполнен строго в границах B38 из канонического
  `BASECONF_AUDIT_B32.md`. ULAplus/4:4:4 (B39), итоговые contention/video
  (B40), ZX-BUS, CD/ATAPI и независимый backlog не затрагивались.
- Before-checkpoint:
  `backup/ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-before-20260921-045812/snapshot`.
- Официальный контракт повторно сверялся по `base_trdemu/trunk/z80/zports.v`,
  `zkbdmus.v`, `sound/sound.v`, `top.v` r1364 и AVR-файлам
  `pentevo/avr/current/zx.c`, `config.h`, `joystick.c`, `tape.c`.
- В профиль ZX-Evo BSconf добавлен Kempston joystick с точным младшим байтом
  `#1F` и восемью битами. D0..D3 — right/left/down/up, D4..D7 — B/fire, C,
  A, Start. Старые пятибитные профили не изменены. VG93 остаётся раньше
  joystick в порядке устройств и владеет пересекающимся `#1F` в Shadow/DOS.
- Tape-in подключён к D6 чтений `xxFE/xxF6`. Только для ZX-Evo отключены
  generic FE output-loopback и отдельный tape monitor, чтобы вход не подменялся
  FE.D4 и не дублировал аппаратный звуковой тракт.
- Новый `BeeperPentEvo` реализует официальный mux config0.D3: FE.D4 beeper или
  FE.D3 tape-out. Num Lock повторяет AVR `func_beeper()`, переключение
  комбинационное относительно последнего FE-значения и сохраняется в уже
  существующем CMOS. Scroll Lock video/raster оставлен без изменений.
- AY, Covox, RejectDC, DirectSound underrun и другие машинные профили не
  менялись. Visual Studio 2022 Release: 0 ошибок, два прежних missing-ruleset
  warning.
- Новый `InputAudioProbe-B38` прошёл 196641 проверку: profile wiring,
  исчерпывающие `#1F` и `FE/F6` decode, Normal/Shadow/DOS arbitration,
  joystick 8/5-bit, tape D6/loopback, CMOS persistence, Num Lock edges и
  мгновенные mux-переходы.
- Полная актуальная регрессия прошла: B37 WAIT 197981; B36 INT/NMI 41; B35
  config 539; B34 media 33; IDE 10/786; FDD/Rage 14/113; B30/B23 video
  12748/1807/655922; audio/DirectSound 43/16. В B30 probe изменено только
  прежнее ожидание, что config0.D3 не поддерживается: с B38 этот официальный
  бит входит в readback.
- Runtime:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-20260921-051523\release`.
  ZIP содержит 126 файлов, чистая распаковка проверена 126/126 побайтно;
  размер 5324858 байт, SHA-256
  `7A1F87CFFE1DB43A0F081710C8999350FA68EF3A27166D0932255A0B4F88315A`.
- After-checkpoint:
  `backup/ZXMAK2-v13-ZXEVO-BC-INPUTAUDIO-B38-after-20260921-051523/snapshot`,
  проверено 834/834 source-файла.
- Пользовательская runtime-приёмка B38 пройдена: не замечено критичных
  регрессий; сохранены border/multicolor, смена носителей, IDE browsing и
  ранее принятые рабочие сценарии.

## GitHub prerelease — ZX-Evo BaseConf Alpha 0.4 — B38 — 2026-09-21

- По явному запросу пользователя B38 выпускается как стабильный portable
  prerelease `v0.4-alpha`; B39 с выявленными runtime-регрессиями в этот релиз
  не входит.
- В архив включаются только файлы запуска, зависимости, чистый профиль и ROM.
  Пользовательские `.cmos/.nvram/.vmide`, носители, логи, PDB и probes
  исключаются. ZIP имеет одну корневую папку.
- GitHub prerelease опубликован: `https://github.com/Moro44444444/ZXMAK2-Fork/releases/tag/v0.4-alpha`.
  Asset `ZXMAK2-ZXEvo-BaseConf-Alpha-0.4.zip`: 46 файлов, 3 400 453 байта,
  SHA-256 `5636F40820DD6B0EF9CBDF6B7E7F0F672F5E1BF3299D2EA0BFE42395DAD44979`.

## B39 отклонена; B39A начата заново от B38 — 2026-09-21

- Совмещённая B39 с ULAplus и 4:4:4 отклонена пользовательской runtime-
  проверкой: вернулся паразитный звук FDD, ATM 16-color программы сбрасывались,
  а ULAplus-демо не запускались. Этот код не используется как база.
- По решению пользователя B39A начата от принятой B38 / Alpha 0.4. В неё
  входит только официальная палитра `base_trdemu` 4:4:4 по `#BF.D5`;
  ULAplus отложена и не смешивается с этой правкой.
- Before-checkpoint:
  `backup/ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-before-20260921-152615/snapshot`,
  сверено 826/826 файлов. Рабочая ветка `codex/b39a-palette444-only`.
- Контракт сверён с официальными `base/z80/zports.v` и
  `base/video/video_palframe.v` r1364: верхние пары RGB берутся из данных,
  младшие — из A8/A9/A12-A15; D5 выбирает read/write interpretation и не
  переписывает palette RAM; `BD_COLORRD` возвращает соответствующую пару.
- При D5=0 сохранён буквальный прежний вызов `SetPaletteAtm2`. Новый путь
  активен только для `UlaPentEvo` при D5=1, перед изменением цвета завершает
  уже отрисованную часть кадра и обновляет все семь существующих renderer-
  путей. ULAplus, остальные машины и периферия не затронуты.
- `Palette444Probe-B39A` прошёл 132867 проверок: D5=0, 256×64 официальных
  data/address-векторов, семь renderers, D5=1 readback и сохранение палитры.
  Полный актуальный regression suite также прошёл: B23 1807, B30 12748,
  B35 539, B36 41, B37 197981, B38 196641, FDD 14, Rage 113, TRD 655922,
  media 33, IDE 10/786, audio 19, DirectSound 14.
- Полная Release-сборка MSBuild 17.14.51 завершилась без ошибок; остались два
  прежних missing-ruleset warning. Реализация зафиксирована commit `8f7d9a7`.
- Runtime-приёмка B39A не заявляется. Требуются border/multicolor B30,
  ATM 16-color, Rage, NedoOS, Bad Apple, отсутствие FDD-скрипа, SD/HDD и IDE;
  отдельно — документированный 4:4:4 визуальный тест при наличии.
- Runtime-пакет:
  `K:\Download\ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-20260921-154533\release`.
  ZIP содержит 123 файла, чистая распаковка сверена 123/123 побайтно; процесс
  `ZXMAK2.exe` оставался рабочим после четырёхсекундного startup smoke.
  Размер ZIP 5050610 байт, SHA-256
  `518B1CB99EF204EE501545D7C552B16B9959DED828785E5487427EB4C23E50B6`.
- After-checkpoint:
  `backup/ZXMAK2-v13-ZXEVO-BC-PALETTE444-B39A-after-20260921-154923/snapshot`,
  сверено 829/829 source-файлов.

## Диагностика Rage: контрольные 500-МБ образы — 2026-09-21

- Пользователь уточнил, что повреждённый старт Rage воспроизводится не только
  с 4-ГБ SD IMA/IMG, но и с малым HDD-образом. Поэтому размер носителя и
  SDHC-адресация больше не считаются общей причиной.
- Для изоляции контейнера подготовлены два новых тестовых носителя, не
  затрагивающие исходные образы:
  `L:\Work_two\ZX\ZX_IMG\_Test_ZXMAK2\Rage500Media\RAGE-500MB-rawfat32.ima`
  и `...\RAGE-500MB-rawfat32.img`. Каждый имеет размер 524288000 байт
  (500 МБ), raw FAT32 без MBR, корректную boot signature `55AA` и в корне
  содержит `RAGE.SCL`.
- `RAGE.SCL` записан из проверенного `L:\Work_two\ZX\RAGE_AAA.SCL`; хэш
  файла внутри каждого носителя: SHA-256
  `D530FF773125F4C4E8AEC786FBB8B8021BD67589C039A990B530EAC88D45306F`.
  Тестовые ImDisk-тома после записи отсоединены. Пользователь подтвердил, что
  Rage проходит до конца с обоими чистыми SD-копиями и с synthetic 4-ГБ SDHC;
  исходный код и сборка этой диагностикой не менялись.

### Уточнение по BCVIDTEST — 2026-09-21

- Пользователь сообщил, что `L:\Work_two\ZX\ZX_IMG\_Test_ZXMAK2\BCVIDTEST.TRD`
  аварийно показывает мусор одинаково в ZXMAK2 и UnrealSpeccy. Проверка
  установила, что файл не повреждён записью, но это старый образ BC22R1:
  label `BC22R1`, SHA-256
  `8B8258D0B721E2B953AFB5C2E304A665EB9F0C805835A26C4270D215D2BBEDB4`.
- Принятый B23 отличается: label `BC23`, SHA-256
  `14423989A5819D9C2A74D11D54CE2205D947145BB716752B5B42A84419B89CCA`;
  сохранён в `K:\Download\ZXMAK2-v13-ZXEVO-BC-VIDEOPALETTE-B23-20260918-102642\BCVIDTEST.TRD`
  и в B35/B39 snapshots. Старый BC22R1 исключён из диагностики носителей.
- Пользователь запустил правильный B23 с 4-ГБ образа: загрузилось меню и
  успешно пройдены все семь видеотестов. Это подтверждает для данного
  носителя корректность чтения TRD, virtual FDD, BaseConf page switching и
  видеорежимов; общий дефект «4-ГБ образ» исключён.

### Метод создания контрольных образов и backlog HDD — 2026-09-21

- Контрольный 500-МБ носитель был получен копированием рабочего raw FAT32
  `sd_nedo.vhd`; отсутствие VHD-footer означает, что ZXMAK2 открывает его как
  обычный поток секторов, независимо от расширения `.vhd`, `.ima` или `.img`.
  В каждую копию через временное подключение ImDisk записан проверенный
  `RAGE.SCL`, затем том отсоединён и проверены FAT32 boot signature и хэш
  файла. Поэтому `RAGE-500MB-rawfat32.ima` и `.img` — валидные носители для
  теста расширения при одинаковом содержимом.
- Синтетический `RAGE-4GB-rawfat32-SDHC.ima` создан из этой 500-МБ копии:
  файл помечен sparse и логически расширен до 4 GiB. Его FAT32 volume по BPB
  остаётся 500 МБ; это намеренно изолирует SDHC/CSD-v2 представление ZXMAK2
  от FAT-разметки и не является настоящим 4-ГБ отформатированным носителем.
- По запросу пользователя создан отдельный настоящий 4-ГиБ SD-носитель
  `RAGE-4GB-real-fat32-SDHC.ima`. Это raw FAT32 volume без MBR, начиная с
  LBA 0: 4294967296 байт (8388608 секторов по 512 байт), 8 секторов на
  кластер, reserved=32, две FAT по 8177 секторов, root cluster=2, метка
  `ZXEVO4G`, boot signature `55AA`. В него через независимые временные
  подключения ImDisk скопировано всё содержимое рабочего
  `RAGE-500MB-rawfat32.ima` (19 элементов); `RAGE.SCL` после копирования
  имеет контрольную сумму `D530FF773125F4C4E8AEC786FBB8B8021BD67589C039A990B530EAC88D45306F`.
  Его файловая система действительно сообщает 4 ГиБ, в отличие от
  синтетического SDHC-образа выше. Ожидается отдельная runtime-проверка
  пользователя в ZXMAK2; исходный код и сборка не изменялись.
- Возможная отдельная утилита отложена: она должна создавать по явному
  выбору raw FAT32, MBR+FAT32 и VHD, записывать выбранные файлы, проверять
  контрольные суммы и не смешивать SD-образ с IDE/HDD. Расширение само по
  себе не определяет формат; VHD требует отдельной проверки footer/типа.
- HDD остаётся отдельным backlog: пользователь наблюдает, что Rage с
  64-МБ Nemo IDE HDD запускается, но аварийно завершается после первой
  части. Это не следует смешивать с SD/FAT-диагностикой: нужно отдельно
  сопоставить IDE command/sector trace, геометрию и фактические данные
  виртуального FDD после загрузки с HDD с рабочим сценарием.
- Создан независимый контрольный HDD:
  `L:\Work_two\ZX\ZX_IMG\_Test_ZXMAK2\Rage500Media\NEMO-RAGE-500MB-FAT32.hdd`.
  Это raw IDE image размером 524288000 байт (500 МБ), с MBR, одним LBA FAT32
  разделом типа `0C` от LBA 63, FAT32 cluster size 8 sectors (4 КиБ) и
  `RAGE.SCL` в корне. Файл создан с нуля, не копирован из проблемного HDD.
  После записи данные Rage повторно прочитаны из его кластеров; SHA-256
  совпадает с исходником: `D530FF773125F4C4E8AEC786FBB8B8021BD67589C039A990B530EAC88D45306F`.
  Ожидается пользовательская runtime-проверка через Nemo IDE; код и сборка
  ZXMAK2 этой операцией не менялись.
- Пользовательская runtime-проверка нового 500-МБ HDD успешна: Rage отработал
  до финального экрана. Следовательно, общее IDE/Nemo-HDD чтение, новый MBR
  и виртуальный FDD path исправны; старый 64-МБ HDD не является эталоном и
  требует отдельного исследования своих данных/разметки.

### Паспорт созданных контрольных носителей — 2026-09-21

| Файл | Логический размер | Назначение и формат | FAT32 layout |
| --- | ---: | --- | --- |
| `sd_nedo.vhd` | 524288000 (500 МБ) | исходный рабочий SD-образ; raw sector stream, VHD footer отсутствует | boot LBA 0; 512 B/sector; 8 sectors/cluster; reserved 32; 2 FAT; FAT=1000 sectors; total=1024000 sectors |
| `RAGE-500MB-rawfat32.ima` | 524288000 (500 МБ) | копия рабочего raw SD, `RAGE.SCL` в корне; тест расширения IMA | те же параметры, LBA 0 |
| `RAGE-500MB-rawfat32.img` | 524288000 (500 МБ) | копия рабочего raw SD, `RAGE.SCL` в корне; тест расширения IMG | те же параметры, LBA 0 |
| `RAGE-4GB-rawfat32-SDHC.ima` | 4294967296 (4 ГиБ) | sparse-копия 500-МБ raw SD; заставляет SDHC в ZXMAK2; не является полноценной 4-ГиБ FAT | BPB намеренно остаётся как у 500-МБ образа, LBA 0 |
| `RAGE-4GB-real-fat32-SDHC.ima` | 4294967296 (4 ГиБ) | настоящий raw SDHC FAT32 без MBR, полная копия содержимого рабочего 500-МБ носителя | LBA 0; 512 B/sector; 8 sectors/cluster; reserved 32; 2 FAT; FAT=8177 sectors; total=8388608 sectors; label `ZXEVO4G` |
| `NEMO-RAGE-500MB-FAT32.hdd` | 524288000 (500 МБ) | raw IDE/Nemo HDD, создан с нуля; `RAGE.SCL` в корне | MBR: partition type `0C`, start LBA 63, total 1023937; далее 512 B/sector, 8 sectors/cluster, reserved 32, 2 FAT, FAT=998 sectors |

- Во все носители с Rage записан идентичный файл SHA-256
  `D530FF773125F4C4E8AEC786FBB8B8021BD67589C039A990B530EAC88D45306F`.
- Будущая утилита создания образов отложена до завершения основной ревизии.
  Она должна создавать носители по явным параметрам размера, назначения
  (SD или IDE), layout (raw FAT32 или MBR+FAT32) и контейнера (raw либо
  настоящий VHD с footer), записывать файлы и выполнять read-back checksums.

## B39A — пользовательская runtime-приёмка и зафиксированный B40 — 2026-09-21

- B39A принимается как текущая полностью рабочая локальная база: Rage проходит
  до финала с чистыми 500-МБ SD и IDE HDD, B23 и все семь видеослотов проходят
  с 4-ГБ образом, border/multicolor сохранены, а CPU clock test корректно
  распознаёт 3.5, 7 и 14 МГц. Это не публикует новый GitHub release: публичная
  Alpha 0.4 остаётся B38 до отдельного решения пользователя.
- Следующая и единственная утверждённая область B40: evidence-based
  contention/floating-bus refinement, mid-frame raster/mode/palette и
  raster/INT/video phase, golden-векторы семи renderer-путей, а также
  read-only VG93 command/status trace. До подтверждённого mismatch поведение
  VG93 не меняется.
- Явно исключены из B40: ULAplus, утилита IMG/IMA/VHD/HDD, SD/HDD lifecycle,
  исправления отдельных старых носителей, ZX-BUS, CD/ATAPI и новая периферия.
  ULAplus остаётся отдельным этапом только после B40.

## B40 checkpoint 1 — contention и open bus — 2026-09-21

- Работа начата от принятого B39A commit `f71a211`. Исчезнувший отдельный
  worktree не содержал незакоммиченных изменений; ветка и Git-объекты были
  целы. Рабочая копия восстановлена из точного commit в
  `L:\Work_two\ZX\ZXMAK2-Fork-b40`.
- Сопоставление с `baseconf/trunk/z80/zclock.v`, `dram/arbiter.v` и
  `video/video_sync_h.v` r1364 подтвердило отдельное расхождение: модель уже
  содержала DRAM-арбитраж, но не применяла сигнал `contend_wait`.
- Добавлен только официальный 48K/128K contention: профиль 48K/128K,
  частота 3,5 МГц, память `#4000-#7FFF`, нечётная `#C000-#FFFF` в 128K и
  чётные I/O-порты. Горизонтальное окно начинается после `hcount=127`,
  длится 256 циклов 7 МГц и использует последовательность ожиданий
  6-5-4-3-2-1-0-0 Z80-тактов. DRAM/DOS/AVR/COM задержки объединяются по
  аппаратному OR, а не складываются.
- Open bus проверен без изменения общей логики: незаявленный I/O-порт
  сохраняет текущее значение `CPU.BUS`, что соответствует уже существующей
  модели свободной шины; искусственная выдача видеобайта не добавлялась.
- Новый `ContentionFloatingBusProbe-B40` прошёл 46 проверок адресов, фаз,
  вертикальных окон, 48K/128K страниц, I/O и отсутствия contention в
  Pentagon/60 Hz и на 7/14 МГц.
- Полная Release solution собрана. Регрессии прошли: B30 video timing 12748,
  B36 INT/NMI 41, B37 WAIT 197981, B38 input/audio 196641, B31 FDD trap 14,
  B39A palette 132867. Runtime-приёмка и остальные узлы B40 ещё не заявлены.

## B40 checkpoint 2 — live mode/palette — 2026-09-21

- По `video_modedecode.v` r1364 подтверждено: `atm_vmode`/`pent_vmode`
  декодируются на каждом 28-МГц такте, поэтому прежнее ожидание программной
  границы кадра было эмуляторным приближением. Переключение одного из семи
  renderer-путей теперь сначала дорисовывает старый путь до текущего такта, а
  затем сразу включает новый режим и его страницы.
- Оба BaseConf-пути палитры теперь имеют одинаковую mid-frame семантику:
  обычная 6-битная ATM/BaseConf palette и расширенная 4:4:4 palette сначала
  сохраняют уже отрисованную часть строки/кадра и только затем меняют цвет.
  Формулы цветов и readback B39A не изменены.
- Новый `MidFrameVideoProbe-B40` прошёл 9 проверок мгновенного перехода
  ZX→ATM320→ATM text→BaseConf text, непрерывной позиции отрисовки и обеих
  палитр. Полная Release solution собрана; `Palette444Probe-B39A` (132867) и
  `ContentionFloatingBusProbe-B40` (46) повторно прошли.
- Живой raster transition пока намеренно не заявлен: RTL меняет период на
  текущих H/V-счётчиках, поэтому перед правкой требуется сохранить их фазу,
  а не просто заменить длину программного кадра.

## B40 checkpoint 3 — golden-векторы renderer-путей — 2026-09-22

- Добавлен `GoldenRendererProbe-B40`: он заполняет BaseConf RAM
  детерминированным узором, запускает каждый официальный renderer-путь и
  сверяет FNV-1a hash всего native-size кадрового буфера. Покрыты ровно семь
  маршрутов r1364: ZX attributes, Pentagon HWM, Pentagon 16c, ATM 320, ATM
  640 HWM, ATM text и BaseConf one-page text.
- Принятые vectors: `78707931`, `1BE1F009`, `EE3EDB75`, `6AEA819A`,
  `9221235D`, `5D1983FE`, `18A613CE`. Тест не добавляет режимов и не меняет
  поведение эмулятора; это стабильная защита уже реализованных page/renderer
  mappings перед завершающей проверкой VG93.

## B40 checkpoint 4 — WD1793 trace и финальная compiled-регрессия — 2026-09-22

- Отдельно перепроверен `FddPentEvo`: существующая read-only трасса при
  `logIo=true` уже записывает WD-команду и компактное состояние контроллера
  (`DumpState`, включая DRQ/INTRQ), системный регистр, выбор `#13BD`, вход
  виртуального обработчика RAM page `#FE` и выход через `#BE`. Подтверждённой
  разницы с BaseConf здесь не найдено, поэтому алгоритм WD1793 не менялся.
- Финальная Release-сборка B40 прошла без ошибок (остались только два старых
  предупреждения о отсутствующих `.ruleset`). Последовательно прошли:
  B30 video timing — 12748, B39A palette — 132867, B40 contention/open bus —
  46, B40 live mode/palette — 9, B40 renderer golden — 7.
- B40 source-phase завершён и зафиксирован; перед пользовательской runtime
  приёмкой намеренно не добавлялись ULAplus, новые устройства, CD/ATAPI,
  ZX-BUS либо изменения SD/HDD/FDD поведения.

## B40 local runtime package — 2026-09-22

- Для пользовательской проверки подготовлена отдельная папка
  `L:\Work_two\ZX\ZXMAK2-v13-ZXEVO-BC-B40-20260922\release`.
- В неё скопирован Release output, чистый `tools/ZXEvoBsconf.vmz` заменил
  локальный пользовательский профиль; добавлены `B40_RESULT.md` и короткий
  двуязычный `BUILD_INFO.txt`. В пакете 76 файлов, нет `.cmos`, `.vmide`,
  `.nvram`, логов, образов носителей или пользовательских путей.
- Это локальный runtime-пакет, не GitHub release и не изменение Alpha 0.4.

## BaseConf tape profile correction — 2026-09-22

- Пользователь обнаружил, что в чистом `ZXEvoBsconf.vmz` отсутствовал
  `TapeDevice`, хотя основной профиль `machines.config` уже содержал его
  для ZX-Evo BaseConf. Это была ошибка состава профиля, не отсутствие
  эмуляции tape-in/tape-out.
- В чистый профиль добавлен тот же документированный элемент: `#FE`, маска
  `#F7`, EAR/D6, `noDos=false`, `outputLoopback=false`, volume 0. EXE/DLL и
  поведение остальных устройств не изменяются; для применения достаточно
  заново открыть профиль либо перезапустить эмулятор.

## BaseConf tape timing correction — 2026-09-22

- После добавления профиля пользователь подтвердил, что окно tape-player
  открывается, но ROM BaseConf не видел корректного потока и не показывал
  значок кассеты. Причина: TAP/TZX задают длины импульсов в тактах Z80
  3,5 МГц, а `Cpu.Tact` BaseConf работает в тактах master-clock 28 МГц.
- Множитель больше не хранится в пользовательском профиле: после BusInit он
  выводится только из имени активной машины. `ZX-Evo BSconf` получает 8,
  все прочие машины — 1. Это устраняет оба перехода: ×8 не попадает в
  Pentagon/ATM/Spectrum, а сохранённый ×1 не возвращается в BaseConf.
  Множитель применяется только к TAP/TZX и к порогу ROM-loader autodetect;
  WAV/CSW уже строятся в частоте машины и не менялись.
- Новый `TapeClockProbe-B40` через публичный API сериализаторов подтвердил
  TAP и TZX: 2168 тактов остаются 2168 на обычной машине и становятся 17344
  на BaseConf. Полная Release-сборка проходит без ошибок (те же два старых
  предупреждения `.ruleset`).
- Это узкая post-B40 корректировка ленты; video, звук, FDD/SD/IDE, media
  lifecycle и опубликованные Alpha-релизы не менялись. Нужна отдельная
  runtime-проверка TAP/TZX в BaseConf.
- Чистый `ZXEvoBsconf.vmz` явно сохраняет штатные `useTraps=true` и
  `useAutoPlay=true`, чтобы первый запуск portable-профиля не наследовал
  выключенный autoplay из ранее сохранённого пользовательского состояния.

## BaseConf tape machine-switch correction — 2026-09-22

- Runtime-проверка выявила сохранение неверной скорости при смене машины без
  закрытия приложения: TAP/TZX ранее умножались на ×8 уже при открытии файла.
  Поэтому BaseConf→Pentagon оставлял Pentagon медленным, а
  Pentagon→BaseConf — BaseConf быстрым до перезапуска.
- Теперь `TapeBlock` всегда хранит исходные интервалы формата в 3,5-МГц
  тактах. `TapeDevice` умножает каждый интервал непосредственно перед
  воспроизведением: ×8 для активного `ZX-Evo BSconf`, ×1 для остальных.
  Одна и та же открытая лента корректно переживает оба перехода машин и не
  требует повторного выбора файла.
- Полная Release-сборка прошла без ошибок (два прежних `.ruleset`
  предупреждения). `TapeClockProbe-B40` подтверждает для одного и того же
  блока переход 2168→17344→2168 при переключении профиля.

## B40 stable checkpoint — 2026-09-22

- Пользователь принял B40 с динамической скоростью уже открытой ленты как
  стабильную контрольную точку. Закреплён Git-тег `B40-stable` на коммите,
  содержащем исходники, тест, журнал и локальный portable runtime-пакет.
- Это фиксация рабочего состояния для безопасного отката; публикация нового
  GitHub release этим действием не выполняется.
