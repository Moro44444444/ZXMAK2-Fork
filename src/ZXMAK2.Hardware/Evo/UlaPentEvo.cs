using System;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Attributes;
using ZXMAK2.Hardware.Atm;


namespace ZXMAK2.Hardware.Evo
{
    public enum PentEvoInternalSound
    {
        None,
        AY8910CHRV,
        TurboSoundFmPro,
    }

    public enum PentEvoZxBusDevice
    {
        Empty,
        NeoGS,
        MultiSound,
        MoonSound,
        ZXNetUSB,
    }

    public class UlaPentEvo : UlaAtm450, IUlaFrameTiming
    {
        private const int FrameInterruptMasterClocks = 256;
        private int m_requestedRaster;
        private int m_activeRaster;
        private long m_rasterOrigin;
        private bool m_frameInterruptAcknowledged;
        private int m_frameInterruptWaitClocks;
        private readonly BaseConfVideoModeController m_videoController =
            new BaseConfVideoModeController();
        private bool m_videoMappingValid;
        private int m_pendingVideoPage;
        private int m_pendingPage0000;
        private int m_pendingPage4000;
        private int m_pendingPage8000;
        private int m_pendingPageC000;

        [HardwareValue("RASTER", Description = "B17 active steady raster; immediate RTL transition/INT/contention pending")]
        public int ActiveRasterMode { get { return m_activeRaster; } }
        [HardwareValue("RASTERREQ", Description = "AVR video raster request; software boundary activation")]
        public int RequestedRasterMode { get { return m_requestedRaster; } }
        [HardwareValue("RASTERORG", Description = "Absolute master-tact frame origin")]
        public long RasterFrameOrigin { get { return m_rasterOrigin; } }
        [HardwareValue("VIDEORAW", Description = "B21 requested raw BaseConf video selector")]
        public int RequestedVideoSelector { get { return m_videoController.RequestedRaw; } }
        [HardwareValue("VIDEOACT", Description = "B21 active normalized BaseConf video selector")]
        public int ActiveVideoSelector { get { return (int)m_videoController.Active.Mode; } }
        [HardwareValue("VIDEOPEND", Description = "B40 decoded BaseConf video mode differs from active renderer")]
        public bool VideoModePending { get { return m_videoController.IsPending; } }
        internal EvoRasterTiming ActiveRaster { get { return EvoRasterTiming.ForMode(m_activeRaster); } }

        public PentEvoInternalSound InternalSound { get; set; }
        public int InternalSoundVolume { get; set; }
        public bool ZxBusSlot1Enabled { get; set; }
        public PentEvoZxBusDevice ZxBusSlot1Device { get; set; }
        public bool ZxBusSlot2Enabled { get; set; }
        public PentEvoZxBusDevice ZxBusSlot2Device { get; set; }

        [HardwareValue("INTACK", Description = "BaseConf frame INT released by interrupt acknowledge")]
        public bool FrameInterruptAcknowledged { get { return m_frameInterruptAcknowledged; } }

        [HardwareValue("INTWAIT", Description = "Master clocks added while the external WAIT line holds frame INT")]
        public int FrameInterruptWaitClocks { get { return m_frameInterruptWaitClocks; } }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            // The device list is the executable configuration. Keep the
            // descriptive PENTEVO setting synchronized when an older machine
            // profile does not contain the new XML attribute yet.
            if (bmgr.FindDevice<TurboSoundFmPro>() != null)
                InternalSound = PentEvoInternalSound.TurboSoundFmPro;
            else if (bmgr.FindDevice<AYCHRV>() != null)
                InternalSound = PentEvoInternalSound.AY8910CHRV;
            else
                InternalSound = PentEvoInternalSound.None;
            bmgr.Events.SubscribeIntAck(BusIntAcknowledge);
        }

        internal void RequestRaster(int mode)
        {
            EvoRasterTiming.ForMode(mode); // validate without changing active timing
            m_requestedRaster = mode;
        }

        public int GetFrameTact(long masterTact)
        {
            if (masterTact < 0) throw new ArgumentOutOfRangeException("masterTact");
            long elapsed = masterTact >= m_rasterOrigin ? masterTact - m_rasterOrigin : masterTact;
            return (int)(elapsed % ActiveRaster.FrameMasterTacts);
        }

