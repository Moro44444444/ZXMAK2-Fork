using ZXMAK2.Model.Disk;


namespace ZXMAK2.Engine.Interfaces
{
	public interface IBetaDiskDevice
	{
        bool DOSEN { get; set; }
        DiskImage[] FDD { get; }
        bool IsDriveMounted(int driveIndex);
        bool NoDelay { get; set; }
        bool LogIo { get; set; }

        ISerializeManager[] LoadManagers { get; }
	}
}
