using System.Xml;


using System.Collections.Generic;


namespace ZXMAK2.Engine.Interfaces
{
    public interface IBus
    {
        T FindDevice<T>() where T : class;
        List<T> FindDevices<T>() where T : class;
        
        void LoadConfigXml(XmlNode busNode);
        void SaveConfigXml(XmlNode busNode);
    }
}
