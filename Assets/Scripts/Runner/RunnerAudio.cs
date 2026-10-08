using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

// ======================================================================================
//  COMPOSITOR: gera cada faixa a partir de uma "receita" (escala, ritmo, instrumentos)
// ======================================================================================

public class TrackDef
{
    public string id;
    public float bpm = 120f;
    public int root = 62;                         // tônica da melodia (MIDI)
    public int[] scaleA = Synth.Freygish, scaleB;
    public int bars = 16;
    public string drums = "bulgar";               // bulgar, maqsum, baladi, march, heart, satan, tension, soft, none
    public Synth.Wind melodyA = Synth.Wind.Clarinet, melodyB = Synth.Wind.Ney;
    public bool melodyPluck;                      // melodia no alaúde em vez de sopro
    public string bass = "tremolo";              // tremolo, pulse, sparse, march, none
    public bool drone, pad, riq, canon;
    public int shofarEvery;                       // 0 = sem shofar
    public float density = 0.5f;                  // 0 calmo .. 1 frenético
    public float rest = 0.12f;
    public float reverb = 0.2f, room = 0.8f;
    public float melodyVol = 0.3f;
    public int seed = 1;
    public string electro;                       // null = acústico; house, techno, trap, fast = camada eletrônica (RunnerElectro.cs)
}

public static class Composer
{
    public static readonly Dictionary<string, TrackDef> Tracks = new Dictionary<string, TrackDef>
    {
        // Menu: um nigun calmo na lira e na flauta ney, sobre um bordão (Sl 137)
        ["menu"] = new TrackDef { id = "menu", bpm = 72, root = 62, scaleA = Synth.Freygish, bars = 8, drums = "soft",
            melodyA = Synth.Wind.Ney, melodyB = Synth.Wind.Ney, bass = "sparse", drone = true, density = 0.15f, rest = 0.2f, reverb = 0.38f, room = 0.85f, seed = 7 },
        // Jerusalém (tema principal): "oriental house" — bumbo 4x4, baixo com sidechain, clarinete klezmer, darbuka em 3+3+2
        ["jerusalem"] = new TrackDef { id = "jerusalem", electro = "house", bpm = 122, root = 62, scaleA = Synth.Freygish, scaleB = Synth.Misheberakh, drums = "bulgar",
            melodyA = Synth.Wind.Clarinet, melodyB = Synth.Wind.Ney, density = 0.5f, rest = 0.14f, seed = 112 },
        // Egito: trap oriental em meio-tempo — 808 deslizando, chimbais em tercinas, mizmar e flauta ney (Hijaz Kar)
        ["egito"] = new TrackDef { id = "egito", electro = "trap", bpm = 140, root = 64, scaleA = Synth.DoubleHarmonic, scaleB = Synth.Freygish, drums = "baladi",
            melodyA = Synth.Wind.Mizmar, melodyB = Synth.Wind.Ney, density = 0.45f, rest = 0.16f, shofarEvery = 0, seed = 205 },
        // Roma: marcha militar com cornus de bronze e tambores de guerra
        ["roma"] = new TrackDef { id = "roma", bpm = 106, root = 57, scaleA = Synth.Dorian, scaleB = Synth.Phrygian, bars = 16, drums = "march",
            melodyA = Synth.Wind.Brass, melodyB = Synth.Wind.Clarinet, bass = "march", density = 0.4f, reverb = 0.28f, seed = 23 },
        // Sheol: escuro, lento, coro distante e batida de coração
        ["sheol"] = new TrackDef { id = "sheol", bpm = 64, root = 52, scaleA = Synth.DoubleHarmonic, bars = 8, drums = "heart",
            melodyA = Synth.Wind.Ney, melodyB = Synth.Wind.Ney, bass = "none", drone = true, pad = true, density = 0.12f, rest = 0.3f, reverb = 0.55f, room = 0.88f, seed = 31 },
        // Chefe: "techno oriental" — baixo rolante, supersaw, mizmar, maqsum na darbuka e shofar nas viradas
        ["chefe"] = new TrackDef { id = "chefe", electro = "techno", bpm = 140, root = 62, scaleA = Synth.Freygish, scaleB = Synth.DoubleHarmonic, drums = "maqsum",
            melodyA = Synth.Wind.Mizmar, melodyB = Synth.Wind.Clarinet, shofarEvery = 4, density = 0.7f, rest = 0.08f, seed = 141 },
        // Satanás: techno sombrio com coro e escala dupla harmônica
        ["satanas"] = new TrackDef { id = "satanas", electro = "techno", bpm = 132, root = 50, scaleA = Synth.DoubleHarmonic, scaleB = Synth.Phrygian, drums = "satan",
            melodyA = Synth.Wind.Shofar, melodyB = Synth.Wind.Choir, shofarEvery = 8, density = 0.4f, rest = 0.15f, seed = 666 },
        // Vitória: coro, lira e flauta em escala maior — a Nova Jerusalém (Ap 21)
        ["vitoria"] = new TrackDef { id = "vitoria", bpm = 78, root = 62, scaleA = Synth.Major, bars = 8, drums = "soft",
            melodyA = Synth.Wind.Ney, melodyB = Synth.Wind.Clarinet, bass = "sparse", drone = true, pad = true, shofarEvery = 8, density = 0.25f, rest = 0.15f, reverb = 0.45f, room = 0.86f, seed = 777 },
        // Noite da Páscoa: flauta solitária e pandeiro abafado
        ["noite"] = new TrackDef { id = "noite", bpm = 84, root = 64, scaleA = Synth.Phrygian, bars = 8, drums = "baladi",
            melodyA = Synth.Wind.Ney, melodyB = Synth.Wind.Ney, bass = "sparse", drone = true, density = 0.25f, rest = 0.2f, reverb = 0.45f, room = 0.85f, seed = 53 },
        // Jericó: só bordão e coro — o ritmo vem das trombetas do mini-jogo
        ["jerico"] = new TrackDef { id = "jerico", bpm = 96, root = 62, scaleA = Synth.Freygish, bars = 8, drums = "none",
            melodyA = Synth.Wind.Choir, melodyB = Synth.Wind.Choir, bass = "none", drone = true, pad = true, density = 0.1f, rest = 0.35f, melodyVol = 0.18f, reverb = 0.45f, seed = 61 },
        // Vale de Elá: tensão crescente, tambores de guerra
        ["golias"] = new TrackDef { id = "golias", bpm = 90, root = 57, scaleA = Synth.Phrygian, bars = 8, drums = "tension",
            melodyA = Synth.Wind.Brass, melodyB = Synth.Wind.Ney, bass = "pulse", drone = true, density = 0.3f, reverb = 0.3f, seed = 71 },
        // Babel: a mesma melodia em cânone, "em outra língua" (outro instrumento, uma quarta acima)
        ["babel"] = new TrackDef { id = "babel", bpm = 116, root = 62, scaleA = Synth.Freygish, scaleB = Synth.DoubleHarmonic, bars = 16, drums = "maqsum",
            melodyA = Synth.Wind.Clarinet, melodyB = Synth.Wind.Ney, bass = "pulse", canon = true, density = 0.45f, reverb = 0.25f, seed = 81 },
        // Carros de fogo / corrida: house acelerado com bulgar
        ["carruagem"] = new TrackDef { id = "carruagem", electro = "fast", bpm = 150, root = 62, scaleA = Synth.Freygish, scaleB = Synth.Misheberakh, drums = "bulgar",
            melodyA = Synth.Wind.Clarinet, melodyB = Synth.Wind.Mizmar, density = 0.7f, rest = 0.08f, seed = 151 },
    };

