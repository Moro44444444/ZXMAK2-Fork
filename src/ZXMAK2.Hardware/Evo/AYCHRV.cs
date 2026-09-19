using System;
using System.Linq;
using ZXMAK2.Hardware.General;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    public class AYCHRV : AY8910, ISoundMixerConfiguration
    {
        public AYCHRV()
        {
            Name = "AY8910-CHRV";
            Description = "AY8910 with #FE value on IRB input (required for PentEvo)";
            IrbHandler += PsgDevice_OnIrbHandler;
        }

        public bool RejectDc
        {
            get { return true; }
        }


        private void PsgDevice_OnIrbHandler(IPsgDevice sender, PsgPortState state)
        {
            if (!state.DirOut)
            {
                state.InState = 0xFE; /*always ready??? Alone Coder us0.36.6*/
            }
        }
    }
}
