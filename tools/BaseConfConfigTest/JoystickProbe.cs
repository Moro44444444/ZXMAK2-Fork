using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.General;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Mdx;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;
using ZXMAK2.DirectX.DirectInput;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Dependency;

internal static class JoystickProbe
{
    private static int checks;
    private static void Check(bool value,string text) { checks++; if(!value) throw new Exception(text); }
    private static JoystickInput Sample(int x,int y,uint hat,params int[] pressed)
    { var b=new bool[32]; foreach(int i in pressed) b[i]=true; return new JoystickInput(true,new[] {x,y,0,0,0,0,0,0},b,new[] {hat,uint.MaxValue,uint.MaxValue,uint.MaxValue}); }
    [STAThread] private static int Main(string[] args)
    {
        try
        {
            TestBootstrap();
            var m=new JoystickMapping(); var neutral=Sample(0,0,uint.MaxValue);
            Check(m.Map(neutral)==0,"Neutral");
            byte[] hatValues={8,9,1,5,4,6,2,10};
            for(int i=0;i<8;i++) Check(m.Map(Sample(0,0,(uint)(i*4500)))==hatValues[i],"POV sector "+i);
            Check(m.Map(Sample(0,0,0xFFFF))==0,"POV DWORD low-word centered");
            Check(m.Map(Sample(6553,-6553,uint.MaxValue))==0,"Dead-zone edge");
            Check(m.Map(Sample(6554,-6554,uint.MaxValue))==9,"Dead-zone outside");
            Check(m.Map(Sample(-32768,0,9000))==0,"Opposite directions cancel");
            m.Directions=JoystickDirections.DPad;
            Check(m.Map(Sample(32767,0,uint.MaxValue))==0,"D-pad mode ignores analog");
            m.Directions=JoystickDirections.Stick;
            Check(m.Map(Sample(0,0,9000))==0,"Stick mode ignores D-pad");
            m.Directions=JoystickDirections.Custom; m.Bindings[0]="b:9"; m.Bindings[1]="a:2:+";
            m.Bindings[2]="p:0:3"; m.Bindings[3]="b:8"; m.Bindings[4]="b:7";
            Check(m.Map(Sample(0,0,27000,9,7))==26,"Custom buttons + POV + Fire");
            m.Bindings[4]="invalid"; Check((m.Map(Sample(0,0,uint.MaxValue,7))&16)==0,"Malformed binding safe");
            m=new JoystickMapping(); m.Bindings[5]="b:1";
            Check(m.Map(Sample(0,0,uint.MaxValue,1))==0,"Dedicated toggle excluded from Fire and extended bits");
            Check((m.Map(Sample(0,0,uint.MaxValue,1,4))&16)!=0,"Other Fire buttons still work");
            m.Bindings[5]="none";
            Check((m.Map(Sample(0,0,uint.MaxValue,0,1,2,3))&0xF0)==0xF0,"8-bit extension preserved");
            var xi=DirectJoystick.FromXInput(new DirectJoystick.XGamepad {Buttons=9,LeftY=short.MinValue});
            Check(xi.Hats[0]==4500 && xi.Axes[1]==32767,"XInput diagonal and signed Y clamp");
            xi=DirectJoystick.FromXInput(new DirectJoystick.XGamepad {Buttons=0xFFFF,LeftTrigger=31,RightTrigger=30});
            Check(xi.Buttons[0] && xi.Buttons[9] && xi.Buttons[10] && !xi.Buttons[11],"XInput buttons / triggers");
            Check(xi.Hats[0]==uint.MaxValue,"XInput opposite D-pad directions cancel");
            var di=DirectJoystick.FromDInput(new DIJOYSTATE {lX=65535,lY=32768,lZ=32768,lRx=32768,lRy=32768,lRz=32768,
                rglSlider0=32768,rglSlider1=32768,rgdwPOV0=13500,rgdwPOV1=uint.MaxValue,rgdwPOV2=uint.MaxValue,rgdwPOV3=uint.MaxValue,rgbButtons31=128});
            Check(di.Axes[0]==32767 && di.Axes[1]==0 && di.Buttons[31],"DInput axes and button32");
            Check(di.Hat(0,1) && di.Hat(0,2),"DInput diagonal");
            TestLearn(); TestAutoFire(); TestPersistence(); TestHardwarePort(); TestMachineClock(); TestWindow(args);
            using(var form=new Form()) using(var native=new DirectJoystick(form))
            {
                foreach(var d in native.GetAvailableJoysticks()) Console.WriteLine("Detected: "+d.Name+" / "+d.HostId);
                native.RefreshControllers(); native.CaptureHostDevice("missing-controller"); native.Scan();
                Check(!((JoystickInput)native.GetState("missing-controller")).Connected,"Missing native controller safe");
                native.ReleaseHostDevice("missing-controller");
                native.KeyboardState=new HeldKeyboard(); native.CaptureHostDevice("keyboard");
                typeof(DirectJoystick).GetMethod("Activated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(native,new object[] {null,EventArgs.Empty});
                native.Scan(); Check(native.GetState("keyboard").IsFire,"Numpad still supported");
                typeof(DirectJoystick).GetMethod("Deactivated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(native,new object[] {null,EventArgs.Empty});
                Check(!((JoystickInput)native.GetState("keyboard")).Connected,"Focus loss clears published input immediately");
                typeof(DirectJoystick).GetMethod("Activated",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(native,new object[] {null,EventArgs.Empty});
                native.Scan(); Check(!((JoystickInput)native.GetState("keyboard")).Connected,"Returning focus publishes neutral before held input");
                native.Scan(); Check(native.GetState("keyboard").IsFire,"Input resumes after neutral sample");
            }
            Console.WriteLine("JoystickProbe PASS: "+checks+" checks"); return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
    private static void TestBootstrap()
    {
        // Do not construct the backend directly: reproduce the real launcher's
        // XML/name-based constructor binding, including all View registrations.
        using(var root=new ResolverUnity())
        {
            var view=root.Resolve<IResolver>("View");
            Check(view!=null,"Launcher's View container loads from unity.config");
            using(var form=new Form())
            using(var controller=view.Resolve<IHostJoystick>(new Argument("form",form)))
                Check(controller is DirectJoystick,"Configured joystick constructor resolves through Unity");
        }
    }
    private sealed class HeldKeyboard : IKeyboardState
    { public bool this[ZXMAK2.Host.Entities.Key key] { get { return key==ZXMAK2.Host.Entities.Key.NumPad0; } } }
    private static void TestMachineClock()
    {
        var spec=new Spectrum(); var bus=spec.BusManager;
        bus.Init(spec,true); bus.Disconnect(); bus.Clear();
        var memory=new MemoryPentEvo(); var ula=new UlaPentEvo();
        var j=new KempstonJoystick {NoDos=false,HostId="clock-test"};
        var map=new JoystickMapping {AutoFire=JoystickAutoFire.Hold,FireRate=10};
        j.Profiles=new Dictionary<string,JoystickMapping> {{j.HostId,map}};
        bus.Add(memory); bus.Add(ula); bus.Add(j); Check(bus.Connect(),"Clock-test bus connects");
        var events=Field<EventManager>(bus,"m_eventManager"); var cpu=bus.Cpu;
        cpu.Tact=0; events.BeginFrame(); j.JoystickState=Sample(0,0,uint.MaxValue,0);
        Check((cpu.RDPORT(31)&16)!=0,"Machine port autofire starts ON");
        for(int i=0;i<3;i++) { cpu.Tact+=ula.FrameTactCount; events.EndFrame(); ula.BeginFrameTiming(cpu.Tact); events.BeginFrame(); }
        Check((cpu.RDPORT(31)&16)==0,"Machine autofire OFF after 60ms");
        long origin=cpu.Tact;
        cpu.Tact=origin+ula.FrameTactCount/2;
        Check((cpu.RDPORT(31)&16)==0,"Sub-frame OFF phase");
        for(int i=0;i<3;i++) { cpu.Tact=origin+ula.FrameTactCount; events.EndFrame(); ula.BeginFrameTiming(cpu.Tact); events.BeginFrame(); origin=cpu.Tact; }
        Check((cpu.RDPORT(31)&16)!=0,"Machine autofire next ON cycle");
        long paused=cpu.Tact;
        for(int i=0;i<10;i++) Check((cpu.RDPORT(31)&16)!=0 && cpu.Tact==paused,"Paused CPU does not advance autofire");
        cpu.RESET(); Check(cpu.RDPORT(31)==0,"Machine reset clears input and autofire");
        bus.Disconnect();
    }
    private static void TestLearn()
    {
        var neutral=Sample(0,0,uint.MaxValue); var held=Sample(0,0,uint.MaxValue,1);
        Check(CtlSettingsJoystick.LearnBinding(held,held,held)==null,"Learn ignores already held button");
        Check(CtlSettingsJoystick.LearnBinding(Sample(0,0,uint.MaxValue,1,31),held,held)=="b:31","Learn captures new button32");
        Check(CtlSettingsJoystick.LearnBinding(Sample(0,0,9000),neutral,neutral)=="p:0:1","Learn D-pad");
        Check(CtlSettingsJoystick.LearnBinding(Sample(-20000,0,uint.MaxValue),neutral,neutral)=="a:0:-","Learn analog axis");
        Check(CtlSettingsJoystick.LearnBinding(Sample(500,0,uint.MaxValue),neutral,neutral)==null,"Learn ignores jitter");
        var endpoint=Sample(-32768,0,uint.MaxValue);
        Check(CtlSettingsJoystick.LearnBinding(Sample(-13000,0,uint.MaxValue),endpoint,endpoint)==null,"Learn avoids inverted endpoint rest binding");
        Check(CtlSettingsJoystick.LearnBinding(Sample(20000,0,uint.MaxValue),endpoint,endpoint)=="a:0:+","Learn endpoint axis after crossing neutral");
        Check(CtlSettingsJoystick.LearnBinding(JoystickInput.Empty,neutral,neutral)==null,"Learn disconnect safe");
    }
    private static void TestAutoFire()
    {
        var m=new JoystickMapping {AutoFire=JoystickAutoFire.Hold,FireRate=10};
        var c=new JoystickFireController(); var fire=Sample(0,0,uint.MaxValue,0);
        c.Sample(fire,m); Check((c.Output(fire,m,0)&16)!=0,"Hold starts ON");
        Check((c.Output(fire,m,.06)&16)==0,"Hold release phase");
        Check((c.Output(fire,m,.11)&16)!=0,"Hold next pulse");
        Check(c.Output(Sample(0,0,uint.MaxValue),m,.12)==0,"Hold releases immediately");
        Check((c.Output(fire,m,.13)&16)!=0,"Hold new press starts ON");
        m.AutoFire=JoystickAutoFire.Toggle; m.Bindings[5]="b:6"; c.Reset();
        var toggle=Sample(0,0,uint.MaxValue,6); var neutral=Sample(0,0,uint.MaxValue);
        c.Sample(neutral,m);
        c.Sample(toggle,m); Check((c.Output(toggle,m,1)&16)!=0,"Toggle starts firing");
        c.Sample(toggle,m); Check((c.Output(toggle,m,1.06)&16)==0,"Holding toggle does not retoggle");
        c.Sample(neutral,m); Check((c.Output(neutral,m,1.11)&16)!=0,"Latch remains after releasing toggle");
        c.Sample(toggle,m); Check(c.Output(neutral,m,1.12)==0,"Second edge stops latch");
        c.Sample(neutral,m); c.Sample(toggle,m); c.Sample(JoystickInput.Empty,m);
        Check(c.Output(neutral,m,2)==0,"Disconnect clears latch");
        c.Sample(toggle,m); Check(c.Output(neutral,m,2.1)==0,"Held toggle cannot restart after disconnect");
        c.Sample(neutral,m); c.Sample(toggle,m); c.Reset(); Check(c.Output(neutral,m,3)==0,"Reset clears latch");
        for(int rate=1;rate<=25;rate++)
        {
            m.AutoFire=JoystickAutoFire.Hold; m.FireRate=rate; c.Reset();
            for(int i=0;i<100;i++)
            { double t=i*.001; byte a=c.Output(fire,m,t); Check(a==c.Output(fire,m,t),"Paused emulated time invariant"); }
        }
    }
    private static XmlNode Node() { var d=new XmlDocument(); d.LoadXml("<Device />"); return d.DocumentElement; }
    private static void TestPersistence()
    {
        var j=new KempstonJoystick {HostId="offline-device",BitWidth=8};
        var p=new Dictionary<string,JoystickMapping>();
        p["offline-device"]=new JoystickMapping {DeviceName="Windows HOTAS (DInput)",Directions=JoystickDirections.Custom,AutoFire=JoystickAutoFire.Toggle,DeadZone=37,FireRate=15};
        p["offline-device"].Bindings[0]="p:2:0"; p["offline-device"].Bindings[4]="b:31";
        p["second-device"]=new JoystickMapping {Directions=JoystickDirections.DPad}; j.Profiles=p;
        p["offline-device"].DeadZone=12; Check(j.Mapping.DeadZone==37,"Profiles setter copies");
        var node=Node(); j.SaveConfigXml(node); j.SaveConfigXml(node);
        Check(node.SelectNodes("JoystickProfile").Count==2,"Save does not duplicate profiles");
        var restored=new KempstonJoystick(); restored.LoadConfigXml(node);
        Check(restored.HostId==j.HostId && restored.BitWidth==8,"Host and hardware saved");
        var m=restored.Mapping;
        Check(m.DeviceName=="Windows HOTAS (DInput)","Windows display name retained offline");
        Check(m.DeadZone==37 && m.FireRate==15 && m.AutoFire==JoystickAutoFire.Toggle && m.Bindings[4]=="b:31" && m.Bindings[0]=="p:2:0","Mapping XML round-trip");
        restored.HostId="second-device"; Check(restored.Mapping.Directions==JoystickDirections.DPad,"Separate controller profile");
        restored.LoadConfigXml(Node()); Check(restored.Profiles.Count==0 && restored.Mapping.AutoFire==JoystickAutoFire.Off,"Legacy config safe defaults");
    }
    private static byte Read(KempstonJoystick j)
    {
        var method=typeof(KempstonJoystick).GetMethod("ReadPort1F",BindingFlags.Instance|BindingFlags.NonPublic);
        object[] args={(ushort)31,(byte)255,false}; method.Invoke(j,args); Check((bool)args[2],"Kempston read handled"); return (byte)args[1];
    }
    private static void TestHardwarePort()
    {
        var j=new KempstonJoystick {NoDos=false}; j.JoystickState=Sample(0,0,4500,1);
        Check(Read(j)==25,"5-bit mapped port"); j.BitWidth=8; Check(Read(j)==57,"8-bit mapped port + additional button");
        j.JoystickState=JoystickInput.Empty; Check(Read(j)==0,"Disconnected port neutral");
    }
    private static T Field<T>(object o,string name) { return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o); }
    private static void TestWindow(string[] args)
    {
        using(var f=new Form {ClientSize=new Size(440,580),StartPosition=FormStartPosition.Manual,Location=new Point(-30000,-30000)})
        using(var ui=new CtlSettingsJoystick {Dock=DockStyle.Fill})
        {
            var host=new FakeHost(); var j=new KempstonJoystick {HostId="saved-offline"};
            f.Controls.Add(ui); ui.Init(null,host,j); f.Show(); Application.DoEvents();
            var combo=Field<ComboBox>(ui,"cbxType"); Check(((IHostDeviceInfo)combo.SelectedItem).HostId=="saved-offline","UI preserves missing device");
            Field<Button>(ui,"refresh").PerformClick(); Check(host.Controller.Refreshes==1,"Refresh is explicit");
            ui.Apply(); Check(j.HostId=="saved-offline","Apply does not replace missing device");
            combo.SelectedIndex=2;
            Field<ComboBox>(ui,"directions").SelectedIndex=1; Field<ComboBox>(ui,"autoFire").SelectedIndex=1;
            Field<NumericUpDown>(ui,"rate").Value=15;
            combo.SelectedIndex=3; Field<ComboBox>(ui,"directions").SelectedIndex=2;
            combo.SelectedIndex=2; Check(Field<ComboBox>(ui,"directions").SelectedIndex==1,"UI restores per-device edits");
            ui.Apply(); Check(j.Mapping.AutoFire==JoystickAutoFire.Hold && j.Mapping.FireRate==15,"UI applies autofire");
            Check(j.Profiles["test-xinput"].Directions==JoystickDirections.Stick,"Other device edits retained");
            Field<Button[]>(ui,"bindings")[4].PerformClick();
            Check(Field<int>(ui,"learning")==4,"UI enters learn mode");
            object[] escapeArgs={new Message(),Keys.Escape};
            bool escaped=(bool)typeof(CtlSettingsJoystick).GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,escapeArgs);
            Check(escaped && Field<int>(ui,"learning")==-1 && f.Visible,"Esc cancels assignment without closing settings");
            if(args.Length>0) { f.ClientSize=new Size(300,430); Application.DoEvents(); using(var bmp=new Bitmap(ui.Width,ui.Height)) { ui.DrawToBitmap(bmp,new Rectangle(Point.Empty,ui.Size)); bmp.Save(args[0]); } }
            f.Close(); Check(host.Controller.Releases>0,"Preview released on close");
        }
    }
    private sealed class FakeController : IHostJoystick,IHostJoystickPreview
    {
        public int Refreshes,Releases;
        public void CaptureHostDevice(string id) { } public void ReleaseHostDevice(string id) { } public void Scan() { }
        public IJoystickState GetState(string id) { return JoystickInput.Empty; }
        public IKeyboardState KeyboardState { set { } } public bool IsKeyboardStateRequired { get { return false; } }
        public IEnumerable<IHostDeviceInfo> GetAvailableJoysticks() { return new IHostDeviceInfo[] {new HostDeviceInfo("None",""),new HostDeviceInfo("Keyboard Numpad","keyboard"),new HostDeviceInfo("Windows HOTAS (DInput)","test-dinput"),new HostDeviceInfo("Windows Gamepad (XInput)","test-xinput")}; }
        public JoystickInput Preview(string id) { return Sample(0,0,4500,0); }
        public void ReleasePreview() { Releases++; } public void RefreshControllers() { Refreshes++; } public void Dispose() { }
    }
    private sealed class FakeHost : IHostService
    {
        public readonly FakeController Controller=new FakeController();
        public IHostJoystick Joystick { get { return Controller; } } public IHostKeyboard Keyboard { get { return null; } }
        public IHostMouse Mouse { get { return null; } } public SyncSource SyncSource { get; set; }
        public int SampleRate { get { return 44100; } } public bool IsCaptured { get { return false; } }
        public IMediaRecorder MediaRecorder { get; set; } public bool CheckSyncSourceSupported(SyncSource s) { return false; }
        public void PushFrame(IFrameInfo a,IFrameVideo b,IFrameSound c) { } public void CancelPush() { }
        public void Capture() { } public void Uncapture() { } public void Dispose() { }
    }
}