        public bool IsFrameComplete(long masterTact)
        {
            return masterTact < m_rasterOrigin ||
                masterTact - m_rasterOrigin >= ActiveRaster.FrameMasterTacts;
        }

        public void BeginFrameTiming(long masterTact)
        {
            if (masterTact < 0) throw new ArgumentOutOfRangeException("masterTact");
            int oldPeriod = ActiveRaster.FrameMasterTacts;
            if (masterTact < m_rasterOrigin)
                m_rasterOrigin = masterTact - masterTact % oldPeriod;
            else
                m_rasterOrigin += (masterTact - m_rasterOrigin) / oldPeriod * oldPeriod;
            // Explicit B17 approximation: commit at a software frame boundary,
            // not the immediate FPGA register/hsync transition in RTL r1364.
            if (m_activeRaster != m_requestedRaster)
            {
                m_activeRaster = m_requestedRaster;
                ApplyActiveRaster();
            }
            // B21 deterministic policy: a picture-format write is decoded
            // immediately, but the renderer route changes only here, at the
            // same software frame boundary used by the raster controller.
            if (m_videoController.Commit() && m_videoMappingValid)
            {
                base.SetPageMappingAtm(
                    m_videoController.Active.Mode,
                    m_pendingVideoPage,
                    m_pendingPage0000,
                    m_pendingPage4000,
                    m_pendingPage8000,
                    m_pendingPageC000);
            }
            var memory = Memory as MemoryPentEvo;
            if (memory != null)
                memory.ActivateRaster(ActiveRaster, m_rasterOrigin);

            // zint.v starts a fresh active-low INT pulse at int_start.
            // Acknowledge and WAIT extension belong to this frame only.
            m_frameInterruptAcknowledged = false;
            m_frameInterruptWaitClocks = 0;
        }

        public override bool CheckInt(int frameTact)
        {
            return !m_frameInterruptAcknowledged &&
                frameTact < FrameInterruptMasterClocks + m_frameInterruptWaitClocks;
        }

        /// <summary>
        /// Records time for which the physical external WAIT line is active.
        /// In BaseConf zint.v this pauses the 256-clock INT counter.  DRAM and
        /// ordinary turbo stalls do not call this method; the AVR/COM WAIT
        /// transactions added by the following stage will do so.
        /// </summary>
        public void PauseFrameInterruptForWait(int masterClocks)
        {
            if (masterClocks < 0)
                throw new ArgumentOutOfRangeException("masterClocks");
            if (masterClocks == 0 || CPU == null)
                return;
            var frameTact = GetFrameTact(CPU.Tact);
            if (CheckInt(frameTact))
                m_frameInterruptWaitClocks = checked(
                    m_frameInterruptWaitClocks + masterClocks);
        }

        private void BusIntAcknowledge()
        {
            // EventManager invokes this at the CPU interrupt-acknowledge M1.
            // This is the software event corresponding to !IORQ && !M1 at
            // zneg in zint.v and releases INT before its nominal timeout.
            m_frameInterruptAcknowledged = true;
        }

        protected override int GetCurrentFrameTact()
        {
            return GetFrameTact(CPU.Tact);
        }

        private void ApplyActiveRaster()
        {
            ApplyRendererTiming(ActiveRaster);
            // Restore the current border through the inherited full-width path.
            PortFE = PortFE;
        }

        private void ApplyRendererTiming(EvoRasterTiming raster)
        {
            var standard = CreateSpectrumRendererParams();
            ConfigureStandardTiming(standard, raster);
            SpectrumRenderer.Params = standard;
            EvoHwmRenderer.Params = standard;
            EvoA16Renderer.Params = standard;

            var ega = Atm320Renderer.CreateParams();
            ConfigureWideTiming(ega, raster);
            Atm320Renderer.Params = ega;
            var hwm = Atm640Renderer.CreateParams();
            ConfigureWideTiming(hwm, raster);
            Atm640Renderer.Params = hwm;
            var text = AtmTxtRenderer.CreateParams();
            ConfigureWideTiming(text, raster);
            AtmTxtRenderer.Params = text;
            EvoTxtRenderer.Params = text;
        }

