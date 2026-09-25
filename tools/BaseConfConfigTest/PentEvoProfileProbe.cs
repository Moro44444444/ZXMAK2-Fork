using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;
using ZXMAK2.Mvvm;


internal static class PentEvoProfileProbe
{
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyFloppyIndicators();
            VerifySecureDigitalMenu();
            VerifyPentEvoMusicSelector();
            VerifyMachineSettingsNavigation();

            var bus = new BusManager();
            bus.Init(null, true);
            bus.Disconnect();
            bus.Clear();
            var ula = new UlaPentEvo();
            var ay = new AYCHRV();
            ay.Volume = 73;
            bus.Add(ula);
            bus.Add(ay);

            var control = new CtlSettingsPentEvoZxBus();
            control.Size = new Size(300, 430);
            control.Initialize(bus, null, ula);
            AssertLayoutFits(control);
            var neoGsSd = new CtlSettingsNeoGsSd();
            neoGsSd.Size = new Size(300, 240);
            neoGsSd.Initialize(bus, null);
            neoGsSd.SetBoardEnabled(control.IsNeoGsBoardEnabled);
            AssertLayoutFits(neoGsSd);
            var neoGsConnected =
                GetField<CheckBox>(neoGsSd, "m_connected");
            Assert(!neoGsConnected.Enabled,
                "NeoGS microSD was enabled without a ZXBUS board");
            control.ConfigurationChanged += delegate
            {
                neoGsSd.SetBoardEnabled(control.IsNeoGsBoardEnabled);
            };
            var slot1 = GetField<CheckBox>(control, "m_slot1Enabled");
            var slot1Device = GetField<ComboBox>(control, "m_slot1Device");
            var slot2 = GetField<CheckBox>(control, "m_slot2Enabled");
            var slot2Device = GetField<ComboBox>(control, "m_slot2Device");

            slot1Device.SelectedIndex = 1;
            slot1.Checked = true;
            Assert(neoGsConnected.Enabled,
                "NeoGS microSD was not enabled with the ZXBUS board");
            slot2.Checked = false;
            control.Apply();
            Assert(bus.FindDevice<AYCHRV>() != null,
                "ZXBUS settings unexpectedly changed the Music device");
            Assert(ula.ZxBusSlot1Enabled, "Slot 1 state was not stored");
            Assert(!ula.ZxBusSlot2Enabled, "Slot 2 state was not stored");
            Assert(
                ula.ZxBusSlot1Device == PentEvoZxBusDevice.NeoGS,
                "NeoGS slot choice was not stored");
            Assert(
                bus.FindDevice<NeoGsDevice>() != null,
                "NeoGS was not added to the bus");

            slot2Device.SelectedIndex = 1;
            slot2.Checked = true;
            Assert(
                !slot1.Checked && slot1Device.SelectedIndex == 0,
                "One NeoGS was allowed in both physical slots");
            Assert(neoGsConnected.Enabled,
                "NeoGS microSD was disabled while Slot 2 was active");
            slot1Device.SelectedIndex = 1;
            slot1.Checked = true;
            Assert(
                !slot2.Checked && slot2Device.SelectedIndex == 0,
                "Moving NeoGS back to slot 1 did not clear slot 2");

            control.Apply();
            Assert(bus.FindDevice<AYCHRV>() != null,
                "ZXBUS settings removed the Music device");

            // NeoGS and MultiSound may occupy either physical connector at
            // the same time. Automatic mode disables only colliding blocks.
            slot2Device.SelectedIndex = 2;
            slot2.Checked = true;
            control.Apply();
            var multiSound = bus.FindDevice<ZxMultiSoundDevice>();
            Assert(bus.FindDevice<NeoGsDevice>() != null && multiSound != null,
                "NeoGS and MultiSound did not coexist in separate slots");
            Assert(bus.FindDevice<AYCHRV>() == null &&
                ula.InternalSound == PentEvoInternalSound.None,
                "Automatic MultiSound YM did not disable internal AY");
            Assert(multiSound.EffectiveYmEnabled &&
                !multiSound.EffectiveGeneralSoundEnabled,
                "Automatic conflict policy did not preserve YM/disable GS");

