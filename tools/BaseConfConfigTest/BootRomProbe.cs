using System;
using System.IO;
using System.Text;
using System.Drawing;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Hardware.General;

internal static class BootRomProbe
{
    private static bool Contains(byte[] bytes,string text)
    { return Encoding.ASCII.GetString(bytes).Contains(text); }
    private static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    private static int Main(string[] args)
    {
        try
        {
            byte[] original=File.ReadAllBytes(args[0]),fork=File.ReadAllBytes(args[1]);
            Check(original.Length==0x80000 && fork.Length==original.Length,"ROM sizes");
            for(int i=0;i<fork.Length;i++) if(i/0x4000!=26) Check(fork[i]==original[i],"Other ROM pages changed");
            var spec=new Spectrum(); var bus=spec.BusManager;
            bus.Init(spec,true); bus.Disconnect(); bus.Clear();
            var memory=new MemoryPentEvo(); var ula=new UlaPentEvo();
            bus.Add(memory); bus.Add(ula); bus.Add(new CmosPentEvo()); bus.Add(new KeyboardDevice {NoDos=false});
            Check(bus.Connect(),"Boot bus connects");
            for(int page=0;page<32;page++) Array.Copy(fork,page*0x4000,memory.RomPages[page],0,0x4000);
            spec.DebugReset();
            for(int i=0;i<300;i++) spec.ExecuteFrame();
            bool conf=false,boot=false,web=false;
            foreach(var page in memory.RamPages)
            {
                conf|=Contains(page,"BaseConf Emu (ZXMAK2-Fork)");
                boot|=Contains(page,"Boot Emu (ZXMAK2-Fork)");
                web|=Contains(page,"www.nedopc.com");
            }
            Check(conf && boot && web,"Guest boot did not unpack fork labels and NedoPC credit");
            if(args.Length>2)
            {
                ula.ForceRedrawFrame(); var video=ula.VideoData;
                using(var image=new Bitmap(video.Size.Width,video.Size.Height))
                {
                    for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) image.SetPixel(x,y,Color.FromArgb(video.Buffer[y*image.Width+x]));
                    image.Save(args[2]);
                }
            }
            spec.DebugReset(); for(int i=0;i<100;i++) spec.ExecuteFrame();
            bus.Disconnect();
            Console.WriteLine("BootRomProbe PASS: real guest boot, reset, fork labels, NedoPC credit, unchanged 31 ROM pages");
            return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
