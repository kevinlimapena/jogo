using System;
using System.Collections.Generic;

/// <summary>
/// Camada "eletrônica oriental": bumbo, palmas, chimbais, baixo com sidechain, 808, supersaw,
/// risers e impactos — misturados com darbuka, alaúde, clarinete, mizmar e shofar.
/// Cada faixa é uma música de 32 compassos com forma de verdade, para não repetir tão rápido:
///   0-3 intro (filtro abrindo)  •  4-11 verso  •  12-15 subida  •  16-23 DROP
///   24-27 respiro (flauta + coro)  •  28-31 final
/// Thread-safe (só System.*).
/// </summary>
public static class Electro
{
    const int SR = Synth.SR;

    // ================================================================== instrumentos eletrônicos

    public static void Kick(float[] buf, int at, float vol, bool hard)
    {
        int n = (int)(SR * 0.42f);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            ph += (46f + (hard ? 150f : 110f) * (float)Math.Exp(-t * 32f)) / SR;
            float s = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * (hard ? 7.5f : 6f));
            s = (float)Math.Tanh(s * (hard ? 2.2f : 1.5f));
            if (i < 60) s += (1f - i / 60f) * 0.35f;   // clique do batedor
            Synth.Put(buf, at + i, s * vol, true);
        }
    }

    public static void Hat(float[] buf, int at, float vol, bool open, Random r)
    {
        int n = (int)(SR * (open ? 0.22f : 0.045f));
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float x = (float)(r.NextDouble() * 2 - 1);
            lp += (x - lp) * 0.35f;
            float hp = x - lp;
            float env = (float)Math.Exp(-t * (open ? 14f : 95f));
            Synth.Put(buf, at + i, hp * env * vol, true);
        }
    }

    public static void Clap(float[] buf, int at, float vol, Random r)
    {
        int n = (int)(SR * 0.22f);
        float low = 0f, band = 0f;
        float f = 2f * (float)Math.Sin(Math.PI * 1300f / SR);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float x = (float)(r.NextDouble() * 2 - 1);
            low += f * band; float high = x - low - 0.5f * band; band += f * high;
            // três "batidas" de mão bem juntas e uma cauda
            float env = 0f;
            for (int k = 0; k < 3; k++) { float tk = t - k * 0.011f; if (tk >= 0f) env = Math.Max(env, (float)Math.Exp(-tk * 160f)); }
            env = Math.Max(env, 0.45f * (float)Math.Exp(-t * 18f));
            Synth.Put(buf, at + i, band * env * vol * 1.6f, true);
        }
    }

    public static void Snare(float[] buf, int at, float vol, Random r)
    {
        int n = (int)(SR * 0.18f);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            ph += (185f + 60f * (float)Math.Exp(-t * 40f)) / SR;
            float tone = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 30f);
            float noise = (float)(r.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 22f);
            Synth.Put(buf, at + i, (tone * 0.6f + noise * 0.55f) * vol, true);
        }
    }

    /// 808: seno saturado com deslize de afinação vindo da nota anterior.
    public static void Sub808(float[] buf, int at, float fromHz, float hz, float dur, float vol)
    {
        int n = (int)(SR * dur);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float f = hz + (fromHz - hz) * (float)Math.Exp(-t * 22f);
            ph += f / SR;
            float env = Math.Min(1f, t / 0.004f) * Math.Min(1f, (n - i) / (SR * 0.05f));
            float s = (float)Math.Tanh(Math.Sin(2 * Math.PI * ph) * 1.8f);
            Synth.Put(buf, at + i, s * env * vol, true);
        }
    }

    /// Serra(s) desafinadas com filtro passa-baixa que fecha (supersaw, pluck, reese, pad).
    public static void Saw(float[] buf, int at, float hz, float dur, float vol, int voices, float detune,
                           float cutStart, float cutEnd, float cutSpeed, float attack, float release, float res, Random r)
    {
        var tab = Synth.Cached(20, hz, () => Synth.Table(hz, 40, (k, f) => 1f / k));
        int len = (int)(SR * (dur + release));
        var ph = new double[voices];
        var fr = new float[voices];
        for (int v = 0; v < voices; v++)
        {
            ph[v] = r.NextDouble();
            fr[v] = hz * (1f + (voices == 1 ? 0f : detune * (v / (float)(voices - 1) * 2f - 1f)));
        }
        float norm = 1f / (float)Math.Sqrt(voices);
        float low = 0f, band = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float env = t < attack ? t / attack : 1f;
            if (t > dur) env *= Math.Max(0f, 1f - (t - dur) / release);
            float s = 0f;
            for (int v = 0; v < voices; v++) { ph[v] += fr[v] / SR; s += Synth.Read(tab, ph[v]); }
            s *= norm;
            float fc = cutEnd + (cutStart - cutEnd) * (float)Math.Exp(-t * cutSpeed);
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SR * 0.22f) / SR);
            low += f * band; float high = s - low - res * band; band += f * high;
            Synth.Put(buf, at + i, low * env * vol, true);
        }
    }

    /// Subida de tensão antes do drop: ruído com filtro abrindo + tom subindo.
    public static void Riser(float[] buf, int at, float dur, float vol, Random r)
    {
        int n = (int)(SR * dur);
        float low = 0f, band = 0f;
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float fc = 300f * (float)Math.Pow(30.0, k);
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SR * 0.22f) / SR);
            float x = (float)(r.NextDouble() * 2 - 1);
            low += f * band; float high = x - low - 0.4f * band; band += f * high;
            ph += (180f * (float)Math.Pow(8.0, k)) / SR;
            float s = band * 0.8f + (float)Math.Sin(2 * Math.PI * ph) * 0.25f;
            Synth.Put(buf, at + i, s * k * k * vol, true);
        }
    }

    /// Impacto do drop: bumbo grave + prato de ruído + queda de sub.
    public static void Impact(float[] buf, int at, float vol, Random r)
    {
        Kick(buf, at, vol, true);
        int n = (int)(SR * 1.6f);
        float lp = 0f;
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float x = (float)(r.NextDouble() * 2 - 1);
            lp += (x - lp) * 0.3f;
            float crash = (x - lp) * (float)Math.Exp(-t * 2.8f) * 0.5f;
            ph += (35f + 60f * (float)Math.Exp(-t * 3f)) / SR;
            float sub = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 2.2f) * 0.6f;
            Synth.Put(buf, at + i, (crash + sub) * vol, true);
        }
    }

    public static void Downlifter(float[] buf, int at, float dur, float vol, Random r)
    {
        int n = (int)(SR * dur);
        float low = 0f, band = 0f;
        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float fc = 6000f * (float)Math.Pow(0.03, k);
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SR * 0.22f) / SR);
            float x = (float)(r.NextDouble() * 2 - 1);
            low += f * band; float high = x - low - 0.5f * band; band += f * high;
            Synth.Put(buf, at + i, band * (1f - k) * vol, true);
        }
    }

    /// Passa-baixa variável (abre o filtro na intro).
    static void FilterSweep(float[] buf, int from, int to, float f0, float f1)
    {
        float low = 0f, band = 0f;
        for (int i = from; i < to && i < buf.Length; i++)
        {
            float k = (i - from) / (float)Math.Max(1, to - from);
            float fc = f0 * (float)Math.Pow(f1 / f0, k * k);
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SR * 0.22f) / SR);
            low += f * band; float high = buf[i] - low - 0.7f * band; band += f * high;
            buf[i] = low;
        }
    }

    // ================================================================== a música

    static string Section(int bar)
    {
        int b = bar % 32;
        if (b < 4) return "intro";
        if (b < 12) return "a";
        if (b < 16) return "build";
        if (b < 24) return "drop";
        if (b < 28) return "break";
        return "final";
    }

    public static float[] Render(TrackDef d)
    {
        var r = new Random(d.seed);
        string style = d.electro;
        bool trap = style == "trap", techno = style == "techno", house = style == "house" || style == "fast";
        float stepSec = 60f / d.bpm / 4f;
        int stepS = Math.Max(1, (int)(SR * stepSec));
        int bars = 32;
        int total = bars * 16 * stepS;
        var main = new float[total];
        var pump = new float[total];   // vai receber sidechain do bumbo
        var wet = new float[total];    // instrumentos com reverb
        var intro = new float[total];  // intro filtrada
        var kicks = new List<int>();

        int[] scA = d.scaleA, scB = d.scaleB ?? d.scaleA;
        var melA = Composer.Section(r, d, 8, scA, 0);
        var hook = Composer.Section(r, d, 8, scB, 4);
        var rhythm = Composer.Rhythms.ContainsKey(d.drums) ? Composer.Rhythms[d.drums] : Composer.Rhythms["maqsum"];
        int bassRoot = d.root - 24;
        float prev808 = Synth.Hz(bassRoot);

        for (int bar = 0; bar < bars; bar++)
        {
            string sec = Section(bar);
            bool full = sec == "a" || sec == "drop" || sec == "final";
            bool dropish = sec == "drop" || sec == "final";
            int[] sc = sec == "drop" ? scB : scA;
            int chord = (sec == "drop" ? Composer.ProgB : Composer.ProgA)[bar % 8];
            int bs = bar * 16 * stepS;
            int bIn = bar % 32;
            Func<float, int> At = st => bs + (int)(st * stepS);

            // ---------------- bumbo / palmas / chimbais
            if (!trap)
            {
                bool kickOn = full || sec == "intro" || (sec == "build" && bIn < 14);
                if (kickOn)
                    for (int s = 0; s < 16; s += 4)
                    {
                        if (sec == "intro") Kick(intro, At(s), 0.9f, techno);
                        else { Kick(main, At(s), 0.95f, techno); kicks.Add(At(s)); }
                    }
                if (full)
                {
                    Clap(main, At(4), 0.5f, r); Clap(main, At(12), 0.5f, r);
                    for (int s = 0; s < 16; s++)
                    {
                        if (house && s % 4 == 2) Hat(main, At(s), 0.32f, true, r);
                        else if (techno || dropish) Hat(main, At(s), s % 2 == 0 ? 0.12f : 0.22f, false, r);
                    }
                    if (techno && s16(bIn)) Clap(main, At(14), 0.25f, r);
                }
                if (sec == "intro" && bIn >= 2)
                    for (int s = 2; s < 16; s += 4) Hat(intro, At(s), 0.25f, false, r);
            }
            else
            {
                // trap em meio-tempo: bumbo sincopado, caixa no 3º tempo, chimbais com "rolls"
                string kp = bIn % 2 == 0 ? "x.....x...x....." : "x..x......x..x..";
                if (full || sec == "intro")
                    for (int s = 0; s < 16; s++)
                    {
                        if (kp[s] != 'x') continue;
                        if (sec == "intro") { if (s == 0) Kick(intro, At(s), 0.9f, false); continue; }
                        Kick(main, At(s), 0.95f, false);
                        kicks.Add(At(s));
                        // 808 segue o bumbo, deslizando de uma nota para a outra
                        int deg = chord + (s == 10 ? 4 : 0) + (r.NextDouble() < 0.2 ? 7 : 0);
                        float hz = Synth.Hz(Synth.Note(bassRoot, sc, deg));
                        int nextK = s + 1; while (nextK < 16 && kp[nextK] != 'x') nextK++;
                        Sub808(main, At(s), prev808, hz, (nextK - s) * stepSec * 0.95f, 0.55f);
                        prev808 = hz;
                    }
                if (full)
                {
                    Snare(main, At(8), 0.6f, r); Clap(main, At(8), 0.35f, r);
                    for (int s = 0; s < 16; s++)
                    {
                        if (bIn % 2 == 1 && s >= 12)
                        {
                            // roll de tercinas
                            for (int k = 0; k < 3; k++) Hat(main, At(s + k / 3f), 0.16f + 0.02f * k, false, r);
                        }
                        else Hat(main, At(s), s % 4 == 0 ? 0.24f : 0.15f, false, r);
                    }
                    if (bIn % 4 == 3) Hat(main, At(6), 0.3f, true, r);
                }
            }

            // ---------------- subida (build): palmas acelerando + riser
            if (sec == "build")
            {
                int step = bIn < 14 ? 4 : (bIn == 14 ? 2 : 1);
                for (int s = 0; s < 16; s += step)
                {
                    float k = ((bIn - 12) * 16 + s) / 64f;
                    Snare(main, At(s), 0.15f + 0.45f * k, r);
                }
                if (bIn == 12) Riser(main, bs, 4 * 16 * stepSec, 0.55f, r);
            }
            if (bIn == 3) Riser(intro, bs, 16 * stepSec, 0.35f, r);
            if (bIn == 27) Riser(main, bs, 16 * stepSec, 0.3f, r);
            if (bIn == 4 || bIn == 28) Impact(main, bs, 0.5f, r);
            if (bIn == 16) Impact(main, bs, 0.85f, r);
            if (bIn == 24) Downlifter(main, bs, 2 * 16 * stepSec, 0.35f, r);

            // ---------------- darbuka por cima do eletrônico
            string pat = rhythm[(bIn % 4 == 3 || sec == "break") ? 1 : 0];
            float dv = sec == "break" ? 0.55f : (full ? 0.38f : 0f);
            var dbuf = sec == "intro" ? intro : main;
            if (sec == "intro") dv = 0.45f;
            if (dv > 0f)
                for (int s = 0; s < 16; s++)
                {
                    switch (pat[s])
                    {
                        case 'D': Synth.Hit(dbuf, At(s), Synth.Drum.Doum, dv * (trap ? 0.5f : 0.8f), true, r); break;
                        case 'T': Synth.Hit(dbuf, At(s), Synth.Drum.Tek, dv * 0.8f, true, r); break;
                        case 'k': Synth.Hit(dbuf, At(s), Synth.Drum.Ka, dv * 0.6f, true, r); break;
                    }
                    if (s % 2 == 1 && sec == "break") Synth.Hit(dbuf, At(s), Synth.Drum.Riq, 0.1f, true, r);
                }

            // ---------------- baixo (com sidechain)
            bool bassOn = full || (sec == "build" && bIn < 14);
            if (bassOn && !trap)
            {
                float root = Synth.Hz(Synth.Note(bassRoot + 12, sc, chord));
                if (house)
                {
                    for (int s = 2; s < 16; s += 4)
                    {
                        Saw(pump, At(s), root, stepSec * 1.6f, 0.38f, 2, 0.006f, 1800f, 500f, 14f, 0.004f, 0.04f, 0.6f, r);
                        Sub808(pump, At(s), root * 0.5f, root * 0.5f, stepSec * 1.6f, 0.3f);
                    }
                }
                else
                {
                    // techno: baixo "rolante" em semicolcheias entre os bumbos
                    for (int s = 0; s < 16; s++)
                    {
                        if (s % 4 == 0) continue;
                        float hz = s % 4 == 3 && r.NextDouble() < 0.3 ? root * 2f : root;
                        Saw(pump, At(s), hz, stepSec * 0.85f, 0.42f, 2, 0.012f, 2400f, 380f, 22f, 0.003f, 0.03f, 0.9f, r);
                    }
                }
            }

            // ---------------- arpejo (alaúde elétrico) no verso e no final; no techno também no drop
            if (sec == "a" || sec == "final" || (techno && sec == "drop"))
            {
                int[] arp = { 0, 2, 4, 7, 4, 2, 0, 4 };
                for (int s = 0; s < 16; s++)
                {
                    if (house && s % 2 == 1 && sec == "a") continue;
                    float hz = Synth.Hz(Synth.Note(d.root, sc, chord + arp[(s + bIn) % arp.Length]));
                    Saw(pump, At(s), hz, stepSec * 0.6f, 0.13f, 3, 0.007f, 4500f, 700f, 18f, 0.002f, 0.05f, 0.3f, r);
                }
            }
            // alaúde de verdade na intro (filtrado)
            if (sec == "intro")
                for (int s = 0; s < 16; s += 2)
                    Synth.Pluck(intro, At(s), Synth.Hz(Synth.Note(d.root - 12, sc, chord + (s % 8 == 6 ? 1 : 0))), stepSec * 1.5f, 0.3f, 0.8f, 0.995f, true, r);

            // ---------------- pad (supersaw) no drop, coro no respiro
            if (bIn % 2 == 0 && (sec == "drop" || sec == "break"))
                foreach (int deg in new[] { chord, chord + 2, chord + 4 })
                {
                    float hz = Synth.Hz(Synth.Note(d.root - 12, sc, deg));
                    if (sec == "drop") Saw(pump, bs, hz, 32 * stepSec, 0.1f, 5, 0.012f, 1600f, 1600f, 1f, 0.2f, 0.4f, 0.4f, r);
                    else Synth.WindNote(wet, bs, hz, 32 * stepSec, 0.09f, Synth.Wind.Choir, true, r);
                }

            // ---------------- shofar marcando as seções
            if (d.shofarEvery > 0 && (bIn == 16 || bIn == 20 || bIn == 28))
                Synth.WindNote(wet, bs, Synth.Hz(d.root - 12), stepSec * 6f, 0.3f, Synth.Wind.Shofar, true, r);
        }

        // ---------------- melodias
        // verso: instrumento oriental (clarinete / mizmar / ney)
        Lead(wet, melA, 4 * 16, 128, stepS, stepSec, scA, d.root, d.melodyA, 0.3f, r, false);
        // drop: o "gancho" no supersaw, dobrado pelo instrumento oriental uma oitava abaixo
        Lead(main, hook, 16 * 16, 128, stepS, stepSec, scB, d.root + 12, d.melodyA, 0.2f, r, true);
        Lead(wet, hook, 16 * 16, 128, stepS, stepSec, scB, d.root, d.melodyB, 0.16f, r, false);
        // respiro: flauta ney sozinha
        Lead(wet, melA, 24 * 16, 64, stepS, stepSec, scA, d.root, Synth.Wind.Ney, 0.28f, r, false);
        // final: tudo junto
        Lead(wet, melA, 28 * 16, 64, stepS, stepSec, scA, d.root, d.melodyA, 0.28f, r, false);
        Lead(main, melA, 28 * 16, 64, stepS, stepSec, scA, d.root + 12, d.melodyA, 0.14f, r, true);

        // ---------------- mixagem
        FilterSweep(intro, 0, 4 * 16 * stepS, 220f, 9000f);
        var duck = new float[total];
        for (int i = 0; i < total; i++) duck[i] = 1f;
        float depth = trap ? 0.35f : 0.7f;
        float rel = SR * 0.12f;
        foreach (int k in kicks)
            for (int i = 0; i < (int)(rel * 3); i++)
            {
                int j = (k + i) % total;
                duck[j] = Math.Min(duck[j], 1f - depth * (float)Math.Exp(-i / rel));
            }
        Synth.Reverb(wet, 0.32f, 0.82f, true);
        for (int i = 0; i < total; i++) main[i] += pump[i] * duck[i] + wet[i] * (0.85f + 0.15f * duck[i]) + intro[i];
        Synth.Master(main, 0.24f);
        return main;
    }

    static bool s16(int bar) => bar % 2 == 1;

    static void Lead(float[] buf, List<Composer.N> notes, int offset, int maxStep, int stepS, float stepSec, int[] scale, int root,
                     Synth.Wind inst, float vol, Random r, bool supersaw)
    {
        foreach (var n in notes)
        {
            if (n.step >= maxStep) continue;
            int at = (n.step + offset) * stepS;
            float hz = Synth.Hz(Synth.Note(root, scale, n.deg));
            float dur = Math.Min(n.len, maxStep - n.step) * stepSec * 0.92f;
            if (supersaw) Saw(buf, at, hz, dur, vol, 7, 0.011f, 5200f, 2200f, 6f, 0.008f, 0.12f, 0.5f, r);
            else
            {
                float glide = inst == Synth.Wind.Clarinet && n.len >= 2 && r.NextDouble() < 0.35 ? -1f : 0f;
                Synth.WindNote(buf, at, hz, dur, vol, inst, true, r, glide);
            }
        }
    }
}
