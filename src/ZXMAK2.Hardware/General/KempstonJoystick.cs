using System;
using System.Xml;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using System.Text;
using System.Collections.Generic;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Cpu;


namespace ZXMAK2.Hardware.General
{
    public enum JoystickInterface { Kempston, Sinclair1, Sinclair2, Cursor, Fuller }

    // Keep the original class/XML identity for old machine configurations.
    public class KempstonJoystick : BusDeviceBase, IJoystickKeyboardDevice
    {
        #region Fields

        private IMemoryDevice m_memory;
        private string m_hostId = string.Empty;

        private bool m_noDos;
        private int m_mask;
        private int m_port;
        private int m_bitWidth;
        private readonly JoystickFireController fireController = new JoystickFireController();
        private Dictionary<string,JoystickMapping> profiles = new Dictionary<string,JoystickMapping>();
        private IJoystickState joystickState;
        private CpuUnit cpu;
        private IUlaDevice ula;
        private IUlaFrameTiming frameTiming;
        private double frameSeconds;
        private long frameStart;
        private JoystickInterface interfaceType;

        public JoystickInterface InterfaceType
        {
            get { return interfaceType; }
            set
            {
                interfaceType = Enum.IsDefined(typeof(JoystickInterface), value) ? value : JoystickInterface.Kempston;
                fireController.Reset();
                OnConfigChanged();
            }
        }

        public Dictionary<string,JoystickMapping> Profiles
        {
            get { var copy=new Dictionary<string,JoystickMapping>(); foreach(var p in profiles) copy[p.Key]=p.Value.Copy(); return copy; }
            set { profiles=new Dictionary<string,JoystickMapping>(); if(value!=null) foreach(var p in value) profiles[p.Key]=p.Value.Copy(); fireController.Reset(); OnConfigChanged(); }
        }
        public JoystickMapping Mapping
        { get { JoystickMapping result; return profiles.TryGetValue(HostId,out result) ? result : new JoystickMapping(); } }

        #endregion Fields


        public KempstonJoystick()
        {
            Category = BusDeviceCategory.Joystick;
            Name = "JOYSTICK KEMPSTON";
            Description = "Kempston Joystick";

            m_noDos = true;
            m_mask = 0xE0;    // zx128 by default
            m_port = 0x1F;
            m_bitWidth = 5;
            OnProcessConfigChange();
        }

        
        #region Properties

        public bool NoDos
        {
            get { return m_noDos; }
            set
            {
                m_noDos = value;
                OnConfigChanged();
            }
        }

        public int Mask
        {
            get { return m_mask; }
            set
            {
                m_mask = value;
                OnConfigChanged();
            }
        }

        public int Port
        {
            get { return m_port; }
            set
            {
                m_port = value;
                OnConfigChanged();
            }
        }

        public int BitWidth
        {
            get { return m_bitWidth; }
            set
            {
                m_bitWidth = value < 5 ? 5 : value > 8 ? 8 : value;
                OnConfigChanged();
            }
        }

        protected override void OnConfigLoad(XmlNode node)
        {
            base.OnConfigLoad(node);
            InterfaceType = (JoystickInterface)Utils.GetXmlAttributeAsInt32(node, "interface", 0);
            NoDos = Utils.GetXmlAttributeAsBool(node, "noDos", NoDos);
            Mask = Utils.GetXmlAttributeAsInt32(node, "mask", Mask);
            Port = Utils.GetXmlAttributeAsInt32(node, "port", Port);
            BitWidth = Utils.GetXmlAttributeAsInt32(node, "bitWidth", BitWidth);
            HostId = Utils.GetXmlAttributeAsString(node, "hostId", HostId);
            var loaded=new Dictionary<string,JoystickMapping>();
            foreach(XmlNode child in node.SelectNodes("JoystickProfile"))
            {
                var map=new JoystickMapping(); int mode;
                map.DeviceName=Utils.GetXmlAttributeAsString(child,"deviceName","");
                mode=Utils.GetXmlAttributeAsInt32(child,"directions",0);
                map.Directions=(JoystickDirections)Math.Max(0,Math.Min(3,mode));
                mode=Utils.GetXmlAttributeAsInt32(child,"autoFire",0);
                map.AutoFire=(JoystickAutoFire)Math.Max(0,Math.Min(2,mode));
                map.DeadZone=Math.Max(5,Math.Min(80,Utils.GetXmlAttributeAsInt32(child,"deadZone",20)));
                map.FireRate=Math.Max(1,Math.Min(25,Utils.GetXmlAttributeAsInt32(child,"fireRate",10)));
                for(int i=0;i<6;i++) map.Bindings[i]=Utils.GetXmlAttributeAsString(child,"binding"+i,map.Bindings[i]);
                loaded[Utils.GetXmlAttributeAsString(child,"hostId","")]=map;
            }
            Profiles=loaded;
        }

