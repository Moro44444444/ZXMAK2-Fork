using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;


internal static class PentEvoProfileProbe
{
    [STAThread]
    private static int Main()
    {
        try
        {
            var bus = new BusManager();
            bus.Init(null, true);
            bus.Disconnect();
            bus.Clear();
            var ula = new UlaPentEvo();
            var ay = new AYCHRV();
            ay.Volume = 73;
            bus.Add(ula);
            bus.Add(ay);

            var control = new CtlSettingsPentEvo();
            control.Size = new Size(584, 420);
            control.Init(bus, null, ula);
            AssertLayoutFits(control);
            var sound = GetField<ComboBox>(control, "m_internalSound");
            var slot1 = GetField<CheckBox>(control, "m_slot1Enabled");
            var slot2 = GetField<CheckBox>(control, "m_slot2Enabled");

            sound.SelectedIndex = 1;
            slot1.Checked = true;
            slot2.Checked = false;
            control.Apply();
            Assert(bus.FindDevice<AYCHRV>() == null, "AY was not removed");
            Assert(
                ula.InternalSound == PentEvoInternalSound.None,
                "None selection was not stored");
            Assert(ula.ZxBusSlot1Enabled, "Slot 1 state was not stored");
            Assert(!ula.ZxBusSlot2Enabled, "Slot 2 state was not stored");

            sound.SelectedIndex = 0;
            control.Apply();
            Assert(bus.FindDevice<AYCHRV>() != null, "AY was not restored");
            Assert(
                ula.InternalSound == PentEvoInternalSound.AY8910CHRV,
                "AY selection was not stored");

            var xml = new XmlDocument();
            var node = xml.AppendChild(xml.CreateElement("Device"));
            ula.SaveConfigXml(node);
            Assert(
                node.Attributes["internalSound"].Value == "AY8910CHRV",
                "Internal sound XML mismatch");
            Assert(
                node.Attributes["zxBusSlot1Enabled"].Value == "True",
                "Slot 1 XML mismatch");
            Assert(
                node.Attributes["zxBusSlot1Device"].Value == "Empty",
                "Slot 1 device XML mismatch");

            var restored = new UlaPentEvo();
            restored.LoadConfigXml(node);
            Assert(restored.ZxBusSlot1Enabled, "Slot 1 XML did not reload");
            Assert(
                restored.ZxBusSlot1Device == PentEvoZxBusDevice.Empty,
                "Slot 1 device XML did not reload");

            var init = typeof(CtlSettingsPentEvo).GetMethod(
                "Init",
                new Type[]
                {
                    typeof(BusManager),
                    typeof(ZXMAK2.Host.Interfaces.IHostService),
                    typeof(UlaPentEvo),
                });
            Assert(init != null, "Exact PENTEVO settings binding is missing");

            if (Environment.GetCommandLineArgs().Length > 1)
            {
                using (var bitmap = new Bitmap(control.Width, control.Height))
                {
                    control.DrawToBitmap(
                        bitmap,
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(
                        Environment.GetCommandLineArgs()[1],
                        ImageFormat.Png);
                }
            }

            Console.WriteLine("PASS: PENTEVO sound and ZXBUS profile round-trip");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static T GetField<T>(object target, string name)
        where T : class
    {
        var field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "Field not found: " + name);
        var value = field.GetValue(target) as T;
        Assert(value != null, "Field has unexpected type: " + name);
        return value;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void AssertLayoutFits(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            Assert(
                child.Left >= 0 && child.Top >= 0 &&
                child.Right <= parent.ClientSize.Width &&
                child.Bottom <= parent.ClientSize.Height,
                "Control is clipped: " + child.GetType().Name +
                " " + child.Bounds + " in " + parent.ClientSize);
            AssertLayoutFits(child);
        }
    }
}
