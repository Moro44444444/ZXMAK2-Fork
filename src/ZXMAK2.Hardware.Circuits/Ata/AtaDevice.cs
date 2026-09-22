using System;
using System.Text;


namespace ZXMAK2.Hardware.Circuits.Ata
{
    /// <summary>
    /// ATA device emulation 
    /// based on unrealspeccy source
    /// </summary>
    public class AtaDevice : IDisposable
    {
        private readonly byte device_id;             // 0x00 - master, 0x10 - slave
        private readonly AtaDeviceInfo _deviceInfo = new AtaDeviceInfo();

        private UInt32 c, h, s, lba;
        private byte[] regs { get { return reg.__regs; } }
        private readonly AtaRegsUnion reg = new AtaRegsUnion();

        private bool intrq;
        private bool atapi;                 // flag for CD-ROM device

        private HD_STATE state;
        private uint transptr, transcount;
        private int phys_dev;
        private readonly byte[] transbf = new byte[0xFFFF]; // ATAPI is able to tranfer 0xFFFF bytes. passing more leads to error

        private readonly AtaPasser ata_p = new AtaPasser();
        private readonly AtapiPasser atapi_p = new AtapiPasser();
        private byte[] atapiResponse;
        private int atapiResponseOffset;
        private uint atapiReadLba;
        private uint atapiReadBlocks;
        private byte atapiSenseKey;
        private byte atapiSenseAsc;
        private byte atapiSenseAscq;

        public bool LedIo;
        public bool LogIo;

        /// <param name="id">0x00 - master, 0x10 - slave</param>
        public AtaDevice(byte id)
        {
            device_id = id;
            reset(RESET_TYPE.RESET_HARD);
            configure(_deviceInfo);
        }

        public void Dispose()
        {
            ata_p.Dispose();
            atapi_p.Dispose();
        }

        public byte Id
        {
            get { return device_id; }
        }

        public AtaDeviceInfo DeviceInfo
        {
            get { return _deviceInfo; }
        }

        public void Open()
        {
            configure(_deviceInfo);
        }


        public bool loaded()
        {
            return atapi ? atapi_p.IsAttached : ata_p.IsLoaded();
        }

        private void configure(AtaDeviceInfo cfg)
        {
            ata_p.Close();
            atapi_p.Close();
            atapi = false;
            c = cfg.Cylinders;
            h = cfg.Heads;
            s = cfg.Sectors;
            lba = cfg.Lba;

            for (int i = 0; i < regs.Length; i++)	// clear registers
                regs[i] = 0;
            command_ok(); // reset state and transfer position

            phys_dev = -1;
            if (String.IsNullOrEmpty(cfg.FileName))
                return;

            if (cfg.IsCdrom)
            {
                atapi = atapi_p.Open(cfg.FileName);
                lba = atapi_p.BlockCount;
                if (lba == 0)
                {
                    // An empty tray is still a present ATAPI device.  The
                    // packet command will report "medium not present".
                    lba = 1;
                }
                return;
            }

            var filedev = new PhysicalDeviceInfo();
            filedev.filename = cfg.FileName;
            filedev.type = DEVTYPE.ATA_FILEHDD;

            bool success = false;
            if (filedev.type == DEVTYPE.ATA_FILEHDD)
            {
                filedev.usage = DEVUSAGE.ATA_OP_USE;
                success = ata_p.Open(filedev, cfg.ReadOnly);
            }
            if (success)
            {
                return;
            }
            filedev.filename = string.Empty;
        }


        public byte read(AtaReg n_reg)
        {
            if (!loaded())
                return 0xFF;
            if (((reg.devhead ^ device_id) & 0x10) != 0)
            {
                return 0xFF;
            }

            if (n_reg == AtaReg.CommandStatus)
                intrq = false;
            if (n_reg == AtaReg.ControlAltStatus)
                n_reg = AtaReg.CommandStatus; // read alt.status -> read status
            if (n_reg == AtaReg.CommandStatus ||
                (reg.status & HD_STATUS.STATUS_BSY) != 0)
            {
                //	   printf("state=%d\n",state); //Alone Coder
                return (byte)reg.status;
            } // BSY=1 or read status
            // BSY = 0
            //// if (reg.status & STATUS_DRQ) return 0xFF;    // DRQ.  ATA-5: registers should not be queried while DRQ=1, but programs do this!
            // DRQ = 0
            return regs[(int)n_reg];
        }

