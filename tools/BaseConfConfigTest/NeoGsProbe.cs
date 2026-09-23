using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
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
            Assert(
                (int)InvokeStatic(
                    typeof(NeoGsDevice),
                    "ApplyMp3Gain",
                    1000) == 1400 &&
                (int)InvokeStatic(
                    typeof(NeoGsDevice),
                    "ApplyMp3Gain",
                    -1000) == -1400,
                "NeoGS MP3 gain is not exactly 140 percent");

            Invoke(card, "ResetCard");
            Invoke(card, "WritePort", (ushort)0x001B, (byte)1);
            Invoke(card, "WritePort", (ushort)0x001C, (byte)0x3F);
            Invoke(card, "WritePort", (ushort)0x001D, (byte)0xA5);
            Invoke(card, "WritePort", (ushort)0x001E, (byte)0x5A);
            Assert((byte)Invoke(card, "ReadPort", (ushort)0x001C) == 0x3F &&
                (byte)Invoke(card, "ReadPort", (ushort)0x001D) == 0xA5 &&
                (byte)Invoke(card, "ReadPort", (ushort)0x001E) == 0x5A,
                "ZX DMA does not retain the documented 22-bit address");
            Invoke(card, "WritePort", (ushort)0x001F, (byte)0x80);
            Assert((byte)Invoke(card, "ReadPort", (ushort)0x001F) == 0x80,
                "ZX DMA enable bit is not readable");
            Invoke(card, "WritePort", (ushort)0x001F, (byte)0x00);
            Assert((byte)Invoke(card, "ReadPort", (ushort)0x001F) == 0x00,
                "ZX DMA disable bit is not readable");

            VerifyVs1011Protocol();
            VerifyVs1011DecodePath();
            VerifySdAndPeripheralDma();

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
                "PASS: NeoGS Rev. C-VS core, ROM 1.11, DMA, microSD and VS1011");
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
            neoNode.SetAttribute("sdImage", @"C:\NeoGS\card.img");
            root.AppendChild(neoNode);

            bus.LoadConfigXml(root);
            Assert(bus.FindDevice<NeoGsDevice>() != null,
                "NeoGS did not connect through the real machine XML path");
            Assert(bus.FindDevice<NeoGsDevice>().ConfiguredSdImageFileName ==
                @"C:\NeoGS\card.img",
                "NeoGS microSD image path was not restored from the profile");
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

    private static void VerifyVs1011Protocol()
    {
        var codecType = typeof(NeoGsDevice).Assembly.GetType(
            "ZXMAK2.Hardware.Evo.NeoGsVs10xx", true);
        var codec = Activator.CreateInstance(codecType, true);
        try
        {
            Invoke(codec, "HardwareReset", true);
            Invoke(codec, "SetControlSelected", true);
            Invoke(codec, "WriteControl", (byte)3);
            Invoke(codec, "WriteControl", (byte)1);
            var high = (byte)Invoke(codec, "ReadControl");
            var low = (byte)Invoke(codec, "ReadControl");
            Assert((high & 0xF0) == 0 && (low & 0xF0) == 0x20,
                "VS1011 STATUS does not report decoder version 2");

            Invoke(codec, "WriteControl", (byte)2);
            Invoke(codec, "WriteControl", (byte)11);
            Invoke(codec, "WriteControl", (byte)0x10);
            Invoke(codec, "WriteControl", (byte)0x20);
            Invoke(codec, "WriteControl", (byte)3);
            Invoke(codec, "WriteControl", (byte)11);
            high = (byte)Invoke(codec, "ReadControl");
            low = (byte)Invoke(codec, "ReadControl");
            Assert(high == 0x10 && low == 0x20,
                "VS1011 SCI write/read transaction failed");
        }
        finally
        {
            ((IDisposable)codec).Dispose();
        }
    }

    private static void VerifyVs1011DecodePath()
    {
        var codecType = typeof(NeoGsDevice).Assembly.GetType(
            "ZXMAK2.Hardware.Evo.NeoGsVs10xx", true);
        var codec = Activator.CreateInstance(codecType, true);
        try
        {
            Invoke(codec, "HardwareReset", true);
            var stream = CreateSilentMp3(12);
            for (var index = 0; index < stream.Length; index++)
            {
                Assert((bool)Invoke(codec, "WriteData", stream[index]),
                    "VS1011 input FIFO rejected a valid MP3 stream");
            }

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while ((int)GetProperty(codec, "SampleRate") == 0 &&
                DateTime.UtcNow < deadline)
                Thread.Sleep(10);

            Assert((int)GetProperty(codec, "SampleRate") == 44100,
                "VS1011/NLayer path did not recognize MPEG-1 Layer III");

            var args = new object[] { (short)123, (short)123 };
            deadline = DateTime.UtcNow.AddSeconds(3);
            while (!(bool)Invoke(codec, "TryReadSample", args) &&
                DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10);
                args[0] = (short)123;
                args[1] = (short)123;
            }
            Assert((short)args[0] == 0 && (short)args[1] == 0,
                "Decoded silent MP3 frame is not silent");
        }
        finally
        {
            ((IDisposable)codec).Dispose();
        }
    }

    private static void VerifySdAndPeripheralDma()
    {
        var image = Path.Combine(
            Path.GetTempPath(),
            "zxmak2-neogs-sd-" + Guid.NewGuid().ToString("N") + ".img");
        try
        {
            var bootSector = CreateFatBootSector();
            using (var output = new FileStream(
                image,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                output.SetLength(1024 * 1024);
                output.Position = 0;
                output.Write(bootSector, 0, bootSector.Length);
            }

            var card = new NeoGsDevice();
            Invoke(card, "LoadRom");
            SetField(card, "m_sandbox", false);
            SetField(
                card,
                "m_sdCard",
                new ZXMAK2.Hardware.Circuits.SecureDigital.SdCard());
            SetField(
                card,
                "m_codec",
                Activator.CreateInstance(
                    typeof(NeoGsDevice).Assembly.GetType(
                        "ZXMAK2.Hardware.Evo.NeoGsVs10xx", true),
                    true));
            card.ConfigureSdCard(image);
            Invoke(card, "ResetCard");
            Assert(card.IsSdCardMounted,
                "NeoGS microSD image did not mount");

            // Select SD (active low), issue CMD17 for sector zero, then let
            // module 2 consume the FE token, 512 bytes and both CRC bytes.
            Invoke(card, "WritePort", (ushort)0x0011, (byte)0x01);
            foreach (var value in new byte[] { 0x51, 0, 0, 0, 0, 0xFF })
                Invoke(card, "WritePort", (ushort)0x0013, value);
            Assert((byte)Invoke(card, "ReadPort", (ushort)0x0013) == 0,
                "microSD CMD17 did not return a ready R1 response");

            SetDma(card, 2, 0x001000U);
            RunPeripheralDma(card, 2, 2048);
            var ram = (byte[])GetField(card, "m_ram");
            Assert(bootSector.SequenceEqual(ram.Skip(0x1000).Take(512)),
                "SD DMA did not transfer the complete 512-byte sector");
            var addresses = (uint[])GetField(card, "m_dmaAddress");
            Assert(addresses[2] == 0x001200U,
                "SD DMA did not advance its 22-bit address by 512 bytes");
            Assert((((byte)GetField(card, "m_interruptRequest")) & 2) != 0,
                "SD DMA did not raise the documented interrupt request");

            // Release the VS1011 reset and verify module 3 transfers one
            // complete hardware block from NeoGS RAM into the data interface.
            Invoke(card, "WritePort", (ushort)0x0011, (byte)0x84);
            for (var index = 0; index < 512; index++)
                ram[0x2000 + index] = (byte)index;
            SetDma(card, 3, 0x002000U);
            RunPeripheralDma(card, 3, 2048);
            Assert(addresses[3] == 0x002200U,
                "MP3 DMA did not advance its 22-bit address by 512 bytes");
            Assert((((byte)GetField(card, "m_interruptRequest")) & 4) != 0,
                "MP3 DMA did not raise the documented interrupt request");

            ((IDisposable)GetField(card, "m_codec")).Dispose();
            ((ZXMAK2.Hardware.Circuits.SecureDigital.SdCard)GetField(
                card, "m_sdCard")).Close();
        }
        finally
        {
            if (File.Exists(image))
                File.Delete(image);
        }
    }

    private static void SetDma(NeoGsDevice card, int module, uint address)
    {
        Invoke(card, "WritePort", (ushort)0x001B, (byte)module);
        Invoke(card, "WritePort", (ushort)0x001C,
            (byte)((address >> 16) & 0x3F));
        Invoke(card, "WritePort", (ushort)0x001D, (byte)(address >> 8));
        Invoke(card, "WritePort", (ushort)0x001E, (byte)address);
        Invoke(card, "WritePort", (ushort)0x001F, (byte)0x80);
    }

    private static void RunPeripheralDma(
        NeoGsDevice card,
        int module,
        int maximumSteps)
    {
        var enabled = (bool[])GetField(card, "m_dmaEnabled");
        for (var step = 0; step < maximumSteps && enabled[module]; step++)
        {
            SetField(
                card,
                "m_boardMaster",
                (long)GetField(card, "m_nextPeripheralDmaMaster"));
            Invoke(card, "ProcessPeripheralDma");
        }
        Assert(!enabled[module],
            "DMA module " + module + " did not finish");
    }

    private static byte[] CreateFatBootSector()
    {
        var sector = new byte[512];
        sector[0] = 0xEB;
        sector[1] = 0x3C;
        sector[2] = 0x90;
        sector[11] = 0x00;
        sector[12] = 0x02; // 512 bytes per sector
        sector[13] = 1;
        sector[14] = 1;
        sector[16] = 2;
        sector[19] = 0x00;
        sector[20] = 0x08; // 2048 sectors
        sector[510] = 0x55;
        sector[511] = 0xAA;
        return sector;
    }

    private static byte[] CreateSilentMp3(int frameCount)
    {
        const int frameLength = 417;
        var header = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };
        var result = new byte[frameLength * frameCount];
        for (var frame = 0; frame < frameCount; frame++)
            Array.Copy(header, 0, result, frame * frameLength, header.Length);
        return result;
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
            BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public);
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

    private static object GetProperty(object target, string name)
    {
        var property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public);
        Assert(property != null, "Property not found: " + name);
        return property.GetValue(target, null);
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
