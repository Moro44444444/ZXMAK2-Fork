ZXMAK2 Fork — ZX Evolution BaseConf — v56 Media Icons, 2026-09-26

На базе v55 с проверенной пользователем ULAplus. v54 и v55 не изменены.
В runtime заменена только ZXMAK2.Host.WinForms.dll, без изменений логики.

Новые иконки: синяя кассета, флоппи-дисковод с красной дискетой,
синий CD-привод с выдвинутым лотком. У CD-привода сплошная боковина
без разъёмов; на дискете надпись ZX SPECTRUM. Этикетка кассеты сохранена.
Прозрачный фон, без рамки. Пропорции предметов сохранены, масштаб
согласован с HDD и SD. Стрелки выпадающих меню и индикаторы сохранены:
красный — пусто, зелёный — носитель вставлен, серый — недоступно.

Resource canvas: 104x72 RGBA, displayed at the existing 52x36 size.
Object bounds are fitted within 78x64, leaving the right side for the dot.
ToolStrip button dimensions and all menu/keyboard/media behaviour unchanged.

Assets saved in source:
src/ZXMAK2.Host.WinForms/Resources/EmuTapeImage_104x72.png
src/ZXMAK2.Host.WinForms/Resources/EmuFddImage_104x72.png
src/ZXMAK2.Host.WinForms/Resources/EmuCdImage_104x72.png

Preparation used built-in image_gen, followed by deterministic bicubic
canvas fitting with tools/FitMediaArtwork.ps1 (Windows PowerShell 5.1).
Prompt set: extract each supplied object onto a genuine transparent
background, preserve angle/colours/details and remove external shadows;
replace CD side ports/holes with solid matching blue metal;
replace only floppy label text with ZX / SPECTRUM. Cassette text-edit
variant was discarded following the user's correction.

UI probe passed: alpha backgrounds, red/green/grey dots for all media,
arrow clearance, existing UI/media/hotkey/ULAplus/master-volume contracts.
Visual previews checked at toolbar and enlarged icon size.
No GitHub publication. Previous firmware distribution restrictions unchanged.