        public void write(AtaReg n_reg, byte data)
        {
            //   printf("dev=%d, reg=%d, data=%02X\n", device_id, n_reg, data);
            if (!loaded())
                return;
            if (n_reg == AtaReg.FeatureError)
            {
                reg.feat = data;
                return;
            }

            if (n_reg != AtaReg.CommandStatus)
            {
                regs[(int)n_reg] = data;
                if ((reg.control & HD_CONTROL.CONTROL_SRST) != 0)
                {
                    //          printf("dev=%d, reset\n", device_id);
                    reset(RESET_TYPE.RESET_SRST);
                }
                return;
            }

            // execute command!
            if (((reg.devhead ^ device_id) & 0x10) != 0 &&
                data != 0x90)
            {
                return;
            }
            if ((reg.status & HD_STATUS.STATUS_DRDY) == 0 && !atapi)
            {
                Logger.Warn("ATA{0:X2}: hdd not ready cmd = #{1:X2} (ignored)", Id, data);
                return;
            }

            reg.err = HD_ERROR.ERR_NONE;
            intrq = false;

            //{printf(" [");for (int q=1;q<9;q++) printf("-%02X",regs[q]);printf("]\n");}
            if (exec_atapi_cmd(data))
                return;
            if (exec_ata_cmd(data))
                return;
            reg.status = HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_ERR;
            reg.err = HD_ERROR.ERR_ABRT;
            state = HD_STATE.S_IDLE;
            intrq = true;
        }

        public UInt16 read_data()
        {
            if (!loaded())
                return 0xFFFF;
            if (((reg.devhead ^ device_id) & 0x10) != 0)
                return 0xFFFF;
            if (/* (reg.status & (STATUS_DRQ | STATUS_BSY)) != STATUS_DRQ ||*/ transptr >= transcount)
                return 0xFFFF;

            LedIo = true;
            // DRQ=1, BSY=0, data present
            UInt16 result = (UInt16)(transbf[transptr * 2] | (transbf[transptr * 2 + 1] << 8));
            transptr++;
            //   printf(__FUNCTION__" data=0x%04X\n", result & 0xFFFF);

            if (transptr < transcount)
                return result;
            // look to state, prepare next block
            if (state == HD_STATE.S_READ_ID)
                command_ok();
            else if (state == HD_STATE.S_READ_ATAPI)
                prepare_next_atapi_data_phase();
            if (state == HD_STATE.S_READ_SECTORS)
            {
                //       __debugbreak();
                //       printf("dev=%d, cnt=%d\n", device_id, reg.count);
                if (--reg.count == 0)
                    command_ok();
                else
                {
                    next_sector();
                    read_sectors();
                }
            }

            return result;
        }

        public void write_data(UInt16 data)
        {
            if (!loaded())
                return;
            if (((reg.devhead ^ device_id) & 0x10) != 0)
                return;
            if (/* (reg.status & (STATUS_DRQ | STATUS_BSY)) != STATUS_DRQ ||*/ transptr >= transcount)
                return;

            LedIo = true;
            transbf[transptr * 2] = (byte)data;
            transbf[transptr * 2 + 1] = (byte)(data >> 8);
            transptr++;
            if (transptr < transcount)
                return;
            // look to state, prepare next block
            if (state == HD_STATE.S_WRITE_SECTORS)
            {
                write_sectors();
                return;
            }

            if (state == HD_STATE.S_FORMAT_TRACK)
            {
                format_track();
                return;
            }

            if (state == HD_STATE.S_RECV_PACKET)
            {
                handle_atapi_packet();
                return;
            }
            /*   if (state == S_MODE_SELECT) { exec_mode_select(); return; } */
        }

        public byte read_intrq()
        {
            if (!loaded() ||
                ((reg.devhead ^ device_id) & 0x10) != 0 ||
                (reg.control & HD_CONTROL.CONTROL_nIEN) != 0)
            {
                return 0xFF;
            }
            return intrq ? (byte)0xFF : (byte)0x00;
        }

