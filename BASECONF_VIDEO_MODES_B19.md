# ZX-Evo BaseConf r1364: video-mode contract (B19)

Date: 2026-09-17
Checkpoint: `ZXMAK2-v13-ZXEVO-BC-VIDEOCONTRACT-B19-20260917-231704`

## Scope and authority

This passport separates three things which must not be conflated:

1. the raster/output profile (`modes_raster`, TV/VGA output selection);
2. the implemented picture format selected by `atm_vmode` and `pent_vmode`;
3. the generic bandwidth catalogue in `texts/video_modes.txt`.

The executable contract below is taken from the BaseConf FPGA sources in
`pentevo-fpga.r1364.tar.gz`, SHA-256
`7A509FBCEF3AF85380EC475AD622A0682E714AF54625851FCB9AA678DA0BBB82`.
The relevant unmodified files are preserved under the B19 backup in
`reference/fpga.r1364/baseconf/trunk`.

`texts/video_modes.txt` is a design/bandwidth catalogue. Its entries `256c`
and `16+16c` are not proof that these formats are implemented in r1364. The
authoritative implemented-mode set is the combination of
`video_modedecode.v`, `video_addrgen.v`, `video_fetch.v` and
`video_render.v`.

## Register path

- `atm_vmode = atm_scr_mode = xx77.D2..D0`.
- `pent_vmode = { EFF7.D0, EFF7.D5 }` (`top.v`).
- In the emulator `RG = xx77.D2..D0`, while
  `RGEX = { EFF7.D5, EFF7.D0 }` as an integer and
  `VIDEO = RG | (RGEX << 3)`. This is the same bit assignment expressed in a
  different packing.
- `xx77` is accepted by RTL only while `shadow = dos || shadow_en`; the port
  address also latches ATM pager/turbo/palette control bits from A8/A9/A14.
- EFF7 mode bits are written outside shadow mode through the EFF7-compatible
  F7 decode. EFF7 reset value is zero.
- `7FFD.D3` selects primary/secondary screen pages.
- Reset selects `atm_vmode=011`, `pent_vmode=00`: standard ZX format.

## Implemented r1364 mode matrix

| RG / `atm_vmode` | RGEX | `pent_vmode` | Effective format | Active picture | Pixel rate | RTL bandwidth | Current renderer | B19 verdict |
|---:|---:|---:|---|---:|---:|---:|---|---|
| 0 (`000`) | any | ignored | ATM 16c, packed two pixels/byte | 320×200 | 7 MHz | 1/4 | `Atm320Renderer` | Logical format/addressing present; common raster phase still approximate |
| 2 (`010`) | any | ignored | ATM hardware multicolor, 1 pixel bit plus one 4+4 attribute byte per 8 pixels | 640×200 | 14 MHz | 1/4 | `Atm640Renderer` | Logical format/addressing present; common raster phase still approximate |
| 3 (`011`) | 0 | `00` | standard ZX attr | 256×192 | 7 MHz | 1/8 | `SpectrumRenderer` | Present; BaseConf raster/output integration incomplete |
| 3 (`011`) | 1 | `10` | Pentagon 16c packed pixels | 256×192 | 7 MHz | 1/4 | `EvoA16Renderer` | Present; enum name `EvoAlco16c`; needs image-vector verification |
| 3 (`011`) | 2 | `01` | Pentagon hardware multicolor (bitmap plus per-line attributes) | 256×192 | 7 MHz | 1/8 | `EvoHwmRenderer` | Present, but enum name `Evo256x192` is misleading: this is not 256 colours |
| 3 (`011`) | 3 | `11` | undefined Pentagon code; decoder falls back to ZX | 256×192 | 7 MHz | 1/8 | default `SpectrumRenderer` | Fallback is not explicitly represented by an enum member |
| 6 (`110`) | any | ignored | ATM text, symbols and attributes in two page pairs | 80×25 cells / 640×200 | 14 MHz | 1/4 | `AtmTxtRenderer` | Present; writable font path exists, needs end-to-end verification |
| 7 (`111`) | any | ignored | BaseConf one-page text modifier | 80×25 cells / 640×200 | 14 MHz | 1/4 | `EvoTxtRenderer` | Present; enum name `EvoText080`; needs address-vector verification |

RG values 1, 4 and 5 are marked undefined by the RTL. They are not test modes
and must not be advertised by the future TRD menu.

## Pixel and memory contracts

### Standard ZX attr

- Primary display base is byte address `$14000` (page 5); secondary is
  `$1C000` (page 7).
- Bitmap address is the standard ZX line permutation.
- Attribute address is display base + `$1800` bytes.
- One bitmap byte and one attribute byte produce eight pixels.

### Pentagon hardware multicolor