    public struct N { public int step, len, deg; }

    // progressões típicas (graus da escala, um por compasso)
    public static readonly int[] ProgA = { 0, 0, 3, 3, 6, 6, 0, 0 };
    public static readonly int[] ProgB = { 3, 3, 0, 0, 6, 0, 4, 0 };

    public static readonly Dictionary<string, string[]> Rhythms = new Dictionary<string, string[]>
    {
        // 16 passos por compasso: D = doum, T = tek, k = ka, W = tambor de guerra, H = coração, F = pandeiro, . = nada
        ["bulgar"] = new[] { "D..T..D.T..k.T.k", "D..T..D.T.kT.TkT" },          // 3+3+2
        ["maqsum"] = new[] { "D.T...T.D...T.k.", "D.TkkkT.D.k.T.kk" },
        ["baladi"] = new[] { "D.D...T.D...T...", "D.D.k.T.D.k.T.k." },
        ["march"] = new[] { "W...T...W...T...", "W...T.T.W.W.T.TT" },
        ["heart"] = new[] { "H..H............", "H..H............" },
        ["satan"] = new[] { "W.WT..W.W.TT..W.", "W.WT..W.WWTTW.WT" },
        ["tension"] = new[] { "W.......W.......", "W...W...W...W.TT" },
        ["soft"] = new[] { "F.........k.....", "F.....k...F.k..." },
        ["none"] = new[] { "................", "................" },
    };

