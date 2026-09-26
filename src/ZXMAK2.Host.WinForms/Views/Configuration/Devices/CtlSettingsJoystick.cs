using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Xml;
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
        private Dictionary<string,JoystickGameProfile> games=new Dictionary<string,JoystickGameProfile>(StringComparer.OrdinalIgnoreCase);
        private List<JoystickKeyAction> actions=new List<JoystickKeyAction>();
        private string selectedGame="";
        private ComboBox gameChoice;
        private Button assignments,createGame,deleteGame,importGame,exportGame;
        private readonly Button[] extraFire=new Button[3];
        private readonly Label[] extraLabels=new Label[3];
        private bool libraryDirty;
        private bool controllerConnected;
        public static string ProfileLibraryPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"joystick-profiles.xml");
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
            emulates.Items.AddRange(new object[] {"Kempston","Sinclair Joy 1 (6-0)","Sinclair Joy 2 (1-5)","Cursor (AGF / Protek)","Fuller","Kempston Extended (8-bit)"});
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
            AddRow(table,"PC controller",cbxType); AddWide(table,refresh);
            gameChoice=new ComboBox {Name="gameProfile",DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
            AddRow(table,"Game profile",gameChoice);
            var profileButtons=new FlowLayoutPanel {AutoSize=true,Dock=DockStyle.Fill};
            createGame=new Button {Text="New/Copy",Width=68}; deleteGame=new Button {Text="Delete",Width=48};
            importGame=new Button {Text="Import...",Width=54}; exportGame=new Button {Text="Export...",Width=54};
            profileButtons.Controls.AddRange(new Control[] {createGame,deleteGame,importGame,exportGame}); AddWide(table,profileButtons);
            AddRow(table,"Emulates",emulates);
            assignments=new Button {Name="gameKeys",Text="Spectrum key assignments...",AutoSize=true,Dock=DockStyle.Fill};
            AddWide(table,assignments);
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
            for(int i=0;i<3;i++)
            {
                int action=i; extraFire[i]=new Button {Dock=DockStyle.Fill,AutoSize=true};
                extraFire[i].Click+=delegate {StartLearn(6+action);};
                extraFire[i].ContextMenuStrip=new ContextMenuStrip(components);
                extraFire[i].ContextMenuStrip.Items.Add("Clear assignment",null,delegate {CancelLearn(); mapping.ExtraFire[action]="none"; ShowMapping();});
                extraLabels[i]=AddRow(table,"Fire "+(i+2),extraFire[i]);
            }
            AddRow(table,"Auto-fire",autoFire); rateLabel=AddRow(table,"Shots / second",rate);
            AddWide(table,live); AddWide(table,hint); AddWide(table,defaults);
            hardware=new Label { Name="hardwareInfo",Text="Kempston interface",AutoSize=true,Dock=DockStyle.Fill };
            AddWide(table,hardware);
            scroll.Controls.Add(table); group.Controls.Add(scroll); Controls.Add(group);
            cbxType.SelectedIndexChanged+=DeviceChanged;
            emulates.SelectedIndexChanged+=delegate { UpdateHardwareInfo(); UpdateEnabled(); };
            gameChoice.SelectedIndexChanged+=delegate {if(!updating) ChangeGame();};
            createGame.Click+=delegate {CreateGame();}; deleteGame.Click+=delegate {DeleteGame();};
            importGame.Click+=delegate {ImportGame();}; exportGame.Click+=delegate {ExportGame();};
            assignments.Click+=delegate {EditActions();};
            directions.SelectedIndexChanged+=delegate { if(!updating) { mapping.Directions=(JoystickDirections)Math.Max(0,directions.SelectedIndex); UpdateEnabled(); } };
            autoFire.SelectedIndexChanged+=delegate { if(!updating) { mapping.AutoFire=(JoystickAutoFire)Math.Max(0,autoFire.SelectedIndex); UpdateEnabled(); } };
            deadZone.ValueChanged+=delegate { if(!updating) mapping.DeadZone=(int)deadZone.Value; };
            rate.ValueChanged+=delegate { if(!updating) mapping.FireRate=(int)rate.Value; };
            var tips=new ToolTip(components); tips.SetToolTip(rate,"10 shots/second = a 100 ms cycle: 50 ms pressed, 50 ms released.");
            tips.SetToolTip(assignments,"Map controller buttons to the Spectrum keyboard, including Caps Shift and Symbol Shift.");
            refresh.Click+=delegate { CancelLearn(); if(preview!=null) preview.RefreshControllers(); FillDevices(selectedId); };
            defaults.Click+=delegate { if(MessageBox.Show(this,"Restore controls and clear Spectrum key assignments for this profile?","Restore defaults",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;
                CancelLearn(); string name=mapping.DeviceName; mapping=new JoystickMapping {DeviceName=name}; actions.Clear(); ShowMapping(); };
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
            selectedGame=""; actions=mapping.KeyActions; games.Clear(); libraryDirty=false;
            try {if(File.Exists(ProfileLibraryPath)) foreach(var p in ReadLibrary(ProfileLibraryPath)) games[p.Name]=p;}
            catch(Exception ex) {MessageBox.Show(this,"Cannot read saved profiles: "+ex.Message,"Joystick profiles");}
            var current=kempston!=null ? kempston.GameProfile : null;
            if(current!=null) {games[current.Name]=current; selectedGame=current.Name; mapping=current.Mapping.Copy(); actions=current.Actions;}
            emulates.Enabled=kempston!=null;
            emulates.SelectedIndex=kempston!=null ? (int)kempston.InterfaceType : 0;
            if(selectedGame.Length==0 && mapping.Interface<0) mapping.Interface=emulates.SelectedIndex;
            UpdateHardwareInfo();
            FillGames();
            FillDevices(selectedId); ShowMapping(); if(Visible) timer.Start();
        }
        private JoystickMapping GetMapping(string id)
        { JoystickMapping saved; return profiles.TryGetValue(id,out saved) ? saved.Copy() : new JoystickMapping(); }
        private void FillDevices(string id)
        {
            SaveCurrentGame(); updating=true;
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
            controllerConnected=preview!=null && preview.Preview(selectedId).Connected;
            UpdateEnabled();
        }
        private void DeviceChanged(object sender,EventArgs e)
        {
            if(updating) return; CancelLearn(); SaveCurrentGame();
            var item=cbxType.SelectedItem as IHostDeviceInfo;
            selectedId=item!=null ? item.HostId : ""; if(selectedGame.Length==0) {mapping=GetMapping(selectedId); if(mapping.Interface>=0) emulates.SelectedIndex=mapping.Interface;}
            if(selectedGame.Length==0) actions=mapping.KeyActions;
            if(item!=null) mapping.DeviceName=item.Name;
            if(preview!=null) preview.ReleasePreview(); previous=JoystickInput.Empty; ShowMapping();
            controllerConnected=preview!=null && preview.Preview(selectedId).Connected; UpdateEnabled();
        }
        private void ShowMapping()
        {
            updating=true;
            directions.SelectedIndex=(int)mapping.Directions; autoFire.SelectedIndex=(int)mapping.AutoFire;
            deadZone.Value=Math.Max(5,Math.Min(80,mapping.DeadZone)); rate.Value=Math.Max(1,Math.Min(25,mapping.FireRate));
            for(int i=0;i<6;i++) bindings[i].Text=JoystickMapping.Caption(mapping.Bindings[i]);
            for(int i=0;i<3;i++) extraFire[i].Text=JoystickMapping.Caption(mapping.ExtraFire[i]);
            mapping.Reserved.Clear(); foreach(var a in actions) mapping.Reserved.Add(a.Binding);
            mapping.SeparateExtraFire=emulates.SelectedIndex==5;
            assignments.Text="Spectrum key assignments... ("+actions.Count+")";
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
            bool available=kempston!=null && selectedId.Length!=0 && controllerConnected;
            directions.Enabled=deadZone.Enabled=autoFire.Enabled=defaults.Enabled=available;
            deadZone.Enabled=available && mapping.Directions!=JoystickDirections.DPad;
            mapping.SeparateExtraFire=emulates.SelectedIndex==5;
            for(int i=0;i<6;i++) bindings[i].Enabled=available && preview!=null && (i!=5 || mapping.AutoFire==JoystickAutoFire.Toggle);
            rate.Enabled=available && mapping.AutoFire!=JoystickAutoFire.Off;
            for(int i=0;i<4;i++) bindingLabels[i].Visible=bindings[i].Visible=mapping.Directions==JoystickDirections.Custom;
            bindingLabels[5].Visible=bindings[5].Visible=mapping.AutoFire==JoystickAutoFire.Toggle;
            rateLabel.Visible=rate.Visible=mapping.AutoFire!=JoystickAutoFire.Off;
            for(int i=0;i<3;i++) {extraFire[i].Enabled=available && preview!=null && emulates.SelectedIndex==5;
                extraLabels[i].Visible=extraFire[i].Visible=emulates.SelectedIndex==5;}
            assignments.Enabled=available && preview!=null;
            deleteGame.Enabled=exportGame.Enabled=selectedGame.Length>0;
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
                case 5: hardware.Text="Kempston Extended: Fire 2/3/4 on bits 5/6/7. Games must support this extension; Spectrum keys work in all modes."; break;
                default: hardware.Text=kempston!=null ? string.Format("Kempston: port #{0:X4}, mask #{1:X4}, {2} bits",kempston.Port,kempston.Mask,kempston.BitWidth) : "Kempston interface"; break;
            }
        }
        private void StartLearn(int action)
        {
            CancelLearn(); if(preview==null) return;
            baseline=previous=preview.Preview(selectedId);
            if(!baseline.Connected) { hint.Text="Controller disconnected. Reconnect it, then click Refresh controllers."; return; }
            learning=action; learnStarted=Environment.TickCount;
            var button=action<6 ? bindings[action] : extraFire[action-6];
            button.Text="Press / move..."; button.BackColor=Color.LightSkyBlue;
            hint.Text="Waiting for input (10 seconds). Esc cancels; right-click clears an assignment.";
        }
        private void CancelLearn()
        {
            if(learning>=0)
            {
                var button=learning<6 ? bindings[learning] : extraFire[learning-6];
                button.BackColor=SystemColors.Control;
                button.Text=JoystickMapping.Caption(learning<6 ? mapping.Bindings[learning] : mapping.ExtraFire[learning-6]);
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
            if(controllerConnected!=input.Connected) {controllerConnected=input.Connected; UpdateEnabled();}
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
                        int action=learning; CancelLearn();
                        if(!ResolveBinding(binding,action,null)) {ShowMapping(); previous=input; return;}
                        if(action<6) mapping.Bindings[action]=binding; else mapping.ExtraFire[action-6]=binding;
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
            CancelLearn(); SaveCurrentGame();
            device.HostId=selectedId;
            if(kempston!=null) { kempston.Profiles=profiles; kempston.GameProfile=selectedGame.Length>0 ? games[selectedGame] :
                null;
                kempston.InterfaceType=(JoystickInterface)Math.Max(0,emulates.SelectedIndex); }
        }
        public void CommitProfileLibrary()
        {if(libraryDirty) {WriteLibrary(ProfileLibraryPath,games.Values); libraryDirty=false;}}
        public bool ProfileLibraryDirty {get {return libraryDirty;}}
        public Dictionary<string,JoystickGameProfile> CaptureProfileLibrary()
        {SaveCurrentGame(); var copy=new Dictionary<string,JoystickGameProfile>(StringComparer.OrdinalIgnoreCase);
            foreach(var p in games) copy[p.Key]=p.Value.Copy(); return copy;}
        public void RestoreProfileLibrary(Dictionary<string,JoystickGameProfile> pending,bool dirty)
        {
            games=new Dictionary<string,JoystickGameProfile>(StringComparer.OrdinalIgnoreCase);
            foreach(var p in pending) games[p.Key]=p.Value.Copy(); libraryDirty=dirty;
            if(selectedGame.Length>0 && games.ContainsKey(selectedGame))
            {var p=games[selectedGame].Copy(); mapping=p.Mapping; actions=p.Actions; emulates.SelectedIndex=p.Interface;}
            FillGames(); ShowMapping();
        }

        private JoystickGameProfile Snapshot(string name)
        { var p=new JoystickGameProfile {Name=name,Interface=Math.Max(0,emulates.SelectedIndex),Mapping=mapping.Copy()};
            foreach(var a in actions) p.Actions.Add(a.Copy()); return p; }
        private void SaveCurrentGame()
        {
            mapping.KeyActions=actions;
            mapping.Interface=Math.Max(0,emulates.SelectedIndex);
            if(selectedGame.Length==0) profiles[selectedId]=mapping.Copy();
            else {games[selectedGame]=Snapshot(selectedGame); libraryDirty=true;}
        }
        private void FillGames()
        {
            updating=true; gameChoice.Items.Clear(); gameChoice.Items.Add("Standard (per controller)");
            var names=new List<string>(games.Keys); names.Sort(StringComparer.CurrentCultureIgnoreCase);
            foreach(var n in names) gameChoice.Items.Add(n);
            gameChoice.SelectedIndex=selectedGame.Length>0 ? gameChoice.Items.IndexOf(selectedGame) : 0;
            updating=false; UpdateEnabled();
        }
        private void ChangeGame()
        {
            CancelLearn(); SaveCurrentGame(); selectedGame=gameChoice.SelectedIndex>0 ? (string)gameChoice.SelectedItem : "";
            actions=new List<JoystickKeyAction>();
            if(selectedGame.Length>0) {var p=games[selectedGame].Copy(); mapping=p.Mapping; actions=p.Actions; emulates.SelectedIndex=p.Interface;}
            else {mapping=GetMapping(selectedId); actions=mapping.KeyActions; if(mapping.Interface>=0) emulates.SelectedIndex=mapping.Interface;}
            if(preview!=null) preview.ReleasePreview(); previous=JoystickInput.Empty; ShowMapping();
        }
        private void CreateGame()
        {
            using(var f=new Form {Text="New game profile",FormBorderStyle=FormBorderStyle.FixedDialog,ClientSize=new Size(350,110),StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false})
            {
                var name=new TextBox {Left=12,Top=15,Width=325,MaxLength=64};
                var ok=new Button {Text="Create",Left=170,Top=60,DialogResult=DialogResult.OK};
                var cancel=new Button {Text="Cancel",Left=255,Top=60,DialogResult=DialogResult.Cancel};
                f.Controls.AddRange(new Control[] {name,ok,cancel}); f.AcceptButton=ok; f.CancelButton=cancel;
                if(f.ShowDialog(this)!=DialogResult.OK) return;
                string n=name.Text.Trim();
                if(n.Length==0 || n.Equals("Standard",StringComparison.OrdinalIgnoreCase) || games.ContainsKey(n))
                {MessageBox.Show(this,"Choose a unique profile name (Standard is reserved)."); return;}
                SaveCurrentGame(); games[n]=Snapshot(n); selectedGame=n; libraryDirty=true; FillGames(); ShowMapping();
            }
        }
        private void DeleteGame()
        {
            if(selectedGame.Length==0 || MessageBox.Show(this,"Delete profile '"+selectedGame+"'?","Delete profile",MessageBoxButtons.OKCancel)!=DialogResult.OK) return;
            CancelLearn(); games.Remove(selectedGame); selectedGame=""; mapping=GetMapping(selectedId); actions=mapping.KeyActions;
            if(mapping.Interface>=0) emulates.SelectedIndex=mapping.Interface;
            libraryDirty=true; FillGames(); ShowMapping();
        }
        public static List<JoystickGameProfile> ReadLibrary(string path)
        {
            var doc=new XmlDocument {XmlResolver=null};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024})) doc.Load(reader);
            if(doc.DocumentElement==null || doc.DocumentElement.Name!="JoystickProfiles") throw new InvalidDataException("Not a joystick profile file.");
            var result=new List<JoystickGameProfile>();
            foreach(XmlElement node in doc.DocumentElement.SelectNodes("GameProfile"))
            {var p=JoystickGameProfile.FromXml(node); p.Name=p.Name.Trim(); if(p.Name.Length==0 || p.Name.Length>64) throw new InvalidDataException("Invalid profile name."); result.Add(p); if(result.Count>128) throw new InvalidDataException("Too many profiles.");}
            return result;
        }
        public static void WriteLibrary(string path,IEnumerable<JoystickGameProfile> profiles)
        {
            var doc=new XmlDocument(); var root=doc.CreateElement("JoystickProfiles"); doc.AppendChild(root); root.SetAttribute("version","1");
            foreach(var p in profiles) root.AppendChild(p.ToXml(doc));
            string temp=path+".tmp"; doc.Save(temp);
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        }
        private void ImportGame()
        {
            using(var d=new OpenFileDialog {Filter="Joystick profiles (*.xml)|*.xml"})
            {if(d.ShowDialog(this)!=DialogResult.OK) return;
                try {var imported=ReadLibrary(d.FileName); SaveCurrentGame();
                    foreach(var p in imported) {if(p.Name.Equals("Standard",StringComparison.OrdinalIgnoreCase)) p.Name="Imported Standard";
                        string name=p.Name; int i=2; while(games.ContainsKey(p.Name)) p.Name=name+" ("+(i++)+")";
                        games[p.Name]=p; selectedGame=p.Name;}
                    if(imported.Count>0) {var p=games[selectedGame].Copy(); mapping=p.Mapping; actions=p.Actions; emulates.SelectedIndex=p.Interface;}
                    libraryDirty=true; FillGames(); ShowMapping();}
                catch(Exception ex) {MessageBox.Show(this,"Cannot import profiles: "+ex.Message);}}
        }
        private void ExportGame()
        {
            using(var d=new SaveFileDialog {Filter="Joystick profiles (*.xml)|*.xml",FileName="joystick-profile.xml"})
            {if(d.ShowDialog(this)!=DialogResult.OK) return; try {WriteLibrary(d.FileName,new[] {Snapshot(selectedGame)});} catch(Exception ex) {MessageBox.Show(this,"Cannot export profile: "+ex.Message);}}
        }
        private bool ResolveBinding(string binding,int target,JoystickKeyAction current)
        {
            var conflicts=new List<int>(); var keyConflicts=new List<JoystickKeyAction>();
            for(int i=0;i<6;i++) if(i!=target && mapping.Bindings[i]==binding && (i>=4 || mapping.Directions==JoystickDirections.Custom) && (i!=5 || mapping.AutoFire==JoystickAutoFire.Toggle)) conflicts.Add(i);
            if(emulates.SelectedIndex==5) for(int i=0;i<3;i++) if(i+6!=target && mapping.ExtraFire[i]==binding) conflicts.Add(i+6);
            foreach(var a in actions) if(a!=current && a.Binding==binding) keyConflicts.Add(a);
            if(conflicts.Count+keyConflicts.Count==0) return true;
            if(MessageBox.Show(this,JoystickMapping.Caption(binding)+" is already assigned. Replace the existing assignment?","Assignment conflict",MessageBoxButtons.OKCancel)!=DialogResult.OK) return false;
            foreach(int i in conflicts) {if(i<6) mapping.Bindings[i]="none"; else mapping.ExtraFire[i-6]="none";}
            foreach(var a in keyConflicts) actions.Remove(a); return true;
        }
        private void EditActions()
        {
            CancelLearn();
            using(var editor=new JoystickKeysDialog(preview,selectedId,actions,mapping,emulates.SelectedIndex))
            {if(editor.ShowDialog(this)==DialogResult.OK) {actions=editor.Actions; mapping=editor.Mapping; ShowMapping();}}
            previous=JoystickInput.Empty;
        }
    }
}
