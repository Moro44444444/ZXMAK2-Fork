// DirectInput support originally (c) 2013 Eltaron.
using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Entities;
using ZXMAK2.DirectX;
using ZXMAK2.DirectX.DirectInput;
using ZxKey = ZXMAK2.Host.Entities.Key;

namespace ZXMAK2.Host.WinForms.Mdx
{
    public sealed class DirectJoystick : IHostJoystick, IHostJoystickPreview, IDisposable
    {
        private readonly object sync = new object();
        private readonly Dictionary<string,DirectInputDevice8W> devices = new Dictionary<string,DirectInputDevice8W>();
        private readonly Dictionary<string,IJoystickState> states = new Dictionary<string,IJoystickState>();
        private readonly HashSet<string> requested = new HashSet<string>();
        private readonly Form form;
        private readonly IntPtr hwnd;
        private bool focused, releasePending, disposed;
        private string previewId = "";
        private int lastRetry;
        // unity.config binds this parameter by NAME, not just by its type.
        public DirectJoystick(Form form)
        {
            this.form=form; hwnd=form.Handle; focused=form.ContainsFocus;
            form.Activated+=Activated; form.Deactivate+=Deactivated;
        }
        public IKeyboardState KeyboardState { get; set; }
        public bool IsKeyboardStateRequired { get { lock(sync) return requested.Contains("keyboard"); } }
        public void CaptureHostDevice(string id)
        { lock(sync) { if(!string.IsNullOrEmpty(id)) { requested.Add(id); EnsureDevice(id); } } }
        public void ReleaseHostDevice(string id)
        { lock(sync) { requested.Remove(id); states.Remove(id); if(previewId!=id) CloseDevice(id); } }
        public IJoystickState GetState(string id)
        { lock(sync) { IJoystickState state; return id!=null && states.TryGetValue(id,out state) ? state : JoystickInput.Empty; } }
        public void Scan()
        {
            lock(sync)
            {
                if(disposed) return;
                bool retry=unchecked(Environment.TickCount-lastRetry)>1000;
                if(retry) lastRetry=Environment.TickCount;
                foreach(var id in requested)
                {
                    if(!focused || releasePending) { states[id]=JoystickInput.Empty; continue; }
                    if(retry) EnsureDevice(id);
                    states[id]=Poll(id);
                }
                releasePending=false;
            }
        }
        public JoystickInput Preview(string id)
        {
            lock(sync)
            {
                if(disposed) return JoystickInput.Empty;
                id=id??"";
                if(previewId!=id) { ReleasePreview(); previewId=id; EnsureDevice(id); }
                return Poll(id);
            }
        }
        public void ReleasePreview()
        { lock(sync) { var old=previewId; previewId=""; if(!requested.Contains(old)) CloseDevice(old); } }
        public void RefreshControllers()
        {
            lock(sync)
            {
                foreach(var id in new List<string>(devices.Keys)) CloseDevice(id);
                states.Clear(); releasePending=true;
                foreach(var id in requested) EnsureDevice(id);
                EnsureDevice(previewId);
            }
        }
        private void EnsureDevice(string id)
        {
            Guid guid;
            if(disposed || string.IsNullOrEmpty(id) || devices.ContainsKey(id) || !Guid.TryParse(id,out guid)) return;
            DirectInputDevice8W device=null;
            try
            {
                using(var di=new DirectInput8W()) device=di.CreateDevice(guid,null);
                device.SetDataFormat(DIDATAFORMAT.c_dfDIJoystick).CheckError();
                device.SetCooperativeLevel(hwnd,DISCL.BACKGROUND|DISCL.NONEXCLUSIVE).CheckError();
                device.Acquire(); devices.Add(id,device);
            }
            catch { if(device!=null) device.Dispose(); }
        }
        private void CloseDevice(string id)
        {
            DirectInputDevice8W device;
            if(id==null || !devices.TryGetValue(id,out device)) return;
            try { device.Unacquire(); } finally { device.Dispose(); devices.Remove(id); }
        }
        private JoystickInput Poll(string id)
        {
            if(id=="keyboard")
            {
                var k=KeyboardState;
                if(k==null) return JoystickInput.Empty;
                return new JoystickInput(true,new[] {
                    (k[ZxKey.NumPad6]?32767:0)-(k[ZxKey.NumPad4]?32767:0),
                    (k[ZxKey.NumPad2]?32767:0)-(k[ZxKey.NumPad8]?32767:0),0,0,0,0,0,0 },
                    new[] { k[ZxKey.NumPad0]||k[ZxKey.NumPad5],false,false,false },new[] { uint.MaxValue });
            }
            int slot;
            if(id!=null && id.StartsWith("xinput:") && int.TryParse(id.Substring(7),out slot))
            { XState state; return ReadXInput(slot,out state) ? FromXInput(state.Gamepad) : JoystickInput.Empty; }
            DirectInputDevice8W device;
            if(id==null || !devices.TryGetValue(id,out device)) return JoystickInput.Empty;
            try
            {
                device.Acquire(); var hr=device.Poll(); DIJOYSTATE state;
                if(hr.IsSuccess && device.GetDeviceState(out state).IsSuccess) return FromDInput(state);
            }
            catch { }
            return JoystickInput.Empty;
        }
        public static JoystickInput FromDInput(DIJOYSTATE s)
        {
            var bytes=s.GetButtons(); var buttons=new bool[bytes.Length];
            for(int i=0;i<bytes.Length;i++) buttons[i]=(bytes[i]&128)!=0;
            var axes=new[] {s.lX,s.lY,s.lZ,s.lRx,s.lRy,s.lRz,s.rglSlider0,s.rglSlider1};
            for(int i=0;i<axes.Length;i++) axes[i]=Math.Max(-32768,Math.Min(32767,axes[i]-32768));
            return new JoystickInput(true,axes,buttons,new[] {s.rgdwPOV0,s.rgdwPOV1,s.rgdwPOV2,s.rgdwPOV3});
        }
        [StructLayout(LayoutKind.Sequential)] public struct XGamepad
        { public ushort Buttons; public byte LeftTrigger,RightTrigger; public short LeftX,LeftY,RightX,RightY; }
        [StructLayout(LayoutKind.Sequential)] private struct XState
        { public uint Packet; public XGamepad Gamepad; }
        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] private static extern uint X14(uint index,out XState state);
        [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")] private static extern uint X13(uint index,out XState state);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")] private static extern uint X91(uint index,out XState state);
        private static int xVersion;
        private static bool ReadXInput(int slot,out XState state)
        {
            state=new XState(); if(slot<0 || slot>3) return false;
            try { if(xVersion==0 || xVersion==14) { xVersion=14; return X14((uint)slot,out state)==0; } }
            catch(DllNotFoundException) { xVersion=13; } catch(EntryPointNotFoundException) { xVersion=13; }
            try { if(xVersion==13) return X13((uint)slot,out state)==0; }
            catch(DllNotFoundException) { xVersion=91; } catch(EntryPointNotFoundException) { xVersion=91; }
            try { if(xVersion==91) return X91((uint)slot,out state)==0; }
            catch(DllNotFoundException) { xVersion=-1; } catch(EntryPointNotFoundException) { xVersion=-1; }
            return false;
        }
        public static JoystickInput FromXInput(XGamepad s)
        {
            int[] masks={0x1000,0x2000,0x4000,0x8000,0x100,0x200,0x10,0x20,0x40,0x80};
            var buttons=new bool[12]; for(int i=0;i<10;i++) buttons[i]=(s.Buttons&masks[i])!=0;
            buttons[10]=s.LeftTrigger>30; buttons[11]=s.RightTrigger>30;
            int x=((s.Buttons&8)!=0?1:0)-((s.Buttons&4)!=0?1:0);
            int y=((s.Buttons&2)!=0?1:0)-((s.Buttons&1)!=0?1:0);
            uint hat=uint.MaxValue;
            if(x!=0 || y!=0) { double angle=Math.Atan2(x,-y)*180/Math.PI; if(angle<0) angle+=360; hat=(uint)Math.Round(angle*100); }
            return new JoystickInput(true,new[] {(int)s.LeftX,Math.Min(32767,-(int)s.LeftY),(int)s.RightX,
                Math.Min(32767,-(int)s.RightY),s.LeftTrigger*257-32768,s.RightTrigger*257-32768,0,0},buttons,new[] {hat});
        }
        public IEnumerable<IHostDeviceInfo> GetAvailableJoysticks()
        {
            var result=new List<IHostDeviceInfo>();
            var productNames=new Dictionary<uint,HashSet<string>>();
            var seen=new HashSet<string>(); var counts=new Dictionary<string,int>();
            try
            {
                using(var di=new DirectInput8W())
                    foreach(var d in di.EnumDevices(DI8DEVCLASS.GAMECTRL,DIEDFL.ATTACHEDONLY))
                    {
                        string id=d.guidInstance.ToString(); if(!seen.Add(id)) continue;
                        int n; counts.TryGetValue(d.tszInstanceName,out n); counts[d.tszInstanceName]=++n;
                        uint product=BitConverter.ToUInt32(d.guidProduct.ToByteArray(),0);
                        HashSet<string> names;
                        if(!productNames.TryGetValue(product,out names)) productNames[product]=names=new HashSet<string>();
                        names.Add(d.tszInstanceName);
                        result.Add(new HostDeviceInfo(d.tszInstanceName+(n>1?" ["+n+"]":"")+" (DInput)",id));
                    }
            }
            catch(Exception ex) { Logger.Error(ex); }
            var slots=new List<int>(); XState state;
            for(int i=0;i<4;i++) if(ReadXInput(i,out state)) slots.Add(i);
            string xName=null;
            if(slots.Count==1)
            {
                // XInput exposes a slot, not a product name. Use Windows PnP
                // only when identification is unambiguous; never guess by order.
                try
                {
                    var names=new HashSet<string>();
                    using(var search=new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE PNPDeviceID LIKE '%IG_%'"))
                    using(var items=search.Get())
                        foreach(ManagementObject item in items)
                        {
                            using(item)
                            {
                                var pnp=(item["PNPDeviceID"] as string??"").ToUpperInvariant();
                                if(!pnp.Contains("IG_")) continue;
                                var name=item["Name"] as string;
                                int vi=pnp.IndexOf("VID_"),pi=pnp.IndexOf("PID_"); ushort vid,pid;
                                HashSet<string> matches;
                                if(vi>=0 && pi>=0 && pnp.Length>=vi+8 && pnp.Length>=pi+8 &&
                                    ushort.TryParse(pnp.Substring(vi+4,4),System.Globalization.NumberStyles.HexNumber,null,out vid) &&
                                    ushort.TryParse(pnp.Substring(pi+4,4),System.Globalization.NumberStyles.HexNumber,null,out pid) &&
                                    productNames.TryGetValue((uint)(vid|pid<<16),out matches) && matches.Count==1)
                                    foreach(var match in matches) name=match;
                                if(!string.IsNullOrEmpty(name)) names.Add(name);
                            }
                        }
                    if(names.Count==1) foreach(var name in names) xName=name;
                }
                catch { }
            }
            foreach(var i in slots) result.Add(new HostDeviceInfo((xName??("Controller "+(i+1)))+" (XInput)","xinput:"+i));
            result.Sort(); result.Insert(0,new HostDeviceInfo("Keyboard Numpad","keyboard"));
            result.Insert(0,new HostDeviceInfo("None","")); return result;
        }
        private void Activated(object sender,EventArgs e) { lock(sync) focused=true; }
        private void Deactivated(object sender,EventArgs e)
        { lock(sync) { focused=false; releasePending=true; states.Clear(); foreach(var d in devices.Values) d.Unacquire(); } }
        public void Dispose()
        {
            lock(sync)
            {
                if(disposed) return; disposed=true; form.Activated-=Activated; form.Deactivate-=Deactivated;
                foreach(var id in new List<string>(devices.Keys)) CloseDevice(id);
                states.Clear(); requested.Clear();
            }
        }
    }
}