        public bool exec_ata_cmd(byte cmd)
        {
            //   printf(__FUNCTION__" cmd=%02X\n", cmd);
            // EXECUTE DEVICE DIAGNOSTIC for both ATA and ATAPI
            if (cmd == 0x90)
            {
                reset_signature(RESET_TYPE.RESET_SOFT);
                return true;
            }

            if (atapi)
                return false;

            // [DEVICE RESET]
            if (cmd == 0x08)
            {
                reset(RESET_TYPE.RESET_SOFT);
                return true;
            }
            // INITIALIZE DEVICE PARAMETERS
            if (cmd == 0x91)
            {
                // pos = (reg.cyl * h + (reg.devhead & 0x0F)) * s + reg.sec - 1;
                h = (uint)((reg.devhead & 0xF) + 1);
                s = reg.count;
                if (s == 0)
                {
                    reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DF | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_ERR;
                    return true;
                }

                c = lba / s / h;

                reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC;
                return true;
            }

            if ((cmd & 0xFE) == 0x20) // ATA-3 (mandatory), read sectors
            { // cmd #21 obsolette, rqd for is-dos
                //       printf(__FUNCTION__" sec_cnt=%d\n", reg.count);
                read_sectors();
                return true;
            }

            if ((cmd & 0xFE) == 0x40) // ATA-3 (mandatory),  verify sectors
            { //rqd for is-dos
                verify_sectors();
                return true;
            }

            if ((cmd & 0xFE) == 0x30) // ATA-3 (mandatory), write sectors
            {
                if (seek())
                {
                    state = HD_STATE.S_WRITE_SECTORS;
                    reg.status = HD_STATUS.STATUS_DRQ | HD_STATUS.STATUS_DSC;
                    transptr = 0;
                    transcount = 0x100;
                }
                return true;
            }

            if (cmd == 0x50) // format track (данная реализация - ничего не делает)
            {
                reg.sec = 1;
                if (seek())
                {
                    state = HD_STATE.S_FORMAT_TRACK;
                    reg.status = HD_STATUS.STATUS_DRQ | HD_STATUS.STATUS_DSC;
                    transptr = 0;
                    transcount = 0x100;
                }
                return true;
            }

            if (cmd == 0xEC)
            {
                prepare_id();
                return true;
            }

            if (cmd == 0xE7)
            { // FLUSH CACHE
                if (ata_p.Flush())
                {
                    command_ok();
                    intrq = true;
                }
                else
                {
                    reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DF | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_ERR; // 0x71
                }
                return true;
            }

            if (cmd == 0x10)
            {
                recalibrate();
                command_ok();
                intrq = true;
                return true;
            }

            if (cmd == 0x70)
            { // seek
                if (!seek())
                    return true;
                command_ok();
                intrq = true;
                return true;
            }

            Logger.Error("ATA{0:X2}: Unknown ATA command #{1:X2}", Id, cmd);
            return false;
        }

        public bool exec_atapi_cmd(byte cmd)
        {
            if (!atapi)
                return false;

            // soft reset
            if (cmd == 0x08)
            {
                reset(RESET_TYPE.RESET_SOFT);
                return true;
            }
            if (cmd == 0xA1) // IDENTIFY PACKET DEVICE
            {
                prepare_id();
                return true;
            }

            if (cmd == 0xA0)
            { // packet
                state = HD_STATE.S_RECV_PACKET;
                reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_DRQ;
                reg.intreason = ATAPI_INT_REASON.INT_COD;
                transptr = 0;
                transcount = 6;
                intrq = true;
                return true;
            }

            if (cmd == 0xEC)
            {
                reg.count = 1;
                reg.sec = 1;
                reg.cyl = 0xEB14;

                reg.status = HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_ERR;
                reg.err = HD_ERROR.ERR_ABRT;
                state = HD_STATE.S_IDLE;
                intrq = true;
                return true;
            }

            Logger.Error("ATA{0:X2}: Unknown ATAPI command #{1:X2}", Id, cmd);
            // "command aborted" with ATAPI signature
            reg.count = 1;
            reg.sec = 1;
            reg.cyl = 0xEB14;
            return false;
        }

        public void reset_signature(RESET_TYPE mode = RESET_TYPE.RESET_SOFT)
        {
            reg.count = reg.sec = 1;
            reg.err = HD_ERROR.ERR_AMNF;	// = 1
            reg.cyl = atapi ? (ushort)0xEB14 : (ushort)0;
            reg.devhead &= (atapi && mode == RESET_TYPE.RESET_SOFT) ? (byte)0x10 : (byte)0;
            reg.status = (mode == RESET_TYPE.RESET_SOFT || !atapi) ?
                (HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC) : HD_STATUS.STATUS_NONE;
        }

        public void reset(RESET_TYPE mode)
        {
            reg.control = 0; // clear SRST
            intrq = false;

            command_ok();
            reset_signature(mode);
        }

