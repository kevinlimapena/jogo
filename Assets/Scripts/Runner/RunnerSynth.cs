using System;
using System.Collections.Generic;

/// <summary>
/// Sintetizador procedural (sem arquivos de áudio): instrumentos do Oriente Médio antigo.
/// Tudo aqui é matemática pura e thread-safe (usa System.Random e System.Math), para
/// poder gerar as músicas numa thread de fundo sem travar o jogo.
///   • alaúde (oud) e lira (kinnor): corda pinçada (Karplus-Strong)
///   • flauta ney, clarinete klezmer, trombeta/cornu romano, shofar, coro, bordão (wavetables)
///   • tambores: doum/tek/ka do darbuka, pandeiro (tof), riq/zils, tambor de guerra
/// </summary>
public static class Synth
{
    public const int SR = 22050;

    // ------------------------------------------------------------------ escalas (semitons)
    public static readonly int[] Freygish = { 0, 1, 4, 5, 7, 8, 10 };        // Ahava Rabbah / Hijaz
    public static readonly int[] Misheberakh = { 0, 2, 3, 6, 7, 9, 10 };     // "dórico ucraniano" (klezmer)
    public static readonly int[] Phrygian = { 0, 1, 3, 5, 7, 8, 10 };
    public static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
    public static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 };
    public static readonly int[] DoubleHarmonic = { 0, 1, 4, 5, 7, 8, 11 };
    public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };

    public static float Hz(float midi) => (float)(440.0 * Math.Pow(2.0, (midi - 69.0) / 12.0));

    public static int Note(int rootMidi, int[] scale, int degree)
    {
        int oct = (int)Math.Floor(degree / 7.0);
        int d = degree - oct * 7;
        return rootMidi + oct * 12 + scale[d];
    }

    public static void Put(float[] buf, int i, float v, bool wrap)
    {
        int n = buf.Length;
        if (wrap) { i %= n; if (i < 0) i += n; buf[i] += v; }
        else if (i >= 0 && i < n) buf[i] += v;
    }

    static float Nz(Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    // ------------------------------------------------------------------ cordas pinçadas

    /// Karplus-Strong. bright 0..1 (timbre inicial), decay ~0.990..0.999.
    public static void Pluck(float[] buf, int start, float freq, float dur, float vol, float bright, float decay, bool wrap, Random r)
    {
        int period = Math.Max(2, (int)(SR / freq));
        var d = new float[period];
        float prev = 0f;
        for (int i = 0; i < period; i++) { prev = prev + (Nz(r) - prev) * bright; d[i] = prev; }
        int len = (int)(SR * Math.Min(dur + 1.2f, 3.5f));
        int rel = (int)(SR * 0.06f);
        int stopAt = (int)(SR * dur) + (int)(SR * 1.2f);
        int idx = 0;
        for (int i = 0; i < len; i++)
        {
            int j = (idx + 1) % period;
            float y = d[idx];
            d[idx] = 0.5f * (d[idx] + d[j]) * decay;
            idx = j;
            float env = i < 3 ? i / 3f : 1f;
            if (i > stopAt - rel) env *= Math.Max(0f, (stopAt - i) / (float)rel);
            if (env <= 0f) break;
            Put(buf, start + i, y * vol * env, wrap);
        }
    }

    // ------------------------------------------------------------------ wavetables

    const int TN = 1024;

    // tabelas já calculadas (somente leitura depois de criadas → seguro entre threads)
    static readonly System.Collections.Concurrent.ConcurrentDictionary<long, float[]> cache = new System.Collections.Concurrent.ConcurrentDictionary<long, float[]>();

    public static float[] Cached(int kind, float freq, Func<float[]> make)
    {
        long key = (long)kind * 10000000L + (long)Math.Round(freq * 20f);
        return cache.GetOrAdd(key, _ => make());
    }

    /// Tabela de um ciclo com harmônicos limitados pela frequência (sem aliasing).
    public static float[] Table(float freq, int maxK, Func<int, float, float> weight)
    {
        var t = new float[TN];
        int kmax = Math.Max(1, Math.Min(maxK, (int)((SR * 0.45f) / Math.Max(20f, freq))));
        for (int k = 1; k <= kmax; k++)
        {
            float w = weight(k, k * freq);
            if (Math.Abs(w) < 1e-4f) continue;
            for (int i = 0; i < TN; i++) t[i] += w * (float)Math.Sin(2.0 * Math.PI * k * i / TN);
        }
        float peak = 1e-4f;
        for (int i = 0; i < TN; i++) peak = Math.Max(peak, Math.Abs(t[i]));
        for (int i = 0; i < TN; i++) t[i] /= peak;
        return t;
    }

    public static float Read(float[] t, double phase)
    {
        double p = (phase - Math.Floor(phase)) * TN;
        int i0 = (int)p;
        float f = (float)(p - i0);
        return t[i0 % TN] * (1f - f) + t[(i0 + 1) % TN] * f;
    }

    public enum Wind { Ney, Clarinet, Brass, Shofar, Choir, Drone, Mizmar }

    static float Formant(float f)
    {
        // vogal "a": formantes perto de 700 Hz e 1150 Hz
        double a = (f - 700.0) / 260.0, b = (f - 1150.0) / 320.0;
        return (float)(Math.Exp(-a * a) + 0.7 * Math.Exp(-b * b) + 0.12);
    }

    /// Instrumento de sopro / sustentado.
    public static void WindNote(float[] buf, int start, float freq, float dur, float vol, Wind kind, bool wrap, Random r,
                                float glideSemis = 0f, float vibrato = -1f)
    {
        float[] tab, tab2 = null;
        float attack, release, breath, vibDepth, vibRate;
        switch (kind)
        {
            case Wind.Ney:
                tab = Cached(1, freq, () => Table(freq, 4, (k, f) => k == 1 ? 1f : (k == 2 ? 0.18f : (k == 3 ? 0.1f : 0.04f))));
                attack = 0.07f; release = 0.12f; breath = 0.16f; vibDepth = 0.007f; vibRate = 5.2f; break;
            case Wind.Clarinet:
                tab = Cached(2, freq, () => Table(freq, 11, (k, f) => k % 2 == 1 ? 1f / k : 0.04f / k));
                attack = 0.03f; release = 0.06f; breath = 0.05f; vibDepth = 0.006f; vibRate = 6f; break;
            case Wind.Brass:
                tab = Cached(3, freq, () => Table(freq, 4, (k, f) => 1f / k));                       // escuro
                tab2 = Cached(4, freq, () => Table(freq, 14, (k, f) => 1f / (float)Math.Pow(k, 0.8))); // brilhante
                attack = 0.06f; release = 0.1f; breath = 0.03f; vibDepth = 0.003f; vibRate = 5f; break;
            case Wind.Shofar:
                tab = Cached(5, freq, () => Table(freq, 14, (k, f) => (float)Math.Pow(k, -0.55) * (0.5f + Formant(f * 1.3f))));
                attack = 0.05f; release = 0.15f; breath = 0.12f; vibDepth = 0.012f; vibRate = 4.3f; break;
            case Wind.Choir:
                tab = Cached(6, freq, () => Table(freq, 18, (k, f) => Formant(f) / (float)Math.Sqrt(k)));
                attack = Math.Min(0.45f, dur * 0.35f); release = Math.Min(0.6f, dur * 0.4f); breath = 0.02f; vibDepth = 0.005f; vibRate = 4.8f; break;
            case Wind.Mizmar:
                // mizmar / zurna egípcio: palheta dupla, nasal e penetrante
                tab = Cached(7, freq, () => Table(freq, 16, (k, f) => (float)Math.Pow(k, -0.35) * (0.25f + (float)Math.Exp(-Math.Pow((f - 1400.0) / 500.0, 2)) + 0.5f * (float)Math.Exp(-Math.Pow((f - 2600.0) / 700.0, 2)))));
                attack = 0.025f; release = 0.06f; breath = 0.05f; vibDepth = 0.009f; vibRate = 6.5f; break;
            default: // Drone
                tab = Cached(8, freq, () => Table(freq, 10, (k, f) => 1f / (float)Math.Pow(k, 1.4)));
                attack = 0.5f; release = 0.5f; breath = 0f; vibDepth = 0f; vibRate = 0f; break;
        }
        if (vibrato >= 0f) vibDepth = vibrato;
        int len = (int)(SR * (dur + release));
        double ph = r.NextDouble(), ph2 = r.NextDouble(), ph3 = r.NextDouble();
        float lp = 0f;
        float detune = kind == Wind.Choir ? 0.004f : 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR;
            float env = t < attack ? t / attack : 1f;
            if (t > dur) env *= Math.Max(0f, 1f - (t - dur) / release);
            float vib = 1f + vibDepth * (float)Math.Sin(2 * Math.PI * vibRate * t) * Math.Min(1f, Math.Max(0f, (t - 0.12f) / 0.25f));
            float glide = glideSemis != 0f ? (float)Math.Pow(2.0, glideSemis * Math.Max(0f, 1f - t / 0.09f) / 12.0) : 1f;
            float f = freq * vib * glide;
            if (kind == Wind.Shofar) f *= 1f + 0.06f * (float)Math.Exp(-t * 9f) * -1f + 0.004f * (float)Math.Sin(t * 23f);
            ph += f / SR;
            float s;
            if (kind == Wind.Brass)
            {
                float bright = Math.Min(1f, t / 0.09f) * 0.8f + 0.2f;
                s = Read(tab, ph) * (1f - bright) + Read(tab2, ph) * bright;
            }
            else if (kind == Wind.Choir)
            {
                ph2 += f * (1f + detune) / SR;
                ph3 += f * (1f - detune) / SR;
                s = (Read(tab, ph) + Read(tab, ph2) + Read(tab, ph3)) / 3f;
            }
            else s = Read(tab, ph);
            if (breath > 0f) { lp += (Nz(r) - lp) * 0.25f; s += lp * breath; }
            if (kind == Wind.Shofar) s = (float)Math.Tanh(s * 1.6f);
            Put(buf, start + i, s * env * vol, wrap);
        }
    }

    // ------------------------------------------------------------------ percussão

    public enum Drum { Doum, Tek, Ka, Riq, Tof, War, Heart }

    public static void Hit(float[] buf, int start, Drum kind, float vol, bool wrap, Random r)
    {
        float len;
        switch (kind)
        {
            case Drum.Doum: len = 0.45f; break;
            case Drum.War: len = 0.7f; break;
            case Drum.Tof: len = 0.35f; break;
            case Drum.Heart: len = 0.3f; break;
            case Drum.Riq: len = 0.18f; break;
            default: len = 0.09f; break;
        }
        int n = (int)(SR * len);
        double ph = 0;
        float prevN = 0f, hp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float s = 0f;
            float nz = Nz(r);
            hp = nz - prevN; prevN = nz;   // passa-alta simples
            switch (kind)
            {
                case Drum.Doum:
                    ph += (52f + 70f * (float)Math.Exp(-t * 28f)) / SR;
                    s = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 7f) + nz * (float)Math.Exp(-t * 250f) * 0.35f;
                    break;
                case Drum.War:
                    ph += (42f + 50f * (float)Math.Exp(-t * 18f)) / SR;
                    s = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 4.5f) + nz * (float)Math.Exp(-t * 40f) * 0.3f;
                    break;
                case Drum.Heart:
                    ph += (45f + 25f * (float)Math.Exp(-t * 20f)) / SR;
                    s = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 11f);
                    break;
                case Drum.Tof:
                    ph += (75f + 55f * (float)Math.Exp(-t * 35f)) / SR;
                    s = (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 10f) * 0.8f + hp * (float)Math.Exp(-t * 60f) * 0.35f;
                    break;
                case Drum.Tek:
                    s = hp * (float)Math.Exp(-t * 70f) * 0.9f + (float)Math.Sin(2 * Math.PI * 3100 * t) * (float)Math.Exp(-t * 55f) * 0.35f;
                    break;
                case Drum.Ka:
                    s = hp * (float)Math.Exp(-t * 90f) * 0.55f + (float)Math.Sin(2 * Math.PI * 2300 * t) * (float)Math.Exp(-t * 70f) * 0.25f;
                    break;
                case Drum.Riq:
                    s = ((float)Math.Sin(2 * Math.PI * 5200 * t) + 0.7f * (float)Math.Sin(2 * Math.PI * 6900 * t) + 0.5f * (float)Math.Sin(2 * Math.PI * 8400 * t)) * 0.3f
                        * (float)Math.Exp(-t * 22f) + hp * (float)Math.Exp(-t * 35f) * 0.4f;
                    break;
            }
            Put(buf, start + i, s * vol, wrap);
        }
    }

    /// Sino / zil inarmônico (moedas, críticos, relíquias).
    public static void Bell(float[] buf, int start, float freq, float dur, float vol, bool wrap)
    {
        int n = (int)(SR * dur);
        float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
        float[] amps = { 1f, 0.5f, 0.28f, 0.14f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float s = 0f;
            for (int k = 0; k < ratios.Length; k++)
            {
                float f = freq * ratios[k];
                if (f > SR * 0.45f) continue;
                s += amps[k] * (float)Math.Sin(2 * Math.PI * f * t) * (float)Math.Exp(-t * (3.5f + k * 2.5f) / dur);
            }
            float env = t < 0.003f ? t / 0.003f : 1f;
            Put(buf, start + i, s * env * vol * 0.6f, wrap);
        }
    }

    /// Ruído filtrado com corte que varre de f0 a f1 (vento, espada, fogo, mar).
    public static void Whoosh(float[] buf, int start, float dur, float f0, float f1, float vol, float attackFrac, Random r, bool wrap = false)
    {
        int n = (int)(SR * dur);
        float low = 0f, band = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float fc = f0 + (f1 - f0) * t;
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SR * 0.2f) / SR);
            float x = Nz(r);
            low += f * band;
            float high = x - low - 0.6f * band;
            band += f * high;
            float env = t < attackFrac ? t / attackFrac : (1f - t) / (1f - attackFrac);
            Put(buf, start + i, band * env * env * vol, wrap);
        }
    }

    /// Varredura de tom (sobe/desce), com timbre de onda escolhido.
    public static void Sweep(float[] buf, int start, float dur, float f0, float f1, float vol, float square, bool wrap = false)
    {
        int n = (int)(SR * dur);
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            ph += (f0 + (f1 - f0) * t) / SR;
            float sn = (float)Math.Sin(2 * Math.PI * ph);
            float s = sn * (1f - square) + Math.Sign(sn) * 0.6f * square;
            float env = Math.Min(1f, t * 30f) * (1f - t);
            Put(buf, start + i, s * env * vol, wrap);
        }
    }

    // ------------------------------------------------------------------ pós-processamento

    /// Reverb de Schroeder. Com wrap = true, roda duas vezes para a cauda "dar a volta" no loop.
    public static void Reverb(float[] buf, float wet, float room, bool wrap)
    {
        if (wet <= 0f) return;
        int n = buf.Length;
        int[] combs = { 1557, 1617, 1491, 1422 };
        int[] aps = { 225, 341 };
        float scale = SR / 44100f;
        var cb = new float[combs.Length][];
        var ci = new int[combs.Length];
        var lpState = new float[combs.Length];
        for (int k = 0; k < combs.Length; k++) cb[k] = new float[Math.Max(1, (int)(combs[k] * scale))];
        var ab = new float[aps.Length][];
        var ai = new int[aps.Length];
        for (int k = 0; k < aps.Length; k++) ab[k] = new float[Math.Max(1, (int)(aps[k] * scale))];
        var dry = (float[])buf.Clone();
        var outp = new float[n];
        int passes = wrap ? 2 : 1;
        for (int pass = 0; pass < passes; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                float x = dry[i] * 0.3f;
                float y = 0f;
                for (int k = 0; k < combs.Length; k++)
                {
                    var b = cb[k];
                    float o = b[ci[k]];
                    lpState[k] = o * 0.75f + lpState[k] * 0.25f;
                    b[ci[k]] = x + lpState[k] * room;
                    ci[k] = (ci[k] + 1) % b.Length;
                    y += o;
                }
                for (int k = 0; k < aps.Length; k++)
                {
                    var b = ab[k];
                    float o = b[ai[k]];
                    float v = y + o * 0.5f;
                    b[ai[k]] = v;
                    ai[k] = (ai[k] + 1) % b.Length;
                    y = o - v * 0.5f;
                }
                if (pass == passes - 1) outp[i] = y;
            }
        }
        for (int i = 0; i < n; i++) buf[i] = dry[i] + outp[i] * wet;
    }

    /// Masterização para música eletrônica: ganho pelo volume médio (RMS) + saturação suave (limitador).
    public static void Master(float[] buf, float targetRms)
    {
        double sum = 0;
        for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
        float rms = (float)Math.Sqrt(sum / Math.Max(1, buf.Length));
        float g = targetRms / Math.Max(1e-4f, rms);
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * g) * 0.92f;
    }

    public static void Normalize(float[] buf, float peakTarget)
    {
        float peak = 1e-4f;
        for (int i = 0; i < buf.Length; i++) peak = Math.Max(peak, Math.Abs(buf[i]));
        float g = peakTarget / peak;
        for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * g * 1.1f) * 0.95f;
    }
}