        protected override void OnConfigSave(XmlNode node)
        {
            base.OnConfigSave(node);
            Utils.SetXmlAttribute(node, "interface", (int)InterfaceType);
            Utils.SetXmlAttribute(node, "noDos", NoDos);
            Utils.SetXmlAttribute(node, "mask", Mask);
            Utils.SetXmlAttribute(node, "port", Port);
            Utils.SetXmlAttribute(node, "bitWidth", BitWidth);
            Utils.SetXmlAttribute(node, "hostId", HostId);
            foreach(XmlNode old in node.SelectNodes("JoystickProfile")) node.RemoveChild(old);
            foreach(var profile in profiles)
            {
                var child=node.OwnerDocument.CreateElement("JoystickProfile"); node.AppendChild(child);
                Utils.SetXmlAttribute(child,"hostId",profile.Key);
                Utils.SetXmlAttribute(child,"deviceName",profile.Value.DeviceName);
                Utils.SetXmlAttribute(child,"directions",(int)profile.Value.Directions);
                Utils.SetXmlAttribute(child,"autoFire",(int)profile.Value.AutoFire);
                Utils.SetXmlAttribute(child,"deadZone",profile.Value.DeadZone);
                Utils.SetXmlAttribute(child,"fireRate",profile.Value.FireRate);
                for(int i=0;i<6;i++) Utils.SetXmlAttribute(child,"binding"+i,profile.Value.Bindings[i]);
            }
        }

        protected override void OnProcessConfigChange()
        {
            base.OnProcessConfigChange();
            Name = "JOYSTICK " + (InterfaceType == JoystickInterface.Sinclair1 ? "SINCLAIR 1" :
                InterfaceType == JoystickInterface.Sinclair2 ? "SINCLAIR 2" : InterfaceType.ToString().ToUpperInvariant());
            if (InterfaceType != JoystickInterface.Kempston)
            {
                Description = InterfaceType == JoystickInterface.Fuller ? "Fuller joystick: port #007F, active-low directions and Fire" :
                    InterfaceType == JoystickInterface.Sinclair1 ? "Sinclair Joy 1: keys 6, 7, 8, 9, 0" :
                    InterfaceType == JoystickInterface.Sinclair2 ? "Sinclair Joy 2: keys 1, 2, 3, 4, 5" :
                    "Cursor (AGF / Protek): keys 5, 6, 7, 8, 0";
                return;
            }
            var builder = new StringBuilder();
            builder.Append("Kempston Joystick");
            builder.Append(Environment.NewLine);
            builder.Append(Environment.NewLine);
            builder.Append(string.Format("NoDos: {0}", NoDos));
            builder.Append(Environment.NewLine);
            builder.Append(string.Format("Mask:  #{0:X4}", Mask));
            builder.Append(Environment.NewLine);
            builder.Append(string.Format("Port:  #{0:X4}", Port));
            builder.Append(Environment.NewLine);
            builder.Append(string.Format("Bits:  {0}", BitWidth));
            builder.Append(Environment.NewLine);
            builder.Append(Environment.NewLine);
            // thanks to weiv for info
            if (Mask == 0x20 && Port == 0x1f)
                builder.Append("Classic ZX48 config");
            else if (Mask == 0xe0 && Port == 0x1f)
                builder.Append("Classic ZX128 config");
            else
                builder.Append("Custom config (not compatible with ZX48 & ZX128)");
            builder.Append(Environment.NewLine);
            Description = builder.ToString();
        }

        #endregion Properties


        #region IBusDevice