        public bool seek()
        {
            uint pos;
            if ((reg.devhead & 0x40) != 0)
            {
                // LBA mode
                long tmp = regs[3] | (regs[4] << 8) | (regs[5] << 16) | (regs[6] << 24);
                pos = (uint)(tmp & 0x0FFFFFFF);
                if (pos >= lba)
                {
                    //          printf("seek error: lba %d:%d\n", lba, pos);
                    reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DF | HD_STATUS.STATUS_ERR;
                    reg.err = HD_ERROR.ERR_IDNF | HD_ERROR.ERR_ABRT;
                    intrq = true;
                    return false;
                }
                //      printf("lba %d:%d\n", lba, pos);
            }
            else
            {
                // CHS mode
                if (reg.cyl >= c || (uint)(reg.devhead & 0x0F) >= h || reg.sec > s || reg.sec == 0)
                {
                    //          printf("seek error: chs %4d/%02d/%02d\n", *(unsigned short*)(regs+4), (reg.devhead & 0x0F), reg.sec);
                    reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DF | HD_STATUS.STATUS_ERR;
                    reg.err = HD_ERROR.ERR_IDNF | HD_ERROR.ERR_ABRT;
                    intrq = true;
                    return false;
                }
                pos = (uint)((reg.cyl * h + (reg.devhead & 0x0F)) * s + reg.sec - 1);
                //      printf("chs %4d/%02d/%02d: %8d\n", *(unsigned short*)(regs+4), (reg.devhead & 0x0F), reg.sec, pos);
            }
            //printf("[seek %I64d]", ((__int64)pos) << 9);
            if (LogIo)
            {
                Logger.Info("ATA{0:X2}: IDE HDD SEEK lba={1} [fileOffset=#{2:X8}]", Id, pos, ((long)pos) << 9);
            }
            if (!ata_p.Seek(pos))
            {
                reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DF | HD_STATUS.STATUS_ERR;
                reg.err = HD_ERROR.ERR_IDNF | HD_ERROR.ERR_ABRT;
                intrq = true;
                return false;
            }
            return true;
        }

        public void recalibrate()
        {
            reg.cyl = 0;
            reg.devhead &= 0xF0;

            if ((reg.devhead & 0x40) != 0) // LBA
            {
                reg.sec = 0;
                return;
            }

            reg.sec = 1;
        }

        public void prepare_id()
        {
            if (phys_dev == -1)
            {
                for (int i = 0; i < 512; i++)
                    transbf[i] = 0;
                make_ata_string(transbf, 10 * 2, 10, _deviceInfo.SerialNumber);	    // Serial number
                make_ata_string(transbf, 23 * 2, 4, _deviceInfo.FirmwareRevision);	// Firmware revision
                make_ata_string(transbf, 27 * 2, 20, _deviceInfo.ModelNumber);	    // Model number

                if (atapi)
                {
                    // ATAPI identify-packet response: CD-ROM, removable,
                    // 12-byte packets and a 2048-byte logical block size.
                    setUInt16(transbf, 0 * 2, 0x8580);
                    setUInt16(transbf, 49 * 2, 0x0200);
                    setUInt16(transbf, 80 * 2, 0x003E);
                    setUInt16(transbf, 81 * 2, 0x0013);
                }
                else
                {
                setUInt16(transbf, 0 * 2, 0x045A);		// [General configuration]
                setUInt16(transbf, 1 * 2, (UInt16)c);
                setUInt16(transbf, 3 * 2, (UInt16)h);
                setUInt16(transbf, 6 * 2, (UInt16)s);
                setUInt16(transbf, 20 * 2, 3);			// a dual ported multi-sector buffer capable of simultaneous transfers with a read caching capability
                setUInt16(transbf, 21 * 2, 512);		// cache size=256k
                setUInt16(transbf, 22 * 2, 4);			// ECC bytes
                setUInt16(transbf, 49 * 2, 0x200);		// LBA supported
                setUInt32(transbf, 60 * 2, lba);		// [Total number of user addressable logical sectors]
                setUInt16(transbf, 80 * 2, 0x3E);		// support specifications up to ATA-5
                setUInt16(transbf, 81 * 2, 0x13);		// ATA/ATAPI-5 T13 1321D revision 3
                setUInt16(transbf, 82 * 2, 0x60);		// supported look-ahead and write cache
                }

                // make checksum
                transbf[510] = 0xA5;
                byte cs = 0;
                for (int i = 0; i < 511; i++)
                    cs += transbf[i];
                transbf[511] = (byte)(0 - cs);
            }
            else
            { // copy as is...
                //for(int i=0; i < 512; i++)
                //	transbf[i] = phys[phys_dev].idsector[i];
            }

            state = HD_STATE.S_READ_ID;
            transptr = 0;
            transcount = 0x100;
            intrq = true;
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DRQ | HD_STATUS.STATUS_DSC;
            reg.err = HD_ERROR.ERR_NONE;
        }