    /// Renderiza a faixa inteira (thread-safe). Loop sem emenda: as notas que passam do fim "dão a volta".
    public static float[] Render(TrackDef d)
    {
        if (!string.IsNullOrEmpty(d.electro)) return Electro.Render(d);
        var r = new System.Random(d.seed);
        float stepSec = 60f / d.bpm / 4f;
        int stepS = Math.Max(1, (int)(Synth.SR * stepSec));
        int totalSteps = d.bars * 16;
        var buf = new float[totalSteps * stepS];
        const bool W = true;

        // ---------------- melodia
        int half = d.bars / 2;
        var secA = Section(r, d, Math.Max(4, half), d.scaleA, 0);
        var secB = d.bars >= 16 ? Section(r, d, half, d.scaleB ?? d.scaleA, 4) : null;
        RenderMelody(buf, secA, 0, stepS, stepSec, d.scaleA, d.root, d.melodyA, d, r, false);
        if (secB != null) RenderMelody(buf, secB, half * 16, stepS, stepSec, d.scaleB ?? d.scaleA, d.root, d.melodyB, d, r, false);
        else if (d.bars > 4 && half < d.bars) RenderMelody(buf, Mutate(r, secA), half * 16, stepS, stepSec, d.scaleA, d.root, d.melodyB, d, r, false);
        if (d.canon)
        {
            // cânone: a mesma frase atrasada um tempo e uma quarta acima (Gn 11:7)
            RenderMelody(buf, secA, 4, stepS, stepSec, d.scaleA, d.root + 5, Synth.Wind.Brass, d, r, true);
            if (secB != null) RenderMelody(buf, secB, half * 16 + 4, stepS, stepSec, d.scaleB ?? d.scaleA, d.root + 5, Synth.Wind.Ney, d, r, true);
        }

        // ---------------- harmonia por compasso
        for (int bar = 0; bar < d.bars; bar++)
        {
            bool inB = secB != null && bar >= half;
            int[] sc = inB ? (d.scaleB ?? d.scaleA) : d.scaleA;
            int chord = (inB ? ProgB : ProgA)[bar % 8];
            int barStart = bar * 16 * stepS;
            int bassRoot = d.root - 24;
            switch (d.bass)
            {
                case "tremolo":
                {
                    int[] pat = { 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 1, 0, 4, 0 };
                    for (int s = 0; s < 16; s++)
                    {
                        float acc = s % 4 == 0 ? 1f : 0.55f;
                        Synth.Pluck(buf, barStart + s * stepS, Synth.Hz(Synth.Note(bassRoot + 12, sc, chord + pat[s])), stepSec * 0.9f, 0.16f * acc, 0.75f, 0.993f, W, r);
                    }
                    break;
                }
                case "pulse":
                    for (int s = 0; s < 16; s += 2)
                        Synth.Pluck(buf, barStart + s * stepS, Synth.Hz(Synth.Note(bassRoot, sc, chord + (s % 8 == 4 ? 4 : 0))), stepSec * 1.8f, 0.24f, 0.6f, 0.995f, W, r);
                    break;
                case "sparse":
                {
                    int[] arp = { 0, 2, 4, 7 };
                    for (int k = 0; k < 4; k++)
                        Synth.Pluck(buf, barStart + k * 4 * stepS, Synth.Hz(Synth.Note(bassRoot + 12, sc, chord + arp[k])), stepSec * 6f, 0.2f, 0.9f, 0.998f, W, r);
                    break;
                }
                case "march":
                    for (int s = 0; s < 16; s += 4)
                        Synth.Pluck(buf, barStart + s * stepS, Synth.Hz(Synth.Note(bassRoot, sc, chord)), stepSec * 3f, 0.3f, 0.5f, 0.995f, W, r);
                    break;
            }
            // coro / pad a cada 2 compassos
            if (d.pad && bar % 2 == 0)
                foreach (int deg in new[] { chord, chord + 2, chord + 4 })
                    Synth.WindNote(buf, barStart, Synth.Hz(Synth.Note(d.root - 12, sc, deg)), stepSec * 30f, 0.07f, Synth.Wind.Choir, W, r);
            // shofar no começo das frases
            if (d.shofarEvery > 0 && bar % d.shofarEvery == 0)
                Synth.WindNote(buf, barStart, Synth.Hz(d.root - 12), stepSec * 6f, 0.2f, Synth.Wind.Shofar, W, r);

            // ---------------- percussão
            var pats = Rhythms.ContainsKey(d.drums) ? Rhythms[d.drums] : Rhythms["none"];
            string p = pats[(bar % 4 == 3) ? 1 : 0];   // a cada 4 compassos, uma virada
            if (d.drums == "tension") p = pats[bar >= d.bars / 2 ? 1 : 0];
            for (int s = 0; s < 16; s++)
            {
                int at = barStart + s * stepS;
                switch (p[s])
                {
                    case 'D': Synth.Hit(buf, at, Synth.Drum.Doum, 0.5f, W, r); break;
                    case 'T': Synth.Hit(buf, at, Synth.Drum.Tek, 0.28f, W, r); break;
                    case 'k': Synth.Hit(buf, at, Synth.Drum.Ka, 0.2f, W, r); break;
                    case 'W': Synth.Hit(buf, at, Synth.Drum.War, 0.55f, W, r); break;
                    case 'H': Synth.Hit(buf, at, Synth.Drum.Heart, 0.5f, W, r); break;
                    case 'F': Synth.Hit(buf, at, Synth.Drum.Tof, 0.35f, W, r); break;
                }
                if (d.riq && s % 2 == 1 && p[s] == '.') Synth.Hit(buf, at, Synth.Drum.Riq, 0.07f, W, r);
            }
        }

        // ---------------- bordão
        if (d.drone)
        {
            float len = buf.Length / (float)Synth.SR;
            Synth.WindNote(buf, 0, Synth.Hz(d.root - 24), len, 0.09f, Synth.Wind.Drone, W, r);
            Synth.WindNote(buf, 0, Synth.Hz(d.root - 17), len, 0.06f, Synth.Wind.Drone, W, r);
        }

        Synth.Reverb(buf, d.reverb, d.room, true);
        Synth.Normalize(buf, 0.85f);
        return buf;
    }

