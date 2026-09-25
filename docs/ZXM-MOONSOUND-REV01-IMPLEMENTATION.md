# ZXM-MoonSound Rev.01 in ZXMAK2

Status: local v49 test implementation for ZX Evolution BaseConf.  It is not
yet a public Alpha release.

## Hardware reference

The implementation follows Mick Laboratory's last published board revision:

- ZXM-MoonSound Rev.01;
- CPLD firmware 1.00 dated 07.09.2016;
- Yamaha YMF278B (OPL4), clocked at 33.8688 MHz;
- 2 MiB YRW801-M instrument ROM;
- 1 MiB external SRAM.

Primary references:

- http://micklab.ru/My%20Soundcard/ZXMMoonSound.htm
- http://micklab.ru/file/zxm_moonsound/zxm_moonsound01_src0100.rar

The published `dd2.tdf` CPLD source is authoritative for ZXBUS decoding:

- low-byte `#7E/#7F`: OPL4 PCM address/data;
- low-byte `#C4/#C5`: OPL3 low register bank address/data;
- low-byte `#C6/#C7`: OPL3 high register bank address/data;
- the high byte is not decoded;
- while PentEvo TR-DOS I/O is enabled, the motherboard gate disconnects both
  MoonSound port groups.

Rev.01 has no user-selectable port jumpers and its removed interrupt-vector
former is not emulated.  The board can occupy either of the two equivalent
ZXBUS slots.  Two copies are rejected because they would decode the same fixed
ports.  NeoGS, ZX-MultiSound and MoonSound are otherwise independent slot
choices; only two physical boards can be present at once.

## Sound core and native bridge

The YMF278B core is current `ymfm` by Aaron Giles rather than the old GPL
MoonSound core from Unreal Speccy.  `ymfm` is BSD-3-Clause and implements the
combined OPL3 FM and 24-channel wavetable/PCM device.  The vendored source,
license, small C ABI bridge and 32-bit DLL live under
`src/Libraries/YmfmOpl4`.

The chip generates at its native 44.1 kHz (`33868800 / 768`).  ZXMAK2's
DirectSound configuration is also 44.1 kHz, so samples are passed to the
existing frame-timestamped sound mixer without pitch conversion.  Writes are
rendered up to their exact position inside the current emulated frame before
the register changes.  Timer, busy and IRQ state are maintained by the native
bridge even though Rev.01 does not route an interrupt vector to the host CPU.

Physical memory mapping is used: the 2 MiB ROM is followed by exactly 1 MiB
SRAM.  Some old emulator implementations allocate 4 MiB; that is deliberately
not copied because it does not describe Rev.01 hardware.

## ROM and redistribution

The Yamaha YRW801-M ROM is copyrighted and is not committed to the repository.
For local use, place one exact 2 MiB file beside `ZXMAK2.exe` or in its `roms`
directory under either name:

- `YRW801-M - Yamaha - 1993.rom`
- `yrw801.rom`

The local v49 test folder may contain the user's existing ROM for immediate
testing.  It must be removed before any public package unless redistribution
permission is established.

## Configuration

Open `Machine Settings`, select the PentEvo `ZXBUS` page, enable either slot
and choose `ZXM-MoonSound Rev.01`.  There are intentionally no extra board
checkboxes.  Slot enable and board selection are the complete configuration.

## Automated acceptance

`Test.exe /moonsound` verifies:

- low-byte-only aliases with non-zero high address bytes;
- OPL4 status/identification reads;
- write/read access to SRAM at address `#200000`;
- audible OPL3 FM output;
- audible ROM wavetable output;
- release of MoonSound ports while PentEvo TR-DOS I/O is active.

The BaseConf settings probe separately checks either-slot placement,
coexistence, duplicate rejection, layout bounds and XML round-trip.  Existing
`/tsfm`, `/multisound` and full renderer regression tests must also remain
green before a portable build is accepted.
