using System;
using ZXMAK2.Host.Interfaces;

namespace ZXMAK2.Host.Entities
{
    public enum JoystickDirections { Auto, DPad, Stick, Custom }
    public enum JoystickAutoFire { Off, Hold, Toggle }

    // Host samples are immutable after publication. Axes use -32768..32767,
    // positive Y points down. POVs use DirectInput hundredths of a degree.
    public sealed class JoystickInput : IJoystickState8
    {
        public readonly bool Connected;
        public readonly int[] Axes;
        public readonly bool[] Buttons;
        public readonly uint[] Hats;
        public static readonly JoystickInput Empty = new JoystickInput(false,
            new int[8], new bool[32], new uint[] { uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue });
        public JoystickInput(bool connected, int[] axes, bool[] buttons, uint[] hats)
        { Connected=connected; Axes=axes; Buttons=buttons; Hats=hats; }
        public bool IsLeft { get { return Connected && (Axes[0]<-6553 || Hat(0,3)); } }
        public bool IsRight { get { return Connected && (Axes[0]>6553 || Hat(0,1)); } }
        public bool IsUp { get { return Connected && (Axes[1]<-6553 || Hat(0,0)); } }
        public bool IsDown { get { return Connected && (Axes[1]>6553 || Hat(0,2)); } }
        public bool IsFire { get { foreach(var b in Buttons) if(Connected && b) return true; return false; } }
        public byte KempstonState { get { byte result=0; for(int i=0;i<4 && i<Buttons.Length;i++)
            if(Connected && Buttons[i]) result|=(byte)(16<<i); return result; } }
        public bool Hat(int index, int direction)
        {
            if(!Connected || index<0 || index>=Hats.Length || (Hats[index]&0xFFFF)==0xFFFF) return false;
            int sector=(int)(((Hats[index]%36000)+2250)/4500)%8;
            int target=direction*2;
            return sector==target || sector==(target+7)%8 || sector==(target+1)%8;
        }
    }

    public sealed class JoystickMapping
    {
        public string DeviceName = "";
        public JoystickDirections Directions = JoystickDirections.Auto;
        public JoystickAutoFire AutoFire = JoystickAutoFire.Off;
        public int DeadZone = 20;
        public int FireRate = 10;
        // Ordering: up, down, left, right, fire, auto-fire toggle.
        public string[] Bindings = { "a:1:-", "a:1:+", "a:0:-", "a:0:+", "any", "none" };
        public JoystickMapping Copy()
        { return new JoystickMapping { DeviceName=DeviceName, Directions=Directions, AutoFire=AutoFire,
            DeadZone=DeadZone, FireRate=FireRate, Bindings=(string[])Bindings.Clone() }; }
        public bool Pressed(JoystickInput input, string binding)
        {
            if(input==null || !input.Connected || string.IsNullOrEmpty(binding)) return false;
            if(binding=="any") return input.IsFire;
            var parts=binding.Split(':'); int index;
            if(parts.Length<2 || !int.TryParse(parts[1],out index) || index<0) return false;
            if(parts[0]=="b") return index<input.Buttons.Length && input.Buttons[index];
            if(parts.Length!=3) return false;
            if(parts[0]=="p") { int direction; return int.TryParse(parts[2],out direction) &&
                direction>=0 && direction<4 && input.Hat(index,direction); }
            if(parts[0]=="a" && index<input.Axes.Length)
            { int threshold=Math.Max(5,Math.Min(80,DeadZone))*32767/100;
                return parts[2]=="-" ? input.Axes[index]<-threshold :
                    parts[2]=="+" && input.Axes[index]>threshold; }
            return false;
        }
        public byte Map(JoystickInput input)
        {
            if(input==null || !input.Connected) return 0;
            bool up=false,down=false,left=false,right=false;
            if(Directions==JoystickDirections.Custom)
            { up=Pressed(input,Bindings[0]); down=Pressed(input,Bindings[1]);
                left=Pressed(input,Bindings[2]); right=Pressed(input,Bindings[3]); }
            else
            {
                if(Directions!=JoystickDirections.DPad)
                { up=Pressed(input,"a:1:-"); down=Pressed(input,"a:1:+");
                    left=Pressed(input,"a:0:-"); right=Pressed(input,"a:0:+"); }
                if(Directions!=JoystickDirections.Stick)
                { up|=input.Hat(0,0); right|=input.Hat(0,1); down|=input.Hat(0,2); left|=input.Hat(0,3); }
            }
            byte result=0;
            if(right && !left) result|=1; if(left && !right) result|=2;
            if(down && !up) result|=4; if(up && !down) result|=8;
            bool fire=Pressed(input,Bindings[4]);
            // A dedicated toggle button must not accidentally become Fire.
            if(Bindings[4]=="any")
            {
                fire=false;
                for(int i=0;i<input.Buttons.Length;i++)
                    if(input.Buttons[i] && Bindings[5]!="b:"+i) fire=true;
            }
            if(fire) result|=16;
            result|=(byte)(input.KempstonState & 0xE0); // existing 8-bit BaseConf extension
            for(int i=1;i<4;i++) if(Bindings[5]=="b:"+i) result&=(byte)~(16<<i);
            return result;
        }
        public static string Caption(string binding)
        {
            if(binding=="any") return "Any button"; if(binding=="none") return "Not assigned";
            var p=(binding??"").Split(':'); int i;
            if(p.Length<2 || !int.TryParse(p[1],out i)) return "Not assigned";
            if(p[0]=="b") return "Button "+(i+1);
            if(p[0]=="a" && p.Length==3) return "Axis "+(i+1)+" "+p[2];
            int direction;
            if(p[0]=="p" && p.Length==3 && int.TryParse(p[2],out direction) && direction>=0 && direction<4)
                return "D-pad "+(i+1)+" "+new[] {"Up","Right","Down","Left"}[direction];
            return "Not assigned";
        }
    }

    // No wall-clock timers: firing advances only with emulated machine time.
    public sealed class JoystickFireController
    {
        private bool latched, previousToggle, running, awaitRelease;
        private double started;
        public void Reset() { latched=previousToggle=running=false; awaitRelease=true; started=0; }
        public void Sample(JoystickInput input, JoystickMapping mapping)
        {
            if(input==null || !input.Connected) { Reset(); return; }
            bool toggle=mapping.Pressed(input,mapping.Bindings[5]);
            // A held button on reconnect / returning focus must not restart
            // a previously cancelled latch. Require release before arming.
            if(awaitRelease) { previousToggle=toggle; if(!toggle) awaitRelease=false; return; }
            if(mapping.AutoFire==JoystickAutoFire.Toggle && toggle && !previousToggle) latched=!latched;
            if(mapping.AutoFire!=JoystickAutoFire.Toggle) latched=false;
            previousToggle=toggle;
        }
        public byte Output(JoystickInput input, JoystickMapping mapping, double seconds)
        {
            byte value=mapping.Map(input);
            bool active=input!=null && input.Connected &&
                (mapping.AutoFire==JoystickAutoFire.Hold && (value&16)!=0 ||
                 mapping.AutoFire==JoystickAutoFire.Toggle && latched);
            if(!active) { running=false; return value; }
            if(!running) { running=true; started=seconds; }
            double phase=Math.Max(0,seconds-started)*Math.Max(1,Math.Min(25,mapping.FireRate));
            bool fire=(phase-Math.Floor(phase))<0.5;
            return (byte)((value&~16)|(fire?16:0));
        }
    }
}
