using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;

namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    internal sealed class JoystickKeysDialog : Form
    {
        public List<JoystickKeyAction> Actions {get; private set;}
        public JoystickMapping Mapping {get; private set;}
        private readonly IHostJoystickPreview preview;
        private readonly string deviceId;
        private readonly int interfaceType;
        private readonly ListView list=new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false};
        private readonly Timer timer=new Timer {Interval=40};
        public JoystickKeysDialog(IHostJoystickPreview source,string id,List<JoystickKeyAction> actions,JoystickMapping mapping,int mode)
        {
            preview=source; deviceId=id; interfaceType=mode; Mapping=mapping.Copy(); Actions=new List<JoystickKeyAction>();
            foreach(var a in actions) Actions.Add(a.Copy());
            Text="Spectrum key assignments"; ClientSize=new Size(630,370); MinimumSize=new Size(530,300);
            StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false;
            var help=new Label {Dock=DockStyle.Top,Height=40,Text="Each controller control presses a Spectrum key or Shift combination.\nGreen rows show active assignments. Changes are saved with Apply in Machine Settings."};
            list.Columns.Add("Action",190); list.Columns.Add("Controller",170); list.Columns.Add("Spectrum key",220);
            var buttons=new FlowLayoutPanel {Dock=DockStyle.Bottom,Height=40};
            var add=new Button {Text="Add..."}; var edit=new Button {Text="Edit..."}; var remove=new Button {Text="Remove"};
            var ok=new Button {Text="OK",DialogResult=DialogResult.OK}; var cancel=new Button {Text="Cancel",DialogResult=DialogResult.Cancel};
            buttons.Controls.AddRange(new Control[] {add,edit,remove,ok,cancel});
            Controls.Add(list); Controls.Add(help); Controls.Add(buttons); AcceptButton=ok; CancelButton=cancel;
            add.Click+=delegate {Edit(-1);}; edit.Click+=delegate {if(list.SelectedIndices.Count>0) Edit(list.SelectedIndices[0]);};
            remove.Click+=delegate {if(list.SelectedIndices.Count>0) {Actions.RemoveAt(list.SelectedIndices[0]); Fill();}};
            list.DoubleClick+=delegate {if(list.SelectedIndices.Count>0) Edit(list.SelectedIndices[0]);};
            list.SelectedIndexChanged+=delegate {edit.Enabled=remove.Enabled=list.SelectedIndices.Count>0;}; edit.Enabled=remove.Enabled=false;
            timer.Tick+=delegate {
                if(!ContainsFocus) return; var input=preview.Preview(deviceId);
                for(int i=0;i<Actions.Count;i++) list.Items[i].BackColor=Mapping.Pressed(input,Actions[i].Binding) ? Color.LightGreen : SystemColors.Window;
            };
            Fill(); timer.Start();
        }
        private void Fill()
        {list.Items.Clear(); foreach(var a in Actions) list.Items.Add(new ListViewItem(new[] {a.Name,JoystickMapping.Caption(a.Binding),a.Caption}));}
        private bool Resolve(string binding,JoystickKeyAction current)
        {
            var indices=new List<int>(); var keys=new List<JoystickKeyAction>();
            for(int i=0;i<6;i++) if(Mapping.Bindings[i]==binding && (i>=4 || Mapping.Directions==JoystickDirections.Custom) && (i!=5 || Mapping.AutoFire==JoystickAutoFire.Toggle)) indices.Add(i);
            if(interfaceType==5) for(int i=0;i<3;i++) if(Mapping.ExtraFire[i]==binding) indices.Add(i+6);
            foreach(var a in Actions) if(a!=current && a.Binding==binding) keys.Add(a);
            if(indices.Count+keys.Count==0 && !Mapping.IsAutomaticDirection(binding)) return true;
            if(MessageBox.Show(this,"This controller control is already assigned. Replace its existing assignment?","Assignment conflict",MessageBoxButtons.OKCancel)!=DialogResult.OK) return false;
            foreach(int i in indices) {if(i<6) Mapping.Bindings[i]="none"; else Mapping.ExtraFire[i-6]="none";}
            foreach(var a in keys) Actions.Remove(a); return true;
        }
        private void Edit(int index)
        {
            if(index<0 && Actions.Count>=64) {MessageBox.Show(this,"A profile can contain up to 64 assignments."); return;}
            var original=index>=0 ? Actions[index] : null;
            var action=original!=null ? original.Copy() : new JoystickKeyAction();
            using(var f=new Form {Text=index<0 ? "Add Spectrum key" : "Edit Spectrum key",ClientSize=new Size(450,230),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false})
            using(var learnTimer=new Timer {Interval=30})
            {
                var name=new TextBox {Left=115,Top=15,Width=310,Text=action.Name,MaxLength=64};
                var key=new ComboBox {Left=115,Top=50,Width=310,DropDownStyle=ComboBoxStyle.DropDownList};
                var keys=new List<SpeccyKey>(); foreach(SpeccyKey k in Enum.GetValues(typeof(SpeccyKey))) keys.Add(k);
                keys.Sort(delegate(SpeccyKey a,SpeccyKey b) {return string.Compare(JoystickKeyAction.KeyCaption(a),JoystickKeyAction.KeyCaption(b),StringComparison.CurrentCulture);});
                foreach(var k in keys) key.Items.Add(JoystickKeyAction.KeyCaption(k)); key.SelectedIndex=keys.IndexOf(action.Key);
                var caps=new CheckBox {Text="Caps Shift",Left=115,Top=85,Width=130,Checked=action.CapsShift};
                var symbol=new CheckBox {Text="Symbol Shift",Left=250,Top=85,Width=140,Checked=action.SymbolShift};
                var learn=new Button {Left=115,Top=115,Width=310,Text=JoystickMapping.Caption(action.Binding)};
                var status=new Label {Left=15,Top=150,Width=415,Height=35,Text="Choose a Spectrum key, then click the controller assignment."};
                var ok=new Button {Text="OK",Left=260,Top=193}; var cancel=new Button {Text="Cancel",Left=350,Top=193,DialogResult=DialogResult.Cancel};
                f.Controls.AddRange(new Control[] {new Label {Text="Action name",Left=15,Top=18},name,new Label {Text="Spectrum key",Left=15,Top=53},key,caps,symbol,new Label {Text="Controller",Left=15,Top=120},learn,status,ok,cancel});
                f.AcceptButton=ok; f.CancelButton=cancel;
                bool learning=false; int started=0; JoystickInput baseline=JoystickInput.Empty,previous=JoystickInput.Empty;
                learn.Click+=delegate {baseline=previous=preview.Preview(deviceId); if(!baseline.Connected) {status.Text="Controller disconnected. Close and Refresh controllers."; return;}
                    learning=true; started=Environment.TickCount; learn.Text="Press a button / move..."; status.Text="Waiting for input (10 seconds). Cancel leaves the old assignment.";};
                learnTimer.Tick+=delegate {
                    if(!learning) return;
                    if(!f.ContainsFocus || unchecked(Environment.TickCount-started)>10000) {learning=false; learn.Text=JoystickMapping.Caption(action.Binding); status.Text="Assignment cancelled. Click to try again."; return;}
                    var input=preview.Preview(deviceId);
                    if(!input.Connected) {learning=false; status.Text="Controller disconnected."; learn.Text=JoystickMapping.Caption(action.Binding); return;}
                    string b=CtlSettingsJoystick.LearnBinding(input,previous,baseline); previous=input;
                    if(b!=null) {action.Binding=b; learning=false; learn.Text=JoystickMapping.Caption(b); status.Text="Assigned. Choose the Spectrum key and press OK.";}
                };
                ok.Click+=delegate {
                    if(learning || action.Binding=="none") {status.Text="Assign a controller control first."; return;}
                    if(!Resolve(action.Binding,original)) return;
                    action.Key=keys[key.SelectedIndex]; action.CapsShift=caps.Checked; action.SymbolShift=symbol.Checked;
                    action.Name=name.Text.Trim(); if(action.Name.Length==0) action.Name=action.Caption;
                    if(original!=null) Actions.Remove(original); Actions.Add(action); f.DialogResult=DialogResult.OK;
                };
                learnTimer.Start(); f.ShowDialog(this); preview.ReleasePreview(); Fill();
            }
        }
        protected override void Dispose(bool disposing)
        {if(disposing) {timer.Stop(); timer.Dispose(); preview.ReleasePreview();} base.Dispose(disposing);}
    }
}