            // The connectors are physically equivalent: swap the two boards
            // and verify that no hidden "preferred slot" exists in the UI or
            // the stored PentEvo profile.
            slot1Device.SelectedIndex = 2;
            slot1.Checked = true;
            slot2Device.SelectedIndex = 1;
            slot2.Checked = true;
            control.Apply();
            multiSound = bus.FindDevice<ZxMultiSoundDevice>();
            Assert(ula.ZxBusSlot1Enabled && ula.ZxBusSlot2Enabled &&
                ula.ZxBusSlot1Device == PentEvoZxBusDevice.MultiSound &&
                ula.ZxBusSlot2Device == PentEvoZxBusDevice.NeoGS,
                "NeoGS/MultiSound could not be swapped between ZXBUS slots");
            Assert(bus.FindDevice<NeoGsDevice>() != null && multiSound != null &&
                multiSound.EffectiveYmEnabled &&
                !multiSound.EffectiveGeneralSoundEnabled,
                "Swapping ZXBUS slots changed the automatic conflict policy");

            var automaticSwitch = GetField<CheckBox>(control, "m_automatic");
            var ymSwitch = GetField<CheckBox>(control, "m_ym");
            var gsSwitch = GetField<CheckBox>(control, "m_gs");
            automaticSwitch.Checked = false;
            ymSwitch.Checked = false;
            gsSwitch.Checked = true;
            var rejectedBeforeMutation = false;
            try
            {
                control.Apply();
            }
            catch (InvalidOperationException)
            {
                rejectedBeforeMutation = true;
            }
            Assert(rejectedBeforeMutation &&
                multiSound.AutomaticConfiguration &&
                bus.FindDevice<NeoGsDevice>() != null,
                "Manual GS/NeoGS conflict changed the bus before rejection");
            automaticSwitch.Checked = true;
            ymSwitch.Checked = true;

            multiSound.AutomaticConfiguration = false;
            multiSound.YmEnabled = false;
            multiSound.SaaEnabled = true;
            multiSound.GeneralSoundEnabled = false;
            multiSound.SoundDriveEnabled = true;
            multiSound.Volume = 79;
            var boardXml = new XmlDocument();
            var boardNode = boardXml.AppendChild(
                boardXml.CreateElement("Device"));
            multiSound.SaveConfigXml(boardNode);
            var restoredBoard = new ZxMultiSoundDevice();
            restoredBoard.LoadConfigXml(boardNode);
            Assert(!restoredBoard.AutomaticConfiguration &&
                !restoredBoard.YmEnabled && restoredBoard.SaaEnabled &&
                !restoredBoard.GeneralSoundEnabled &&
                restoredBoard.SoundDriveEnabled && restoredBoard.Volume == 79,
                "MultiSound switches/volume did not survive XML round-trip");

            // Restore automatic mode for the rest of the profile checks.
            multiSound.AutomaticConfiguration = true;
            multiSound.YmEnabled = true;
            multiSound.GeneralSoundEnabled = true;
            multiSound.Volume = 100;

            var xml = new XmlDocument();
            var node = xml.AppendChild(xml.CreateElement("Device"));
            ula.SaveConfigXml(node);
            Assert(
                node.Attributes["zxBusSlot1Enabled"].Value == "True",
                "Slot 1 XML mismatch");
            Assert(
                node.Attributes["zxBusSlot1Device"].Value == "MultiSound",
                "Slot 1 device XML mismatch");

            var restored = new UlaPentEvo();
            restored.LoadConfigXml(node);
            Assert(restored.ZxBusSlot1Enabled, "Slot 1 XML did not reload");
            Assert(
                restored.ZxBusSlot1Device == PentEvoZxBusDevice.MultiSound,
                "Slot 1 device XML did not reload");
            Assert(restored.ZxBusSlot2Enabled &&
                restored.ZxBusSlot2Device == PentEvoZxBusDevice.NeoGS,
                "Slot 2 device XML did not reload");