        private static void ConfigureStandardTiming(
            SpectrumRendererParams timing,
            EvoRasterTiming raster)
        {
            timing.c_frameTactCount = raster.FrameBaseTacts;
            timing.c_ulaLineTime = raster.LineBaseTacts;
            timing.c_ulaFirstPaperLine = raster.StandardFirstLine;
            timing.c_ulaFirstPaperTact = raster.StandardFirstTact;
            timing.c_ulaBorderTop = 32;
            timing.c_ulaBorderBottom = Math.Min(32,
                raster.Lines - raster.StandardFirstLine - 192);
            timing.c_ulaIntBegin = raster.IntBaseTact;
            timing.c_ulaIntLength = EvoRasterTiming.IntLengthBaseTacts;
            timing.c_ulaBorder4T = raster.Border4T;
            timing.c_ulaBorder4Tstage = raster.Border4TStage;
            timing.c_ulaWidth = (timing.c_ulaBorderLeftT + 128 +
                timing.c_ulaBorderRightT) * 2;
            timing.c_ulaHeight = timing.c_ulaBorderTop + 192 +
                timing.c_ulaBorderBottom;
        }

        private static void ConfigureWideTiming(
            Atm320RendererParams timing,
            EvoRasterTiming raster)
        {
            timing.c_frameTactCount = raster.FrameBaseTacts;
            timing.c_ulaLineTime = raster.LineBaseTacts;
            timing.c_ulaFirstPaperLine = raster.WideFirstLine;
            timing.c_ulaFirstPaperTact = raster.WideFirstTact;
            timing.c_ulaBorderTop = 28;
            timing.c_ulaBorderBottom = Math.Min(28,
                raster.Lines - raster.WideFirstLine - 200);
            timing.c_ulaIntBegin = raster.IntBaseTact;
            timing.c_ulaIntLength = EvoRasterTiming.IntLengthBaseTacts;
            timing.c_ulaBorder4T = raster.Border4T;
            timing.c_ulaBorder4Tstage = raster.Border4TStage;
            timing.c_ulaHeight = timing.c_ulaBorderTop + 200 +
                timing.c_ulaBorderBottom;
        }

        private static void ConfigureWideTiming(
            Atm640RendererParams timing,
            EvoRasterTiming raster)
        {
            timing.c_frameTactCount = raster.FrameBaseTacts;
            timing.c_ulaLineTime = raster.LineBaseTacts;
            timing.c_ulaFirstPaperLine = raster.WideFirstLine;
            timing.c_ulaFirstPaperTact = raster.WideFirstTact;
            timing.c_ulaBorderTop = 28;
            timing.c_ulaBorderBottom = Math.Min(28,
                raster.Lines - raster.WideFirstLine - 200);
            timing.c_ulaIntBegin = raster.IntBaseTact;
            timing.c_ulaIntLength = EvoRasterTiming.IntLengthBaseTacts;
            timing.c_ulaBorder4T = raster.Border4T;
            timing.c_ulaBorder4Tstage = raster.Border4TStage;
            timing.c_ulaHeight = timing.c_ulaBorderTop + 200 +
                timing.c_ulaBorderBottom;
        }

        private static void ConfigureWideTiming(
            AtmTxtRendererParams timing,
            EvoRasterTiming raster)
        {
            timing.c_frameTactCount = raster.FrameBaseTacts;
            timing.c_ulaLineTime = raster.LineBaseTacts;
            timing.c_ulaFirstPaperLine = raster.WideFirstLine;
            timing.c_ulaFirstPaperTact = raster.WideFirstTact;
            timing.c_ulaBorderTop = 28;
            timing.c_ulaBorderBottom = Math.Min(28,
                raster.Lines - raster.WideFirstLine - 200);
            timing.c_ulaIntBegin = raster.IntBaseTact;
            timing.c_ulaIntLength = EvoRasterTiming.IntLengthBaseTacts;
            timing.c_ulaBorder4T = raster.Border4T;
            timing.c_ulaBorder4Tstage = raster.Border4TStage;
            timing.c_ulaHeight = timing.c_ulaBorderTop + 200 +
                timing.c_ulaBorderBottom;
        }

        public UlaPentEvo()
        {
            Name = "PENTEVO";
            Description = "ZX Evolution BaseConf motherboard";
            InternalSound = PentEvoInternalSound.AY8910CHRV;
            InternalSoundVolume = 100;
            ZxBusSlot1Enabled = false;
            ZxBusSlot1Device = PentEvoZxBusDevice.Empty;
            ZxBusSlot2Enabled = false;
            ZxBusSlot2Device = PentEvoZxBusDevice.Empty;
        }

