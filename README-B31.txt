B31 — ZX-Evo BSconf, NedoOS virtual-FDD PEN2 gate

Исправление основано на официальном BaseConf base_trdemu r1364:
virtual FDD handler page #FE включается только при DOS + ROM + !PEN2.

Проверьте по порядку:
1. Запустите sd_boot.$C и убедитесь, что появляется NedoOS.
2. Загрузите Rage из SD/EVO Service/Ramdisk и нажмите Enter один короткий раз.
3. Повторите принятый в B30 border/multicolor-фрагмент.
4. Дополнительно запустите Bad Apple.$C.

Runtime-приёмка этой сборки заранее не заявлена.
