using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Cpu;
using ZXMAK2.Hardware.Evo;


internal static class NeoGsProbe
{
    private const string ExpectedRomHash =
        "764FC819F831F64CE736D9254AF4FB6DD6FCD154D09F494F3AFAB92543A0CD37";

    private static int Main()
    {
        try
        {
            VerifyOfficialRom();

            var card = new NeoGsDevice();
            Invoke(card, "LoadRom");
            Invoke(card, "ResetCard");

            Assert((byte)GetField(card, "m_config") == 0x30,
                "GSCFG0 reset value is not 10 MHz");
            var pages = (byte[])GetField(card, "m_page");
            Assert(pages.SequenceEqual(new byte[] { 0, 3, 0, 1 }),
                "Reset memory map differs from Rev. C FPGA");

            Invoke(card, "WritePort", (ushort)0x000F, (byte)0x8D);
            Assert((byte)Invoke(card, "ReadPort", (ushort)0x000F) == 0x8D,
                "All eight GSCFG0 bits are not retained");

            Invoke(card, "WritePort", (ushort)0x0000, (byte)0x81);
            Assert(pages[2] == 3,
                "Extended MPAG rotation is incorrect");
            Invoke(card, "WritePort", (ushort)0x0010, (byte)0x82);
            Assert(pages[3] == 5,
                "Extended MPAGEX rotation is incorrect");

            Invoke(card, "WritePort", (ushort)0x0020, (byte)0x44);
            Invoke(card, "WritePort", (ushort)0x0021, (byte)0x45);
            Assert(pages[0] == 0x44 && pages[1] == 0x45,
                "Direct page registers are not implemented");

            SetField(card, "m_config", (byte)0x01);
            pages[0] = 7;
            Invoke(card, "WriteMemory", (ushort)0x0100, (byte)0xA5);
            Assert((byte)Invoke(card, "ReadMemory", (ushort)0x0100) == 0xA5,
                "4 MB RAM mapping failed");

            Invoke(card, "WritePort", (ushort)0x0006, (byte)0xFF);
            var volumes = (byte[])GetField(card, "m_volume");
            Assert(volumes[0] == 0x3F,
                "Volume register is not limited to six bits");
            Assert(
                (int)InvokeStatic(
                    typeof(NeoGsDevice),
                    "ApplyOutputGain",
                    1000) == 1500 &&
                (int)InvokeStatic(
                    typeof(NeoGsDevice),
                    "ApplyOutputGain",
                    -1000) == -1500,
                "NeoGS output gain is not exactly 150 percent");

            Invoke(card, "ResetCard");
            var irqCpu = (CpuUnit)GetField(card, "m_cpu");
            SetField(card, "m_interruptEnable", (byte)7);
            SetField(card, "m_interruptRequest", (byte)2);
            Invoke(card, "InterruptAcknowledge");
            Assert(irqCpu.BUS == 0xF7,
                "SD interrupt vector differs from the current FPGA");
            SetField(card, "m_interruptRequest", (byte)4);
            Invoke(card, "InterruptAcknowledge");
            Assert(irqCpu.BUS == 0xEF,
                "MP3 interrupt vector differs from the current FPGA");

            Invoke(card, "ResetCard");
            var cpu = (CpuUnit)GetField(card, "m_cpu");
            var pcBefore = cpu.regs.PC;
            Invoke(card, "ExecuteTo", 24000000L);
            Assert(cpu.regs.PC != pcBefore || cpu.Tact > 3,
                "Official NeoGS firmware did not execute");
            var bootConfig = (byte)GetField(card, "m_config");
            Assert((bootConfig & 0x30) == 0x20,
                "ROM 1.11 did not switch the NeoGS Z80 to documented 20 MHz; " +
                "GSCFG0=" + bootConfig.ToString("X2") +
                ", PC=" + cpu.regs.PC.ToString("X4") +
                ", tacts=" + cpu.Tact);

            SetField(card, "m_commandFromHost", (byte)0xF3);
            SetField(card, "m_status", (byte)0x01);
            var boardMaster = (long)GetField(card, "m_boardMaster");
            Invoke(card, "ExecuteTo", boardMaster + 12000000L);
            Assert((((byte)GetField(card, "m_status")) & 1) == 0,
                "Firmware did not acknowledge a host command through #BB");

            VerifyMachineIntegration();

            Console.WriteLine(
                "PASS: NeoGS Rev. C-VS core, ROM 1.11, paging and FPGA ports");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void VerifyMachineIntegration()
    {
        var bus = new BusManager();
        bus.Init(null, true);
        try
        {
            Assert(bus.FindDevice<UlaPentEvo>() != null,
                "Default test machine is not ZX Evolution BaseConf");
            bus.Disconnect();

            var xml = new XmlDocument();
            var root = xml.AppendChild(xml.CreateElement("Bus"));
            bus.SaveConfigXml(root);
            var ulaNode = root.ChildNodes.Cast<XmlNode>()
                .First(node => node.Attributes["type"].Value.Contains(
                    "UlaPentEvo"));
            ulaNode.Attributes["zxBusSlot1Enabled"].Value = "True";
            ulaNode.Attributes["zxBusSlot1Device"].Value = "NeoGS";

            var neoNode = xml.CreateElement("Device");
            neoNode.SetAttribute(
                "type",
                "ZXMAK2.Hardware.Evo.NeoGsDevice, ZXMAK2.Hardware");
            neoNode.SetAttribute("volume", "100");
            root.AppendChild(neoNode);

            bus.LoadConfigXml(root);
            Assert(bus.FindDevice<NeoGsDevice>() != null,
                "NeoGS did not connect through the real machine XML path");
            for (var i = 0; i < 80000; i++)
                bus.ExecCycle();
            Assert(bus.SoundFrame != null,
                "NeoGS did not join the emulator sound mixer");
        }
        finally
        {
            bus.Disconnect();
        }
    }

    private static void VerifyOfficialRom()
    {
        var assembly = typeof(NeoGsDevice).Assembly;
        using (var stream = assembly.GetManifestResourceStream(
            "ZXMAK2.Hardware.Resources.NeoGS-1.11.rom"))
        {
            Assert(stream != null, "Embedded NeoGS ROM is missing");
            Assert(stream.Length == 524288, "Embedded NeoGS ROM size mismatch");
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(stream))
                    .Replace("-", string.Empty);
                Assert(actual == ExpectedRomHash,
                    "Embedded ROM is not official NeoGS 1.11");
            }
        }
    }

    private static object Invoke(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "Method not found: " + name);
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    private static object InvokeStatic(
        Type type,
        string name,
        params object[] args)
    {
        var method = type.GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(method != null, "Static method not found: " + name);
        try
        {
            return method.Invoke(null, args);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
    }

    private static object GetField(object target, string name)
    {
        var field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Field not found: " + name);
        return field.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Field not found: " + name);
        field.SetValue(target, value);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
