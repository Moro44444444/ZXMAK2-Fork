using System;
using System.IO;
using System.Runtime.InteropServices;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;


namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// ZXM-MoonSound Rev.01, CPLD firmware 1.00 (07.09.2016).
    ///
    /// The board has one YMF278B/OPL4 at 33.8688 MHz, a 2 MiB YRW801-M
    /// instrument ROM and 1 MiB SRAM.  Rev.01 decodes the low byte only:
    /// #7E/#7F for the PCM side and #C4-#C7 for the FM side.  This is the
    /// fixed port map from the published dd2.tdf firmware; there are no
    /// configurable jumpers in this revision.
    /// </summary>
    public sealed class ZxmMoonSoundDevice : SoundDeviceBase,
        ISoundMixerConfiguration
    {
        private const int RomSize = 2 * 1024 * 1024;
        private const int RamSize = 1024 * 1024;
        private static readonly string[] RomNames =
        {
            "YRW801-M - Yamaha - 1993.rom",
            "yrw801.rom",
        };

        private IntPtr m_core;
        private short[] m_nativeBuffer = new short[0];
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private string m_loadedRomPath = string.Empty;
        private MemoryPentEvo m_hostMemory;

        public ZxmMoonSoundDevice()
        {
            Name = "ZXM-MoonSound Rev.01";
            Description =
                "Mick Laboratory ZXM-MoonSound Rev.01, CPLD 1.00: " +
                "YMF278B/OPL4 33.8688 MHz, YRW801-M 2 MB ROM and 1 MB SRAM. " +
                "Ports: #7E/#7F PCM and #C4-#C7 FM.";
            Category = BusDeviceCategory.Music;
        }

        public bool RejectDc { get { return true; } }
        public bool CoreAvailable { get { return m_core != IntPtr.Zero; } }
        public string LoadedRomPath { get { return m_loadedRomPath; } }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            m_hostMemory = bmgr.FindDevice<MemoryPentEvo>();
            InitializeCore();
            bmgr.Events.SubscribeWrIo(0x00FE, 0x007E, WriteWavePort);
            bmgr.Events.SubscribeRdIo(0x00FE, 0x007E, ReadWavePort);
            bmgr.Events.SubscribeWrIo(0x00FC, 0x00C4, WriteFmPort);
            bmgr.Events.SubscribeRdIo(0x00FC, 0x00C4, ReadFmPort);
            bmgr.Events.SubscribeReset(ResetChip);
        }

        public override void BusConnect()
        {
            base.BusConnect();
        }

        public override void BusDisconnect()
        {
            m_rendering = false;
            ReleaseCore();
            base.BusDisconnect();
        }

        public override void ResetState()
        {
            ResetChip();
        }

        protected override void OnBeginFrame()
        {
            base.OnBeginFrame();
            m_frameSamples = Math.Max(1, AudioBuffer.Length);
            int required = m_frameSamples * 2;
            if (m_nativeBuffer.Length != required)
                m_nativeBuffer = new short[required];
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
        }

        private void InitializeCore()
        {
            ReleaseCore();
            m_loadedRomPath = FindRomPath();
            if (string.IsNullOrEmpty(m_loadedRomPath))
            {
                Logger.Error(
                    "ZXM-MoonSound: 2 MB YRW801-M ROM not found beside " +
                    "ZXMAK2.exe or in its roms folder");
                return;
            }

            try
            {
                byte[] rom = File.ReadAllBytes(m_loadedRomPath);
                if (rom.Length != RomSize)
                    throw new InvalidDataException(
                        "YRW801-M ROM must be exactly 2097152 bytes");
                m_core = NativeMethods.Create(rom, (uint)rom.Length, RamSize);
                if (m_core == IntPtr.Zero)
                    throw new InvalidOperationException(
                        "ymfm could not create a YMF278B instance");
            }
            catch (Exception ex)
            {
                m_core = IntPtr.Zero;
                Logger.Error(ex);
            }
        }

        private static string FindRomPath()
        {
            string appFolder = Utils.GetAppFolder();
            foreach (string name in RomNames)
            {
                string direct = Path.Combine(appFolder, name);
                if (File.Exists(direct))
                    return direct;
                string inRoms = Path.Combine(appFolder, "roms", name);
                if (File.Exists(inRoms))
                    return inRoms;
            }
            return string.Empty;
        }

        private void ReleaseCore()
        {
            if (m_core == IntPtr.Zero)
                return;
            NativeMethods.Destroy(m_core);
            m_core = IntPtr.Zero;
        }

        private void ResetChip()
        {
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Reset(m_core);
        }

        private void WriteWavePort(ushort address, byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable)
                return;
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Write(m_core,
                    (uint)((address & 1) == 0 ? 4 : 5), value);
            handled = true;
        }

        private void ReadWavePort(ushort address, ref byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable)
                return;
            RenderToCurrentTime();
            value = m_core != IntPtr.Zero && (address & 1) != 0
                ? NativeMethods.Read(m_core, 5)
                : (byte)0xFF;
            handled = true;
        }

        private void WriteFmPort(ushort address, byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable)
                return;
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Write(m_core, (uint)(address & 3), value);
            handled = true;
        }

        private void ReadFmPort(ushort address, ref byte value,
            ref bool handled)
        {
            if (!AreExpansionPortsAvailable)
                return;
            RenderToCurrentTime();
            // Both even ports expose the common YMF278B status.  OPL3 data
            // ports are write-only on the physical chip.
            value = m_core != IntPtr.Zero && (address & 1) == 0
                ? NativeMethods.Read(m_core, 0)
                : (byte)0xFF;
            handled = true;
        }

        /// <summary>
        /// Rev.01 CPLD signal ENDOS disconnects both MoonSound port groups
        /// while the PentEvo TR-DOS ports are enabled.  A non-PentEvo host
        /// has no such motherboard gate, so retain the board's own decoder.
        /// </summary>
        private bool AreExpansionPortsAvailable
        {
            get { return m_hostMemory == null || !m_hostMemory.DOSEN; }
        }

        private void RenderToCurrentTime()
        {
            if (!m_rendering)
                return;
            double time = Math.Max(0D, Math.Min(1D, GetFrameTime()));
            RenderTo((int)(time * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples, target));
            int count = target - m_renderSample;
            if (count <= 0)
                return;

            if (m_core == IntPtr.Zero)
            {
                while (m_renderSample < target)
                {
                    UpdateDac((double)m_renderSample / m_frameSamples, 0, 0);
                    m_renderSample++;
                }
                return;
            }

            NativeMethods.Generate(m_core, m_nativeBuffer, (uint)count);
            for (int index = 0; index < count; ++index)
            {
                UpdateDac((double)m_renderSample / m_frameSamples,
                    m_nativeBuffer[index * 2],
                    m_nativeBuffer[index * 2 + 1]);
                m_renderSample++;
            }
        }

        private static class NativeMethods
        {
            private const string LibraryName = "ymfm_opl4.dll";

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_create")]
            internal static extern IntPtr Create(
                [In] byte[] rom, uint romSize, uint ramSize);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_destroy")]
            internal static extern void Destroy(IntPtr instance);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_reset")]
            internal static extern void Reset(IntPtr instance);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_read")]
            internal static extern byte Read(IntPtr instance, uint offset);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_write")]
            internal static extern void Write(
                IntPtr instance, uint offset, byte value);

            [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "ymfm_opl4_generate")]
            internal static extern void Generate(
                IntPtr instance, [Out] short[] output, uint samples);
        }
    }
}
