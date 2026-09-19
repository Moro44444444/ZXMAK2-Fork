using System;
using System.Collections.Generic;
using System.IO;

internal static class IdePortDecodeProbeB33
{
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool RtlIdeRegister(int low)
    {
        return (low & 7) == 0 && (((low >> 3) & 1) != ((low >> 4) & 1));
    }

    private static bool EmulatorGenericSubscription(int low)
    {
        return (low & 0x1F) == 0x10 || (low & 0x1F) == 0x08;
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
                throw new ArgumentException("Usage: IdePortDecodeProbe-B33 <IdePentEvo.cs> <r1364-zports.v>");

            string source = File.ReadAllText(args[0]);
            string rtl = File.ReadAllText(args[1]);

            Check(rtl.Contains("`define IS_NIDE_REGS(x) ( (x[2:0]==3'b000) && (x[3]!=x[4]) )"),
                "The pinned r1364 IDE-register formula changed.");
            Check(rtl.Contains("`define IS_NIDE_HIGH(x) ( x[7:0]==8'h11 )"),
                "The pinned r1364 high-byte formula changed.");

            string altRead = "SubscribeRdIo(0xFF, 0xC8, ReadIdeAltStatus)";
            string altWrite = "SubscribeWrIo(0xFF, 0xC8, WriteIdeAltStatus)";
            string highRead = "SubscribeRdIo(0xFF, 0x11, ReadIde)";
            string highWrite = "SubscribeWrIo(0xFF, 0x11, WriteIde)";
            string x10Read = "SubscribeRdIo(0x1F, 0x10, ReadIde)";
            string x10Write = "SubscribeWrIo(0x1F, 0x10, WriteIde)";
            string x08Read = "SubscribeRdIo(0x1F, 0x08, ReadIde)";
            string x08Write = "SubscribeWrIo(0x1F, 0x08, WriteIde)";

            foreach (string marker in new[]
            {
                altRead, altWrite, highRead, highWrite,
                x10Read, x10Write, x08Read, x08Write
            })
                Check(source.Contains(marker), "Missing exact subscription: " + marker);

            Check(!source.Contains("SubscribeRdIo(0x1E, 0x10, ReadIde)"),
                "The obsolete broad read mask is still present.");
            Check(!source.Contains("SubscribeWrIo(0x1E, 0x10, WriteIde)"),
                "The obsolete broad write mask is still present.");
            Check(source.IndexOf(altRead, StringComparison.Ordinal) < source.IndexOf(x08Read, StringComparison.Ordinal),
                "C8 read handler must precede the generic x08 subscription.");
            Check(source.IndexOf(altWrite, StringComparison.Ordinal) < source.IndexOf(x08Write, StringComparison.Ordinal),
                "C8 write handler must precede the generic x08 subscription.");

            var expected = new HashSet<int>
            {
                0x08, 0x10, 0x28, 0x30, 0x48, 0x50, 0x68, 0x70,
                0x88, 0x90, 0xA8, 0xB0, 0xC8, 0xD0, 0xE8, 0xF0
            };

            int rtlCount = 0;
            int subscriptionCount = 0;
            for (int low = 0; low < 256; low++)
            {
                bool rtlRegister = RtlIdeRegister(low);
                bool subscribed = EmulatorGenericSubscription(low);
                Check(rtlRegister == expected.Contains(low),
                    string.Format("Unexpected pinned RTL set membership at #{0:X2}.", low));
                Check(subscribed == rtlRegister,
                    string.Format("Subscription differs from r1364 at #{0:X2}.", low));
                Check((low == 0x11) == ((low & 0xFF) == 0x11),
                    string.Format("High-byte decode mismatch at #{0:X2}.", low));
                if (rtlRegister) rtlCount++;
                if (subscribed) subscriptionCount++;
            }

            Check(rtlCount == 16, "r1364 must expose sixteen IDE register aliases.");
            Check(subscriptionCount == 16, "The emulator must subscribe sixteen IDE register aliases.");
            Check(RtlIdeRegister(0xC8), "C8 must remain inside IS_NIDE_REGS.");
            Check(source.Contains("addr >>= 5;") && source.Contains("addr &= 7;"),
                "ATA register selection must remain A7..A5.");

            Console.WriteLine("PASS: {0} B33 Nemo IDE checks: all 256 low-byte values, #11 high byte, #C8 priority and A7..A5 register selection.", s_checks);
            Console.WriteLine("LIMIT: compiled structural probe only; HDD boot/read/write runtime acceptance remains a user test.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("B33 NEMO IDE PROBE FAILED: " + ex);
            return 1;
        }
    }
}