    public static List<N> Section(System.Random r, TrackDef d, int bars, int[] scale, int startDeg)
    {
        // motivo de 2 compassos, repetido com variações e cadência (A A' / ... )
        var motif = Motif(r, d, 2, startDeg);
        var res = new List<N>();
        for (int b = 0; b < bars; b += 2)
        {
            var m = b == 0 ? motif : Mutate(r, motif);
            bool phraseEnd = (b + 2) % 4 == 0;
            foreach (var n in m) res.Add(new N { step = n.step + b * 16, len = n.len, deg = n.deg });
            if (phraseEnd && res.Count > 0)
            {
                // a frase termina na tônica (ou na quinta, no meio)
                var last = res[res.Count - 1];
                last.deg = (b + 2) % 8 == 0 ? 0 : 4;
                last.len = Math.Max(last.len, (b + 2) * 16 - last.step);
                res[res.Count - 1] = last;
            }
        }
        return res;
    }

    static List<N> Motif(System.Random r, TrackDef d, int bars, int startDeg)
    {
        int[][] calm = { new[] { 4 }, new[] { 6, 2 }, new[] { 8 }, new[] { 2, 2 }, new[] { 4, 4 } };
        int[][] mid = { new[] { 2, 2 }, new[] { 3, 1 }, new[] { 4 }, new[] { 1, 1, 2 }, new[] { 2, 1, 1 } };
        int[][] fast = { new[] { 1, 1, 1, 1 }, new[] { 2, 1, 1 }, new[] { 1, 1, 2 }, new[] { 2, 2 }, new[] { 3, 1 } };
        var pool = d.density < 0.3f ? calm : (d.density < 0.6f ? mid : fast);
        var res = new List<N>();
        int step = 0, deg = startDeg;
        int[] moves = { -2, -1, -1, 0, 1, 1, 2 };
        while (step < bars * 16)
        {
            var cell = pool[r.Next(pool.Length)];
            foreach (int len in cell)
            {
                if (step >= bars * 16) break;
                int l = Math.Min(len, bars * 16 - step);
                if (r.NextDouble() >= d.rest || step == 0)
                {
                    res.Add(new N { step = step, len = l, deg = deg });
                    deg += moves[r.Next(moves.Length)];
                    if (deg < -2) deg = -1;
                    if (deg > 9) deg = 8;
                }
                step += l;
            }
        }
        return res;
    }

    static List<N> Mutate(System.Random r, List<N> src)
    {
        var res = new List<N>(src.Count);
        foreach (var n in src)
        {
            var m = n;
            if (r.NextDouble() < 0.25) m.deg += r.Next(2) == 0 ? -1 : 1;
            res.Add(m);
        }
        return res;
    }

