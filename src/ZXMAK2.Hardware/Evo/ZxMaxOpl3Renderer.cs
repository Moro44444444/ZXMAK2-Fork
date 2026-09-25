using System;
using System.Runtime.InteropServices;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// Max's YMF262 register interface at #C4-#C7. The existing BSD-licensed
    /// ymfm YMF278B core contains its YMF262 FM engine; with PCM untouched it
    /// runs in OPL3-compatible mode. The ymfm YMF278B core already performs
    /// the extra FM clocks needed to match the YMF262 pitch at 44.1 kHz.
    /// </summary>
    internal sealed class ZxMaxOpl3Renderer : SoundDeviceBase
    {
        private static readonly byte[] EmptyRom = { 0xFF };
        private IntPtr m_core;
        private short[] m_nativeBuffer = new short[0];
        private int m_frameSamples;
        private int m_renderSample;
        private bool m_rendering;
        private bool m_portsAvailable;

        public ZxMaxOpl3Renderer()
        {
            Name = "ZX-MultiSound Max YMF262 OPL3";
            Category = BusDeviceCategory.Music;
        }

        public override void BusInit(IBusManager bmgr)
        {
            base.BusInit(bmgr);
            // Both MoonSound and Max decode #C4-#C7. Automatic protection
            // gives the existing MoonSound profile priority at these ports.
            m_portsAvailable = bmgr.FindDevice<ZxmMoonSoundDevice>() == null;
            if (m_portsAvailable)
            {
                bmgr.Events.SubscribeWrIo(0x00FC, 0x00C4, WritePort);
                bmgr.Events.SubscribeRdIo(0x00FC, 0x00C4, ReadPort);
            }
            m_core = NativeMethods.Create(EmptyRom, 1, 0);
            if (m_core == IntPtr.Zero)
                Logger.Error("ZX-MultiSound Max: ymfm OPL3 core unavailable");
            else
                NativeMethods.Read(m_core, 0); // Consume YMF278B-only ID read.
            bmgr.Events.SubscribeReset(ResetChip);
        }

        public override void BusDisconnect()
        {
            m_rendering = false;
            if (m_core != IntPtr.Zero)
            {
                NativeMethods.Destroy(m_core);
                m_core = IntPtr.Zero;
            }
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
            m_renderSample = 0;
            m_rendering = true;
        }

        protected override void OnEndFrame()
        {
            RenderTo(m_frameSamples);
            m_rendering = false;
            base.OnEndFrame();
        }

        private void ResetChip()
        {
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
            {
                NativeMethods.Reset(m_core);
                NativeMethods.Read(m_core, 0);
            }
        }

        private void WritePort(ushort address, byte value, ref bool handled)
        {
            if (!m_portsAvailable)
                return;
            RenderToCurrentTime();
            if (m_core != IntPtr.Zero)
                NativeMethods.Write(m_core, (uint)(address & 3), value);
            handled = true;
        }

        private void ReadPort(ushort address, ref byte value,
            ref bool handled)
        {
            if (!m_portsAvailable || handled)
                return;
            RenderToCurrentTime();
            value = m_core != IntPtr.Zero && (address & 1) == 0
                ? NativeMethods.Read(m_core, 0) : (byte)0xFF;
            handled = true;
        }

        private void RenderToCurrentTime()
        {
            if (m_rendering)
                RenderTo((int)(Math.Max(0D, Math.Min(1D,
                    GetFrameTime())) * m_frameSamples));
        }

        private void RenderTo(int target)
        {
            target = Math.Max(m_renderSample, Math.Min(m_frameSamples,
                target));
            if (target == m_renderSample)
                return;
            var count = target - m_renderSample;
            if (m_core != IntPtr.Zero)
            {
                var required = count * 2;
                if (m_nativeBuffer.Length < required)
                    m_nativeBuffer = new short[required];
                NativeMethods.Generate(m_core, m_nativeBuffer,
                    (uint)count);
            }
            for (var output = m_renderSample; output < target; output++)
            {
                var source = output - m_renderSample;
                var left = m_core == IntPtr.Zero ? 0 :
                    m_nativeBuffer[source * 2];
                var right = m_core == IntPtr.Zero ? 0 :
                    m_nativeBuffer[source * 2 + 1];
                var gain = Volume / 100D;
                UpdateDac((double)output / m_frameSamples,
                    (short)Math.Max(short.MinValue, Math.Min(short.MaxValue,
                        (int)(left * gain))),
                    (short)Math.Max(short.MinValue, Math.Min(short.MaxValue,
                        (int)(right * gain))));
            }
            m_renderSample = target;
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