        public override void BusInit(IBusManager bmgr)
        {
            m_memory = m_noDos ? bmgr.FindDevice<IMemoryDevice>() : null;
            if (InterfaceType == JoystickInterface.Kempston)
                bmgr.Events.SubscribeRdIo(Mask, Port & Mask, ReadPort1F);
            else if (InterfaceType == JoystickInterface.Fuller)
                bmgr.Events.SubscribeRdIo(0xFF, 0x7F, ReadPortFuller);
            else if (bmgr.FindDevice<IKeyboardJoystickSink>() == null)
                // A machine with only an AT keyboard can still have an
                // experimental external keyboard-contact joystick attached.
                bmgr.Events.SubscribeRdIo(1, 0, ReadKeyboardContacts);
            cpu=bmgr.CPU; ula=bmgr.FindDevice<IUlaDevice>(); frameTiming=ula as IUlaFrameTiming;
            bmgr.Events.SubscribeBeginFrame(delegate { frameStart=cpu.Tact; });
            bmgr.Events.SubscribeEndFrame(delegate { frameSeconds+=0.02; });
            bmgr.Events.SubscribeReset(delegate { fireController.Reset(); frameSeconds=0; frameStart=cpu.Tact; joystickState=null; });
        }

        public override void BusConnect()
        {
        }

        public override void BusDisconnect()
        {
            fireController.Reset(); joystickState=null;
        }

        #endregion IBusDevice


        public IJoystickState JoystickState
        {
            get { return joystickState; }
            set { joystickState=value; fireController.Sample(value as JoystickInput,Mapping); }
        }
        
        public string HostId 
        {
            get { return m_hostId; }
            set { m_hostId = value ?? ""; fireController.Reset(); joystickState=null; OnConfigChanged(); }
        }


        protected virtual void ReadPort1F(ushort addr, ref byte value, ref bool handled)
        {
            if (handled || (m_memory != null && m_memory.DOSEN)) // nodos?
                return;
            handled = true;
            value = GetOutput();
        }

        private void ReadPortFuller(ushort addr, ref byte value, ref bool handled)
        {
            if (handled || (m_memory != null && m_memory.DOSEN)) return;
            handled = true;
            byte state = GetOutput();
            int pressed = ((state & 8) >> 3) | ((state & 4) >> 1) |
                ((state & 2) << 1) | ((state & 1) << 3) | ((state & 16) << 3);
            value = (byte)~pressed;
        }

        private void ReadKeyboardContacts(ushort addr, ref byte value, ref bool handled)
        {
            if (!handled) value &= (byte)~GetKeyboardMask(addr);
        }

        public byte GetKeyboardMask(ushort address)
        {
            if (InterfaceType == JoystickInterface.Kempston || InterfaceType == JoystickInterface.Fuller) return 0;
            byte state = GetOutput();
            int pressed = 0;
            if (InterfaceType == JoystickInterface.Sinclair1 && (address & 0x1000) == 0)
                pressed = ((state & 2) << 3) | ((state & 1) << 3) | (state & 4) |
                    ((state & 8) >> 2) | ((state & 16) >> 4);
            else if (InterfaceType == JoystickInterface.Sinclair2 && (address & 0x0800) == 0)
                pressed = ((state & 2) >> 1) | ((state & 1) << 1) | (state & 4) | (state & 8) | (state & 16);
            else if (InterfaceType == JoystickInterface.Cursor)
            {
                if ((address & 0x0800) == 0) pressed |= (state & 2) << 3;
                if ((address & 0x1000) == 0) pressed |= ((state & 4) << 2) | (state & 8) |
                    ((state & 1) << 2) | ((state & 16) >> 4);
            }
            return (byte)pressed;
        }

        private byte GetOutput()
        {
            byte value = 0;
            if (JoystickState == null)
                return value;
            var raw=JoystickState as JoystickInput;
            if(raw!=null)
            {
                int frameTacts=ula!=null ? Math.Max(1,ula.FrameTactCount) : 71680;
                long tact=cpu!=null ? cpu.Tact : frameStart;
                long elapsed=frameTiming!=null ? frameTiming.GetFrameTact(tact) : tact-frameStart;
                double seconds=frameSeconds+Math.Max(0,Math.Min(frameTacts,elapsed))/(double)frameTacts*0.02;
                value=fireController.Output(raw,Mapping,seconds);
                if(BitWidth!=8) value&=0x1F;
                return value;
            }
            if (JoystickState.IsRight) value |= 0x01;
            if (JoystickState.IsLeft) value |= 0x02;
            if (JoystickState.IsDown) value |= 0x04;
            if (JoystickState.IsUp) value |= 0x08;
            if (JoystickState.IsFire) value |= 0x10;
            var extended = JoystickState as IJoystickState8;
            if (BitWidth == 8 && extended != null)
                value = (byte)((value & 0x0F) | (extended.KempstonState & 0xF0));
            return value;
        }
    }
}