    static void RenderMelody(float[] buf, List<N> notes, int stepOffset, int stepS, float stepSec, int[] scale, int root, Synth.Wind inst,
                             TrackDef d, System.Random r, bool echo)
    {
        float vol = d.melodyVol * (echo ? 0.55f : 1f);
        foreach (var n in notes)
        {
            int at = (n.step + stepOffset) * stepS;
            float f = Synth.Hz(Synth.Note(root, scale, n.deg));
            float dur = n.len * stepSec * 0.92f;
            if (d.melodyPluck) { Synth.Pluck(buf, at, f, dur, vol, 0.8f, 0.996f, true, r); continue; }
            // ornamentos: "krekhts" (deslizar de baixo) no clarinete e notinha de graça na flauta
            float glide = inst == Synth.Wind.Clarinet && n.len >= 2 && r.NextDouble() < 0.35 ? -1f : 0f;
            if ((inst == Synth.Wind.Ney || inst == Synth.Wind.Clarinet) && n.len >= 3 && r.NextDouble() < 0.3)
            {
                float g = Synth.Hz(Synth.Note(root, scale, n.deg + 1));
                Synth.WindNote(buf, at, g, 0.045f, vol * 0.7f, inst, true, r, 0f, 0f);
                at += (int)(Synth.SR * 0.045f);
            }
            Synth.WindNote(buf, at, f, dur, vol, inst, true, r, glide);
        }
    }
}

// ======================================================================================
//  MÚSICA: gera as faixas em segundo plano e troca entre elas com crossfade
// ======================================================================================

public class RunnerMusic : MonoBehaviour
{
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt("runner_music", 1) == 1;
        set { PlayerPrefs.SetInt("runner_music", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public float volume = 0.38f;
    public float fadeTime = 1.4f;

    AudioSource srcA, srcB, cur;
    string curId, wantId;
    float duck = 1f;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float[]> ready = new Dictionary<string, float[]>();
    readonly HashSet<string> requested = new HashSet<string>();
    readonly Queue<string> queue = new Queue<string>();
    readonly object gate = new object();
    bool working;

    void Awake()
    {
        srcA = MakeSource();
        srcB = MakeSource();
    }

    AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.volume = 0f;
        s.spatialBlend = 0f;
        return s;
    }

    /// Pede para gerar várias faixas em ordem de prioridade.
    public void Prewarm(params string[] ids)
    {
        foreach (var id in ids) Request(id);
    }

    void Request(string id)
    {
        if (string.IsNullOrEmpty(id) || !Composer.Tracks.ContainsKey(id)) return;
        lock (gate)
        {
            if (requested.Contains(id)) return;
            requested.Add(id);
            queue.Enqueue(id);
            if (working) return;
            working = true;
        }
        ThreadPool.QueueUserWorkItem(_ => Worker());
    }

    void Worker()
    {
        while (true)
        {
            string id;
            lock (gate)
            {
                if (queue.Count == 0) { working = false; return; }
                id = queue.Dequeue();
            }
            float[] data;
            try { data = Composer.Render(Composer.Tracks[id]); }
            catch (Exception) { data = null; }
            lock (gate) { if (data != null) ready[id] = data; }
        }
    }

    /// BPM da faixa que está tocando (0 se nada toca).
    public float Bpm => cur != null && cur.isPlaying && curId != null && Composer.Tracks.ContainsKey(curId) ? Composer.Tracks[curId].bpm : 0f;

    /// Posição em batidas da faixa atual (a faixa começa numa batida). -1 se nada toca.
    public double BeatPos
    {
        get
        {
            float bpm = Bpm;
            if (bpm <= 0f || cur.clip == null) return -1;
            return cur.timeSamples / (double)Synth.SR * bpm / 60.0;
        }
    }

    /// Qual faixa deve tocar agora (o jogo chama todo frame). duckMul abaixa o volume (pausa).
    public void Want(string id, float duckMul)
    {
        Request(id);
        wantId = id;
        duck = duckMul;
    }

    void Update()
    {
        // transforma as faixas prontas em AudioClips (só pode na thread principal)
        lock (gate)
        {
            if (ready.Count > 0)
            {
                foreach (var kv in ready)
                {
                    var clip = AudioClip.Create("musica_" + kv.Key, kv.Value.Length, 1, Synth.SR, false);
                    clip.SetData(kv.Value, 0);
                    clips[kv.Key] = clip;
                }
                ready.Clear();
            }
        }

        if (wantId != curId && wantId != null && clips.ContainsKey(wantId))
        {
            var next = cur == srcA ? srcB : srcA;
            next.clip = clips[wantId];
            next.volume = 0f;
            next.Play();
            cur = next;
            curId = wantId;
        }

        float target = Enabled ? volume * duck : 0f;
        float k = Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeTime);
        foreach (var s in new[] { srcA, srcB })
        {
            float goal = s == cur ? target : 0f;
            s.volume = Mathf.MoveTowards(s.volume, goal, k * Mathf.Max(volume, 0.2f));
            if (s != cur && s.isPlaying && s.volume <= 0.001f) s.Stop();
        }
    }
}

// ======================================================================================
//  EFEITOS SONOROS: banco de sons sintetizados para cada efeito de carta / evento
// ======================================================================================

