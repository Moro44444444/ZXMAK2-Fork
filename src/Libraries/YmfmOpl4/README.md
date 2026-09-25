# ymfm OPL4 bridge

This directory contains the `ymfm` YMF278B core by Aaron Giles and a small
native C ABI bridge used by ZXMAK2's ZXM-MoonSound device.

- Upstream: https://github.com/aaronsgiles/ymfm
- License: BSD 3-Clause (`LICENSE` in this directory)
- Chip clock: 33,868,800 Hz
- Native output rate: 44,100 Hz

The copyrighted Yamaha YRW801-M 2 MiB instrument ROM is deliberately not kept
in source control.  Put `YRW801-M - Yamaha - 1993.rom` or `yrw801.rom` beside
`ZXMAK2.exe` for local use.

Build the 32-bit bridge from a Visual Studio developer prompt:

```text
cl /nologo /LD /EHsc /O2 /std:c++14 /I. ymfm_opl4_wrapper.cpp ymfm_opl.cpp ymfm_pcm.cpp ymfm_adpcm.cpp /link /OUT:ymfm_opl4.dll
```
