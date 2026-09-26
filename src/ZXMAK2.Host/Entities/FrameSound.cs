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
        private readonly int[] _sourceGains;
        private readonly bool _hasSourceGain;
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
            : this(sampleRate, sources, rejectDc, masterVolume, null)
        {
        }

        public FrameSound(
            int sampleRate,
            IEnumerable<uint[]> sources,
            bool rejectDc,
            int masterVolume,
            IEnumerable<int> sourceGains)
        {
            _mixBuffer = new uint[(int)(sampleRate / 50D + 0.5D)];
            SampleRate = sampleRate;
            _sources = sources.ToArray();
            _rejectDc = rejectDc;
            _masterVolume = Math.Max(0, Math.Min(200, masterVolume));
            _sourceGains = sourceGains != null
                ? sourceGains.Select(gain => Math.Max(0, Math.Min(200, gain))).ToArray()
                : Enumerable.Repeat(100, _sources.Length).ToArray();
            if (_sourceGains.Length != _sources.Length)
                throw new ArgumentException("One gain is required for each source", "sourceGains");
            _hasSourceGain = _sourceGains.Any(gain => gain != 100);
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

        private unsafe void Mix(uint[] dst, uint[][] sources)
        {
            fixed (uint* puidst = dst)
            {
                var pdst = (short*)puidst;
                for (var i = 0; i < dst.Length; i++)
                {
                    var index = i * 2;
                    var left = 0;
                    var right = 0;
                    for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                    {
                        var src = sources[sourceIndex];
                        fixed (uint* puisrc = src)
                        {
                            var psrc = (short*)puisrc;
                            left += psrc[index] * _sourceGains[sourceIndex];
                            right += psrc[index + 1] * _sourceGains[sourceIndex];
                        }
                    }
                    var divisor = 100 * Math.Max(1, sources.Length);
                    left /= divisor;
                    right /= divisor;
                    // Limiting occurs only after the sources have been mixed.
                    // 100% sources retain the previous bit-exact arithmetic.
                    pdst[index] = _hasSourceGain ? SoftLimit(left) : (short)left;
                    pdst[index + 1] = _hasSourceGain ? SoftLimit(right) : (short)right;
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
                    samples[i] = _masterVolume > 100 ? SoftLimit(scaled) : (short)Math.Max(
                        short.MinValue,
                        Math.Min(short.MaxValue, scaled));
                }
            }
        }

        private static short SoftLimit(int value)
        {
            const int knee = 24576;
            var magnitude = Math.Abs(value);
            if (magnitude > knee)
            {
                var excess = magnitude - knee;
                var headroom = short.MaxValue - knee;
                magnitude = knee + (int)((long)headroom * excess /
                    (headroom + excess));
                value = value < 0 ? -magnitude : magnitude;
            }
            return (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));
        }

        #endregion Private
    }
}