        public void command_ok()
        {
            state = HD_STATE.S_IDLE;
            transptr = 0xFFFFFFFF;
            reg.err = 0;
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC;
        }

        public void next_sector()
        {
            if ((reg.devhead & 0x40) != 0)
            { // LBA
                // Original C++:
                //*(unsigned*)&reg.sec = (*(unsigned*)&reg.sec & 0xF0000000) + ((*(unsigned*)&reg.sec+1) & 0x0FFFFFFF);
                long tmp = regs[3] | (regs[4] << 8) | (regs[5] << 16) | (regs[6] << 24);
                tmp = (tmp & 0xF0000000) + ((tmp + 1) & 0x0FFFFFFF);
                regs[3] = (byte)tmp;
                regs[4] = (byte)(tmp >> 8);
                regs[5] = (byte)(tmp >> 16);
                regs[6] = (byte)(tmp >> 24);
                return;
            }
            // need to recalc CHS for every sector, coz ATA registers
            // should contain current position on failure
            if (reg.sec < s)
            {
                reg.sec++;
                return;
            }
            reg.sec = 1;
            byte head = (byte)((reg.devhead & 0x0F) + 1);
            if (head < h)
            {
                reg.devhead = (byte)((reg.devhead & 0xF0) | head);
                return;
            }
            reg.devhead &= 0xF0;
            reg.cyl++;
        }

        public void read_sectors()
        {
            //   __debugbreak();
            intrq = true;
            if (!seek())
                return;

            if (LogIo)
            {
                Logger.Info("ATA{0:X2}: IDE HDD READ SECTOR", Id);
            }
            if (!ata_p.ReadSector(transbf, 0))
            {
                reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_ERR;
                reg.err = HD_ERROR.ERR_UNC | HD_ERROR.ERR_IDNF;
                state = HD_STATE.S_IDLE;
                return;
            }
            transptr = 0;
            transcount = 0x100;
            state = HD_STATE.S_READ_SECTORS;
            reg.err = HD_ERROR.ERR_NONE;
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DRQ | HD_STATUS.STATUS_DSC;

            /*
               if(reg.devhead & 0x40)
                   printf("dev=%d lba=%d\n", device_id, *(unsigned*)(regs+3) & 0x0FFFFFFF);
               else
                   printf("dev=%d c/h/s=%d/%d/%d\n", device_id, reg.cyl, (reg.devhead & 0xF), reg.sec);
            */
        }

        public void verify_sectors()
        {
            intrq = true;
            //   __debugbreak();

            do
            {
                --reg.count;
                /*
                       if(reg.devhead & 0x40)
                           printf("lba=%d\n", *(unsigned*)(regs+3) & 0x0FFFFFFF);
                       else
                           printf("c/h/s=%d/%d/%d\n", reg.cyl, (reg.devhead & 0xF), reg.sec);
                */
                if (!seek())
                    return;
                /*
                       u8 Buf[512];
                       if (!ata_p.read_sector(Buf))
                       {
                          reg.status = STATUS_DRDY | STATUS_DF | STATUS_CORR | STATUS_DSC | STATUS_ERR;
                          reg.err = ERR_UNC | ERR_IDNF | ERR_ABRT | ERR_AMNF;
                          state = S_IDLE;
                          return;
                       }
                */
                if (reg.count != 0)
                    next_sector();
            } while (reg.count != 0);
            command_ok();
        }

        public void write_sectors()
        {
            intrq = true;
            //printf(" [write] ");
            if (!seek())
                return;

            if (LogIo)
            {
                Logger.Info("ATA{0:X2}: IDE HDD WRITE SECTOR", Id);
            }
            if (!ata_p.WriteSector(transbf, 0))
            {
                reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_ERR;
                reg.err = HD_ERROR.ERR_UNC;
                state = HD_STATE.S_IDLE;
                return;
            }

            if (--reg.count == 0)
            {
                command_ok();
                return;
            }
            next_sector();

            transptr = 0;
            transcount = 0x100;
            state = HD_STATE.S_WRITE_SECTORS;
            reg.err = HD_ERROR.ERR_NONE;
            // Alex: DRDY added for SPRINTER 
            ///Missing DRDY produce not ready error on write operation
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DRQ | HD_STATUS.STATUS_DSC;
        }

        public void format_track()
        {
            intrq = true;
            if (!seek())
                return;

            command_ok();
            return;
        }

