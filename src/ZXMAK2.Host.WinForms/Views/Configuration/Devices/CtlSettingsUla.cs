using System;

using ZXMAK2.Engine;
using ZXMAK2.Hardware;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Hardware.Evo;


namespace ZXMAK2.Host.WinForms.Views.Configuration.Devices
{
    public partial class CtlSettingsUla : ConfigScreenControl
    {
        private BusManager m_bmgr;
        private IHostService m_host;
        private UlaDeviceBase m_device;

        public CtlSettingsUla()
        {
            InitializeComponent();
            BindTypeList();
        }

        private void BindTypeList()
        {
            cbxType.Items.Clear();
            foreach (var bdd in DeviceEnumerator.SelectByType<IUlaDevice>())
            {
                cbxType.Items.Add(bdd);
            }
            cbxType.Sorted = true;
        }

        public void Init(BusManager bmgr, IHostService host, UlaDeviceBase device)
        {
            m_bmgr = bmgr;
            m_host = host;
            m_device = device;

            cbxType.SelectedIndex = -1;
            if (m_device != null)
            {
                for (int i = 0; i < cbxType.Items.Count; i++)
                {
                    var bdd = (BusDeviceDescriptor)cbxType.Items[i];
                    if (m_device.GetType() == bdd.Type)
                    {
                        cbxType.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        public void Init(BusManager bmgr, IHostService host, UlaPentEvo device)
        {
            Init(bmgr, host, (UlaDeviceBase)device);
        }

        public override void Apply()
        {
            var bdd = (BusDeviceDescriptor)cbxType.SelectedItem;

            var ula = (IUlaDevice)Activator.CreateInstance(bdd.Type);
            var oldUla = m_bmgr.FindDevice<IUlaDevice>();
            if (oldUla != null && oldUla.GetType() == ula.GetType())
            {
                Init(m_bmgr, m_host, (UlaDeviceBase)oldUla);
                return;
            }
            if (oldUla != null)
            {
                var busOldUla = (BusDeviceBase)oldUla;
                var busNewUla = (BusDeviceBase)ula;
                if (busOldUla != null)
                {
                    m_bmgr.Remove(busOldUla);
                    ula.PortFE = oldUla.PortFE;
                }
                m_bmgr.Add(busNewUla);
            }
            Init(m_bmgr, m_host, (UlaDeviceBase)ula);
        }
    }
}