- Uses the same ZX bitmap address permutation for both streams.
- Pixel and attribute streams occupy the two 8 KiB halves selected by the
  alternating fetch address.
- Each eight-pixel group therefore has a line-specific attribute byte.
- This is a 16-colour attribute format, not an 8-bit/256-colour pixel format.

### Pentagon 16c

- Uses pages 4+5 for primary screen and 6+7 for secondary screen, with both
  `$0000` and `$2000` halves.
- Each byte is `IiGRBgrb`: the two pixels are
  `{D6,D2,D1,D0}` and `{D7,D5,D4,D3}`.
- The four byte lanes are fetched in BaseConf order and form eight adjacent
  pixels.

### ATM 320×200 16c

- Uses pages 1+5 for primary screen and 3+7 for secondary screen.
- Four interleaved byte lanes cover each group of eight pixels; linear line
  stride is 160 bytes.
- Pixel nibble extraction is the same `IiGRBgrb` mapping as Pentagon 16c.

### ATM 640×200 hardware multicolor

- Bitmap is in page 5/7 and attributes are in page 1/3.
- Even/odd lanes use the `$0000`/`$2000` halves; the line stride is 80 bytes
  per stream.
- An attribute byte contains 4-bit ink and 4-bit paper; a bitmap byte selects
  between them for eight pixels.

### Text 80×25

- Normal mode (`RG=6`) fetches symbols from pages 5/7 and attributes from
  pages 1/3 using the documented even/odd lane swap.
- One-page mode (`RG=7`) uses the BaseConf page-8/page-10 organisation and
  interleaves symbols/attributes inside that 16 KiB page.
- Glyph address is `{character, row-in-cell}` into the 2 KiB writable font
  RAM. Port `xxBF.D2` enables writes to this font RAM in RTL.
- Text attributes use 4-bit ink and 4-bit paper.

## Palette and physical output

- Every implemented BaseConf picture path above produces a 4-bit colour
  index. The ATM palette therefore exposes 16 logical entries, each converted
  by RTL to DAC colour data.
- ULAplus is a separate 64-entry palette overlay for ZX-style attributes; it
  is not the absent BaseConf `256c` framebuffer.
- TV and VGA share the same rendered colour stream. `vga_on` selects either
  the direct TV-rate path or the line-buffered scandoubled VGA path and sync.
- The emulator currently renders a host frame. It does not emulate the
  physical DAC/sync/scandoubler path. B18 only reports AVR TV/VGA bit 0; it
  does not claim physical-output equivalence.

## Discrepancy map against the current emulator

### Present, but not yet accepted as exact

- All seven valid selector combinations route to a renderer class.
- The main page pairs and packed-pixel/attribute interpretations are present.
- Normal text and one-page text are separate render paths.
- ATM palette writes feed a shared 16-entry palette.

These facts are structural. They do not constitute runtime acceptance of
pixel phase, border placement, per-line fetch phase, mid-frame switching,
floating bus, contention or physical TV/VGA timing.

### Known naming/contract defects

- `AtmVideoMode.Evo256x192` actually selects Pentagon hardware multicolor.
  It must be renamed or documented as such before the TRD test menu exposes
  it.
- `EvoAlco16c` is the Pentagon 16c path; the name does not state its exact
  memory contract.
- The generic `256c` and `16+16c` catalogue entries have no decode, address,
  fetch or render implementation in BaseConf r1364. They cannot be treated as
  required r1364 modes or presented as already supported.

### Missing or approximate implementation work

- B17 deliberately applies raster changes at a software frame boundary,
  whereas the FPGA registers and counters have their own clocked transition
  semantics.
- Wide-mode horizontal start, border window, INT waveform and contention are
  still approximate in renderer coordinates.
- Mode writes do not yet have a single explicit BaseConf video controller
  responsible for renderer selection, timing epoch and deterministic
  mid-frame policy.
- Renderer memory-address tables are not yet guarded by mode-specific golden
  vectors derived from the RTL equations.
- There is no bootable TRD conformance suite yet.

## Consequence for the sequence

B20 must fix the shared raster/picture-window/border/INT foundation without
changing individual pixel formats. B21 may then introduce the explicit mode
controller and the first bootable `BCVIDTEST.TRD` shell. Only after those two
nodes should the implemented r1364 formats be accepted one at a time, each
with deterministic address/pixel vectors and a visual TRD screen.

The future test menu must describe the real r1364 modes:

1. ZX 256×192 attr;
2. Pentagon 256×192 hardware multicolor;
3. Pentagon 256×192 16c;
4. ATM 320×200 16c;
5. ATM 640×200 hardware multicolor;
6. ATM 80×25 text (two-page);
7. BaseConf 80×25 text (one-page).

`256c` and `16+16c` may be added only as explicitly non-r1364 extensions,
with a separate authoritative specification and separate implementation
nodes.
