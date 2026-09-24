using System.Collections.Generic;


namespace ZXMAK2.Engine.Interfaces
{
    /// <summary>
    /// Exposes the independent audio outputs contained by one composite board.
    /// </summary>
    public interface IAdditionalSoundRenderers
    {
        IEnumerable<ISoundRenderer> SoundRenderers { get; }
    }
}