        protected override void OnConfigLoad(XmlNode itemNode)
        {
            base.OnConfigLoad(itemNode);
            InternalSound = Utils.GetXmlAttributeAsEnum(
                itemNode,
                "internalSound",
                InternalSound);
            InternalSoundVolume = Utils.GetXmlAttributeAsInt32(
                itemNode,
                "internalSoundVolume",
                InternalSoundVolume);
            ZxBusSlot1Enabled = Utils.GetXmlAttributeAsBool(
                itemNode,
                "zxBusSlot1Enabled",
                ZxBusSlot1Enabled);
            ZxBusSlot1Device = Utils.GetXmlAttributeAsEnum(
                itemNode,
                "zxBusSlot1Device",
                ZxBusSlot1Device);
            ZxBusSlot2Enabled = Utils.GetXmlAttributeAsBool(
                itemNode,
                "zxBusSlot2Enabled",
                ZxBusSlot2Enabled);
            ZxBusSlot2Device = Utils.GetXmlAttributeAsEnum(
                itemNode,
                "zxBusSlot2Device",
                ZxBusSlot2Device);
        }

        protected override void OnConfigSave(XmlNode itemNode)
        {
            base.OnConfigSave(itemNode);
            Utils.SetXmlAttributeAsEnum(itemNode, "internalSound", InternalSound);
            Utils.SetXmlAttribute(
                itemNode, "internalSoundVolume", InternalSoundVolume);
            Utils.SetXmlAttribute(itemNode, "zxBusSlot1Enabled", ZxBusSlot1Enabled);
            Utils.SetXmlAttributeAsEnum(itemNode, "zxBusSlot1Device", ZxBusSlot1Device);
            Utils.SetXmlAttribute(itemNode, "zxBusSlot2Enabled", ZxBusSlot2Enabled);
            Utils.SetXmlAttributeAsEnum(itemNode, "zxBusSlot2Device", ZxBusSlot2Device);
        }

        /// <summary>
        /// Writes the BaseConf 4:4:4 palette extension selected by #BF.D5.
        /// The upper two bits of every component keep the ordinary ATM2
        /// active-low data encoding; the lower two bits come from the I/O
        /// address exactly as in video_palframe.v.
        /// </summary>
        public void SetPaletteBaseConf444(ushort addr, byte value)
        {
            // Preserve the already rendered part of the frame before the
            // visible palette RAM changes.  The accepted D5=0 path remains
            // in UlaAtm450.SetPaletteAtm2 and is not routed through here.
            UpdateState(GetCurrentFrameTact());

            var index = ReadConfigBorder() & 0x0F;

            // Keep the inherited six-bit latch synchronized with the upper
            // pairs.  This is what #0DBD/#0DBE exposes again after D5 clears.
            SetPaletteAtm2(value);

            var red = (ActiveLowPair(value, 1, 6) << 2) |
                ActiveLowPair(addr, 9, 14);
            var green = (ActiveLowPair(value, 4, 7) << 2) |
                ActiveLowPair(addr, 12, 15);
            var blue = (ActiveLowPair(value, 0, 5) << 2) |
                ActiveLowPair(addr, 8, 13);
            var color = 0xFF000000U |
                ((uint)(red * 17) << 16) |
                ((uint)(green * 17) << 8) |
                (uint)(blue * 17);

            SpectrumRenderer.UpdatePalette(index, color);
            Atm320Renderer.UpdatePalette(index, color);
            Atm640Renderer.UpdatePalette(index, color);
            AtmTxtRenderer.UpdatePalette(index, color);
            EvoTxtRenderer.UpdatePalette(index, color);
            EvoHwmRenderer.UpdatePalette(index, color);
            EvoA16Renderer.UpdatePalette(index, color);
        }

        /// <summary>
        /// Writes the ordinary six-bit BaseConf/ATM palette after completing
        /// the part of the scan already produced with the previous color.
        /// video_palframe.v writes its palette RAM on the live 28 MHz clock;
        /// it is not a frame-boundary operation.
        /// </summary>
        public void SetPaletteBaseConf6(byte value)
        {
            UpdateState(GetCurrentFrameTact());
            SetPaletteAtm2(value);
        }

