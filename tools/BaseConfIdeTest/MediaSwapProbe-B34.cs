using System;
using System.IO;
using System.Reflection;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.WinForms.Views;


internal static class MediaSwapProbeB34
{
    private static int s_checks;

    private static void Check(bool condition, string message)
    {
        s_checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static int Position(string text, string value, int after)
    {
        var result = text.IndexOf(value, after, StringComparison.Ordinal);
        Check(result >= 0, "Missing source contract: " + value);
        return result;
    }

    private static FieldInfo Field(string name)
    {
        var field = typeof(IdePentEvo).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check(field != null, "Missing IDE latch: " + name);
        return field;
    }

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3)
                throw new ArgumentException(
                    "Usage: MediaSwapProbe-B34 <MainViewModel.cs> <FormMachineSettings.cs> <IdePentEvo.cs>");

            var main = File.ReadAllText(args[0]);
            var mediaStart = Position(main, "public void ExecuteMediaChange", 0);
            var execute = Position(main, "command.Execute(commandParameter);", mediaStart);
            var powerCycle = Position(main, "m_vm.DoPowerCycle();", execute);
            var finallyBlock = Position(main, "finally", powerCycle);
            var resume = Position(main, "m_vm.DoRun();", finallyBlock);
            Check(execute < powerCycle && powerCycle < finallyBlock && finallyBlock < resume,
                "SD replacement does not finish its cold cycle before resume.");

            var settings = File.ReadAllText(args[1]);
            var applyStart = Position(settings, "private void btnApply_Click", 0);
            var detect = Position(settings, "var ideMediaChanged = IsIdeMediaChanged", applyStart);
            var reconnect = Position(settings, "bmgr.LoadConfigXml(root);", detect);
            var hddPowerCycle = Position(settings, "m_vm.DoPowerCycle();", reconnect);
            var hddResume = Position(settings, "m_vm.DoRun();", hddPowerCycle);
            Check(detect < reconnect && reconnect < hddPowerCycle && hddPowerCycle < hddResume,
                "HDD replacement ordering is not disconnect/open/cold-cycle/resume.");

            var ideSource = File.ReadAllText(args[2]);
            Check(ideSource.Contains("m_ata.Reset();"),
                "IDE hardware reset does not reach the ATA core.");

            var ide = new IdePentEvo();
            var latchNames = new[]
            {
                "m_ide_write",
                "m_ide_hi_byte_w",
                "m_ide_hi_byte_w1",
                "m_ide_hi_byte_r",
                "m_ide_read"
            };
            foreach (var name in latchNames)
                Field(name).SetValue(ide, 0x55);

            var reset = typeof(IdePentEvo).GetMethod(
                "BusReset",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(reset != null, "IDE reset handler is missing.");
            reset.Invoke(ide, null);
            foreach (var name in latchNames)
                Check((int)Field(name).GetValue(ide) == 0,
                    "IDE latch survived reset: " + name);

            var first = Path.Combine(Path.GetTempPath(), "zxmak2-b34-a.hdd");
            var second = Path.Combine(Path.GetTempPath(), "zxmak2-b34-b.hdd");
            try
            {
                using (var stream = new FileStream(first, FileMode.Create, FileAccess.Write, FileShare.None))
                    stream.SetLength(512 * 32);
                using (var stream = new FileStream(second, FileMode.Create, FileAccess.Write, FileShare.None))
                    stream.SetLength(512 * 64);

                var current = new IdePentEvo();
                var pending = new IdePentEvo();
                current.ConfigureHardDisk(first, false);
                pending.ConfigureHardDisk(first, false);

                var changed = typeof(FormMachineSettings).GetMethod(
                    "IsIdeMediaChanged",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Check(changed != null, "HDD media comparison is missing.");
                Check(!(bool)changed.Invoke(null, new object[] { current, pending }),
                    "An unchanged HDD was reported as a replacement.");

                pending.ConfigureHardDisk(second, false);
                Check((bool)changed.Invoke(null, new object[] { current, pending }),
                    "A different HDD was not reported as a replacement.");

                pending.DisconnectHardDisk();
                Check((bool)changed.Invoke(null, new object[] { current, pending }),
                    "HDD eject was not reported as a media change.");
            }
            finally
            {
                if (File.Exists(first))
                    File.Delete(first);
                if (File.Exists(second))
                    File.Delete(second);
            }

            Console.WriteLine("MediaSwapProbe-B34: {0} PASS", s_checks);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
