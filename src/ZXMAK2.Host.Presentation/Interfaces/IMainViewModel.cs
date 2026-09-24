using System;
using System.ComponentModel;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Mvvm;


namespace ZXMAK2.Host.Presentation.Interfaces
{
    public interface IMainViewModel : IDisposable
    {
        void Run();
        void Attach(ISynchronizeInvoke synchronizeInvoke);
        void ExecuteMediaChange(ISuccessCommand command, object commandParameter);
        void ExecuteFloppyMediaChange(ISuccessCommand command, object commandParameter);
        bool IsMediaMounted(MediaStatusKind mediaKind);
        bool IsFloppyMounted(int driveIndex);
        bool IsSecureDigitalMounted(int secureDigitalIndex);
        MediaState GetMediaState(MediaStatusKind mediaKind, int index);
        bool ResetCmosState();
        bool PrepareFactoryReset();
    }
}