        public void handle_atapi_packet()
        {
            var command = transbf[0];
            if (LogIo)
            {
                Logger.Info("ATA{0:X2}: ATAPI packet #{1:X2}", Id, command);
            }

            switch (command)
            {
                case 0x00: // TEST UNIT READY
                    if (atapi_p.IsMediaReady)
                        complete_atapi_command();
                    else
                        fail_atapi_command(0x02, 0x3A, 0x00); // not ready, no medium
                    return;

                case 0x03: // REQUEST SENSE
                    start_atapi_response(create_request_sense_response(transbf[4]));
                    return;

                case 0x12: // INQUIRY
                    start_atapi_response(create_inquiry_response(transbf[4]));
                    return;

                case 0x1A: // MODE SENSE (6)
                    start_atapi_response(create_mode_sense_response(transbf[4]));
                    return;

                case 0x5A: // MODE SENSE (10)
                    start_atapi_response(create_mode_sense_10_response(read_be_uint16(transbf, 7)));
                    return;

                case 0x1B: // START STOP UNIT
                case 0x1E: // PREVENT/ALLOW MEDIUM REMOVAL
                    // The host tray is deliberately not controlled by guest
                    // software.  The selected Windows drive remains attached.
                    complete_atapi_command();
                    return;

                case 0x25: // READ CAPACITY (10)
                    if (!atapi_p.IsMediaReady)
                    {
                        fail_atapi_command(0x02, 0x3A, 0x00);
                        return;
                    }
                    start_atapi_response(create_read_capacity_response());
                    return;

                case 0x28: // READ (10)
                    start_atapi_read(
                        read_be_uint32(transbf, 2),
                        read_be_uint16(transbf, 7));
                    return;

                case 0xBB: // SET CD SPEED
                    // The Time Gal Pentagon loader issues TEST UNIT READY
                    // before this command.  The Windows host controls the
                    // actual speed, so acknowledging the requested setting
                    // is the compatible ATAPI behaviour.
                    if (!atapi_p.IsMediaReady)
                    {
                        fail_atapi_command(0x02, 0x3A, 0x00);
                        return;
                    }
                    complete_atapi_command();
                    return;

                case 0x43: // READ TOC/PMA/ATIP, formats 0 and 1
                    if (!atapi_p.IsMediaReady)
                    {
                        fail_atapi_command(0x02, 0x3A, 0x00);
                        return;
                    }
                    var tocFormat = (byte)(transbf[9] & 0x0F);
                    if (tocFormat == 0)
                    {
                        start_atapi_response(create_read_toc_response(
                            transbf[6],
                            (transbf[1] & 0x02) != 0,
                            read_be_uint16(transbf, 7)));
                        return;
                    }
                    if (tocFormat == 1)
                    {
                        // Multi-session information. Time Gal's standard
                        // loader uses this to locate the current ISO session
                        // before it starts reading its directory. A Windows
                        // logical optical drive exposes the active medium as
                        // one address space, so that session starts at LBA 0.
                        start_atapi_response(create_multi_session_response(
                            (transbf[1] & 0x02) != 0,
                            read_be_uint16(transbf, 7)));
                        return;
                    }
                    fail_atapi_command(0x05, 0x24, 0x00);
                    return;

                default:
                    Logger.Warn("ATA{0:X2}: unsupported ATAPI packet #{1:X2}", Id, command);
                    fail_atapi_command(0x05, 0x20, 0x00); // illegal request/opcode
                    return;
            }
        }

        private void start_atapi_read(uint startLba, uint blocks)
        {
            if (!atapi_p.IsMediaReady)
            {
                fail_atapi_command(0x02, 0x3A, 0x00);
                return;
            }
            if (blocks == 0)
            {
                complete_atapi_command();
                return;
            }
            var end = (ulong)startLba + blocks;
            if (end > atapi_p.BlockCount)
            {
                fail_atapi_command(0x05, 0x21, 0x00); // LBA out of range
                return;
            }
            atapiResponse = null;
            atapiResponseOffset = 0;
            atapiReadLba = startLba;
            atapiReadBlocks = blocks;
            prepare_next_atapi_data_phase();
        }

        private void start_atapi_response(byte[] response)
        {
            atapiResponse = response ?? new byte[0];
            atapiResponseOffset = 0;
            atapiReadBlocks = 0;
            prepare_next_atapi_data_phase();
        }

