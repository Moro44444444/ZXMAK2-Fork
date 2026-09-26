using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZXMAK2.Engine;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Hardware.General;

namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    public partial class CtlSettingsJoystick : ConfigScreenControl
    {
        private IHostService host;
        private IJoystickDevice device;
        private KempstonJoystick kempston;
        private IHostJoystickPreview preview;
        private Dictionary<string,JoystickMapping> profiles=new Dictionary<string,JoystickMapping>();
        private JoystickMapping mapping=new JoystickMapping();
        private ComboBox cbxType,directions,autoFire,emulates;
        private readonly Label[] bindingLabels=new Label[6];
        private Label rateLabel;
        private NumericUpDown deadZone,rate;
        private Button refresh,defaults;
        private readonly Button[] bindings=new Button[6];
        private Label live,hint,hardware;
        private Timer timer;
        private bool updating;
        private string selectedId="";
        private int learning=-1,learnStarted;
        private JoystickInput previous=JoystickInput.Empty,baseline=JoystickInput.Empty;
        public CtlSettingsJoystick() { InitializeComponent(); }
        private void BuildControls()
        {
            var group=new GroupBox { Text="Joystick Settings",Dock=DockStyle.Fill,Padding=new Padding(6) };
            var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true };
            var table=new TableLayoutPanel { Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(0,4,0,4) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));
            emulates=new ComboBox { Name="emulates",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill };
            emulates.Items.AddRange(new object[] {"Kempston","Sinclair Joy 1 (6-0)","Sinclair Joy 2 (1-5)","Cursor (AGF / Protek)","Fuller"});
            emulates.SelectedIndex=0;
            cbxType=new ComboBox { Name="cbxType",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DropDownWidth=360 };
            refresh=new Button { Name="refreshControllers",Text="Refresh controllers",Height=26,Dock=DockStyle.Top };
            directions=new ComboBox { Name="directions",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill };
            directions.Items.AddRange(new object[] {"Auto: D-pad + stick","D-pad only","Stick only","Custom mapping"});
            autoFire=new ComboBox { Name="autoFire",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill };
            autoFire.Items.AddRange(new object[] {"Off","While Fire is held","Toggle button"});
            deadZone=new NumericUpDown { Name="deadZone",Minimum=5,Maximum=80,Value=20,Dock=DockStyle.Fill };
            rate=new NumericUpDown { Name="fireRate",Minimum=1,Maximum=25,Value=10,Dock=DockStyle.Fill };
            live=new Label { Name="liveInput",Text="No device",Dock=DockStyle.Fill,AutoSize=true,MinimumSize=new Size(0,28) };
            hint=new Label { Text="Click an action, then press a button or move a stick / D-pad. Esc cancels.",Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(350,0) };
            defaults=new Button { Text="Restore mapping defaults",AutoSize=true,Dock=DockStyle.Fill };
            AddRow(table,"Emulates",emulates);
            AddRow(table,"PC controller",cbxType); AddWide(table,refresh);
            AddRow(table,"Directions",directions); AddRow(table,"Dead zone (%)",deadZone);
            string[] names={"Up","Down","Left","Right","Fire","Auto-fire toggle"};
            for(int i=0;i<6;i++)
            {
                int action=i;
                bindings[i]=new Button { Name="binding"+i,Dock=DockStyle.Fill,AutoSize=true,ContextMenuStrip=new ContextMenuStrip(components) };
                bindings[i].Click+=delegate { StartLearn(action); };
                bindings[i].ContextMenuStrip.Items.Add("Clear assignment",null,delegate { CancelLearn(); mapping.Bindings[action]="none"; if(action<4) mapping.Directions=JoystickDirections.Custom; ShowMapping(); });
                if(i==4) bindings[i].ContextMenuStrip.Items.Add("Any button",null,delegate { mapping.Bindings[4]="any"; ShowMapping(); });
                bindingLabels[i]=AddRow(table,names[i],bindings[i]);
            }
            AddRow(table,"Auto-fire",autoFire); rateLabel=AddRow(table,"Shots / second",rate);
            AddWide(table,live); AddWide(table,hint); AddWide(table,defaults);
            hardware=new Label { Name="hardwareInfo",Text="Kempston interface",AutoSize=true,Dock=DockStyle.Fill };
            AddWide(table,hardware);
            scroll.Controls.Add(table); group.Controls.Add(scroll); Controls.Add(group);
            cbxType.SelectedIndexChanged+=DeviceChanged;
            emulates.SelectedIndexChanged+=delegate { UpdateHardwareInfo(); };
            directions.SelectedIndexChanged+=delegate { if(!updating) { mapping.Directions=(JoystickDirections)Math.Max(0,directions.SelectedIndex); UpdateEnabled(); } };
            autoFire.SelectedIndexChanged+=delegate { if(!updating) { mapping.AutoFire=(JoystickAutoFire)Math.Max(0,autoFire.SelectedIndex); UpdateEnabled(); } };
            deadZone.ValueChanged+=delegate { if(!updating) mapping.DeadZone=(int)deadZone.Value; };
            rate.ValueChanged+=delegate { if(!updating) mapping.FireRate=(int)rate.Value; };
            refresh.Click+=delegate { CancelLearn(); if(preview!=null) preview.RefreshControllers(); FillDevices(selectedId); };
            defaults.Click+=delegate { CancelLearn(); string name=mapping.DeviceName; mapping=new JoystickMapping {DeviceName=name}; ShowMapping(); };
            timer=new Timer(components) { Interval=30 }; timer.Tick+=TickPreview;
            VisibleChanged+=delegate { if(Visible) timer.Start(); else StopPreview(); };
            ShowMapping();
        }
        private static Label AddRow(TableLayoutPanel table,string text,Control control)
        {
            int row=table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label=new Label { Text=text,Dock=DockStyle.Fill,AutoSize=true,TextAlign=ContentAlignment.MiddleLeft };
            table.Controls.Add(label,0,row);
            table.Controls.Add(control,1,row);
            return label;
        }
        private static void AddWide(TableLayoutPanel table,Control control)
        { int row=table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.AutoSize)); table.Controls.Add(control,0,row); table.SetColumnSpan(control,2); }
        public void Init(BusManager bmgr,IHostService service,IJoystickDevice joystick)
        {
            StopPreview(); host=service; device=joystick; kempston=joystick as KempstonJoystick;
            preview=host!=null ? host.Joystick as IHostJoystickPreview : null;
            profiles=kempston!=null ? kempston.Profiles : new Dictionary<string,JoystickMapping>();
            selectedId=device.HostId??""; mapping=GetMapping(selectedId);
            emulates.Enabled=kempston!=null;
            emulates.SelectedIndex=kempston!=null ? (int)kempston.InterfaceType : 0;
            UpdateHardwareInfo();
            FillDevices(selectedId); ShowMapping(); if(Visible) timer.Start();
        }
        private JoystickMapping GetMapping(string id)
        { JoystickMapping saved; return profiles.TryGetValue(id,out saved) ? saved.Copy() : new JoystickMapping(); }
        private void FillDevices(string id)
        {
            profiles[selectedId]=mapping.Copy(); updating=true;
            try
            {
                cbxType.Items.Clear(); int found=-1;
                if(host!=null && host.Joystick!=null) foreach(var item in host.Joystick.GetAvailableJoysticks())
                { cbxType.Items.Add(item); if(item.HostId==id) { found=cbxType.Items.Count-1; mapping.DeviceName=item.Name; } }
                if(found<0)
                { cbxType.Items.Add(new HostDeviceInfo(id.Length==0?"None":(mapping.DeviceName.Length>0?mapping.DeviceName:"Saved controller")+" (disconnected)",id)); found=cbxType.Items.Count-1; }
                cbxType.SelectedIndex=found;
            }
            finally { updating=false; }
            UpdateEnabled();
        }
        private void DeviceChanged(object sender,EventArgs e)
        {
            if(updating) return; CancelLearn(); profiles[selectedId]=mapping.Copy();
            var item=cbxType.SelectedItem as IHostDeviceInfo;
            selectedId=item!=null ? item.HostId : ""; mapping=GetMapping(selectedId);
            if(item!=null) mapping.DeviceName=item.Name;
            if(preview!=null) preview.ReleasePreview(); previous=JoystickInput.Empty; ShowMapping();
        }
        private void ShowMapping()
        {
            updating=true;
            directions.SelectedIndex=(int)mapping.Directions; autoFire.SelectedIndex=(int)mapping.AutoFire;
            deadZone.Value=Math.Max(5,Math.Min(80,mapping.DeadZone)); rate.Value=Math.Max(1,Math.Min(25,mapping.FireRate));
            for(int i=0;i<6;i++) bindings[i].Text=JoystickMapping.Caption(mapping.Bindings[i]);
            for(int i=0;i<4;i++)
            {
                if(mapping.Directions==JoystickDirections.Auto) bindings[i].Text="D-pad + stick";
                else if(mapping.Directions==JoystickDirections.DPad) bindings[i].Text=JoystickMapping.Caption("p:0:"+new[] {0,2,3,1}[i]);
                else if(mapping.Directions==JoystickDirections.Stick) bindings[i].Text=JoystickMapping.Caption(new[] {"a:1:-","a:1:+","a:0:-","a:0:+"}[i]);
            }
            updating=false; UpdateEnabled();
        }
        private void UpdateEnabled()
        {
            bool available=kempston!=null && selectedId.Length!=0;
            directions.Enabled=deadZone.Enabled=autoFire.Enabled=defaults.Enabled=available;
            for(int i=0;i<6;i++) bindings[i].Enabled=available && preview!=null && (i!=5 || mapping.AutoFire==JoystickAutoFire.Toggle);
            rate.Enabled=available && mapping.AutoFire!=JoystickAutoFire.Off;
            for(int i=0;i<4;i++) bindingLabels[i].Visible=bindings[i].Visible=mapping.Directions==JoystickDirections.Custom;
            bindingLabels[5].Visible=bindings[5].Visible=mapping.AutoFire==JoystickAutoFire.Toggle;
            rateLabel.Visible=rate.Visible=mapping.AutoFire!=JoystickAutoFire.Off;
        }
        private void UpdateHardwareInfo()
        {
            if(hardware==null) return;
            switch(emulates.SelectedIndex)
            {
                case 1: hardware.Text="Sinclair Joy 1: Left 6, Right 7, Down 8, Up 9, Fire 0"; break;
                case 2: hardware.Text="Sinclair Joy 2: Left 1, Right 2, Down 3, Up 4, Fire 5"; break;
                case 3: hardware.Text="Cursor: Left 5, Down 6, Up 7, Right 8, Fire 0"; break;
                case 4: hardware.Text="Fuller: port #007F, active-low directions and Fire"; break;
                default: hardware.Text=kempston!=null ? string.Format("Kempston: port #{0:X4}, mask #{1:X4}, {2} bits",kempston.Port,kempston.Mask,kempston.BitWidth) : "Kempston interface"; break;
            }
        }
        private void StartLearn(int action)
        {
            CancelLearn(); if(preview==null) return;
            baseline=previous=preview.Preview(selectedId);
            if(!baseline.Connected) { hint.Text="Controller disconnected. Reconnect it, then click Refresh controllers."; return; }
            learning=action; learnStarted=Environment.TickCount;
            bindings[action].Text="Press / move..."; bindings[action].BackColor=Color.LightSkyBlue;
            hint.Text="Waiting for input (10 seconds). Esc cancels; right-click clears an assignment.";
        }
        private void CancelLearn()
        {
            if(learning>=0)
            {
                bindings[learning].BackColor=SystemColors.Control;
                bindings[learning].Text=JoystickMapping.Caption(mapping.Bindings[learning]);
                hint.Text="Assignment cancelled. Click an action to try again; Esc cancels.";
            }
            learning=-1;
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        { if(learning>=0 && keyData==Keys.Escape) { CancelLearn(); return true; } return base.ProcessCmdKey(ref msg,keyData); }
        private void TickPreview(object sender,EventArgs e)
        {
            if(preview==null || !Visible || FindForm()==null || !FindForm().ContainsFocus) { CancelLearn(); return; }
            var input=preview.Preview(selectedId);
            byte state=mapping.Map(input);
            live.Text=input.Connected ? "Input: "+((state&8)!=0?"Up ":"")+((state&4)!=0?"Down ":"")+
                ((state&2)!=0?"Left ":"")+((state&1)!=0?"Right ":"")+((state&16)!=0?"Fire":"") : "Controller disconnected / not selected";
            if(learning>=0)
            {
                if(!input.Connected || unchecked(Environment.TickCount-learnStarted)>10000) CancelLearn();
                else
                {
                    string binding=LearnBinding(input,previous,baseline);
                    if(binding!=null)
                    {
                        int action=learning; CancelLearn(); mapping.Bindings[action]=binding;
                        if(action<4) mapping.Directions=JoystickDirections.Custom;
                        ShowMapping(); hint.Text="Assigned. Click another action to change it; right-click to clear.";
                    }
                }
            }
            previous=input;
        }
        public static string LearnBinding(JoystickInput input,JoystickInput previous,JoystickInput baseline)
        {
            if(input==null || !input.Connected || previous==null || baseline==null) return null;
            for(int i=0;i<input.Buttons.Length;i++)
                if(input.Buttons[i] && (i>=previous.Buttons.Length || !previous.Buttons[i])) return "b:"+i;
            for(int i=0;i<input.Hats.Length;i++) for(int d=0;d<4;d++)
                if(input.Hat(i,d) && !previous.Hat(i,d)) return "p:"+i+":"+d;
            for(int i=0;i<input.Axes.Length && i<baseline.Axes.Length;i++)
                if(Math.Abs(input.Axes[i]-baseline.Axes[i])>16000 && Math.Abs(input.Axes[i])>12000 &&
                    (Math.Abs(baseline.Axes[i])<8000 || Math.Sign(input.Axes[i])!=Math.Sign(baseline.Axes[i])))
                    return "a:"+i+":"+(input.Axes[i]<0?"-":"+");
            return null;
        }
        private void StopPreview()
        { CancelLearn(); if(timer!=null) timer.Stop(); if(preview!=null) preview.ReleasePreview(); }
        public override void Apply()
        {
            CancelLearn(); profiles[selectedId]=mapping.Copy();
            device.HostId=selectedId;
            if(kempston!=null) { kempston.Profiles=profiles; kempston.InterfaceType=(JoystickInterface)Math.Max(0,emulates.SelectedIndex); }
        }
    }
}
