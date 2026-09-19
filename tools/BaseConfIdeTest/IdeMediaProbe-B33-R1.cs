using System;
using System.IO;
using ZXMAK2.Hardware.Circuits.Ata;


internal static class IdeMediaProbeB33R1
{
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1)
                throw new ArgumentException("Usage: IdeMediaProbe-B33-R1 <ATM_HDD.hdd>");

            var info = new AtaDeviceInfo();
            info.ConfigureImage(args[0], false);
            Check(Path.GetFullPath(args[0]) == info.FileName, "Image path was not normalised.");
            Check(info.Lba == 403200, "Unexpected exact LBA.");
            Check(info.Cylinders == 400, "Unexpected cylinders from ATM_HDD.inf.");
            Check(info.Heads == 16, "Unexpected heads from ATM_HDD.inf.");
            Check(info.Sectors == 63, "Unexpected sectors from ATM_HDD.inf.");
            Check(!info.ReadOnly, "Writable image unexpectedly became read-only.");

            info.ConfigureImage(args[0], true);
            Check(info.ReadOnly, "Explicit read-only mode was not preserved.");

            info.Disconnect();
            Check(info.FileName == string.Empty, "Disconnect did not clear the image.");
            Check(!info.ReadOnly, "Disconnected HDD must default to writable mode.");

            var invalid = Path.Combine(Path.GetTempPath(), "zxmak2-b33-r1-invalid.hdd");
            try
            {
                using (var stream = new FileStream(invalid, FileMode.Create, FileAccess.Write, FileShare.None))
                    stream.SetLength(513);
                var rejected = false;
                try
                {
                    info.ConfigureImage(invalid, false);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }
                Check(rejected, "Non-sector-aligned image was accepted.");
            }
            finally
            {
                if (File.Exists(invalid))
                    File.Delete(invalid);
            }

            Console.WriteLine("IdeMediaProbe-B33-R1: {0} PASS", s_checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