public class RunnerSfx : MonoBehaviour
{
    readonly Dictionary<string, AudioClip> bank = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, float> lastTime = new Dictionary<string, float>();
    readonly List<AudioSource> pool = new List<AudioSource>();
    int next;
    public float masterVolume = 0.9f;

    public void Build()
    {
        for (int i = 0; i < 10; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            pool.Add(s);
        }
        Recipes(AddClip);
    }

    /// Receitas de todos os efeitos (separadas do Unity para poderem ser testadas fora do jogo).
    public static void Recipes(Action<string, float, Action<float[]>> Add)
    {
        var r = new System.Random(1234);
        int D = 62;   // ré: a tônica do jogo
        var fr = Synth.Freygish;

        // --- cartas e bênçãos
        Add("espada", 0.22f, b => Synth.Whoosh(b, 0, 0.22f, 600f, 3200f, 0.9f, 0.3f, r));
        Add("espada_hit", 0.2f, b => { Synth.Hit(b, 0, Synth.Drum.Tek, 0.8f, false, r); Synth.Bell(b, 0, 1450f, 0.2f, 0.25f, false); });
        Add("zap", 0.35f, b =>
        {
            for (int k = 0; k < 9; k++) Synth.Sweep(b, (int)(Synth.SR * k * 0.03f), 0.05f, 900f + (float)r.NextDouble() * 1800f, 500f + (float)r.NextDouble() * 900f, 0.35f, 0.8f);
            Synth.Whoosh(b, 0, 0.3f, 3000f, 5000f, 0.4f, 0.05f, r);
        });
        Add("fogo", 0.75f, b => { Synth.Whoosh(b, 0, 0.55f, 4000f, 300f, 0.9f, 0.15f, r); Synth.Hit(b, (int)(Synth.SR * 0.42f), Synth.Drum.War, 0.9f, false, r); });
        Add("nova", 0.9f, b => { Synth.Whoosh(b, 0, 0.8f, 200f, 2600f, 0.8f, 0.1f, r); Synth.Sweep(b, 0, 0.6f, 180f, 45f, 0.5f, 0f); Synth.Hit(b, 0, Synth.Drum.War, 0.7f, false, r); });
        Add("mar", 1.6f, b =>
        {
            Synth.Whoosh(b, 0, 1.5f, 250f, 900f, 0.8f, 0.45f, r);
            Synth.Whoosh(b, (int)(Synth.SR * 0.2f), 1.3f, 700f, 200f, 0.6f, 0.5f, r);
        });
        Add("shofar", 1.2f, b => Synth.WindNote(b, 0, Synth.Hz(D - 12), 0.9f, 0.7f, Synth.Wind.Shofar, false, r));
        Add("shofar_curto", 0.45f, b => Synth.WindNote(b, 0, Synth.Hz(D - 12), 0.22f, 0.6f, Synth.Wind.Shofar, false, r));
        Add("shofar_longo", 2.6f, b =>
        {
            // tekiá gedolá: nota longa que salta para a quinta (Ap 8)
            Synth.WindNote(b, 0, Synth.Hz(D - 12), 0.8f, 0.7f, Synth.Wind.Shofar, false, r);
            Synth.WindNote(b, (int)(Synth.SR * 0.75f), Synth.Hz(D - 5), 1.6f, 0.75f, Synth.Wind.Shofar, false, r);
        });
        Add("cura", 0.9f, b => { int[] a = { 0, 2, 4, 7 }; for (int k = 0; k < 4; k++) Synth.Pluck(b, (int)(Synth.SR * k * 0.07f), Synth.Hz(Synth.Note(D, fr, a[k])), 0.5f, 0.45f, 0.9f, 0.997f, false, r); });
        Add("mana", 1.0f, b => { Synth.WindNote(b, 0, Synth.Hz(D + 4), 0.6f, 0.25f, Synth.Wind.Choir, false, r); Synth.Bell(b, 0, Synth.Hz(D + 24), 0.8f, 0.35f, false); });
        Add("crit", 0.4f, b => { Synth.Bell(b, 0, 1760f, 0.4f, 0.6f, false); Synth.Hit(b, 0, Synth.Drum.Ka, 0.5f, false, r); });
        Add("escudo_quebra", 0.6f, b =>
        {
            for (int k = 0; k < 6; k++) Synth.Bell(b, (int)(Synth.SR * k * 0.012f), 2000f + (float)r.NextDouble() * 3000f, 0.45f, 0.25f, false);
            Synth.Whoosh(b, 0, 0.3f, 5000f, 2000f, 0.5f, 0.02f, r);
        });
        Add("escudo", 0.6f, b => { Synth.Bell(b, 0, Synth.Hz(D + 12), 0.5f, 0.4f, false); Synth.Bell(b, (int)(Synth.SR * 0.08f), Synth.Hz(D + 19), 0.5f, 0.35f, false); });
        Add("po", 0.5f, b => { for (int k = 0; k < 7; k++) Synth.Whoosh(b, (int)(Synth.SR * k * 0.05f), 0.1f, 400f, 1500f, 0.5f, 0.1f, r); });
        // --- ritmo e impacto
        Add("impacto", 0.5f, b => { Synth.Hit(b, 0, Synth.Drum.War, 1f, false, r); Synth.Sweep(b, 0, 0.35f, 140f, 40f, 0.7f, 0f); });
        Add("emboscada", 1.0f, b =>
        {
            for (int k = 0; k < 6; k++) Synth.Hit(b, (int)(Synth.SR * k * 0.09f), k == 5 ? Synth.Drum.War : Synth.Drum.Doum, 0.5f + k * 0.08f, false, r);
        });
        Add("pisao", 0.6f, b => { Synth.Hit(b, 0, Synth.Drum.War, 1f, false, r); Synth.Whoosh(b, 0, 0.35f, 800f, 150f, 0.6f, 0.05f, r); });
        Add("laminas", 0.25f, b => Synth.Whoosh(b, 0, 0.22f, 1500f, 4500f, 0.4f, 0.5f, r));

        // --- cartas, relíquias e evolução
        Add("abrir_carta", 0.8f, b => { for (int k = 0; k < 6; k++) Synth.Pluck(b, (int)(Synth.SR * k * 0.045f), Synth.Hz(Synth.Note(D, fr, k)), 0.4f, 0.32f, 0.9f, 0.996f, false, r); });
        Add("carta", 0.6f, b => { Synth.Pluck(b, 0, Synth.Hz(D + 7), 0.3f, 0.5f, 0.9f, 0.997f, false, r); Synth.Pluck(b, (int)(Synth.SR * 0.08f), Synth.Hz(D + 12), 0.4f, 0.5f, 0.9f, 0.997f, false, r); });
        Add("embaralhar", 0.35f, b => { for (int k = 0; k < 5; k++) Synth.Whoosh(b, (int)(Synth.SR * k * 0.055f), 0.07f, 2000f, 5000f, 0.5f, 0.2f, r); });
        Add("reliquia", 1.8f, b =>
        {
            foreach (int st in new[] { 0, 7, 12, 16 }) Synth.WindNote(b, 0, Synth.Hz(D - 12 + st), 1.2f, 0.16f, Synth.Wind.Choir, false, r);
            Synth.Hit(b, 0, Synth.Drum.Riq, 0.4f, false, r);
            Synth.Bell(b, (int)(Synth.SR * 0.3f), Synth.Hz(D + 24), 1.2f, 0.3f, false);
        });
        Add("evolucao", 2.4f, b =>
        {
            foreach (int st in new[] { 0, 4, 7, 12 }) Synth.WindNote(b, 0, Synth.Hz(D - 12 + st), 1.6f, 0.15f, Synth.Wind.Choir, false, r);
            int[] a = { 0, 2, 4, 7, 9, 11 };
            for (int k = 0; k < 6; k++) Synth.Bell(b, (int)(Synth.SR * (0.15f + k * 0.09f)), Synth.Hz(Synth.Note(D + 12, fr, a[k])), 0.9f, 0.25f, false);
            Synth.WindNote(b, (int)(Synth.SR * 0.1f), Synth.Hz(D - 12), 1.2f, 0.35f, Synth.Wind.Shofar, false, r);
        });
        Add("moeda", 0.4f, b => { Synth.Bell(b, 0, 2637f, 0.25f, 0.35f, false); Synth.Bell(b, (int)(Synth.SR * 0.06f), 3520f, 0.3f, 0.35f, false); });
        Add("clique", 0.08f, b => Synth.Hit(b, 0, Synth.Drum.Ka, 0.6f, false, r));

        // --- eventos
        Add("rugido", 1.5f, b =>
        {
            Synth.WindNote(b, 0, 55f, 1.1f, 0.5f, Synth.Wind.Brass, false, r, 0f, 0.03f);
            Synth.WindNote(b, 0, 58f, 1.1f, 0.4f, Synth.Wind.Shofar, false, r);
            Synth.Whoosh(b, 0, 1.2f, 150f, 500f, 0.5f, 0.2f, r);
        });
        Add("vitoria", 2.0f, b =>
        {
            int[] f = { 0, 4, 7, 12 };
            for (int k = 0; k < 3; k++) Synth.WindNote(b, (int)(Synth.SR * k * 0.18f), Synth.Hz(D + f[k]), 0.16f, 0.35f, Synth.Wind.Brass, false, r);
            Synth.WindNote(b, (int)(Synth.SR * 0.54f), Synth.Hz(D + 12), 1.0f, 0.4f, Synth.Wind.Brass, false, r);
            Synth.Hit(b, (int)(Synth.SR * 0.54f), Synth.Drum.War, 0.8f, false, r);
            Synth.Hit(b, (int)(Synth.SR * 0.54f), Synth.Drum.Riq, 0.4f, false, r);
        });
        Add("lamento", 3.2f, b =>
        {
            int[] m = { 4, 3, 1, 1, 0 };
            float[] l = { 0.45f, 0.45f, 0.3f, 0.6f, 1.2f };
            float t = 0f;
            for (int k = 0; k < m.Length; k++) { Synth.WindNote(b, (int)(Synth.SR * t), Synth.Hz(Synth.Note(D, fr, m[k])), l[k], 0.4f, Synth.Wind.Ney, false, r); t += l[k]; }
            Synth.Reverb(b, 0.4f, 0.85f, false);
        });
        Add("julgamento", 1.4f, b => { Synth.Bell(b, 0, 98f, 1.3f, 0.9f, false); Synth.Whoosh(b, 0, 0.6f, 300f, 1200f, 0.4f, 0.2f, r); });
        Add("ressurreicao", 2.4f, b =>
        {
            foreach (int st in new[] { 0, 4, 7, 12, 16 }) Synth.WindNote(b, 0, Synth.Hz(D - 12 + st), 1.8f, 0.14f, Synth.Wind.Choir, false, r);
            Synth.WindNote(b, (int)(Synth.SR * 0.2f), Synth.Hz(D - 5), 1.2f, 0.35f, Synth.Wind.Shofar, false, r);
        });
        Add("caixa", 0.5f, b => { Synth.Pluck(b, 0, Synth.Hz(D), 0.2f, 0.5f, 0.8f, 0.996f, false, r); Synth.Hit(b, 0, Synth.Drum.Riq, 0.5f, false, r); });

        // --- mini-jogos
        Add("doum", 0.45f, b => Synth.Hit(b, 0, Synth.Drum.Doum, 1f, false, r));
        Add("tek", 0.1f, b => Synth.Hit(b, 0, Synth.Drum.Tek, 0.9f, false, r));
        Add("funda", 0.3f, b => Synth.Whoosh(b, 0, 0.28f, 900f, 2400f, 0.8f, 0.6f, r));
        Add("giro", 0.18f, b => Synth.Whoosh(b, 0, 0.16f, 500f, 1400f, 0.4f, 0.5f, r));
        Add("pedra", 0.3f, b => { Synth.Hit(b, 0, Synth.Drum.Tof, 0.9f, false, r); Synth.Hit(b, 0, Synth.Drum.Ka, 0.6f, false, r); });
        Add("pulo", 0.15f, b => Synth.Sweep(b, 0, 0.12f, 280f, 640f, 0.4f, 0.1f));
        Add("mola", 0.35f, b => Synth.Sweep(b, 0, 0.3f, 300f, 1300f, 0.45f, 0.2f));
    }

