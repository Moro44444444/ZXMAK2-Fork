using System;
using System.IO;


namespace ZXMAK2.Hardware.Circuits.SecureDigital
{
    /// <summary>
    /// SD Card emulator
    /// Written by ZEK
    /// </summary>
    public class SdCard
    {
        private const int SectorSize = 512;
        private const long MaxImageLength = 8L * 1024L * 1024L * 1024L;
        private const long SdScMaxLength = 2L * 1024L * 1024L * 1024L;

        #region Fields

        private readonly byte[] cid;
        private readonly byte[] csd;
        private readonly byte[] buff;

        private SdState currState;
        private SdCommand cmd;

        private UInt32 argCnt;
        private UInt32 ocrCnt;
        private UInt32 r7_Cnt;


        private UInt32 cidCnt;
        private UInt32 csdCnt;

        private bool appCmd;
        private bool highCapacity;

        private int dataBlockLen;
        private UInt32 dataCnt;
        private long wrPos;

        private UInt32 arg;

        private Stream fstream;
        private string mountedFileName;

        #endregion Fields


        public SdCard()
        {
            cid = new byte[16] { 0x00, (byte)'U', (byte)'S', (byte)'U', (byte)'S', (byte)'3', (byte)'7', (byte)'6', 0x03, 0x12, 0x34, 0x56, 0x78, 0x00, 0xC1, 0x0F };
            csd = new byte[16] { 0x00, 0x0E, 0x00, 0x32, 0x5B, 0x59, 0x03, 0xFF, 0xED, 0xB7, 0xBF, 0xBF, 0x06, 0x40, 0x00, 0xF5 };

            buff = new byte[4096];
        }

        public string MountedFileName
        {
            get { return mountedFileName; }
        }

        public void Open(string fname)
        {
            if (!File.Exists(fname))
            {
                throw new FileNotFoundException("The SD Card image file was not found.", fname);
            }

            var fullName = Path.GetFullPath(fname);
            if (fstream != null &&
                string.Equals(
                    mountedFileName,
                    fullName,
                    StringComparison.OrdinalIgnoreCase))
            {
                Reset();
                return;
            }

            Stream newStream = null;
            try
            {
                newStream = OpenImageStream(fullName);
                ValidateImage(newStream);

                // Keep the currently mounted image intact until the replacement
                // has been opened and validated successfully.
                var oldStream = fstream;

                fstream = newStream;
                mountedFileName = fullName;
                newStream = null;
                UpdateCardCapacity(fstream.Length);
                Reset();

                if (oldStream != null)
                {
                    oldStream.Dispose();
                }
            }
            finally
            {
                if (newStream != null)
                {
                    newStream.Dispose();
                }
            }
        }

        private static Stream OpenImageStream(string fileName)
        {
            if (string.Compare(Path.GetExtension(fileName), ".vhd", true) == 0 &&
                VhdStream.HasFooter(fileName))
            {
                return VhdStream.Open(fileName);
            }

            // UnrealSpeccy treats its official sd_nedo.vhd as a raw sector
            // stream.  Preserve that compatibility when no VHD footer exists.
            return File.Open(
                fileName,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.Read);
        }

        private static void ValidateImage(Stream stream)
        {
            if (stream.Length < SectorSize)
            {
                throw new InvalidDataException("The SD Card image is too small.");
            }
            if ((stream.Length % SectorSize) != 0)
            {
                throw new InvalidDataException("The SD Card image size is not a multiple of 512 bytes.");
            }
            if (stream.Length > MaxImageLength)
            {
                throw new NotSupportedException("SD Card images larger than 8 GiB are not supported.");
            }

            var sector = new byte[SectorSize];
            stream.Position = 0;
            ReadFully(stream, sector, 0, sector.Length);
            stream.Position = 0;

            if (sector[510] != 0x55 || sector[511] != 0xAA)
            {
                throw new InvalidDataException(
                    "The SD Card image has no valid MBR or FAT boot-sector signature.");
            }
            if (!HasValidPartition(stream.Length, sector) &&
                !HasValidFatBootSector(stream.Length, sector))
            {
                throw new InvalidDataException(
                    "The SD Card image does not contain a compatible partition table or FAT volume.");
            }
        }

