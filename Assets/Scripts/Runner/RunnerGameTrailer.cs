using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ABERTURA (trailer de ~40 s): aparece sozinha na primeira vez que o jogo abre e pode ser vista de novo em OPÇÕES → VER ABERTURA.
///   1. A VISÃO — noite em Babilônia, a tempestade de fogo e as rodas cheias de olhos (Ez 1).
///   2. AS TREVAS — céu vermelho, o exército das trevas e Satanás surgindo do abismo.
///   3. O ENVIO — o feixe de luz e o cavalo-anjo descendo em Jerusalém (Ez 2:3).
///   4-6. DEMO — o jogo se joga sozinho (piloto automático, sem dano): Jerusalém, Egito e Roma com um chefe.
///   7. LOGO — EZEQUIEL.
/// Qualquer toque / Enter / Esc pula para o menu. Nada da demo conta (pontos, talentos, Livro da Vida).
/// </summary>
public partial class RunnerGame
{
    const string TrailerKey = "runner_trailer_seen_v1";
    public static bool TrailerSeen => PlayerPrefs.GetInt(TrailerKey, 0) == 1;

    // início de cada trecho (s): visão, trevas, envio, Jerusalém, Egito, Roma+chefe, logo, fim
    static readonly float[] TrSeg = { 0f, 5f, 9.6f, 14.2f, 20.6f, 27f, 34f, 41f };

    [HideInInspector] public bool trailerActive;
    public bool TrailerAuto => trailerActive && state == RunnerState.Playing;
    float trailerT;
    int trailerSeg = -1;
    float trailerFlash;
    float uiBlockUntil;
    Transform trWheels, trStorm, trCore, trSatan;
    readonly List<Transform> trStars = new List<Transform>();

    // piloto automático
    float autoMoveCd, autoWanderT;
    readonly float[] autoDanger = new float[3];
    readonly bool[] autoLow = new bool[3];
    readonly float[] autoTarget = new float[3];

    // ================================================================== início / fim

    void StartTrailer()
    {
        PlayerPrefs.SetInt(TrailerKey, 1);
        PlayerPrefs.Save();
        menuPanel = 0;
        Meta.DailyMode = false;
        ResetRun();
        trailerActive = true;
        trailerT = 0f;
        trailerSeg = -1;
        trailerFlash = 0f;
        EnterTrailerSeg(0);
    }

    /// Termina (ou pula) a abertura e volta ao menu com tudo limpo.
    void FinishTrailer()
    {
        trailerActive = false;
        trailerSeg = -1;
        DevResetToRunning();   // desliga chefe, mini-jogos e voo, se houver
        CineClear();
        trStars.Clear();
        trWheels = trStorm = trCore = trSatan = null;
        ResetRun();
        prophet = Meta.Selected;
        if (trackRoot != null) trackRoot.gameObject.SetActive(true);
        ApplyAtmosphere();
        player.transform.rotation = Quaternion.identity;
        player.SetVisible(true);
        player.invuln = 0f;
        if (cam != null) cam.fieldOfView = 60f;
        shake = 0f;
        uiBlockUntil = Time.unscaledTime + 0.35f;
        state = RunnerState.Menu;
    }