        private void prepare_next_atapi_data_phase()
        {
            if (atapiResponse == null || atapiResponseOffset >= atapiResponse.Length)
            {
                if (atapiReadBlocks == 0)
                {
                    complete_atapi_command();
                    return;
                }

                atapiResponse = new byte[AtapiPasser.BlockSize];
                atapiResponseOffset = 0;
                if (!atapi_p.ReadBlock(atapiReadLba, atapiResponse, 0))
                {
                    fail_atapi_command(0x03, 0x11, 0x00); // unrecovered read error
                    return;
                }
                atapiReadLba++;
                atapiReadBlocks--;
            }

            var available = atapiResponse.Length - atapiResponseOffset;
            // The byte count is a 16-bit ATA register, but an ATAPI phase
            // is word based.  Keep the transfer even and inside transbf.
            var limit = reg.atapi_count == 0 ? 0xFFFE : (reg.atapi_count & 0xFFFE);
            if (limit == 0)
            {
                limit = 0xFFFE;
            }
            var bytes = Math.Min(available, limit);
            if ((bytes & 1) != 0)
            {
                bytes++;
            }
            for (var i = 0; i < bytes; i++)
            {
                transbf[i] = i < available ? atapiResponse[atapiResponseOffset + i] : (byte)0;
            }
            atapiResponseOffset += Math.Min(available, bytes);
            transptr = 0;
            transcount = (uint)(bytes >> 1);
            state = HD_STATE.S_READ_ATAPI;
            reg.atapi_count = (ushort)bytes;
            reg.err = HD_ERROR.ERR_NONE;
            reg.intreason = ATAPI_INT_REASON.INT_IO;
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_DRQ;
            intrq = true;
        }

        private void complete_atapi_command()
        {
            atapiResponse = null;
            atapiResponseOffset = 0;
            atapiReadBlocks = 0;
            command_ok();
            reg.intreason = ATAPI_INT_REASON.INT_COD | ATAPI_INT_REASON.INT_IO;
            intrq = true;
        }

        private void fail_atapi_command(byte senseKey, byte asc, byte ascq)
        {
            atapiSenseKey = senseKey;
            atapiSenseAsc = asc;
            atapiSenseAscq = ascq;
            atapiResponse = null;
            atapiResponseOffset = 0;
            atapiReadBlocks = 0;
            state = HD_STATE.S_IDLE;
            transptr = 0xFFFFFFFF;
            reg.err = HD_ERROR.ERR_ABRT;
            reg.intreason = ATAPI_INT_REASON.INT_COD | ATAPI_INT_REASON.INT_IO;
            reg.status = HD_STATUS.STATUS_DRDY | HD_STATUS.STATUS_DSC | HD_STATUS.STATUS_ERR;
            intrq = true;
        }

        private byte[] create_request_sense_response(byte allocationLength)
        {
            var response = new byte[18];
            response[0] = 0x70;
            response[2] = atapiSenseKey;
            response[7] = 10;
            response[12] = atapiSenseAsc;
            response[13] = atapiSenseAscq;
            atapiSenseKey = atapiSenseAsc = atapiSenseAscq = 0;
            return truncate_atapi_response(response, allocationLength);
        }

        private static byte[] create_inquiry_response(byte allocationLength)
        {
            var response = new byte[36];
            response[0] = 0x05; // CD/DVD device
            response[1] = 0x80; // removable medium
            response[2] = 0x05; // MMC-3 compatible command set
            response[3] = 0x02;
            response[4] = 31;
            copy_scsi_ascii(response, 8, 8, "ZXMAK2");
            copy_scsi_ascii(response, 16, 16, "CD/DVD-ROM");
            copy_scsi_ascii(response, 32, 4, "1.0");
            return truncate_atapi_response(response, allocationLength);
        }

        private static byte[] create_mode_sense_response(byte allocationLength)
        {
            var response = new byte[4];
            response[0] = 3;
            return truncate_atapi_response(response, allocationLength);
        }

        private static byte[] create_mode_sense_10_response(ushort allocationLength)
        {
            var response = new byte[8];
            response[1] = 6;
            return truncate_atapi_response(response, allocationLength);
        }

        private byte[] create_read_capacity_response()
        {
            var response = new byte[8];
            write_be_uint32(response, 0, atapi_p.BlockCount - 1);
            write_be_uint32(response, 4, AtapiPasser.BlockSize);
            return response;
        }