        /// <summary>
        /// Returns the official BD_COLORRD layout for the lower component
        /// pairs selected by #BF.D5.  Toggling D5 never rewrites palette RAM.
        /// </summary>
        public byte ReadConfigPaletteBaseConf444()
        {
            var index = ReadConfigBorder() & 0x0F;
            var color = SpectrumRenderer.Palette[index];
            var red = ((int)(color >> 16) & 0xFF) / 17;
            var green = ((int)(color >> 8) & 0xFF) / 17;
            var blue = ((int)color & 0xFF) / 17;

            // zports.v: { ~palcolor[4], ~palcolor[2], ~palcolor[0],
            //             ~palcolor[5], 2'b11,
            //             ~palcolor[3], ~palcolor[1] }
            // where palcolor is { G[1:0], R[1:0], B[1:0] } in 4:4:4 mode.
            return (byte)(
                (((~green) & 0x01) << 7) |
                (((~red) & 0x01) << 6) |
                (((~blue) & 0x01) << 5) |
                (((~green >> 1) & 0x01) << 4) |
                0x0C |
                (((~red >> 1) & 0x01) << 1) |
                ((~blue >> 1) & 0x01));
        }

        private static int ActiveLowPair(int source, int highBit, int lowBit)
        {
            return (((source >> highBit) & 1) ^ 1) << 1 |
                (((source >> lowBit) & 1) ^ 1);
        }

        protected override void OnRendererInit()
        {
            base.OnRendererInit();
            ApplyRendererTiming(EvoRasterTiming.Normal);
        }

        protected override int FrameTactMultiplier
        {
            // Renderer tacts are 3.5 MHz; CPU/bus master tacts are 28 MHz.
            get { return 8; }
        }

        public override void SetPageMappingAtm(
            AtmVideoMode mode,
            int videoPage,
            int page0000,
            int page4000,
            int page8000,
            int pageC000)
        {
            m_videoController.Request((int)mode);
            m_videoMappingValid = true;
            m_pendingVideoPage = videoPage;
            m_pendingPage0000 = page0000;
            m_pendingPage4000 = page4000;
            m_pendingPage8000 = page8000;
            m_pendingPageC000 = pageC000;

            // video_modedecode.v samples atm_vmode/pent_vmode on every 28 MHz
            // clock.  SetPageMapping() first flushes the old renderer through
            // the current tact, then the decoded route and page pointers become
            // active for the rest of this frame.
            m_videoController.Commit();
            base.SetPageMappingAtm(
                m_videoController.Active.Mode,
                videoPage,
                page0000,
                page4000,
                page8000,
                pageC000);
        }


        protected override void WritePortFE(ushort addr, byte value, ref bool handled)
        {
            // BaseConf zports.v: only FE/F6/FC strobe the border register.
            // The inherited handler uses inverted A3 for border bit 3.
            var port = addr & 0x00FF;
            if (port != 0x00FE && port != 0x00F6 && port != 0x00FC)
            {
                return;
            }
            base.WritePortFE(addr, value, ref handled);
        }
        