    /// Chamado no Update antes de tudo. Devolve true se a abertura acabou neste quadro (para o clique não iniciar o jogo).
    bool UpdateTrailer(float udt)
    {
        trailerT += udt;
        if (trailerT > 0.6f && (ConfirmPressed() || PausePressed())) { FinishTrailer(); return true; }
        if (trailerT >= TrSeg[TrSeg.Length - 1]) { FinishTrailer(); return true; }

        int seg = 0;
        while (seg < TrSeg.Length - 2 && trailerT >= TrSeg[seg + 1]) seg++;
        if (seg != trailerSeg) EnterTrailerSeg(seg);

        // a demo nunca para em telas de escolha
        if (state == RunnerState.LevelUp || state == RunnerState.Choice || state == RunnerState.Paused) state = RunnerState.Playing;
        if (trailerFlash > 0f) trailerFlash -= udt * 2.5f;

        float lt = trailerT - TrSeg[seg];
        switch (seg)
        {
            case 0: UpdateVision(lt, udt); break;
            case 1: UpdateDarkness(lt, udt); break;
            case 2:
                // a mesma descida da intro, um pouco mais rápida
                cineT = lt * (IntroLen / (TrSeg[3] - TrSeg[2]));
                UpdateIntro(udt * (IntroLen / (TrSeg[3] - TrSeg[2])));
                break;
            case 6: UpdateLogo(lt, udt); break;
            default:
                nextCardScore = int.MaxValue / 4;   // sem subir de nível na demo
                if (lives < maxLives) lives = maxLives;
                break;
        }
        return false;
    }

    void EnterTrailerSeg(int seg)
    {
        trailerSeg = seg;
        CineClear();
        trStars.Clear();
        trWheels = trStorm = trCore = trSatan = null;
        switch (seg)
        {
            case 0: BuildVision(); break;
            case 1: BuildDarkness(); break;
            case 2:
                if (trackRoot != null) trackRoot.gameObject.SetActive(true);
                ApplyAtmosphere();
                StartIntro();
                cineKind = 2;
                break;
            case 3: StartTrailerRun(); break;
            case 4: TrailerCut(Biomes.Egypt, 75f, "anjo_guarda", "relampago"); break;
            case 5:
                TrailerCut(Biomes.Rome, 95f, "granizo", null);
                bossesDefeated = 3;
                bossPending = true;
                bossWarn = 0.4f;
                break;
            case 6: BuildLogo(); break;
        }
    }

    // ================================================================== cenas

