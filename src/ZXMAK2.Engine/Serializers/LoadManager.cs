using System;
using System.IO;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Serializers.SnapshotSerializers;


namespace ZXMAK2.Serializers
{
	public class LoadManager : SerializeManager
	{
		private readonly Spectrum _spec;

        public LoadManager(Spectrum spec)
        {
            _spec = spec;
            Clear();
        }

        public override void Clear()
        {
            base.Clear();
            // Default Serializers (Snapshots)...
            AddSerializer(new SzxSerializer(_spec));
            AddSerializer(new Z80Serializer(_spec));
            AddSerializer(new SnaSerializer(_spec));
            AddSerializer(new SitSerializer(_spec));
            AddSerializer(new ZxSerializer(_spec));
            AddSerializer(new RzxSerializer(_spec));
            AddSerializer(new IntelHexSerializer(_spec));
        }

        /// <summary>
        /// Starts the bundled TR-DOS Quick Boot shell without performing the
        /// full snapshot reset path.  Existing floppy images stay connected
        /// and all available drive track views are refreshed in place.
        /// </summary>
        public bool TryOpenQuickBootFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            {
                return false;
            }
            var betaDisk = _spec.BusManager.FindDevice<IBetaDiskDevice>();
            var trDosSession = _spec.BusManager.FindDevice<ITrDosSessionDevice>();
            if (betaDisk == null || trDosSession == null ||
                !trDosSession.IsTrDosSessionActive)
            {
                return false;
            }

            using (var zip = new ZipLib.Zip.ZipFile(fileName))
            {
                ZipLib.Zip.ZipEntry snapshotEntry = null;
                foreach (ZipLib.Zip.ZipEntry entry in zip)
                {
                    if (!entry.IsFile || !entry.CanDecompress ||
                        string.Compare(Path.GetExtension(entry.Name), ".szx", true) != 0)
                    {
                        continue;
                    }
                    snapshotEntry = entry;
                    if (string.Compare(Path.GetFileName(entry.Name), "boot.szx", true) == 0)
                    {
                        break;
                    }
                }
                if (snapshotEntry == null)
                {
                    return false;
                }

                using (var stream = zip.GetInputStream(snapshotEntry))
                {
                    new SzxSerializer(_spec).DeserializeQuickBoot(stream);
                }
            }

            RefreshQuickBootDrives();
            return true;
        }

        private void RefreshQuickBootDrives()
        {
            var betaDisk = _spec.BusManager.FindDevice<IBetaDiskDevice>();
            if (betaDisk == null || betaDisk.FDD == null)
            {
                return;
            }
            foreach (var disk in betaDisk.FDD)
            {
                if (disk != null && disk.Present)
                {
                    // Refresh the WD1793 track view without disconnecting,
                    // reopening or discarding modified in-memory media.
                    disk.t = disk.CurrentTrack;
                }
            }
        }
	}
}
