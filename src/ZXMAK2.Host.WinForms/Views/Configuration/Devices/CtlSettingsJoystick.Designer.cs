namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    partial class CtlSettingsJoystick
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if(disposing) { StopPreview(); if(components!=null) components.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components=new System.ComponentModel.Container();
            AutoScaleDimensions=new System.Drawing.SizeF(6,13);
            AutoScaleMode=System.Windows.Forms.AutoScaleMode.Font;
            Name="CtlSettingsJoystick"; Size=new System.Drawing.Size(300,400);
            BuildControls();
        }
    }
}