    public static float[] RenderRecipe(float dur, Action<float[]> render)
    {
        var data = new float[Math.Max(1, (int)(Synth.SR * dur))];
        render(data);
        Synth.Normalize(data, 0.9f);
        // fade-out nos últimos milissegundos (sem estalos)
        int f = Math.Min(200, data.Length);
        for (int i = 0; i < f; i++) data[data.Length - 1 - i] *= i / (float)f;
        return data;
    }

    void AddClip(string id, float dur, Action<float[]> render)
    {
        var data = RenderRecipe(dur, render);
        var clip = AudioClip.Create("sfx_" + id, data.Length, 1, Synth.SR, false);
        clip.SetData(data, 0);
        bank[id] = clip;
    }

    public AudioClip Get(string id) => bank.TryGetValue(id, out var c) ? c : null;

    /// Toca um efeito com variação de tom e limite de repetição (evita "metralhadora" de sons).
    public void Play(string id, float vol = 1f, float pitchVar = 0.05f, float minGap = 0.04f, float pitch = 1f)
    {
        if (!bank.TryGetValue(id, out var clip)) return;
        float now = Time.unscaledTime;
        if (lastTime.TryGetValue(id, out var last) && now - last < minGap) return;
        lastTime[id] = now;
        var s = pool[next];
        next = (next + 1) % pool.Count;
        s.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchVar, pitchVar));
        s.PlayOneShot(clip, vol * masterVolume);
    }
}