        private static bool HasValidPartition(long imageLength, byte[] sector)
        {
            var imageSectors = (UInt64)(imageLength / SectorSize);
            for (var index = 0; index < 4; index++)
            {
                var offset = 446 + index * 16;
                var status = sector[offset];
                var type = sector[offset + 4];
                var firstSector = ReadUInt32LittleEndian(sector, offset + 8);
                var sectorCount = ReadUInt32LittleEndian(sector, offset + 12);
                if ((status == 0x00 || status == 0x80) &&
                    type != 0 &&
                    firstSector != 0 &&
                    sectorCount != 0 &&
                    (UInt64)firstSector + sectorCount <= imageSectors)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasValidFatBootSector(long imageLength, byte[] sector)
        {
            if (sector[0] != 0xE9 && sector[0] != 0xEB)
            {
                return false;
            }

            var bytesPerSector = ReadUInt16LittleEndian(sector, 11);
            var sectorsPerCluster = sector[13];
            var reservedSectors = ReadUInt16LittleEndian(sector, 14);
            var fatCount = sector[16];
            var totalSectors16 = ReadUInt16LittleEndian(sector, 19);
            var totalSectors32 = ReadUInt32LittleEndian(sector, 32);
            var totalSectors = totalSectors16 != 0 ? totalSectors16 : totalSectors32;
            var imageSectors = (UInt64)(imageLength / SectorSize);

            return bytesPerSector == SectorSize &&
                sectorsPerCluster != 0 &&
                sectorsPerCluster <= 128 &&
                (sectorsPerCluster & (sectorsPerCluster - 1)) == 0 &&
                reservedSectors != 0 &&
                fatCount != 0 &&
                totalSectors != 0 &&
                totalSectors <= imageSectors;
        }

        private static UInt16 ReadUInt16LittleEndian(byte[] data, int offset)
        {
            return (UInt16)(data[offset] | (data[offset + 1] << 8));
        }

        private static UInt32 ReadUInt32LittleEndian(byte[] data, int offset)
        {
            return (UInt32)(
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
                count -= read;
            }
        }

        private void UpdateCardCapacity(long imageLength)
        {
            highCapacity = imageLength > SdScMaxLength;
            if (highCapacity)
            {
                SetHighCapacityCsd(imageLength);
            }
            else
            {
                SetStandardCapacityCsd(imageLength);
            }
        }

        private void SetHighCapacityCsd(long imageLength)
        {
            Array.Clear(csd, 0, csd.Length);
            csd[0] = 0x40; // CSD version 2.0
            csd[1] = 0x0E;
            csd[3] = 0x32;
            csd[4] = 0x5B;
            csd[5] = 0x59;

            var capacityUnits = (UInt64)(imageLength + 512L * 1024L - 1) /
                (512UL * 1024UL);
            var cSize = capacityUnits - 1;
            csd[7] = (byte)((cSize >> 16) & 0x3F);
            csd[8] = (byte)(cSize >> 8);
            csd[9] = (byte)cSize;
            csd[10] = 0x7E;
            csd[11] = 0x80;
            csd[12] = 0x0A;
            csd[13] = 0x40;
            csd[15] = 0x01;
        }

        private void SetStandardCapacityCsd(long imageLength)
        {
            var readBlockLength = 9;
            var sizeMultiplier = 0;
            UInt64 cSize = 0;
            var found = false;

            for (readBlockLength = 9; readBlockLength <= 11 && !found; readBlockLength++)
            {
                for (sizeMultiplier = 0; sizeMultiplier <= 7; sizeMultiplier++)
                {
                    var unitSize = (1UL << readBlockLength) << (sizeMultiplier + 2);
                    var units = ((UInt64)imageLength + unitSize - 1) / unitSize;
                    if (units <= 4096)
                    {
                        cSize = units - 1;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                throw new NotSupportedException("The SD Card image capacity cannot be represented as SDSC.");
            }
            readBlockLength--;

            csd[0] = 0x00; // CSD version 1.0
            csd[1] = 0x0E;
            csd[2] = 0x00;
            csd[3] = 0x32;
            csd[4] = 0x5B;
            csd[5] = (byte)(0x50 | readBlockLength);
            csd[6] = (byte)((cSize >> 10) & 0x03);
            csd[7] = (byte)(cSize >> 2);
            csd[8] = (byte)(((cSize & 0x03) << 6) | 0x2D);
            csd[9] = (byte)(0xB4 | ((sizeMultiplier >> 1) & 0x03));
            csd[10] = (byte)(0x3F | ((sizeMultiplier & 0x01) << 7));
            csd[11] = 0xBF;
            csd[12] = 0x06;
            csd[13] = 0x40;
            csd[14] = 0x00;
            csd[15] = 0xF5;
        }

        public void Close()
        {
            var stream = fstream;
            fstream = null;
            mountedFileName = null;
            if (stream != null)
            {
                try
                {
                    stream.Flush();
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }

        public void Reset()
        {
            currState = SdState.IDLE;
            argCnt = 0;
            cmd = SdCommand.INVALID;
            dataBlockLen = 512;
            dataCnt = 0;

            csdCnt = 0;
            cidCnt = 0;
            ocrCnt = 0;
            r7_Cnt = 0;

            appCmd = false;
            wrPos = -1;
        }

        public void Wr(byte val)
        {
            SdState NextState = SdState.IDLE;

            if (fstream == null)
                return;

            switch (currState)
            {
                case SdState.IDLE:
                    {
                        if ((val & 0xC0) != 0x40)
                            break;

                        cmd = (SdCommand)(val & 0x3F);
                        if (!appCmd)
                        {
                            switch (cmd)
                            {

                                case SdCommand.SEND_CSD:
                                    csdCnt = 0;
                                    break;

                                case SdCommand.SEND_CID:
                                    cidCnt = 0;
                                    break;

                                case SdCommand.READ_OCR:
                                    ocrCnt = 0;
                                    break;

                                case SdCommand.SEND_IF_COND:
                                    r7_Cnt = 0;
                                    break;
                            }
                        }
                        NextState = SdState.RD_ARG;
                        argCnt = 0;
                    }
                    break;

                case SdState.RD_ARG:
                    NextState = SdState.RD_ARG;
                    SetByte(ref arg, 3 - argCnt++, val);

                    if (argCnt == 4)
                    {
                        if (!appCmd)
                        {
                            switch (cmd)
                            {
                                case SdCommand.SET_BLOCKLEN:
                                    if (highCapacity)
                                    {
                                        dataBlockLen = SectorSize;
                                    }
                                    else if (arg > 0 && arg <= buff.Length)
                                    {
                                        dataBlockLen = (int)arg;
                                    }
                                    break;

                                case SdCommand.READ_SINGLE_BLOCK:
                                    ReadDataBlock(GetDataOffset(arg));
                                    break;

                                case SdCommand.READ_MULTIPLE_BLOCK:
                                    ReadDataBlock(GetDataOffset(arg));
                                    break;

                                case SdCommand.WRITE_BLOCK:
                                    wrPos = GetDataOffset(arg);
                                    break;

                                case SdCommand.WRITE_MULTIPLE_BLOCK:
                                    wrPos = GetDataOffset(arg);
                                    break;
                            }
                        }

                        NextState = SdState.RD_CRC;
                        argCnt = 0;
                    }
                    break;

                case SdState.RD_CRC:

                    NextState = GetRespondType();
                    break;

                case SdState.RD_DATA_SIG:
                    if (val == 0xFE) // Проверка сигнатуры данных
                    {
                        dataCnt = 0;
                        NextState = SdState.RD_DATA;
                    }
                    else
                        NextState = SdState.RD_DATA_SIG;
                    break;

                case SdState.RD_DATA_SIG_MUL:
                    switch (val)
                    {
                        case 0xFC: // Проверка сигнатуры данных

                            dataCnt = 0;
                            NextState = SdState.RD_DATA_MUL;
                            break;
                        case 0xFD: // Окончание передачи

                            dataCnt = 0;
                            NextState = SdState.IDLE;
                            break;
                        default:
                            NextState = SdState.RD_DATA_SIG_MUL;
                            break;
                    }
                    break;

                case SdState.RD_DATA: // Прием данных в буфер
                    {

                        buff[dataCnt++] = val;
                        NextState = SdState.RD_DATA;
                        if (dataCnt == dataBlockLen) // Запись данных в SD карту
                        {
                            dataCnt = 0;
                            WriteDataBlock(wrPos);


                            NextState = SdState.WR_DATA_RESP;
                        }
                    }
                    break;

                case SdState.RD_DATA_MUL: // Прием данных в буфер
                    {

                        buff[dataCnt++] = val;
                        NextState = SdState.RD_DATA_MUL;
                        if (dataCnt == dataBlockLen) // Запись данных в SD карту
                        {
                            dataCnt = 0;

                            WriteDataBlock(wrPos);
                            wrPos += dataBlockLen;
                            NextState = SdState.RD_DATA_SIG_MUL;
                        }
                    }
                    break;

                case SdState.RD_CRC16_1: // Чтение старшего байта CRC16
                    NextState = SdState.RD_CRC16_2;
                    break;

                case SdState.RD_CRC16_2: // Чтение младшего байта CRC16
                    NextState = SdState.WR_DATA_RESP;
                    break;

                default:
                    return;
            }
            currState = NextState;
        }

        private SdState GetRespondType()
        {
            if (!appCmd)
            {
                switch (cmd)
                {
                    case SdCommand.APP_CMD:
                        appCmd = true;
                        return SdState.R1;
                    case SdCommand.GO_IDLE_STATE:
                    case SdCommand.SEND_OP_COND:
                    case SdCommand.SET_BLOCKLEN:
                    case SdCommand.READ_SINGLE_BLOCK:
                    case SdCommand.READ_MULTIPLE_BLOCK:
                    case SdCommand.CRC_ON_OFF:
                    case SdCommand.STOP_TRANSMISSION:
                    case SdCommand.SEND_CSD:
                    case SdCommand.SEND_CID:
                        return SdState.R1;
                    case SdCommand.READ_OCR:
                        return SdState.R3;
                    case SdCommand.SEND_IF_COND:
                        return SdState.R7;

                    case SdCommand.WRITE_BLOCK:
                        return SdState.RD_DATA_SIG;
                    case SdCommand.WRITE_MULTIPLE_BLOCK:
                        return SdState.RD_DATA_SIG_MUL;
                }
            }
            else
            {
                appCmd = false;
                switch (cmd)
                {
                    case SdCommand.SD_SEND_OP_COND:
                        return SdState.R1;
                }
            }

            cmd = SdCommand.INVALID;
            return SdState.R1;
        }

        //ArgArr[3 - ArgCnt++] = Val;
        private void SetByte(ref uint arg, uint bnum, byte val)
        {
            UInt32 mask = (UInt32)((0x000000FF << (byte)(bnum * 8)) ^ 0xFFFFFFFF);
            arg &= mask;
            arg |= (UInt32)(val << (byte)(bnum * 8));
        }

        private long GetDataOffset(UInt32 commandArgument)
        {
            return highCapacity ? (long)commandArgument * SectorSize : commandArgument;
        }

        private void ReadDataBlock(long position)
        {
            Array.Clear(buff, 0, dataBlockLen);
            if (position < 0 || position > fstream.Length - dataBlockLen)
            {
                return;
            }
            fstream.Position = position;
            var offset = 0;
            while (offset < dataBlockLen)
            {
                var read = fstream.Read(buff, offset, dataBlockLen - offset);
                if (read == 0)
                {
                    break;
                }
                offset += read;
            }
        }

        private void ReadNextDataBlock()
        {
            var position = fstream.Position;
            ReadDataBlock(position);
        }

        private void WriteDataBlock(long position)
        {
            if (position < 0 || position > fstream.Length - dataBlockLen)
            {
                return;
            }
            fstream.Position = position;
            fstream.Write(buff, 0, dataBlockLen);
        }

        public byte Rd()
        {
            if (fstream == null) return 0xFF;

            switch (cmd)
            {
                case SdCommand.GO_IDLE_STATE:
                    if (currState == SdState.R1)
                    {
                        //            Cmd = CMD.INVALID;
                        currState = SdState.IDLE;
                        return 1;
                    }
                    break;
                case SdCommand.SEND_OP_COND:
                    if (currState == SdState.R1)
                    {
                        //            Cmd = CMD.INVALID;
                        currState = SdState.IDLE;
                        return 0;
                    }
                    break;
                case SdCommand.SET_BLOCKLEN:
                    if (currState == SdState.R1)
                    {
                        //            Cmd = CMD.INVALID;
                        currState = SdState.IDLE;
                        return 0;
                    }
                    break;
                case SdCommand.SEND_IF_COND:
                    if (currState == SdState.R7)
                    {
                        switch (r7_Cnt++)
                        {
                            case 0: return 0x01; // R1
                            case 1: return 0x00;
                            case 2: return 0x00;
                            case 3: return 0x01;
                            default:
                                currState = SdState.IDLE;
                                r7_Cnt = 0;
                                return (byte)(arg & 0xFF); // echo-back
                        }
                    }
                    break;

                case SdCommand.READ_OCR:
                    if (currState == SdState.R3)
                    {
                        switch (ocrCnt++)
                        {
                            case 0: return 0x00; // R1
                            case 1: return highCapacity ? (byte)0xC0 : (byte)0x80;
                            case 2: return 0xFF;
                            case 3: return 0x80;
                            default:
                                currState = SdState.IDLE;
                                ocrCnt = 0;
                                return 0x00;
                        }
                    }
                    break;

                case SdCommand.APP_CMD:
                    if (currState == SdState.R1)
                    {
                        currState = SdState.IDLE;
                        return 0;
                    }
                    break;

                case SdCommand.SD_SEND_OP_COND:
                    if (currState == SdState.R1)
                    {
                        currState = SdState.IDLE;
                        return 0;
                    }
                    break;

                case SdCommand.CRC_ON_OFF:
                    if (currState == SdState.R1)
                    {
                        currState = SdState.IDLE;
                        return 0;
                    }
                    break;

                case SdCommand.STOP_TRANSMISSION:
                    switch (currState)
                    {
                        case SdState.R1:
                            dataCnt = 0;
                            currState = SdState.R1b;
                            return 0;
                        case SdState.R1b:
                            currState = SdState.IDLE;
                            return 0xFF;
                    }
                    break;

                case SdCommand.READ_SINGLE_BLOCK:
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.DELAY_S;
                            return 0;
                        case SdState.DELAY_S:
                            currState = SdState.STARTBLOCK;
                            return 0xFF;
                        case SdState.STARTBLOCK:
                            currState = SdState.WR_DATA;
                            dataCnt = 0;
                            return 0xFE;
                        case SdState.WR_DATA:
                            {
                                byte Val = buff[dataCnt++];
                                if (dataCnt == dataBlockLen)
                                {
                                    dataCnt = 0;
                                    currState = SdState.WR_CRC16_1;
                                }
                                return Val;
                            }
                        case SdState.WR_CRC16_1:
                            currState = SdState.WR_CRC16_2;
                            return 0xFF; // crc
                        case SdState.WR_CRC16_2:
                            currState = SdState.IDLE;
                            cmd = SdCommand.INVALID;
                            return 0xFF; // crc
                    }
                    //        Cmd = CMD.INVALID;
                    break;

                case SdCommand.READ_MULTIPLE_BLOCK:
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.DELAY_S;
                            return 0;
                        case SdState.DELAY_S:
                            currState = SdState.STARTBLOCK;
                            return 0xFF;
                        case SdState.STARTBLOCK:
                            currState = SdState.IDLE;
                            dataCnt = 0;
                            return 0xFE;
                        case SdState.IDLE:
                            {
                                if (dataCnt < dataBlockLen)
                                {
                                    byte Val = buff[dataCnt++];
                                    if (dataCnt == dataBlockLen)
                                        ReadNextDataBlock();
                                    return Val;
                                }
                                else if (dataCnt > (dataBlockLen + 8))
                                {
                                    dataCnt = 0;
                                    return 0xFE; // next startblock
                                }
                                else
                                {
                                    dataCnt++;
                                    return 0xFF; // crc & pause
                                }
                            }


                    }
                    break;

                case SdCommand.SEND_CSD:
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.DELAY_S;
                            return 0;
                        case SdState.DELAY_S:
                            currState = SdState.STARTBLOCK;
                            return 0xFF;
                        case SdState.STARTBLOCK:
                            currState = SdState.WR_DATA;
                            return 0xFE;
                        case SdState.WR_DATA:
                            {
                                byte Val = csd[csdCnt++];
                                if (csdCnt == 16)
                                {
                                    csdCnt = 0;
                                    currState = SdState.IDLE;
                                    cmd = SdCommand.INVALID;
                                }
                                return Val;
                            }
                    }
                    //        Cmd = CMD.INVALID;
                    break;

                case SdCommand.SEND_CID:
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.DELAY_S;
                            return 0;
                        case SdState.DELAY_S:
                            currState = SdState.STARTBLOCK;
                            return 0xFF;
                        case SdState.STARTBLOCK:
                            currState = SdState.WR_DATA;
                            return 0xFE;
                        case SdState.WR_DATA:
                            {
                                byte Val = cid[cidCnt++];
                                if (cidCnt == 16)
                                {
                                    cidCnt = 0;
                                    currState = SdState.IDLE;
                                    cmd = SdCommand.INVALID;
                                }
                                return Val;
                            }
                    }
                    //        Cmd = CMD.INVALID;
                    break;

                case SdCommand.WRITE_BLOCK:
                    //        printf(__FUNCTION__" cmd=0x%X, St=0x%X\n", Cmd, CurrState);
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.RD_DATA_SIG;
                            return 0xFE;

                        case SdState.WR_DATA_RESP:
                            {
                                currState = SdState.IDLE;
                                byte Resp = (((byte)SdStatus.DATA_ACCEPTED) << 1) | 1;
                                return Resp;
                            }
                    }
                    break;

                case SdCommand.WRITE_MULTIPLE_BLOCK:
                    switch (currState)
                    {
                        case SdState.R1:
                            currState = SdState.RD_DATA_SIG_MUL;
                            return 0xFE;
                        case SdState.WR_DATA_RESP:
                            {
                                currState = SdState.RD_DATA_SIG_MUL;
                                byte Resp = (((byte)SdStatus.DATA_ACCEPTED) << 1) | 1;
                                return Resp;
                            }
                    }
                    break;
            }

            if (currState == SdState.R1) // CMD.INVALID
            {
                currState = SdState.IDLE;
                return 0x05;
            }

            return 0xFF;
        }
    }
}
