using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NLayer;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// VS1011-compatible serial control/data front end used by NeoGS C-VS.
    /// MPEG decoding is performed by the managed NLayer decoder.  The input
    /// and PCM queues deliberately remain bounded so DREQ follows playback
    /// consumption instead of accepting an entire file instantaneously.
    /// </summary>
    internal sealed class NeoGsVs10xx : IDisposable
    {
        private const int EncodedCapacity = 32 * 1024;
        private const int DreqReserve = 32;
        private const int PcmCapacityFrames = 96000;

        private const int RegisterMode = 0;
        private const int RegisterStatus = 1;
        private const int RegisterClockF = 3;
        private const int RegisterDecodeTime = 4;
        private const int RegisterAudioData = 5;
        private const int RegisterHdat0 = 8;
        private const int RegisterHdat1 = 9;
        private const int RegisterAiAddr = 10;
        private const int RegisterVolume = 11;
        private const int RegisterAiCtrl0 = 12;
        private const int RegisterAiCtrl1 = 13;
        private const int RegisterAiCtrl2 = 14;
        private const int RegisterAiCtrl3 = 15;
        private const ushort ModeSoftwareReset = 0x0004;

        private readonly object m_sync = new object();
        private readonly ushort[] m_registers = new ushort[16];
        private readonly Queue<PcmFrame> m_pcm = new Queue<PcmFrame>();

        private EncodedStream m_encoded;
        private Thread m_decoderThread;
        private bool m_disposed;
        private bool m_resetReleased;
        private bool m_controlSelected;
        private ControlState m_controlState;
        private int m_controlRegister;
        private byte m_controlMsb;
        private int m_sampleRate;
        private int m_channels;
        private long m_playedFrames;

        public NeoGsVs10xx()
        {
            HardwareReset(false);
        }

        public bool DataRequest
        {
            get
            {
                lock (m_sync)
                {
                    return m_resetReleased && m_encoded != null &&
                        m_encoded.FreeCount >= DreqReserve;
                }
            }
        }

        public int SampleRate
        {
            get { lock (m_sync) { return m_sampleRate; } }
        }

        public void HardwareReset(bool released)
        {
            StopDecoder();
            lock (m_sync)
            {
                Array.Clear(m_registers, 0, m_registers.Length);
                // VS1011E reports version 2 and starts in SDI-new mode.
                m_registers[RegisterMode] = 0x0800;
                m_registers[RegisterStatus] = 0x0020;
                m_registers[RegisterVolume] = 0x0000;
                m_controlState = ControlState.Idle;
                m_controlSelected = false;
                m_sampleRate = 0;
                m_channels = 0;
                m_playedFrames = 0;
                m_pcm.Clear();
                m_resetReleased = released;
                if (released && !m_disposed)
                    StartDecoderLocked();
            }
        }

        public void SetControlSelected(bool selected)
        {
            lock (m_sync)
            {
                m_controlSelected = selected;
                if (!selected)
                    m_controlState = ControlState.Idle;
            }
        }

        public void WriteControl(byte value)
        {
            lock (m_sync)
            {
                if (!m_resetReleased || !m_controlSelected)
                    return;
                switch (m_controlState)
                {
                    case ControlState.Idle:
                        if (value == 2)
                            m_controlState = ControlState.WriteIndex;
                        else if (value == 3)
                            m_controlState = ControlState.ReadIndex;
                        break;
                    case ControlState.WriteIndex:
                        m_controlRegister = value & 15;
                        m_controlState = ControlState.WriteMsb;
                        break;
                    case ControlState.WriteMsb:
                        m_controlMsb = value;
                        m_controlState = ControlState.WriteLsb;
                        break;
                    case ControlState.WriteLsb:
                        WriteRegisterLocked(
                            m_controlRegister,
                            (ushort)((m_controlMsb << 8) | value));
                        m_controlState = ControlState.Idle;
                        break;
                    case ControlState.ReadIndex:
                        m_controlRegister = value & 15;
                        m_controlState = ControlState.ReadMsb;
                        break;
                }
            }
        }

        public byte ReadControl()
        {
            lock (m_sync)
            {
                if (!m_resetReleased || !m_controlSelected)
                    return 0xFF;
                var value = ReadRegisterLocked(m_controlRegister);
                if (m_controlState == ControlState.ReadMsb)
                {
                    m_controlState = ControlState.ReadLsb;
                    return (byte)(value >> 8);
                }
                if (m_controlState == ControlState.ReadLsb)
                {
                    m_controlState = ControlState.Idle;
                    return (byte)value;
                }
                return 0xFF;
            }
        }

        public bool WriteData(byte value)
        {
            lock (m_sync)
            {
                if (!m_resetReleased || m_encoded == null ||
                    m_encoded.FreeCount == 0)
                    return false;
                m_encoded.WriteByte(value);
                return true;
            }
        }

        public bool TryReadSample(out short left, out short right)
        {
            lock (m_sync)
            {
                if (m_pcm.Count == 0)
                {
                    left = 0;
                    right = 0;
                    return false;
                }
                var frame = m_pcm.Dequeue();
                Monitor.PulseAll(m_sync);
                m_playedFrames++;
                var leftGain = GetVolumeGain((byte)(m_registers[RegisterVolume] >> 8));
                var rightGain = GetVolumeGain((byte)m_registers[RegisterVolume]);
                left = Clamp16((int)(frame.Left * leftGain));
                right = Clamp16((int)(frame.Right * rightGain));
                return true;
            }
        }

        public void Dispose()
        {
            m_disposed = true;
            StopDecoder();
        }

        private void WriteRegisterLocked(int index, ushort value)
        {
            switch (index)
            {
                case RegisterMode:
                    m_registers[index] = value;
                    if ((value & ModeSoftwareReset) != 0)
                    {
                        ThreadPool.QueueUserWorkItem(delegate
                        {
                            SoftwareReset();
                        });
                    }
                    break;
                case RegisterStatus:
                    // Version bits are read-only on VS1011.
                    m_registers[index] = (ushort)((value & 0xFF0F) | 0x0020);
                    break;
                case RegisterClockF:
                case RegisterAiAddr:
                case RegisterVolume:
                case RegisterAiCtrl0:
                case RegisterAiCtrl1:
                case RegisterAiCtrl2:
                case RegisterAiCtrl3:
                    m_registers[index] = value;
                    break;
                case 2: // BASS
                case 6: // WRAM
                case 7: // WRAMADDR
                    m_registers[index] = value;
                    break;
            }
        }

        private ushort ReadRegisterLocked(int index)
        {
            if (index == RegisterDecodeTime)
            {
                if (m_sampleRate <= 0)
                    return 0;
                return (ushort)Math.Min(
                    ushort.MaxValue,
                    m_playedFrames / m_sampleRate);
            }
            if (index == RegisterAudioData && m_sampleRate > 0)
                return (ushort)Math.Min(ushort.MaxValue, m_sampleRate);
            if (index == RegisterHdat0 || index == RegisterHdat1)
                return m_registers[index];
            return m_registers[index & 15];
        }

        private void SoftwareReset()
        {
            StopDecoder();
            lock (m_sync)
            {
                if (m_disposed || !m_resetReleased)
                    return;
                m_pcm.Clear();
                m_sampleRate = 0;
                m_channels = 0;
                m_playedFrames = 0;
                m_registers[RegisterMode] &= unchecked((ushort)~ModeSoftwareReset);
                StartDecoderLocked();
            }
        }

        private void StartDecoderLocked()
        {
            m_encoded = new EncodedStream(EncodedCapacity);
            m_decoderThread = new Thread(DecodeLoop);
            m_decoderThread.IsBackground = true;
            m_decoderThread.Name = "NeoGS VS1011 decoder";
            m_decoderThread.Start(m_encoded);
        }

        private void StopDecoder()
        {
            Thread thread;
            EncodedStream encoded;
            lock (m_sync)
            {
                thread = m_decoderThread;
                encoded = m_encoded;
                m_decoderThread = null;
                m_encoded = null;
                if (encoded != null)
                    encoded.Cancel();
                Monitor.PulseAll(m_sync);
            }
            if (thread != null && thread != Thread.CurrentThread)
                thread.Join(1000);
            if (encoded != null)
                encoded.Dispose();
        }

        private void DecodeLoop(object state)
        {
            var encoded = (EncodedStream)state;
            var decoder = new MpegFrameDecoder();
            try
            {
                uint header = 0;
                var headerBytes = 0;
                while (!encoded.Cancelled)
                {
                    var next = encoded.ReadByteBlocking();
                    if (next < 0)
                        return;
                    header = (header << 8) | (byte)next;
                    if (headerBytes < 4)
                        headerBytes++;
                    if (headerBytes < 4)
                        continue;

                    var frameLength = StreamingMpegFrame.GetFrameLength(header);
                    if (frameLength <= 4)
                        continue;

                    var frameData = new byte[frameLength];
                    frameData[0] = (byte)(header >> 24);
                    frameData[1] = (byte)(header >> 16);
                    frameData[2] = (byte)(header >> 8);
                    frameData[3] = (byte)header;
                    if (!encoded.ReadExactly(
                        frameData,
                        4,
                        frameData.Length - 4))
                        return;

                    var frame = new StreamingMpegFrame(frameData, header);
                    var channels = frame.ChannelMode == MpegChannelMode.Mono
                        ? 1
                        : 2;
                    var samples = new float[frame.SampleCount * channels];
                    int count;
                    try
                    {
                        count = decoder.DecodeFrame(frame, samples, 0);
                    }
                    catch
                    {
                        decoder.Reset();
                        header = 0;
                        headerBytes = 0;
                        continue;
                    }

                    lock (m_sync)
                    {
                        if (m_encoded != encoded)
                            return;
                        m_sampleRate = frame.SampleRate;
                        m_channels = channels;
                        m_registers[RegisterAudioData] =
                            (ushort)Math.Min(ushort.MaxValue, frame.SampleRate);
                        m_registers[RegisterHdat0] =
                            (ushort)Math.Min(ushort.MaxValue, frame.BitRate / 1000);
                        m_registers[RegisterHdat1] = (ushort)(header >> 16);
                    }

                    var frameCount = count / channels;
                    var offset = 0;
                    for (var i = 0; i < frameCount; i++)
                    {
                        var left = FloatToShort(samples[offset]);
                        var right = channels > 1
                            ? FloatToShort(samples[offset + 1])
                            : left;
                        offset += channels;
                        lock (m_sync)
                        {
                            while (!encoded.Cancelled &&
                                m_pcm.Count >= PcmCapacityFrames)
                                Monitor.Wait(m_sync, 25);
                            if (encoded.Cancelled || m_encoded != encoded)
                                return;
                            m_pcm.Enqueue(new PcmFrame(left, right));
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Normal when a hardware/software reset cancels the stream.
            }
            catch (ObjectDisposedException)
            {
                // Normal during card removal or machine shutdown.
            }
            catch
            {
                // A damaged stream must behave like a decoder that stopped
                // consuming data; the guest firmware may then reset it.
            }
        }

        /// <summary>
        /// A complete MPEG frame backed by the bytes just received through SDI.
        /// NLayer's MpegFile scans to end-of-stream before returning from its
        /// constructor on older .NET builds, which is unsuitable for a live
        /// VS1011 data pin.  Feeding MpegFrameDecoder one frame at a time keeps
        /// the bit reservoir while allowing playback to start immediately.
        /// </summary>
        private sealed class StreamingMpegFrame : IMpegFrame
        {
            private static readonly int[][] Mpeg1BitRates =
            {
                new[] { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448, 0 },
                new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384, 0 },
                new[] { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0 },
            };

            private static readonly int[][] Mpeg2BitRates =
            {
                new[] { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256, 0 },
                new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
                new[] { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0 },
            };

            private readonly byte[] m_data;
            private readonly uint m_header;
            private int m_readOffset;
            private int m_bitsRead;
            private ulong m_bitBucket;

            public StreamingMpegFrame(byte[] data, uint header)
            {
                m_data = data;
                m_header = header;
                Reset();
            }

            public static int GetFrameLength(uint header)
            {
                if (!IsHeader(header))
                    return 0;
                var version = GetVersion(header);
                var layer = GetLayer(header);
                var bitRateIndex = (int)((header >> 12) & 15);
                if (bitRateIndex == 0 || bitRateIndex == 15)
                    return 0;
                var layerIndex = (int)layer - 1;
                var bitRates = version == MpegVersion.Version1
                    ? Mpeg1BitRates
                    : Mpeg2BitRates;
                var bitRate = bitRates[layerIndex][bitRateIndex] * 1000;
                var sampleRate = GetSampleRate(header, version);
                var padding = (int)((header >> 9) & 1);
                if (sampleRate == 0 || bitRate == 0)
                    return 0;
                if (layer == MpegLayer.LayerI)
                    return (12 * bitRate / sampleRate + padding) * 4;
                if (layer == MpegLayer.LayerIII &&
                    version != MpegVersion.Version1)
                    return 72 * bitRate / sampleRate + padding;
                return 144 * bitRate / sampleRate + padding;
            }

            private static bool IsHeader(uint header)
            {
                if ((header & 0xFFE00000U) != 0xFFE00000U)
                    return false;
                if ((header & 0x00180000U) == 0x00080000U)
                    return false;
                if ((header & 0x00060000U) == 0)
                    return false;
                if ((header & 0x0000F000U) == 0x0000F000U)
                    return false;
                return (header & 0x00000C00U) != 0x00000C00U;
            }

            private static MpegVersion GetVersion(uint header)
            {
                switch ((header >> 19) & 3)
                {
                    case 0: return MpegVersion.Version25;
                    case 2: return MpegVersion.Version2;
                    case 3: return MpegVersion.Version1;
                    default: return MpegVersion.Unknown;
                }
            }

            private static MpegLayer GetLayer(uint header)
            {
                switch ((header >> 17) & 3)
                {
                    case 3: return MpegLayer.LayerI;
                    case 2: return MpegLayer.LayerII;
                    case 1: return MpegLayer.LayerIII;
                    default: return MpegLayer.Unknown;
                }
            }

            private static int GetSampleRate(
                uint header,
                MpegVersion version)
            {
                int rate;
                switch ((header >> 10) & 3)
                {
                    case 0: rate = 44100; break;
                    case 1: rate = 48000; break;
                    case 2: rate = 32000; break;
                    default: return 0;
                }
                if (version == MpegVersion.Version2)
                    rate /= 2;
                else if (version == MpegVersion.Version25)
                    rate /= 4;
                return rate;
            }

            public int SampleRate { get { return GetSampleRate(m_header, Version); } }
            public int SampleRateIndex { get { return (int)((m_header >> 10) & 3); } }
            public int FrameLength { get { return m_data.Length; } }
            public int BitRate
            {
                get
                {
                    var rates = Version == MpegVersion.Version1
                        ? Mpeg1BitRates
                        : Mpeg2BitRates;
                    return rates[(int)Layer - 1][BitRateIndex] * 1000;
                }
            }
            public MpegVersion Version { get { return GetVersion(m_header); } }
            public MpegLayer Layer { get { return GetLayer(m_header); } }
            public MpegChannelMode ChannelMode
            {
                get { return (MpegChannelMode)((m_header >> 6) & 3); }
            }
            public int ChannelModeExtension { get { return (int)((m_header >> 4) & 3); } }
            public int SampleCount
            {
                get
                {
                    if (Layer == MpegLayer.LayerI)
                        return 384;
                    if (Layer == MpegLayer.LayerIII &&
                        Version != MpegVersion.Version1)
                        return 576;
                    return 1152;
                }
            }
            public int BitRateIndex { get { return (int)((m_header >> 12) & 15); } }
            public bool IsCopyrighted { get { return (m_header & 8) != 0; } }
            public bool HasCrc { get { return (m_header & 0x10000) == 0; } }
            public bool IsCorrupted { get { return false; } }

            public void Reset()
            {
                m_readOffset = 4 + (HasCrc ? 2 : 0);
                m_bitsRead = 0;
                m_bitBucket = 0;
            }

            public int ReadBits(int bitCount)
            {
                if (bitCount < 1 || bitCount > 32)
                    throw new ArgumentOutOfRangeException("bitCount");
                while (m_bitsRead < bitCount)
                {
                    if (m_readOffset >= m_data.Length)
                        throw new EndOfStreamException();
                    m_bitBucket = (m_bitBucket << 8) | m_data[m_readOffset++];
                    m_bitsRead += 8;
                }
                var mask = bitCount == 32
                    ? uint.MaxValue
                    : (1UL << bitCount) - 1UL;
                var result = (int)((m_bitBucket >> (m_bitsRead - bitCount)) & mask);
                m_bitsRead -= bitCount;
                return result;
            }
        }

        private static float GetVolumeGain(byte attenuation)
        {
            if (attenuation >= 0xFE)
                return 0F;
            return (float)Math.Pow(10D, -(attenuation * 0.5D) / 20D);
        }

        private static short FloatToShort(float value)
        {
            if (value < -1F)
                value = -1F;
            if (value > 1F)
                value = 1F;
            return Clamp16((int)(value * 32767F));
        }

        private static short Clamp16(int value)
        {
            if (value < short.MinValue)
                return short.MinValue;
            if (value > short.MaxValue)
                return short.MaxValue;
            return (short)value;
        }

        private enum ControlState
        {
            Idle,
            WriteIndex,
            WriteMsb,
            WriteLsb,
            ReadIndex,
            ReadMsb,
            ReadLsb,
        }

        private struct PcmFrame
        {
            public PcmFrame(short left, short right)
            {
                Left = left;
                Right = right;
            }

            public readonly short Left;
            public readonly short Right;
        }

        private sealed class EncodedStream : Stream
        {
            private readonly object m_sync = new object();
            private readonly byte[] m_buffer;
            private int m_read;
            private int m_write;
            private int m_count;
            private bool m_cancelled;

            public EncodedStream(int capacity)
            {
                m_buffer = new byte[capacity];
            }

            public int FreeCount
            {
                get { lock (m_sync) { return m_buffer.Length - m_count; } }
            }

            public bool Cancelled
            {
                get { lock (m_sync) { return m_cancelled; } }
            }

            public override void WriteByte(byte value)
            {
                lock (m_sync)
                {
                    if (m_cancelled || m_count == m_buffer.Length)
                        return;
                    m_buffer[m_write] = value;
                    m_write = (m_write + 1) % m_buffer.Length;
                    m_count++;
                    Monitor.PulseAll(m_sync);
                }
            }

            public void Cancel()
            {
                lock (m_sync)
                {
                    m_cancelled = true;
                    Monitor.PulseAll(m_sync);
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                lock (m_sync)
                {
                    while (!m_cancelled && m_count == 0)
                        Monitor.Wait(m_sync);
                    if (m_cancelled)
                        return 0;
                    var length = Math.Min(count, m_count);
                    var first = Math.Min(length, m_buffer.Length - m_read);
                    Buffer.BlockCopy(m_buffer, m_read, buffer, offset, first);
                    if (length > first)
                        Buffer.BlockCopy(
                            m_buffer, 0, buffer, offset + first, length - first);
                    m_read = (m_read + length) % m_buffer.Length;
                    m_count -= length;
                    return length;
                }
            }

            public int ReadByteBlocking()
            {
                lock (m_sync)
                {
                    while (!m_cancelled && m_count == 0)
                        Monitor.Wait(m_sync);
                    if (m_cancelled)
                        return -1;
                    var value = m_buffer[m_read];
                    m_read = (m_read + 1) % m_buffer.Length;
                    m_count--;
                    return value;
                }
            }

            public bool ReadExactly(byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    var value = ReadByteBlocking();
                    if (value < 0)
                        return false;
                    buffer[offset++] = (byte)value;
                    count--;
                }
                return true;
            }

            protected override void Dispose(bool disposing)
            {
                Cancel();
                base.Dispose(disposing);
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }
            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }
            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
