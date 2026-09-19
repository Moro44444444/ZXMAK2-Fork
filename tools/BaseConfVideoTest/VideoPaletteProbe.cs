using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class VideoPaletteProbeB23
{
    private const BindingFlags AllInstance = BindingFlags.Instance |
        BindingFlags.Public | BindingFlags.NonPublic;

    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static FieldInfo FindField(Type type, string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(name, AllInstance);
            if (field != null) return field;
            type = type.BaseType;
        }
        throw new MissingFieldException(name);
    }

    private static uint ExpectedPentEvoColor(byte raw)
    {
        byte atm = (byte)((raw & 3) | ((raw >> 2) & 0xFC));
        uint value = (uint)(atm ^ 0xFF);
        uint packed =
            ((value & 0x20) << 1) |
            ((value & 0x10) >> 1) |
            ((value & 0x08) >> 3) |
            ((value & 0x04) << 5) |
            ((value & 0x02) << 3) |
            ((value & 0x01) << 1);
        uint green = ((packed >> 6) & 3) * 85;
        uint red = ((packed >> 3) & 3) * 85;
        uint blue = (packed & 3) * 85;
        return 0xFF000000U | (red << 16) | (green << 8) | blue;
    }

    public static int Main(string[] args)
    {
        try
        {
            Check(args.Length == 1, "Expected release directory argument.");
            string release = Path.GetFullPath(args[0]);
            Directory.SetCurrentDirectory(release);
            AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e)
            {
                string file = Path.Combine(release, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };

            Assembly hardware = Assembly.LoadFrom(Path.Combine(release, "ZXMAK2.Hardware.dll"));
            Type ulaType = hardware.GetType("ZXMAK2.Hardware.Atm.UlaAtm450", true);
            object ula = Activator.CreateInstance(ulaType);
            ulaType.GetMethod("busReset", AllInstance).Invoke(ula, null);

            string[] rendererFields = {
                "SpectrumRenderer", "Atm320Renderer", "Atm640Renderer",
                "AtmTxtRenderer", "EvoTxtRenderer", "EvoHwmRenderer",
                "EvoA16Renderer"
            };
            string[] cacheFields = {
                "m_ulaInk", "m_ink0", "m_ink", "m_ink",
                "m_ink", "m_ulaInk", "m_ink0"
            };
            var renderers = new List<object>();
            var before = new List<uint[]>();
            for (int i = 0; i < rendererFields.Length; i++)
            {
                object renderer = FindField(ulaType, rendererFields[i]).GetValue(ula);
                renderers.Add(renderer);
                uint[] cache = (uint[])FindField(renderer.GetType(), cacheFields[i]).GetValue(renderer);
                before.Add((uint[])cache.Clone());
            }

            MethodInfo writePalette = ulaType.GetMethod("SetPaletteAtm2");
            writePalette.Invoke(ula, new object[] { (byte)0 });
            for (int i = 0; i < renderers.Count; i++)
            {
                object renderer = renderers[i];
                uint[] palette = (uint[])renderer.GetType().GetProperty("Palette").GetValue(renderer, null);
                Check(palette[0] == 0xFFFFFFFFU,
                    rendererFields[i] + " did not receive PentEvo raw color 00.");
                uint[] cache = (uint[])FindField(renderer.GetType(), cacheFields[i]).GetValue(renderer);
                bool changed = false;
                for (int j = 0; j < cache.Length; j++)
                    if (cache[j] != before[i][j]) { changed = true; break; }
                Check(changed, rendererFields[i] + " retained a stale decoded palette cache.");
            }

            for (int raw = 0; raw < 256; raw++)
            {
                writePalette.Invoke(ula, new object[] { (byte)raw });
                uint expected = ExpectedPentEvoColor((byte)raw);
                for (int i = 0; i < renderers.Count; i++)
                {
                    uint[] palette = (uint[])renderers[i].GetType().GetProperty("Palette")
                        .GetValue(renderers[i], null);
                    Check(palette[0] == expected,
                        rendererFields[i] + " mapping mismatch for raw " + raw.ToString("X2") + ".");
                }
            }

            Console.WriteLine("PASS: " + checks +
                " B23 palette checks: all 256 PentEvo values and decoded caches " +
                "of all seven BaseConf render paths.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("B23 PALETTE PROBE FAILED: " + e);
            return 1;
        }
    }
}