    void TrailerCinemaState(Color sky, Color fog, float fogStart, float fogEnd, Color sun, float sunI)
    {
        state = RunnerState.Cutscene;
        cineKind = 2;
        cineT = 0f;
        if (trackRoot != null) trackRoot.gameObject.SetActive(false);
        player.SetVisible(false);
        if (cam != null) { cam.backgroundColor = sky; cam.fieldOfView = 60f; }
        RenderSettings.fogColor = fog;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.color = sun; l.intensity = sunI; }
        cineFocus = new Vector3(0f, 0f, player.transform.position.z);
    }

    GameObject TrPrim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, bool glow = false)
    {
        var g = Prim(t, parent, pos, scale, c, glow);
        if (parent == null) cineObjs.Add(g);
        return g;
    }

    Transform TrRoot(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        cineObjs.Add(go);
        go.transform.position = pos;
        return go.transform;
    }

    // ---------------------------------------------------------------- 1. a visão (Ez 1)

    void BuildVision()
    {
        TrailerCinemaState(new Color(0.02f, 0.02f, 0.06f), new Color(0.02f, 0.02f, 0.06f), 80f, 400f, new Color(0.6f, 0.65f, 1f), 0.2f);
        float z = cineFocus.z;
        // estrelas
        int nStars = IsMobile ? 35 : 70;
        for (int i = 0; i < nStars; i++)
        {
            var d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y) * 0.85f + 0.12f;
            d.z = Mathf.Abs(d.z);
            var st = TrPrim(PrimitiveType.Sphere, null, cineFocus + d.normalized * 150f, Vector3.one * Random.Range(0.5f, 1.2f), new Color(0.9f, 0.92f, 1f), true);
            trStars.Add(st.transform);
        }
        // chão escuro do rio Quebar
        TrPrim(PrimitiveType.Cube, null, new Vector3(0f, -0.1f, z + 40f), new Vector3(200f, 0.2f, 200f), new Color(0.03f, 0.03f, 0.05f));
        TrPrim(PrimitiveType.Cube, null, new Vector3(0f, 0.01f, z + 20f), new Vector3(200f, 0.05f, 6f), new Color(0.08f, 0.12f, 0.25f), true);

        var center = new Vector3(0f, 24f, z + 34f);
        // nuvem de tempestade girando ("um vento tempestuoso vinha do norte", Ez 1:4)
        trStorm = TrRoot("Tempestade", center);
        for (int i = 0; i < 16; i++)
        {
            float a = i * Mathf.PI * 2f / 16f;
            float r = 11f + Random.Range(-1.5f, 1.5f);
            TrPrim(PrimitiveType.Sphere, trStorm, new Vector3(Mathf.Cos(a) * r, Random.Range(-1.5f, 1.5f), Mathf.Sin(a) * r * 0.5f + 3f),
                new Vector3(Random.Range(6f, 9f), Random.Range(3f, 4.5f), Random.Range(5f, 7f)), new Color(0.16f, 0.15f, 0.2f));
        }
        // fogo no meio
        trCore = TrPrim(PrimitiveType.Sphere, null, center, Vector3.one * 6f, new Color(1f, 0.6f, 0.15f), true).transform;
        // as rodas, uma dentro da outra, cheias de olhos (Ez 1:16-18)
        trWheels = TrRoot("Rodas", center);
        for (int w = 0; w < 2; w++)
        {
            var ring = new GameObject("Roda" + w).transform;
            ring.SetParent(trWheels, false);
            ring.localRotation = Quaternion.Euler(0f, w * 90f, 0f);
            const int seg = 22;
            float R = 8.5f;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var p = new Vector3(Mathf.Cos(a) * R, Mathf.Sin(a) * R, 0f);
                var b = Prim(PrimitiveType.Cube, ring, p, new Vector3(0.6f, 2.5f, 0.6f), new Color(1f, 0.8f, 0.3f), true);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                if (i % 3 == 0)
                {
                    Prim(PrimitiveType.Sphere, ring, p * 0.9f, Vector3.one * 1.1f, new Color(0.95f, 0.97f, 1f), true);
                    Prim(PrimitiveType.Sphere, ring, p * 0.9f + new Vector3(0f, 0f, -0.45f), Vector3.one * 0.45f, new Color(0.15f, 0.35f, 0.9f));
                }
            }
        }
        Sfx("shofar_longo", 0.6f, 0f, 1f, 0.8f);
    }

    void UpdateVision(float t, float dt)
    {
        float z = cineFocus.z;
        var center = new Vector3(0f, 24f, z + 34f);
        if (trStorm != null) trStorm.Rotate(0f, 0f, -25f * dt, Space.Self);
        if (trWheels != null)
        {
            float appear = Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 1.6f);
            trWheels.localScale = Vector3.one * Mathf.Max(0.01f, appear);
            trWheels.GetChild(0).Rotate(0f, 0f, 70f * dt, Space.Self);
            trWheels.GetChild(1).Rotate(0f, 0f, -55f * dt, Space.Self);
            trWheels.Rotate(0f, 18f * dt, 0f, Space.World);
        }
        if (trCore != null) trCore.localScale = Vector3.one * (5.5f + Mathf.Sin(t * 9f) * 0.8f);
        // relâmpagos
        if (t > 0.6f && Random.value < dt * 0.9f)
        {
            trailerFlash = Mathf.Max(trailerFlash, 0.45f);
            PlayBoom();
            Explode(center + Random.insideUnitSphere * 8f, new Color(1f, 0.85f, 0.4f), 4);
        }
        // câmera: do chão, subindo devagar em direção às rodas
        float k = Mathf.SmoothStep(0f, 1f, t / (TrSeg[1] - TrSeg[0]));
        var pos = Vector3.Lerp(new Vector3(-3f, 1.4f, z - 14f), new Vector3(1.5f, 7f, z + 4f), k);
        cam.transform.position = pos;
        cam.transform.LookAt(Vector3.Lerp(center + Vector3.up * 4f, center, k));
    }

    // ---------------------------------------------------------------- 2. as trevas

    void BuildDarkness()
    {
        var red = new Color(0.22f, 0.03f, 0.02f);
        TrailerCinemaState(red, red, 25f, 140f, new Color(1f, 0.35f, 0.2f), 0.6f);
        float z = cineFocus.z;
        TrPrim(PrimitiveType.Cube, null, new Vector3(0f, -0.1f, z + 40f), new Vector3(200f, 0.2f, 200f), new Color(0.1f, 0.04f, 0.03f));
        // colunas de fogo
        for (int i = 0; i < 8; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float h = Random.Range(8f, 18f);
            TrPrim(PrimitiveType.Cylinder, null, new Vector3(side * Random.Range(10f, 26f), h / 2f, z + Random.Range(18f, 70f)),
                new Vector3(1.4f, h / 2f, 1.4f), new Color(1f, 0.4f, 0.08f), true);
        }
        // o exército das trevas
        var pal = Biomes.Sheol.bossPalette ?? RunnerCreature.BossPalette;
        int cols = IsMobile ? 4 : 6;
        for (int row = 0; row < 2; row++)
            for (int c = 0; c < cols; c++)
            {
                float x = (c - (cols - 1) / 2f) * 2.6f + (row % 2) * 1.3f;
                var t = TrRoot("Soldado", new Vector3(x, 0f, z + 20f + row * 4f));
                RunnerCreature.Build(t, 0.85f, 0f, pal, true);
            }
        // Satanás, saindo do abismo
        trSatan = TrRoot("Satanas", new Vector3(0f, -16f, z + 46f));
        var cr = RunnerCreature.Build(trSatan, 2.3f, -1.8f, SatanPalette, true);
        BuildSatanExtras(trSatan, cr);
        trSatan.localScale = Vector3.one * 1.7f;
        TrPrim(PrimitiveType.Sphere, null, new Vector3(0f, 0f, z + 46f), new Vector3(16f, 1f, 16f), new Color(1f, 0.25f, 0.05f), true);   // o abismo
        Sfx("lamento", 0.8f, 0f, 1f);
    }

    void UpdateDarkness(float t, float dt)
    {
        float z = cineFocus.z;
        if (trSatan != null)
        {
            float k = EaseOutCubic(Mathf.Clamp01((t - 0.8f) / 2.4f));
            trSatan.position = new Vector3(0f, Mathf.Lerp(-16f, 3.1f, k), z + 46f);
            if (t > 0.8f && t - dt <= 0.8f) { Sfx("rugido", 1f, 0f, 1f, 0.7f); shake = 1f; }
            if (t > 3.2f && t - dt <= 3.2f) { PlayBoom(); shake = 0.6f; FxSphere(trSatan.position + Vector3.up * 3f, 12f, new Color(1f, 0.2f, 0.05f)); }
        }
        if (Random.value < dt * 6f)
            Explode(new Vector3(Random.Range(-12f, 12f), Random.Range(0.5f, 6f), z + Random.Range(16f, 44f)), Random.value < 0.6f ? new Color(1f, 0.45f, 0.1f) : new Color(0.15f, 0.02f, 0.02f), 2);
        // câmera baixa, recuando e subindo o olhar até Satanás
        float c = Mathf.SmoothStep(0f, 1f, t / (TrSeg[2] - TrSeg[1]));
        var pos = Vector3.Lerp(new Vector3(2.5f, 1.1f, z + 11f), new Vector3(-1f, 2.2f, z + 4f), c);
        cam.transform.position = pos + (shake > 0f ? Random.insideUnitSphere * shake * 0.35f : Vector3.zero);
        cam.transform.LookAt(new Vector3(0f, Mathf.Lerp(2f, 9f, c), z + 40f));
        if (shake > 0f) shake -= dt * 1.4f;
    }

    // ---------------------------------------------------------------- 4-6. demo

    void StartTrailerRun()
    {
        CineClear();
        ResetRun();
        prophet = Meta.Selected;
        stats.weapon = prophet.weapon();
        if (player.weaponModel != null) player.weaponModel.currentId = "";
        foreach (var id in new[] { "multi", "multi", "explosivo", "laminas", "drone", "fogoceu", "corrente", "cadencia" }) TrailerCard(id);
        maxLives = lives = 5;
        runTime = 55f;
        trailerFlash = 1f;   // clarão branco do pouso
        state = RunnerState.Playing;
        player.inputLock = 0f;
        player.invuln = 0f;
        player.transform.rotation = Quaternion.identity;
        player.SetVisible(true);
        autoMoveCd = 0f;
        autoWanderT = 1f;
    }

    /// Corte para outra região, já em movimento.
    void TrailerCut(BiomeTheme biome, float time, string card1, string card2)
    {
        DevResetToRunning();
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        SetBiome(biome, false);
        if (card1 != null) TrailerCard(card1);
        if (card2 != null) TrailerCard(card2);
        runTime = time;
        nextSpawnZ = player.transform.position.z + 22f;
        state = RunnerState.Playing;
    }

    /// Carta de mentira: entra na build sem marcar nada no Livro da Vida.
    void TrailerCard(string id)
    {
        var c = FindCard(id);
        if (c == null) return;
        cardStacks[c.id] = Stacks(c) + 1;
        history.Add(c);
        c.apply(this);
    }

    /// Piloto automático da demo: desvia do que machuca, pula o que é baixo e caça alvos para atirar.
    public void AutoPilot(RunnerPlayer p, out bool left, out bool right, out bool jump)
    {
        left = right = jump = false;
        float dt = Time.deltaTime;
        autoMoveCd -= dt;
        autoWanderT -= dt;
        var pp = p.transform.position;
        float look = Mathf.Max(12f, speed * 0.9f);
        for (int i = 0; i < 3; i++) { autoDanger[i] = 999f; autoLow[i] = true; autoTarget[i] = 999f; }

        foreach (var o in obstacles)
        {
            if (o == null || o.dead) continue;
            float dz = o.transform.position.z - o.half.z - pp.z;
            if (dz < -0.5f || dz > look + 25f) continue;
            var t = o.type;
            if (t == ObType.Coin || t == ObType.Ring || t == ObType.Health || t == ObType.Boost || t == ObType.Platform
                || t == ObType.HouseOpen || t == ObType.Note || t == ObType.Shockwave || t == ObType.Boss
                || t == ObType.PlaneBox || t == ObType.CarBox || t == ObType.ShipBox || t == ObType.AngelBox
                || t == ObType.BabelBox || t == ObType.JerichoBox || t == ObType.GoliathBox) continue;
            float ox = o.transform.position.x;
            int lc = Mathf.Clamp(Mathf.RoundToInt(ox / LaneWidth) + 1, 0, 2);
            int l0 = Mathf.Clamp(Mathf.RoundToInt((ox - o.half.x + 0.7f) / LaneWidth) + 1, 0, 2);
            int l1 = Mathf.Clamp(Mathf.RoundToInt((ox + o.half.x - 0.7f) / LaneWidth) + 1, 0, 2);
            if (l1 < l0) l0 = l1 = lc;
            bool target = o.Shootable && !o.Indestructible;
            if (target) for (int l = l0; l <= l1; l++) autoTarget[l] = Mathf.Min(autoTarget[l], dz);
            if (target && o.hp <= stats.Damage * 2.5f && dz > 4f) continue;   // morre antes de chegar
            if (dz > look) continue;
            float top = o.transform.position.y + o.half.y;
            for (int l = l0; l <= l1; l++)
                if (dz < autoDanger[l]) { autoDanger[l] = dz; autoLow[l] = top < 2.4f; }
        }

        int cur = p.lane, want = cur;
        bool grounded = p.feetY <= p.groundY + 0.05f;
        if (autoDanger[cur] < look)
        {
            float best = autoDanger[cur];
            for (int d = -1; d <= 1; d += 2)
            {
                int l = cur + d;
                if (l < 0 || l > 2) continue;
                if (autoDanger[l] > best + 3f) { best = autoDanger[l]; want = l; }
            }
            if (want == cur && autoLow[cur] && grounded && autoDanger[cur] < Mathf.Max(4.5f, speed * 0.32f)) jump = true;
        }
        else if (autoWanderT <= 0f)
        {
            autoWanderT = Random.Range(1.1f, 2.2f);
            float bt = autoTarget[cur];
            for (int d = -1; d <= 1; d += 2)
            {
                int l = cur + d;
                if (l < 0 || l > 2 || autoDanger[l] < look) continue;
                if (autoTarget[l] < bt - 5f) { bt = autoTarget[l]; want = l; }
            }
            if (want == cur && grounded && Random.value < 0.3f) jump = true;   // um pulo de vez em quando dá vida
        }
        if (want != cur && autoMoveCd <= 0f)
        {
            if (want < cur) left = true; else right = true;
            autoMoveCd = 0.22f;
        }
    }

    // ---------------------------------------------------------------- 7. logo

    void BuildLogo()
    {
        DevResetToRunning();
        ResetRun();
        var bg = new Color(0.07f, 0.045f, 0.02f);
        TrailerCinemaState(bg, bg, 30f, 200f, new Color(1f, 0.9f, 0.7f), 1.2f);
        speed = 0f;   // o cavalo fica parado, só girando
        float z = cineFocus.z;
        player.SetVisible(true);
        player.transform.position = new Vector3(0f, 0.9f, z);
        player.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
        TrPrim(PrimitiveType.Cylinder, null, new Vector3(0f, -0.05f, z), new Vector3(6f, 0.05f, 6f), new Color(1f, 0.8f, 0.35f), true);
        cineRays = TrRoot("Gloria", new Vector3(0f, 2.2f, z + 7f));
        for (int i = 0; i < 14; i++)
        {
            var ray = Prim(PrimitiveType.Cube, cineRays, Vector3.zero, new Vector3(0.35f, 60f, 0.1f), new Color(1f, 0.82f, 0.4f), true);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 180f / 14f);
        }
        Sfx("shofar_longo", 1f, 0f, 1f);
        Sfx("evolucao", 0.7f, 0f, 1f);
    }

    void UpdateLogo(float t, float dt)
    {
        float z = cineFocus.z;
        if (cineRays != null) cineRays.Rotate(0f, 0f, 12f * dt, Space.Self);
        player.transform.rotation = Quaternion.Euler(0f, 200f - t * 22f, 0f);
        if (Random.value < dt * 5f)
            Explode(new Vector3(Random.Range(-3f, 3f), Random.Range(0.5f, 3.5f), z + Random.Range(-1f, 3f)), new Color(1f, 0.85f, 0.4f), 1);
        float k = Mathf.SmoothStep(0f, 1f, t / (TrSeg[7] - TrSeg[6]));
        cam.transform.position = Vector3.Lerp(new Vector3(0f, 1.6f, z - 6.5f), new Vector3(0f, 2.4f, z - 8.5f), k);
        cam.transform.LookAt(new Vector3(0f, 1.9f, z));
    }

    // ================================================================== textos e transições

    void DrawTrailer(float s, float W, float H)
    {
        float bar = 80 * s;
        int seg = Mathf.Max(0, trailerSeg);
        float lt = trailerT - TrSeg[seg];
        bool demo = seg >= 3 && seg <= 5;
        Box(new Rect(0, 0, W, bar), Color.black);
        Box(new Rect(0, H - bar, W, bar), Color.black);

        var verse = Sty(midStyle, fs: Mathf.RoundToInt(34 * s), ww: 1, fst: FontStyle.Italic);
        var line = Sty(midStyle, fs: Mathf.RoundToInt(38 * s), ww: 1);
        var title = Sty(bigStyle, fs: Mathf.RoundToInt(64 * s), al: TextAnchor.LowerLeft);
        var sub = Sty(midStyle, fs: Mathf.RoundToInt(28 * s), al: TextAnchor.UpperLeft);
        var gold = new Color(1f, 0.85f, 0.3f);
        float len = TrSeg[seg + 1] - TrSeg[seg];

        switch (seg)
        {
            case 0:
                ShadowLabel(new Rect(W * 0.1f, H * 0.62f, W * 0.8f, 60 * s), "Babilônia, às margens do rio Quebar...", line, new Color(1f, 1f, 1f, Fade(lt, 0.4f, 2.6f)));
                ShadowLabel(new Rect(W * 0.1f, H * 0.62f, W * 0.8f, 90 * s), "\"Abriram-se os céus, e eu tive visões de Deus.\"  (Ez 1:1)", verse, new Color(1f, 0.95f, 0.8f, Fade(lt, 2.6f, len)));
                break;
            case 1:
                ShadowLabel(new Rect(W * 0.1f, bar + 30 * s, W * 0.8f, 60 * s), "Mas as trevas cobriram a terra, de Jerusalém ao Sheol.", line, new Color(1f, 0.85f, 0.8f, Fade(lt, 0.3f, 2.4f)));
                ShadowLabel(new Rect(W * 0.1f, bar + 30 * s, W * 0.8f, 60 * s), "O acusador reuniu seus exércitos.", line, new Color(1f, 0.6f, 0.5f, Fade(lt, 2.4f, len)));
                break;
            case 2:
                ShadowLabel(new Rect(W * 0.1f, bar + 30 * s, W * 0.8f, 90 * s), "\"Filho do homem, eu te envio.\"  (Ez 2:3)", verse, new Color(1f, 1f, 1f, Fade(lt, 0.8f, len)));
                break;
            case 3: DemoCaption(s, W, H, bar, lt, len, title, sub, "CORRA, DESVIE E LUTE", "Jerusalém  •  Egito  •  Roma  •  Sheol"); break;
            case 4:
                DemoCaption(s, W, H, bar, lt, len, title, sub, "MONTE SEU BARALHO", Deck.Collectible.Count + " cartas  •  5 famílias  •  evoluções");
                DrawTrailerCards(s, W, H, bar, lt, len);
                break;
            case 5: DemoCaption(s, W, H, bar, lt, len, title, sub, "ENFRENTE OS GIGANTES", finalBossNumber + " chefes até Satanás"); break;
            case 6:
            {
                float a = Mathf.Clamp01(lt / 0.8f);
                ShadowLabel(new Rect(0, bar + 10 * s, W, 150 * s), "EZEQUIEL", Sty(bigStyle, fs: Mathf.RoundToInt(130 * s)), new Color(gold.r, gold.g, gold.b, a));
                ShadowLabel(new Rect(0, bar + 150 * s, W, 50 * s), "Um roguelike bíblico", Sty(midStyle, fs: Mathf.RoundToInt(34 * s)), new Color(1f, 0.95f, 0.85f, Mathf.Clamp01((lt - 0.8f) / 0.8f)));
                float pa = Mathf.Clamp01((lt - 2.5f) / 0.6f) * (0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f));
                ShadowLabel(new Rect(0, H - bar - 70 * s, W, 50 * s), RunnerTouch.UseTouchUI ? "Toque para começar" : "Pressione Enter para começar", Sty(midStyle, fs: Mathf.RoundToInt(30 * s)), new Color(1f, 1f, 1f, pa));
                break;
            }
        }

        if (demo)
            ShadowLabel(new Rect(20 * s, 20 * s, 300 * s, 40 * s), "DEMONSTRAÇÃO", Sty(cardSmall, al: TextAnchor.MiddleLeft), new Color(1f, 1f, 1f, 0.55f));
        if (trailerT > 0.6f && seg < 6)
            ShadowLabel(new Rect(0, H - bar + 15 * s, W - 30 * s, 50 * s), RunnerTouch.UseTouchUI ? "toque para pular" : "Enter / Esc para pular",
                Sty(cardSmall, al: TextAnchor.MiddleRight), new Color(1f, 1f, 1f, 0.55f));

        // transições: escurece nos cortes; clarão branco ao pousar em Jerusalém
        float black = 0f;
        for (int i = 1; i < TrSeg.Length - 1; i++)
        {
            if (i == 3) continue;
            black = Mathf.Max(black, 1f - Mathf.Abs(trailerT - TrSeg[i]) / 0.3f);
        }
        black = Mathf.Max(black, 1f - trailerT / 0.7f);
        black = Mathf.Max(black, 1f - (TrSeg[TrSeg.Length - 1] - trailerT) / 0.6f);
        float white = Mathf.Max(trailerFlash, 1f - Mathf.Abs(trailerT - TrSeg[3]) / 0.35f);
        if (white > 0f) Box(new Rect(0, 0, W, H), new Color(1f, 0.97f, 0.88f, Mathf.Clamp01(white)));
        if (black > 0f) Box(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, Mathf.Clamp01(black)));
    }

    void DemoCaption(float s, float W, float H, float bar, float lt, float len, GUIStyle title, GUIStyle sub, string t1, string t2)
    {
        float a = Fade(lt, 0.4f, len - 0.2f);
        if (a <= 0f) return;
        float slide = (1f - Mathf.Clamp01((lt - 0.4f) / 0.5f)) * 60f * s;
        Box(new Rect(0, H - bar - 150 * s, W * 0.62f, 140 * s), new Color(0f, 0f, 0f, 0.45f * a));
        ShadowLabel(new Rect(40 * s - slide, H - bar - 150 * s, W, 80 * s), t1, title, new Color(1f, 0.85f, 0.3f, a));
        ShadowLabel(new Rect(44 * s - slide, H - bar - 66 * s, W, 50 * s), t2, sub, new Color(1f, 1f, 1f, a));
    }

    void DrawTrailerCards(float s, float W, float H, float bar, float lt, float len)
    {
        string[] ids = { "laminas", "fogoceu", "querubins" };
        float cw = 150 * s, ch = 200 * s;
        for (int i = 0; i < ids.Length; i++)
        {
            var c = FindCard(ids[i]);
            if (c == null) continue;
            float a = Fade(lt, 0.8f + i * 0.35f, len - 0.2f);
            if (a <= 0f) continue;
            float slide = (1f - Mathf.Clamp01((lt - 0.8f - i * 0.35f) / 0.4f)) * 220f * s;
            float x = W - (ids.Length - i) * (cw + 18 * s) - 20 * s + slide;
            float y = H - bar - ch - 30 * s + (i == 1 ? -18 * s : 0f);
            var col = CardDB.RarityColor(c.rarity);
            Box(new Rect(x + 4 * s, y + 6 * s, cw, ch), new Color(0f, 0f, 0f, 0.5f * a));
            Box(new Rect(x - 3 * s, y - 3 * s, cw + 6 * s, ch + 6 * s), new Color(col.r, col.g, col.b, a));
            Box(new Rect(x, y, cw, ch), new Color(0.08f, 0.06f, 0.1f, 0.95f * a));
            CardIcons.Draw(new Rect(x + 15 * s, y + 14 * s, cw - 30 * s, cw - 30 * s), c, new Color(1f, 1f, 1f, a));
            ShadowLabel(new Rect(x + 6 * s, y + cw - 6 * s, cw - 12 * s, ch - cw), c.name, Sty(cardSmall, fs: Mathf.RoundToInt(19 * s), ww: 1), new Color(1f, 1f, 1f, a));
        }
    }

    string TrailerMusic()
    {
        switch (trailerSeg)
        {
            case 0: return "noite";
            case 1: return "satanas";
            case 2: case 3: return "jerusalem";
            case 4: return "egito";
            case 5: return "chefe";
            default: return "vitoria";
        }
    }
}