            // ZXM-MoonSound is a third independent board type. It may use
            // either physical connector, coexist with NeoGS/MultiSound, and
            // may not be duplicated because both cards would decode the same
            // fixed #7E/#7F and #C4-#C7 ports.
            slot1Device.SelectedIndex = 3;
            slot1.Checked = true;
            slot2Device.SelectedIndex = 1;
            slot2.Checked = true;
            control.Apply();
            Assert(bus.FindDevice<ZxmMoonSoundDevice>() != null &&
                bus.FindDevice<NeoGsDevice>() != null &&
                bus.FindDevice<ZxMultiSoundDevice>() == null,
                "MoonSound and NeoGS did not coexist in separate slots");
            Assert(ula.ZxBusSlot1Device == PentEvoZxBusDevice.MoonSound &&
                ula.ZxBusSlot2Device == PentEvoZxBusDevice.NeoGS,
                "MoonSound was not stored in Slot 1");

            slot2Device.SelectedIndex = 3;
            slot2.Checked = true;
            Assert(!slot1.Checked && slot1Device.SelectedIndex == 0,
                "Two MoonSound boards were allowed simultaneously");
            slot1Device.SelectedIndex = 1;
            slot1.Checked = true;
            control.Apply();
            Assert(ula.ZxBusSlot1Device == PentEvoZxBusDevice.NeoGS &&
                ula.ZxBusSlot2Device == PentEvoZxBusDevice.MoonSound,
                "MoonSound could not be moved to Slot 2");

            var moonXml = new XmlDocument();
            var moonNode = moonXml.AppendChild(moonXml.CreateElement("Device"));
            ula.SaveConfigXml(moonNode);
            var moonRestored = new UlaPentEvo();
            moonRestored.LoadConfigXml(moonNode);
            Assert(moonRestored.ZxBusSlot1Device ==
                    PentEvoZxBusDevice.NeoGS &&
                moonRestored.ZxBusSlot2Device ==
                    PentEvoZxBusDevice.MoonSound,
                "MoonSound slot selection did not survive XML round-trip");

            var init = typeof(CtlSettingsUla).GetMethod(
                "Init",
                new Type[]
                {
                    typeof(BusManager),
                    typeof(ZXMAK2.Host.Interfaces.IHostService),
                    typeof(UlaPentEvo),
                });
            Assert(init != null, "Standard ULA selector is not bound to PENTEVO");

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