        protected override SpectrumRendererParams CreateSpectrumRendererParams()
        {
            // Pentagon 128K
            // Total Size:          448 x 320
            // Visible Size:        384 x 304 (72+256+56 x 64+192+48)
            var timing = SpectrumRenderer.CreateParams();
            timing.c_frameTactCount = EvoRasterTiming.Normal.FrameBaseTacts;
            timing.c_ulaLineTime = EvoRasterTiming.Normal.LineBaseTacts;
            timing.c_ulaFirstPaperLine = EvoRasterTiming.Normal.StandardFirstLine;
            timing.c_ulaFirstPaperTact = EvoRasterTiming.Normal.StandardFirstTact;
            timing.c_ulaBorder4T = false;

            timing.c_ulaBorderTop = 32;
            timing.c_ulaBorderBottom = 32;
            timing.c_ulaBorderLeftT = 16;
            timing.c_ulaBorderRightT = 16;

            timing.c_ulaIntBegin = EvoRasterTiming.Normal.IntBaseTact;
            timing.c_ulaIntLength = EvoRasterTiming.IntLengthBaseTacts;
            timing.c_ulaFlashPeriod = 25;   // TODO: check?

            timing.c_ulaWidth = (timing.c_ulaBorderLeftT + 128 + timing.c_ulaBorderRightT) * 2;
            timing.c_ulaHeight = (timing.c_ulaBorderTop + 192 + timing.c_ulaBorderBottom);
            return timing;
        }
    }

    internal sealed class BaseConfVideoMode
    {
        public readonly AtmVideoMode Mode;
        public readonly int AtmSelector;
        public readonly int PentagonSelector;
        public readonly string Name;

        public BaseConfVideoMode(
            AtmVideoMode mode,
            int atmSelector,
            int pentagonSelector,
            string name)
        {
            Mode = mode;
            AtmSelector = atmSelector;
            PentagonSelector = pentagonSelector;
            Name = name;
        }
    }

    internal sealed class BaseConfVideoModeController
    {
        private static readonly BaseConfVideoMode Atm320 = new BaseConfVideoMode(
            AtmVideoMode.Ega320x200, 0, -1, "ATM 320x200 16c");
        private static readonly BaseConfVideoMode Atm640 = new BaseConfVideoMode(
            AtmVideoMode.Hwm640x200, 2, -1, "ATM 640x200 hardware multicolor");
        private static readonly BaseConfVideoMode Spectrum = new BaseConfVideoMode(
            AtmVideoMode.Std256x192, 3, 0, "ZX 256x192 attributes");
        private static readonly BaseConfVideoMode Pentagon16 = new BaseConfVideoMode(
            AtmVideoMode.EvoAlco16c, 3, 1, "Pentagon 256x192 16c");
        private static readonly BaseConfVideoMode PentagonHwm = new BaseConfVideoMode(
            AtmVideoMode.Evo256x192, 3, 2, "Pentagon 256x192 hardware multicolor");
        private static readonly BaseConfVideoMode AtmText = new BaseConfVideoMode(
            AtmVideoMode.Txt080x025, 6, -1, "ATM 80x25 text");
        private static readonly BaseConfVideoMode BaseConfText = new BaseConfVideoMode(
            AtmVideoMode.EvoText080, 7, -1, "BaseConf 80x25 one-page text");

        private BaseConfVideoMode m_requested = Spectrum;
        private BaseConfVideoMode m_active = Spectrum;
        private int m_requestedRaw = (int)AtmVideoMode.Std256x192;

        public BaseConfVideoMode Active { get { return m_active; } }
        public int RequestedRaw { get { return m_requestedRaw; } }
        public bool IsPending { get { return !Object.ReferenceEquals(m_requested, m_active); } }

        public void Request(int rawSelector)
        {
            m_requestedRaw = rawSelector;
            m_requested = Decode(rawSelector);
        }

        public bool Commit()
        {
            if (!IsPending)
                return false;
            m_active = m_requested;
            return true;
        }

        public static BaseConfVideoMode Decode(int rawSelector)
        {
            int atm = rawSelector & 7;
            int pentagon = (rawSelector >> 3) & 3;
            switch (atm)
            {
                // The Pentagon selector is ignored by RTL in wide modes.
                case 0: return Atm320;
                case 2: return Atm640;
                case 6: return AtmText;
                case 7: return BaseConfText;
                case 3:
                    switch (pentagon)
                    {
                        case 1: return Pentagon16;
                        case 2: return PentagonHwm;
                        // Pentagon code 3 and code 0 both fall back to ZX.
                        default: return Spectrum;
                    }
                // Undefined ATM selectors 1, 4 and 5 fall back to ZX.
                default: return Spectrum;
            }
        }
    }

    // BaseConf RTL r1364: steady raster geometry in 7 MHz DRAM cycles.
    // This table is not an AVR controller, INT waveform, or contend emulator.
    internal sealed class EvoRasterTiming
    {
        public readonly int Mode, LineCycles, Lines, WideFirstLine, StandardFirstLine;
        public readonly int IntLine, IntHorizontal;
        public const int WideFirstCycle = 108;
        public const int StandardFirstCycle = 140;
        // video_sync_h.v gives the raw 7 MHz hpix edge.  SpectrumRenderer's
        // 256x192 action table uses the established ZXMAK2 output phase, which
        // is four 3.5 MHz tacts earlier than that raw gate.  Keep this adapter
        // explicit: it is shared by ZX, Pentagon HWM and Pentagon 16c only.
        public const int StandardRendererPhaseBaseTacts = 4;
        public const int IntLengthBaseTacts = 32;
        private static readonly EvoRasterTiming[] Profiles = {
            new EvoRasterTiming(0, 448, 320, 76, 0, 2),
            new EvoRasterTiming(1, 448, 262, 42, 0, 2),
            new EvoRasterTiming(2, 448, 312, 60, 1, 126),
            new EvoRasterTiming(3, 456, 311, 59, 1, 130)
        };

        private EvoRasterTiming(int mode, int lineCycles, int lines, int wideFirstLine,
            int intLine, int intHorizontal)
        {
            Mode = mode; LineCycles = lineCycles; Lines = lines;
            WideFirstLine = wideFirstLine; StandardFirstLine = wideFirstLine + 4;
            IntLine = intLine; IntHorizontal = intHorizontal;
        }

        public static EvoRasterTiming Normal { get { return Profiles[0]; } }
        public static EvoRasterTiming ForMode(int mode)
        {
            if (mode < 0 || mode > 3)
                throw new System.ArgumentOutOfRangeException("mode");
            return Profiles[mode];
        }
        public int FrameCycles { get { return LineCycles * Lines; } }
        public int FrameBaseTacts { get { return FrameCycles / 2; } }
        public int FrameMasterTacts { get { return FrameCycles * 4; } }
        public int LineBaseTacts { get { return LineCycles / 2; } }
        public int WideFirstTact { get { return WideFirstCycle / 2; } }
        public int StandardFirstTact
        {
            get
            {
                return StandardFirstCycle / 2 - StandardRendererPhaseBaseTacts;
            }
        }
        public int IntCycle { get { return IntLine * LineCycles + IntHorizontal; } }
        public int IntBaseTact { get { return IntCycle / 2; } }
        public bool Border4T { get { return (Mode & 2) != 0; } }
        public int Border4TStage
        {
            get { return (2 - (IntBaseTact & 3) + 4) & 3; }
        }

        public bool IsContentionActive(long masterTact, long masterOrigin,
            int videoMode)
        {
            if (masterTact < 0)
                throw new System.ArgumentOutOfRangeException("masterTact");
            if (masterOrigin < 0 || (masterOrigin & 3) != 0)
                throw new System.ArgumentException("Invalid BaseConf raster origin.");
            if ((Mode & 2) == 0)
                return false;

            long relative = (masterTact - masterOrigin) / 4;
            long position = (relative + IntCycle) % FrameCycles;
            if (position < 0)
                position += FrameCycles;
            int line = (int)(position / LineCycles);
            int horizontal = (int)(position % LineCycles);
            bool wide = videoMode == 0 || videoMode == 2 ||
                videoMode == 6 || videoMode == 7;
            int first = wide ? WideFirstLine : StandardFirstLine;
            int height = wide ? 200 : 192;
            if (line < first || line >= first + height)
                return false;

            // video_sync_h.v loads contend_ctr with zero after hcount 127.
            // Its bit 8 terminates the 256-cycle window.  The official 48K
            // pattern holds six pairs of 7 MHz cycles and releases two.
            int counter = horizontal - 128;
            return counter >= 0 && counter < 256 &&
                (((counter >> 1) & 7) < 6);
        }

        public void GetVideoFetch(long cycle, int videoMode, int pentMode,
            out bool go, out int bandwidth)
        {
            if (cycle < 0)
                throw new System.ArgumentOutOfRangeException("cycle");
            long position = (cycle + IntCycle) % FrameCycles;
            int line = (int)(position / LineCycles);
            int horizontal = (int)(position % LineCycles);
            bool wide = videoMode == 0 || videoMode == 2 || videoMode == 6 || videoMode == 7;
            bool text = videoMode == 6 || videoMode == 7;
            bandwidth = videoMode == 3 && pentMode != 2 ? 0 : 1;
            // Preserve B12/B14 callback convention: go visible one cend later.
            // This is not a claim of full pin-edge or hsync/vcount calibration.
            int start = wide ? (text ? 87 : 91) : 123;
            int end = wide ? 411 : 379;
            int first = wide ? WideFirstLine : StandardFirstLine;
            go = line >= first && line < first + (wide ? 200 : 192) &&
                horizontal >= start && horizontal < end;
        }
    }

}
