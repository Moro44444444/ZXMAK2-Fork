using System;


namespace ZXMAK2.Mvvm
{
    /// <summary>
    /// Command that notifies the host only after its operation succeeds.
    /// </summary>
    public interface ISuccessCommand : ICommand
    {
        event EventHandler ExecutedSuccessfully;
    }
}
