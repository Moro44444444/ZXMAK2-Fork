using System;
using System.Linq;
using System.Collections.Generic;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.Entities
{
    public class FrameSound : IFrameSound
    {
        private readonly uint[] _mixBuffer;
        private readonly bool _rejectDc;
        private readonly int _masterVolume;
        private uint[] _buffer;
        private uint[][] _sources;
        private short _dcPreviousInputLeft;
        private short _dcPreviousInputRight;
        private float _dcPreviousOutputLeft;
        private float _dcPreviousOutputRight;
        private bool _dcPrimed;

        public FrameSound(int sampleRate, IEnumerable<uint[]> sources)
            : this(sampleRate, sources, false)
        {
        }

        public FrameSound(
            int sampleRate,
            IEnumerable<uint[]> sources,
            bool rejectDc)
            : this(sampleRate, sources, rejectDc, 100)
        {
        }

        public FrameSound(
            int sampleRate,
            IEnumerable<uint[]> sources,
            bool rejectDc,
            int masterVolume)
        {
            _mixBuffer = new uint[(int)(sampleRate / 50D + 0.5D)];
            SampleRate = sampleRate;
            _sources = sources.ToArray();
            _rejectDc = rejectDc;
            _masterVolume = Math.Max(0, Math.Min(200, masterVolume));
        }

        #region ISoundFrame

        public int SampleRate { get; private set; }

        public void Refresh()
        {
            _buffer = null;
        }

        public uint[] GetBuffer()
        {
            if (_buffer != null)
            {
                return _buffer;
            }
            _buffer = _mixBuffer;
            Mix(_buffer, _sources);
            if (_rejectDc)
            {
                RejectDc(_buffer);
            }
            if (_masterVolume != 100)
            {
                ApplyMasterVolume(_buffer);
            }
            return _buffer;
        }

        #endregion ISoundFrame


        #region Private

        private unsafe static void Mix(uint[] dst, uint[][] sources)
        {
            fixed (uint* puidst = dst)
            {
                var pdst = (short*)puidst;
                for (var i = 0; i < dst.Length; i++)
                {
                    var index = i * 2;
                    var left = 0;
                    var right = 0;
                    foreach (var src in sources)
                    {
                        fixed (uint* puisrc = src)
                        {
                            var psrc = (short*)puisrc;
                            left += psrc[index];
                            right += psrc[index + 1];
                        }
                    }
                    if (sources.Length > 1)
                    {
                        left /= sources.Length;
                        right /= sources.Length;
                    }
                    pdst[index] = (short)left;
                    pdst[index + 1] = (short)right;
                }
            }
        }

        private unsafe void RejectDc(uint[] buffer)
        {
            // UnrealSpeccy-compatible final high-pass. State intentionally
            // survives Refresh() so every host frame continues the same stream.
            fixed (uint* puiBuffer = buffer)
            {
                var samples = (short*)puiBuffer;
                for (var i = 0; i < buffer.Length; i++)
                {
                    var index = i * 2;
                    var inputLeft = samples[index];
                    var inputRight = samples[index + 1];
                    if (!_dcPrimed)
                    {
                        _dcPreviousInputLeft = inputLeft;
                        _dcPreviousInputRight = inputRight;
                        samples[index] = 0;
                        samples[index + 1] = 0;
                        _dcPrimed = true;
                        continue;
                    }
                    var outputLeft =
                        0.995F * (inputLeft - _dcPreviousInputLeft) +
                        0.99F * _dcPreviousOutputLeft;
                    var outputRight =
                        0.995F * (inputRight - _dcPreviousInputRight) +
                        0.99F * _dcPreviousOutputRight;

                    _dcPreviousInputLeft = inputLeft;
                    _dcPreviousInputRight = inputRight;
                    _dcPreviousOutputLeft = outputLeft;
                    _dcPreviousOutputRight = outputRight;
                    samples[index] = ClampToInt16(outputLeft);
                    samples[index + 1] = ClampToInt16(outputRight);
                }
            }
        }

        private static short ClampToInt16(float value)
        {
            if (value > short.MaxValue)
            {
                return short.MaxValue;
            }
            if (value < short.MinValue)
            {
                return short.MinValue;
            }
            return (short)value;
        }

        private unsafe void ApplyMasterVolume(uint[] buffer)
        {
            // Apply once to the final stereo mix, after DC rejection. A soft
            // knee avoids the hard clipping caused by boosting loud material.
            fixed (uint* puiBuffer = buffer)
            {
                var samples = (short*)puiBuffer;
                for (var i = 0; i < buffer.Length * 2; i++)
                {
                    var scaled = (int)Math.Round(
                        samples[i] * _masterVolume / 100.0);
                    if (_masterVolume > 100)
                    {
                        const int knee = 24576;
                        var magnitude = Math.Abs(scaled);
                        if (magnitude > knee)
                        {
                            var excess = magnitude - knee;
                            var headroom = short.MaxValue - knee;
                            magnitude = knee +
                                (int)((long)headroom * excess /
                                    (headroom + excess));
                            scaled = scaled < 0 ? -magnitude : magnitude;
                        }
                    }
                    samples[i] = (short)Math.Max(
                        short.MinValue,
                        Math.Min(short.MaxValue, scaled));
                }
            }
        }

        #endregion Private
    }
}
