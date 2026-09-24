# ZX-MultiSound Rev.A2 implementation note

This branch is based on the accepted stable v42 source (`aaf78b7`).  The
stable build directory is not modified.  The implementation target is the
latest public ZX-MultiSound hardware revision, Rev.A2.

## Primary references

- Hardware, schematic and CPLD RTL: <https://github.com/UzixLS/zx-multisound>
- RTL reference commit: `d7f3ac293724e1b69cbe9af9894311de4e07e4d1`
- General Sound firmware: <https://github.com/psbhlw/gs-firmware>
- GS ROM release: `v1.05b`
- GS compatibility timing reference: ZX-MultiSound CPLD commit `a696ecd`
- SAA1099 behavioural reference: SAASound 3.5 source,
  commit `3d92322030d1e5627cbf8295bed1efc872bad6eb`

The current RTL remains the authoritative source for address decoding and
jumpers.  Commit `a696ecd` is retained as a regression reference because the
board repository documents a GS timing regression in later CPLD revisions.

## Rev.A2 switches and decoded interfaces

The physical five-position configuration switch is modelled directly:

| Switch | RTL bit | Function |
|---|---:|---|
| YM/TSFM | `cfg[0]` | Two YM2203 chips and the TSFM selector |
| SAA | `cfg[1]` | SAA1099 at `#FF/#1FF` |
| GS | `cfg[2]` | General Sound at `#B3/#BB` |
| SounDrive | `cfg[3]` | Four DACs at `#0F/#1F/#4F/#5F` |
| Reserved | `cfg[4]` | Not connected in the current RTL |

YM address/data decoding is deliberately partial, exactly like the CPLD:
`A15:A14=11/10` and `A3:A0=#D`.  The SAA uses low byte `#FF` with `A8`
selecting address/data.  SounDrive uses low nibble `#F`, with `A7=0`, `A5=0`
and `{A6,A4}` selecting one of four DACs.  GS uses low bytes `#B3/#BB`, a
16 MHz private Z80 and 1 MiB RAM.  The GS interrupt follows the actual RTL
counter period of 321 clocks at 12 MHz (1284 CPLD master clocks), including
its 33-clock active interval (reload edge plus counter values 0 through 31).
SAA and SounDrive also reproduce the CPLD's
`rom_m1_access` latch: writes are blocked after an opcode fetch from
`#0000-#3FFF` until the next opcode fetch outside that range.

## v44 audio correction

Runtime testing of v43 found that SAA1099 produced an incomplete/garbled
melody, while GS and SounDrive did not produce a usable continuous stream.
The three failures had two independent causes.

- The private 16 MHz GS Z80 was executed in batches, but its DAC writes were
  stamped with the host Z80's unchanged frame position.  The common audio
  queue correctly rejects non-increasing timestamps, so almost all samples
  in a batch were discarded.  GS DAC events are now timestamped from the GS
  CPU's own progress and ordered with host SounDrive writes because both
  sources drive the same four physical DAC registers.
- The original managed SAA core was only a loose approximation.  It now
  follows the current SAASound rules for half-cycle-buffered tone data,
  tone/noise physical mixing, mode-3 noise clocking, the verified 18-bit
  Galois LFSR, envelope buffering and 3/4-bit resolution, PDM amplitude
  interaction, sync state and initial oscillator level.  It also uses the
  current library's default 64x internal oversampling before producing the
  44.1-kHz stream.

No external `SAASound.dll` is shipped.  The supplied DLL is 32-bit and would
add a platform-specific runtime dependency; only documented, source-verified
behaviour from the permissively licensed current implementation was ported
into the existing managed renderer.

The external SAM2695 MIDI synthesizer is a separate proprietary sound IC.
The public board sources document its clock and its connection to a YM2203
I/O port, but do not provide a synthesizer core, firmware or sample ROM that
can be executed by this emulator.  This first candidate therefore does not
claim SAM2695 audio emulation; YM/TSFM, SAA1099, GS 1.05b and SounDrive are
the implemented digital blocks.

## ZXBUS and conflict policy

Both ZX Evolution ZXBUS connectors are electrically equivalent.  Either one
can contain NeoGS or ZX-MultiSound; their order never changes emulation.
Duplicate boards of the same type are rejected because both copies would
decode the same ports.

Automatic mode is the safe default:

- MultiSound GS is disabled when NeoGS is installed, preventing `#B3/#BB`
  contention while retaining NeoGS MOD/MP3 support.
- Internal AY/TSFM is set to `None` when MultiSound YM/TSFM is enabled,
  preventing AY address/data contention.
- SAA and SounDrive remain available because their write-only aliases do not
  contend with the BaseConf Kempston read or internal Covox `#FB` path.

Manual mode exposes the four real switches.  A conflicting selection is not
silently accepted: Apply reports the exact collision and leaves the running
machine unchanged.

## Acceptance gates

1. XML round-trip and two-slot permutations.
2. Exact port-decode tests, reset state and automatic/manual conflict tests.
3. Official GS 1.05b boot/command tests, four-channel DAC verification and a
   private-Z80 waveform test that rejects collapsed timestamps; real MOD
   playback remains an emulator acceptance test.
4. Existing NeoGS, internal AY and TSFM regression tests.
5. Six independent SAA tone channels, noise and envelope vectors, plus an
   audible-rate SounDrive waveform test.
6. A single isolated test build only after all compile-time tests pass.
