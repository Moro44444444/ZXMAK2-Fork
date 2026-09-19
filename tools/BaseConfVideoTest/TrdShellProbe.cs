using System;
using System.IO;
using System.Text;
using ZXMAK2.Model.Disk;
using ZXMAK2.Serializers.DiskSerializers;

internal static class TrdShellProbeB23
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static int Word(byte[] data, int offset)
    {
        return data[offset] | data[offset + 1] << 8;
    }

    private static int FileOffset(byte[] data, int entry)
    {
        int offset = entry * 16;
        return (data[offset + 15] * 16 + data[offset + 14]) * 256;
    }

    private static string Name(byte[] data, int entry)
    {
        return Encoding.ASCII.GetString(data, entry * 16, 8);
    }

    private static bool Contains(byte[] data, int offset, int length, string text)
    {
        string body = Encoding.ASCII.GetString(data, offset, length);
        return body.IndexOf(text, StringComparison.Ordinal) >= 0;
    }

    private static int Find(byte[] data, int offset, int length, byte[] pattern)
    {
        for (int i = offset; i <= offset + length - pattern.Length; i++)
        {
            int j = 0;
            while (j < pattern.Length && data[i + j] == pattern[j]) j++;
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    public static int Main(string[] args)
    {
        try
        {
            byte[] trd = File.ReadAllBytes(args[0]);
            Check(trd.Length == 655360,
                "TRD must be a full 80-track double-sided image.");

            var disk = new DiskImage();
            var serializer = new TrdSerializer(disk);
            using (var input = new MemoryStream(trd, false))
                serializer.Deserialize(input);
            Check(disk.Present && disk.CylynderCount == 80 && disk.SideCount == 2,
                "ZXMAK2 TRD loader did not mount an 80-track double-sided disk.");

            byte[] logical = new byte[256];
            disk.ReadLogicalSector(0, 0, 1, logical);
            for (int i = 0; i < 256; i++)
                Check(logical[i] == trd[i], "ZXMAK2 directory-sector mismatch.");
            disk.ReadLogicalSector(0, 0, 9, logical);
            for (int i = 0; i < 256; i++)
                Check(logical[i] == trd[0x800 + i], "ZXMAK2 system-sector mismatch.");

            using (var output = new MemoryStream())
            {
                serializer.Serialize(output);
                byte[] roundTrip = output.ToArray();
                Check(roundTrip.Length == trd.Length,
                    "ZXMAK2 TRD round-trip changed image length.");
                for (int i = 0; i < trd.Length; i++)
                    Check(roundTrip[i] == trd[i],
                        "ZXMAK2 TRD round-trip changed byte " + i + ".");
            }

            int system = 0x800;
            Check(trd[system + 0xE1] == 11 && trd[system + 0xE2] == 1,
                "Wrong first-free TR-DOS position.");
            Check(trd[system + 0xE3] == 0x16, "Wrong TR-DOS disk type.");
            Check(trd[system + 0xE4] == 2, "Unexpected directory file count.");
            Check(Word(trd, system + 0xE5) == 2533, "Wrong free-sector count.");
            Check(trd[system + 0xE7] == 0x10, "Missing TR-DOS signature.");
            Check(Encoding.ASCII.GetString(trd, system + 0xF5, 8) == "BC23    ",
                "Wrong disk label.");

            Check(Name(trd, 0) == "boot    " && trd[8] == (byte)'B',
                "First directory entry is not boot.B.");
            Check(Word(trd, 9) == 53 && Word(trd, 11) == 53 && trd[13] == 1,
                "Unexpected corrected BASIC loader metadata.");
            int boot = FileOffset(trd, 0);
            Check(trd[boot] == 0 && trd[boot + 1] == 10,
                "BASIC line 10 missing.");
            Check(trd[boot + 4] == 0xFD && trd[boot + 5] == 0xB0,
                "CLEAR VAL loader prefix missing.");
            byte[] enterTrDosAndLoad = {
                0xF9, 0xC0, 0xB0, 0x22, 0x31, 0x35, 0x36, 0x31, 0x39, 0x22,
                0x3A, 0xEA, 0x3A, 0xEF, 0x22, 0x42, 0x43, 0x4D, 0x45, 0x4E,
                0x55, 0x22, 0xAF
            };
            Check(Find(trd, boot, 53, enterTrDosAndLoad) >= 0,
                "boot.B does not enter TR-DOS before LOAD BCMENU CODE.");
            Check(trd[boot + 38] == 0 && trd[boot + 39] == 20,
                "BASIC line 20 launcher missing.");
            Check(trd[boot + 53] == 0x80 && trd[boot + 54] == 0xAA &&
                trd[boot + 55] == 10 && trd[boot + 56] == 0,
                "TR-DOS BASIC autostart marker missing.");

            int codeEntry = 16;
            Check(Name(trd, 1) == "BCMENU  " && trd[codeEntry + 8] == (byte)'C',
                "Second directory entry is not BCMENU.C.");
            Check(Word(trd, codeEntry + 9) == 32768,
                "Menu load address is not 32768.");
            int codeLength = Word(trd, codeEntry + 11);
            Check(codeLength > 2048 && codeLength < 4096 && trd[codeEntry + 13] == 10,
                "Unexpected menu code size.");
            int code = FileOffset(trd, 1);
            Check(trd[code] == 0xF3 && trd[code + 1] == 0x31 &&
                trd[code + 2] == 0xF0 && trd[code + 3] == 0xBF,
                "Menu entry does not initialize DI/SP.");

            string[] strings = {
                "BASECONF VIDEO B23", "BOOT SHELL / MODE SLOTS",
                "1 ZX 256X192 ATTR", "2 PENTAGON 256 HWM",
                "3 PENTAGON 256 16C", "4 ATM 320X200 16C",
                "5 ATM 640X200 HWM", "6 ATM TEXT 80X25",
                "7 BASECONF TEXT 80X25", "Q/A MOVE  ENTER OPEN",
                "1-7 DIRECT  SPACE BACK", "B23 TEST SLOT",
                "LABEL + PATTERN / ROUTE ACTIVE",
                "B23 PENTAGON 256 16C", "B23 ATM 320X200 16C",
                "B23 ATM 640X200 HWM", "B23 ATM TEXT 80X25",
                "B23 BASECONF TEXT 80X25",
                "CHARACTER GENERATOR + ATTRIBUTES ACTIVE"
            };
            foreach (string text in strings)
                Check(Contains(trd, code, codeLength, text),
                    "Menu text missing: " + text);

            byte[] read7ffd = { 0x01, 0xBE, 0x0A, 0xED, 0x78 };
            Check(Find(trd, code, codeLength, read7ffd) >= 0,
                "BaseConf #0ABE active-map read is missing.");
            byte[] readDescriptors = { 0x01, 0xBE, 0x08, 0xED, 0x78 };
            Check(Find(trd, code, codeLength, readDescriptors) >= 0,
                "BaseConf RAM/ROM descriptor read is missing.");
            byte[] descriptorWrite = { 0x01, 0xF7, 0xFF, 0x3E, 0x7F, 0xED, 0x79 };
            Check(Find(trd, code, codeLength, descriptorWrite) >= 0,
                "BaseConf #FFF7 descriptor write is missing.");
            byte[] directPageWrite = { 0x01, 0xF7, 0xF7, 0xED, 0x79 };
            Check(Find(trd, code, codeLength, directPageWrite) >= 0,
                "BaseConf #F7F7 direct-page write is missing.");
            byte[] old7ffdMapper = { 0x01, 0xFD, 0x7F, 0xED, 0x79 };
            Check(Find(trd, code, codeLength, old7ffdMapper) < 0,
                "Unsafe standalone #7FFD physical-page mapper remains.");
            byte[] fontWriteEnable = { 0xF6, 0x04, 0xED, 0x79 };
            Check(Find(trd, code, codeLength, fontWriteEnable) >= 0,
                "State-preserving BaseConf font-write enable is missing.");
            byte[] pairPixels = { 0x00, 0xB8, 0x47, 0xFF };
            Check(Find(trd, code, codeLength, pairPixels) >= 0,
                "Packed two-pixel conversion table is missing.");
            byte[] selectors = { 3, 19, 11, 0, 2, 6, 7 };
            Check(Find(trd, code, codeLength, selectors) >= 0,
                "Seven RG/RGEX raw selectors are missing.");
            byte[] frameDelay = { 0xF3, 0x01, 0x00, 0x00, 0x0B, 0x78, 0xB1, 0x20, 0xFB, 0xC9 };
            Check(Find(trd, code, codeLength, frameDelay) >= 0,
                "DI-only frame-boundary delay is missing.");
            byte[] romInterruptWait = { 0xFB, 0x76, 0x76, 0xF3, 0xC9 };
            Check(Find(trd, code, codeLength, romInterruptWait) < 0,
                "ROM EI/HALT wait still writes visible #5Cxx bytes into page 5.");

            Check(trd[32] == 0, "Unexpected third directory entry.");
            Console.WriteLine("PASS: " + checks +
                " B23 TRD checks: ZXMAK2 loader round-trip, catalog, " +
                "USR 15619 disk entry, safe BaseConf paging, DI-only frame wait, " +
                "seven patterns and font load.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("B23 TRD PROBE FAILED: " + e);
            return 1;
        }
    }
}