            Console.WriteLine("PASS: standard PENTEVO ULA, separate Music and ZXBUS round-trip");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void VerifyFloppyIndicators()
    {
        var zController = new ZsdPentEvo();
        var sdStatus = (ZXMAK2.Host.Interfaces.ISecureDigitalMediaStatus)
            zController;
        Assert(sdStatus.SecureDigitalIndex == 0,
            "Z-controller toolbar index is not 0");
        Assert(!((IMediaStatusDevice)zController).IsMediaMounted,
            "Empty Z-controller is reported as mounted");

        var controller = new FddController();
        controller.FDD[2].Present = true;
        Assert(!controller.IsDriveMounted(0),
            "Empty FDD A is reported as mounted");
        Assert(controller.IsDriveMounted(2),
            "Mounted FDD C is reported as empty");

        var factory = typeof(MainView).GetMethod(
            "CreateMediaMenuIndicator",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(factory != null, "FDD menu indicator factory is missing");
        using (var mounted = (Bitmap)factory.Invoke(
            null, new object[] { MediaState.Mounted }))
        using (var empty = (Bitmap)factory.Invoke(
            null, new object[] { MediaState.Empty }))
        using (var unavailable = (Bitmap)factory.Invoke(
            null, new object[] { MediaState.Unavailable }))
        {
            var green = mounted.GetPixel(4, 4);
            var red = empty.GetPixel(4, 4);
            var gray = unavailable.GetPixel(4, 4);
            Assert(green.G > green.R,
                "Mounted FDD indicator is not green");
            Assert(red.R > red.G,
                "Empty FDD indicator is not red");
            Assert(Math.Abs(gray.R - gray.G) < 4 &&
                Math.Abs(gray.G - gray.B) < 4,
                "Unavailable device indicator is not gray");
        }
    }

    private static void VerifyMachineSettingsNavigation()
    {
        var bus = new BusManager();
        bus.Init(null, true);
        bus.Disconnect();
        bus.Clear();
        var ula = new UlaPentEvo();
        ula.ZxBusSlot1Enabled = true;
        ula.ZxBusSlot1Device = PentEvoZxBusDevice.NeoGS;
        bus.Add(ula);
        bus.Add(new AYCHRV());
        bus.Add(new IdePentEvo());
        bus.Add(new ZsdPentEvo());
        bus.Add(new NeoGsDevice());

        using (var form = new FormMachineSettings())
        using (var machine = new ProbeMachine(bus))
        {
            form.Init(null, machine);
            var navigation = GetField<ListView>(form, "lstNavigation");
            var zController = -1;
            var neoGs = -1;
            var zxBus = -1;
            var music = -1;
            var ide = -1;
            for (var i = 0; i < navigation.Items.Count; i++)
            {
                if (navigation.Items[i].SubItems.Count < 2)
                    continue;
                var name = navigation.Items[i].SubItems[1].Text;
                if (name == "SD Z-controller")
                    zController = i;
                else if (name == "SD NeoGS")
                    neoGs = i;
                else if (name == "ZXBUS PentEvo")
                    zxBus = i;
                else if (name == "Internal PentEvo")
                    music = i;
                else if (name == "IDE PentEvo")
                    ide = i;
            }
            Assert(zController >= 0,
                "SD Z-controller navigation item is missing");
            Assert(neoGs == zController + 1,
                "SD NeoGS is not placed after SD Z-controller");
            Assert(navigation.Items[neoGs].ForeColor != SystemColors.GrayText,
                "SD NeoGS is disabled with an active ZXBUS board");
            Assert(zxBus >= 0, "Separate ZXBUS navigation item is missing");
            Assert(music >= 0,
                "PentEvo internal Music selector is missing");
            Assert(navigation.Items[music].Tag is CtlSettingsGenericSound,
                "PentEvo Music does not use the AY/TSFM selector");

            form.SelectDevice("IDE PentEvo");
            Assert(ide >= 0 && navigation.Items[ide].Selected,
                "CD-ROM settings did not select IDE PentEvo");
        }
    }

    private static void VerifyPentEvoMusicSelector()
    {
        var bus = new BusManager();
        bus.Init(null, true);
        bus.Disconnect();
        bus.Clear();
        var ula = new UlaPentEvo();
        var ay = new AYCHRV();
        ay.BusOrder = 25;
        ay.Volume = 67;
        bus.Add(ula);
        bus.Add(ay);
        var musicBusOrder = ay.BusOrder;

        using (var control = new CtlSettingsGenericSound())
        {
            control.InitPentEvo(bus, null, ay);
            var selector = GetField<ComboBox>(
                control,
                "cbxPentEvoDevice");
            var volume = GetField<TrackBar>(control, "trkVolume");
            Assert(selector.Items.Count == 3,
                "PentEvo None/AY/TSFM selector was not initialized");
            Assert(selector.SelectedIndex == 1,
                "PentEvo AY was not selected initially");
            selector.SelectedIndex = 2;
            volume.Value = 67;
            control.Apply();
            var tsfm = bus.FindDevice<TurboSoundFmPro>();
            Assert(tsfm != null && bus.FindDevice<AYCHRV>() == null,
                "PentEvo Music did not replace AY with TSFM");
            Assert(tsfm.Volume == 67 && tsfm.BusOrder == musicBusOrder,
                "AY to TSFM did not preserve volume/order");
            Assert(
                ula.InternalSound == PentEvoInternalSound.TurboSoundFmPro,
                "PentEvo internal sound was not synchronized to TSFM");

            selector.SelectedIndex = 1;
            control.Apply();
            var restoredAy = bus.FindDevice<AYCHRV>();
            Assert(restoredAy != null &&
                bus.FindDevice<TurboSoundFmPro>() == null,
                "PentEvo Music did not restore AY from TSFM");
            Assert(restoredAy.Volume == 67 &&
                restoredAy.BusOrder == musicBusOrder,
                "TSFM to AY did not preserve volume/order");

            selector.SelectedIndex = 0;
            control.Apply();
            Assert(bus.FindDevice<AYCHRV>() == null &&
                bus.FindDevice<TurboSoundFmPro>() == null &&
                ula.InternalSound == PentEvoInternalSound.None,
                "PentEvo Music: None did not remove the internal device");
        }
    }

    private static void VerifySecureDigitalMenu()
    {
        using (var view = new MainView(null))
        {
            var tapeButton = GetField<ToolStripSplitButton>(view, "_tapeButton");
            var opticalButton = GetField<ToolStripSplitButton>(view, "_opticalButton");
            var opticalSettings = GetField<ToolStripMenuItem>(
                view,
                "_opticalSettingsMenuItem");
            var toolbar = GetField<ToolStrip>(view, "tbrStrip");
            Assert(tapeButton.Width == 74 && opticalButton.Width == 74,
                "Tape/CD buttons do not reserve room for the drop-down arrow");
            Assert(tapeButton.Image != null && tapeButton.Image.Size == new Size(52, 36),
                "Tape artwork does not fit the common toolbar canvas");
            Assert(opticalButton.Image != null && opticalButton.Image.Size == new Size(52, 36),
                "CD artwork does not fit the common toolbar canvas");
            Assert(toolbar.Items.IndexOf(opticalButton) == toolbar.Items.Count - 1,
                "CD button is not the rightmost toolbar item");
            Assert((string)opticalSettings.Tag == "IDE PentEvo",
                "CD-ROM Settings does not target IDE PentEvo");
            Assert(tapeButton.DropDownItems.Count == 11,
                "Tape transport menu is incomplete");

            view.Add(new ProbeMediaCommand(
                MediaCommandAction.Load, 0, "load-z"));
            view.Add(new ProbeMediaCommand(
                MediaCommandAction.Eject, 0, "eject-z"));
            view.Add(new ProbeMediaCommand(
                MediaCommandAction.Load, 1, "load-neogs"));
            view.Add(new ProbeMediaCommand(
                MediaCommandAction.Eject, 1, "eject-neogs"));

            var items = GetField<ToolStripMenuItem[]>(
                view, "_ejectSdMenuItems");
            var states = GetField<MediaState?[]>(view, "_sdMountedStates");
            Assert(items[0] != null && items[0].Text == "Eject Z-controller",
                "Z-controller Eject menu item is missing or too long");
            Assert(items[1] != null && items[1].Text == "Eject NeoGS",
                "NeoGS Eject menu item is missing");
            Assert(items[0].Image != null && items[1].Image != null,
                "SD menu indicators are missing");
            var unavailable = ((Bitmap)items[0].Image).GetPixel(4, 4);
            Assert(Math.Abs(unavailable.R - unavailable.G) < 4,
                "Unresolved Z-controller indicator is not gray");

            var update = typeof(MainView).GetMethod(
                "UpdateMediaMenuIndicators",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert(update != null, "Shared media indicator updater is missing");
            update.Invoke(null, new object[]
            {
                items,
                states,
                new Func<int, MediaState>(index => index == 1
                    ? MediaState.Mounted
                    : MediaState.Empty),
            });
            var neoGsGreen = ((Bitmap)items[1].Image).GetPixel(4, 4);
            Assert(neoGsGreen.G > neoGsGreen.R,
                "Mounted NeoGS indicator is not green");
            var zControllerRed = ((Bitmap)items[0].Image).GetPixel(4, 4);
            Assert(zControllerRed.R > zControllerRed.G,
                "Available empty Z-controller indicator is not red");
        }
    }

    private sealed class ProbeMachine : IVirtualMachine
    {
        private readonly IBus m_bus;

        public ProbeMachine(IBus bus)
        {
            m_bus = bus;
        }

        public event EventHandler FrameSizeChanged { add { } remove { } }
        public bool IsRunning { get { return false; } }
        public IBus Bus { get { return m_bus; } }
        public Size FrameSize { get { return new Size(320, 240); } }
        public void DoRun() { }
        public void DoStop() { }
        public void DoReset() { }
        public void DoPowerCycle() { }
        public void DoNmi() { }
        public void SaveConfig() { }
        public void Dispose() { }
    }

    private sealed class ProbeMediaCommand : CommandDelegate, IMediaCommand
    {
        public ProbeMediaCommand(
            MediaCommandAction action,
            int driveIndex,
            string text)
            : base(arg => { }, arg => true, text)
        {
            MediaAction = action;
            DriveIndex = driveIndex;
        }

        public MediaCommandKind MediaKind
        {
            get { return MediaCommandKind.SecureDigital; }
        }

        public MediaCommandAction MediaAction { get; private set; }
        public int DriveIndex { get; private set; }
        public event EventHandler ExecutedSuccessfully
        {
            add { }
            remove { }
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