        private byte[] create_read_toc_response(byte startingTrack, bool msf, ushort allocationLength)
        {
            // Format 0 contains the first/last track followed by 8-byte
            // descriptors.  A data CD has one data track and a lead-out;
            // returning both lets classic NemoIDE loaders determine the
            // complete media layout instead of seeing a truncated TOC.
            var includeTrackOne = startingTrack == 0 || startingTrack == 1;
            var includeLeadOut = startingTrack == 0 || startingTrack == 1 || startingTrack == 0xAA;
            if (!includeTrackOne && !includeLeadOut)
            {
                return new byte[0];
            }

            var descriptorCount = (includeTrackOne ? 1 : 0) + (includeLeadOut ? 1 : 0);
            var response = new byte[4 + descriptorCount * 8];
            var dataLength = response.Length - 2;
            response[0] = (byte)(dataLength >> 8);
            response[1] = (byte)dataLength;
            response[2] = 1;
            response[3] = 1;

            var offset = 4;
            if (includeTrackOne)
            {
                write_toc_descriptor(response, offset, 1, 0, msf);
                offset += 8;
            }
            if (includeLeadOut)
            {
                write_toc_descriptor(response, offset, 0xAA, atapi_p.BlockCount, msf);
            }
            return truncate_atapi_response(response, allocationLength);
        }

        private static byte[] create_multi_session_response(bool msf, ushort allocationLength)
        {
            var response = new byte[12];
            response[1] = 10;
            response[2] = 1; // first complete session
            response[3] = 1; // last complete session
            write_toc_descriptor(response, 4, 1, 0, msf);
            return truncate_atapi_response(response, allocationLength);
        }

        private static void write_toc_descriptor(byte[] response, int offset, byte track, uint lba, bool msf)
        {
            response[offset + 1] = 0x14; // data track, ADR=1
            response[offset + 2] = track;
            if (!msf)
            {
                write_be_uint32(response, offset + 4, lba);
                return;
            }

            // CD MSF reports LBA 0 as 00:02:00 (the mandatory 150-frame
            // lead-in), with the address stored in bytes 5..7.
            var frames = (ulong)lba + 150;
            response[offset + 5] = (byte)(frames / (60 * 75));
            response[offset + 6] = (byte)((frames / 75) % 60);
            response[offset + 7] = (byte)(frames % 75);
        }

        private static byte[] truncate_atapi_response(byte[] response, int allocationLength)
        {
            if (allocationLength >= response.Length)
            {
                return response;
            }
            var result = new byte[allocationLength];
            Array.Copy(response, result, allocationLength);
            return result;
        }

        private static uint read_be_uint32(byte[] source, int offset)
        {
            return ((uint)source[offset] << 24) |
                ((uint)source[offset + 1] << 16) |
                ((uint)source[offset + 2] << 8) |
                source[offset + 3];
        }

        private static ushort read_be_uint16(byte[] source, int offset)
        {
            return (ushort)((source[offset] << 8) | source[offset + 1]);
        }

        private static void write_be_uint32(byte[] destination, int offset, uint value)
        {
            destination[offset] = (byte)(value >> 24);
            destination[offset + 1] = (byte)(value >> 16);
            destination[offset + 2] = (byte)(value >> 8);
            destination[offset + 3] = (byte)value;
        }

        private static void copy_scsi_ascii(byte[] destination, int offset, int length, string source)
        {
            for (var index = 0; index < length; index++)
            {
                destination[offset + index] = index < source.Length ?
                    (byte)source[index] : (byte)0x20;
            }
        }
        #region Utils

        private static void make_ata_string(byte[] dst, int dstOffset, int n_words, string srcText)
        {
            byte[] srcArray = Encoding.ASCII.GetBytes(srcText);
            for (int i = 0; i < n_words * 2; i++)
            {
                byte value = i < srcArray.Length ? srcArray[i] : (byte)0x20;
                if (value < 0x20 || value > 0x7E)
                    value = 0x20;
                dst[dstOffset + i] = value;
            }
            for (int i = 0; i < n_words * 2; i += 2)
            {
                dst[dstOffset + i] ^= dst[dstOffset + i + 1];
                dst[dstOffset + i + 1] ^= dst[dstOffset + i];
                dst[dstOffset + i] ^= dst[dstOffset + i + 1];
            }
        }

        private static void setUInt16(byte[] transbf, int index, UInt16 value)
        {
            transbf[index] = (byte)value;
            transbf[index + 1] = (byte)(value >> 8);
        }

        private static void setUInt32(byte[] transbf, int index, UInt32 value)
        {
            transbf[index] = (byte)value;
            transbf[index + 1] = (byte)(value >> 8);
            transbf[index + 2] = (byte)(value >> 16);
            transbf[index + 3] = (byte)(value >> 24);
        }

        #endregion
    }
}
