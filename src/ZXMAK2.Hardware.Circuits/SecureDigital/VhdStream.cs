using System;
using System.IO;


namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    /// <summary>
    /// Exposes the virtual disk payload of a legacy fixed or dynamic VHD as a stream.
    /// Differencing VHD images are intentionally rejected.
    /// </summary>
    internal sealed class VhdStream : Stream
    {
        private const int SectorSize = 512;
        private const UInt32 UnallocatedBlock = UInt32.MaxValue;

        private readonly FileStream m_stream;
        private readonly byte[] m_footer;
        private readonly long m_length;
        private readonly bool m_dynamic;
        private readonly long m_tableOffset;
        private readonly int m_blockSize;
        private readonly int m_bitmapSize;
        private readonly UInt32[] m_bat;
        private long m_position;

        private VhdStream(
            FileStream stream,
            byte[] footer,
            long length,
            bool isDynamic,
            long tableOffset,
            int blockSize,
            int bitmapSize,
            UInt32[] bat)
        {
            m_stream = stream;
            m_footer = footer;
            m_length = length;
            m_dynamic = isDynamic;
            m_tableOffset = tableOffset;
            m_blockSize = blockSize;
            m_bitmapSize = bitmapSize;
            m_bat = bat;
        }

        public static bool HasFooter(string fileName)
        {
            using (var stream = File.Open(
                fileName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite))
            {
                if (stream.Length < SectorSize)
                {
                    return false;
                }
                var footer = ReadBytes(stream, stream.Length - SectorSize, 8);
                return GetAscii(footer, 0, footer.Length) == "conectix";
            }
        }

        public static Stream Open(string fileName)
        {
            var stream = File.Open(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            try
            {
                if (stream.Length < SectorSize)
                {
                    throw new InvalidDataException("The VHD file is too small.");
                }

                var footer = ReadBytes(stream, stream.Length - SectorSize, SectorSize);
                if (GetAscii(footer, 0, 8) != "conectix")
                {
                    throw new InvalidDataException("The file does not contain a valid VHD footer.");
                }

                var currentSizeValue = ReadUInt64(footer, 48);
                if (currentSizeValue == 0 || currentSizeValue > Int64.MaxValue)
                {
                    throw new InvalidDataException("The VHD virtual disk size is invalid.");
                }
                var currentSize = (long)currentSizeValue;
                var diskType = ReadUInt32(footer, 60);

                if (diskType == 2)
                {
                    if (currentSize > stream.Length - SectorSize)
                    {
                        throw new InvalidDataException("The fixed VHD payload is incomplete.");
                    }
                    return new VhdStream(stream, footer, currentSize, false, 0, 0, 0, null);
                }

                if (diskType != 3)
                {
                    throw new NotSupportedException(
                        diskType == 4 ?
                        "Differencing VHD images are not supported." :
                        "Unsupported VHD disk type: " + diskType.ToString());
                }

                var headerOffsetValue = ReadUInt64(footer, 16);
                if (headerOffsetValue > Int64.MaxValue)
                {
                    throw new InvalidDataException("The VHD dynamic header offset is invalid.");
                }
                var headerOffset = (long)headerOffsetValue;
                var header = ReadBytes(stream, headerOffset, 1024);
                if (GetAscii(header, 0, 8) != "cxsparse")
                {
                    throw new InvalidDataException("The VHD dynamic header is invalid.");
                }

                var tableOffsetValue = ReadUInt64(header, 16);
                var maxTableEntriesValue = ReadUInt32(header, 28);
                var blockSizeValue = ReadUInt32(header, 32);
                if (tableOffsetValue > Int64.MaxValue ||
                    maxTableEntriesValue == 0 ||
                    maxTableEntriesValue > Int32.MaxValue ||
                    blockSizeValue < SectorSize ||
                    blockSizeValue > Int32.MaxValue ||
                    (blockSizeValue % SectorSize) != 0 ||
                    (blockSizeValue & (blockSizeValue - 1)) != 0)
                {
                    throw new InvalidDataException("The VHD block table parameters are invalid.");
                }

                var tableOffset = (long)tableOffsetValue;
                var maxTableEntries = (int)maxTableEntriesValue;
                var blockSize = (int)blockSizeValue;
                var requiredBlocks = ((currentSize - 1) / blockSize) + 1;
                if (requiredBlocks > maxTableEntries)
                {
                    throw new InvalidDataException("The VHD block table is too small.");
                }

                var sectorsPerBlock = blockSize / SectorSize;
                var bitmapBytes = (sectorsPerBlock + 7) / 8;
                var bitmapSize = RoundUp(bitmapBytes, SectorSize);
                var tableBytes = ReadBytes(stream, tableOffset, checked(maxTableEntries * 4));
                var bat = new UInt32[maxTableEntries];
                var firstDataOffset = RoundUp(
                    checked(tableOffset + maxTableEntries * 4L),
                    SectorSize);
                var lastDataOffset = stream.Length - SectorSize;
                for (var index = 0; index < bat.Length; index++)
                {
                    bat[index] = ReadUInt32(tableBytes, index * 4);
                    if (bat[index] != UnallocatedBlock)
                    {
                        var blockOffset = GetBlockOffset(bat[index]);
                        if (blockOffset < firstDataOffset ||
                            blockOffset > lastDataOffset - bitmapSize - blockSize)
                        {
                            throw new InvalidDataException("The VHD block table contains an invalid entry.");
                        }
                    }
                }

                return new VhdStream(
                    stream,
                    footer,
                    currentSize,
                    true,
                    tableOffset,
                    blockSize,
                    bitmapSize,
                    bat);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public override bool CanRead
        {
            get { return m_stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return m_stream.CanWrite; }
        }

        public override long Length
        {
            get { return m_length; }
        }

        public override long Position
        {
            get { return m_position; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                m_position = value;
            }
        }

        public override void Flush()
        {
            m_stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckBufferArguments(buffer, offset, count);
            if (count == 0 || m_position >= m_length)
            {
                return 0;
            }
            count = (int)Math.Min(count, m_length - m_position);

            if (!m_dynamic)
            {
                m_stream.Position = m_position;
                var read = ReadToBuffer(m_stream, buffer, offset, count);
                m_position += read;
                return read;
            }

            var total = 0;
            while (total < count)
            {
                var blockIndex = checked((int)(m_position / m_blockSize));
                var blockOffset = (int)(m_position % m_blockSize);
                var sectorIndex = blockOffset / SectorSize;
                var sectorOffset = blockOffset % SectorSize;
                var part = Math.Min(count - total, SectorSize - sectorOffset);
                var block = m_bat[blockIndex];

                if (block == UnallocatedBlock || !IsSectorPresent(block, sectorIndex))
                {
                    Array.Clear(buffer, offset + total, part);
                }
                else
                {
                    var physicalOffset = GetBlockOffset(block) + m_bitmapSize + blockOffset;
                    m_stream.Position = physicalOffset;
                    var read = ReadToBuffer(m_stream, buffer, offset + total, part);
                    if (read < part)
                    {
                        Array.Clear(buffer, offset + total + read, part - read);
                    }
                }

                total += part;
                m_position += part;
            }
            return total;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    position = offset;
                    break;
                case SeekOrigin.Current:
                    position = checked(m_position + offset);
                    break;
                case SeekOrigin.End:
                    position = checked(m_length + offset);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }
            if (position < 0)
            {
                throw new IOException("Cannot seek before the beginning of the VHD payload.");
            }
            m_position = position;
            return position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("Changing the VHD virtual disk size is not supported.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckBufferArguments(buffer, offset, count);
            if (m_position > m_length || count > m_length - m_position)
            {
                throw new IOException("A write would extend beyond the VHD virtual disk.");
            }

            if (!m_dynamic)
            {
                m_stream.Position = m_position;
                m_stream.Write(buffer, offset, count);
                m_position += count;
                return;
            }

            var total = 0;
            while (total < count)
            {
                var blockIndex = checked((int)(m_position / m_blockSize));
                var blockOffset = (int)(m_position % m_blockSize);
                var sectorIndex = blockOffset / SectorSize;
                var sectorOffset = blockOffset % SectorSize;
                var part = Math.Min(count - total, SectorSize - sectorOffset);
                var block = EnsureBlock(blockIndex);
                var physicalOffset = GetBlockOffset(block) + m_bitmapSize + blockOffset;

                if (!IsSectorPresent(block, sectorIndex) &&
                    (sectorOffset != 0 || part != SectorSize))
                {
                    ClearRange(
                        GetBlockOffset(block) + m_bitmapSize + sectorIndex * SectorSize,
                        SectorSize);
                }
                m_stream.Position = physicalOffset;
                m_stream.Write(buffer, offset + total, part);
                MarkSectorPresent(block, sectorIndex);

                total += part;
                m_position += part;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_stream.Dispose();
            }
            base.Dispose(disposing);
        }

        private UInt32 EnsureBlock(int blockIndex)
        {
            var block = m_bat[blockIndex];
            if (block != UnallocatedBlock)
            {
                return block;
            }

            var oldFooterOffset = m_stream.Length - SectorSize;
            var blockOffset = RoundUp(oldFooterOffset, SectorSize);
            var newFooterOffset = checked(blockOffset + m_bitmapSize + m_blockSize);
            var blockSector = blockOffset / SectorSize;
            if (blockSector > UInt32.MaxValue)
            {
                throw new IOException("The VHD file is too large for a legacy block table.");
            }

            m_stream.SetLength(checked(newFooterOffset + SectorSize));
            m_stream.Position = newFooterOffset;
            m_stream.Write(m_footer, 0, m_footer.Length);
            ClearRange(blockOffset, checked(m_bitmapSize + m_blockSize));

            block = (UInt32)blockSector;
            m_bat[blockIndex] = block;
            var entry = new byte[4];
            WriteUInt32(entry, 0, block);
            m_stream.Position = checked(m_tableOffset + blockIndex * 4L);
            m_stream.Write(entry, 0, entry.Length);
            m_stream.Flush();
            return block;
        }

        private void ClearRange(long offset, int count)
        {
            var zeros = new byte[Math.Min(count, 64 * 1024)];
            m_stream.Position = offset;
            while (count > 0)
            {
                var part = Math.Min(count, zeros.Length);
                m_stream.Write(zeros, 0, part);
                count -= part;
            }
        }

        private bool IsSectorPresent(UInt32 block, int sectorIndex)
        {
            var bitmapByteOffset = GetBlockOffset(block) + sectorIndex / 8;
            m_stream.Position = bitmapByteOffset;
            var value = m_stream.ReadByte();
            if (value < 0)
            {
                return false;
            }
            return (value & (1 << (7 - (sectorIndex & 7)))) != 0;
        }

        private void MarkSectorPresent(UInt32 block, int sectorIndex)
        {
            var bitmapByteOffset = GetBlockOffset(block) + sectorIndex / 8;
            m_stream.Position = bitmapByteOffset;
            var value = m_stream.ReadByte();
            if (value < 0)
            {
                value = 0;
            }
            var mask = 1 << (7 - (sectorIndex & 7));
            if ((value & mask) == 0)
            {
                m_stream.Position = bitmapByteOffset;
                m_stream.WriteByte((byte)(value | mask));
            }
        }

        private static long GetBlockOffset(UInt32 block)
        {
            return checked((long)block * SectorSize);
        }

        private static byte[] ReadBytes(Stream stream, long offset, int count)
        {
            if (offset < 0 || offset > stream.Length - count)
            {
                throw new InvalidDataException("The VHD contains an invalid file offset.");
            }
            var buffer = new byte[count];
            stream.Position = offset;
            if (ReadToBuffer(stream, buffer, 0, count) != count)
            {
                throw new EndOfStreamException("The VHD file ended unexpectedly.");
            }
            return buffer;
        }

        private static int ReadToBuffer(Stream stream, byte[] buffer, int offset, int count)
        {
            var total = 0;
            while (total < count)
            {
                var read = stream.Read(buffer, offset + total, count - total);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            return total;
        }

        private static string GetAscii(byte[] buffer, int offset, int count)
        {
            return System.Text.Encoding.ASCII.GetString(buffer, offset, count);
        }

        private static UInt32 ReadUInt32(byte[] buffer, int offset)
        {
            return ((UInt32)buffer[offset] << 24) |
                   ((UInt32)buffer[offset + 1] << 16) |
                   ((UInt32)buffer[offset + 2] << 8) |
                   buffer[offset + 3];
        }

        private static UInt64 ReadUInt64(byte[] buffer, int offset)
        {
            return ((UInt64)ReadUInt32(buffer, offset) << 32) |
                   ReadUInt32(buffer, offset + 4);
        }

        private static void WriteUInt32(byte[] buffer, int offset, UInt32 value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static int RoundUp(int value, int alignment)
        {
            return checked(((value + alignment - 1) / alignment) * alignment);
        }

        private static long RoundUp(long value, int alignment)
        {
            return checked(((value + alignment - 1) / alignment) * alignment);
        }

        private static void CheckBufferArguments(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }
        }
    }
}
