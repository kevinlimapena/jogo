using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mini-jogos novos: As Muralhas de Jericó (ritmo, Js 6) e Davi e Golias (funda, 1Sm 17).
/// </summary>
public partial class RunnerGame
{
    // ================================================================== estado

    [Header("Muralhas de Jericó / Davi e Golias")]
    public bool enableJerichoBox = true;
    public bool enableGoliathBox = true;
    public float jerichoSpeed = 22f;

    [HideInInspector] public bool jericho;
    [HideInInspector] public bool goliath;
    RunnerGoliath goliathGame;
    int goliathsCleared, jerichosCleared;

    int jeriStyle, jeriGenBeat, jeriPassed, jeriHits, jeriTotal, jeriCombo, jeriBest, jeriLastLane;
    float jeriGenZ, jeriWallZ, jeriPulse;
    bool jeriWallSpawned, jeriResolved;
    readonly List<float> jeriRows = new List<float>();
    readonly List<GameObject> jeriDecor = new List<GameObject>();
    Transform jeriArk, jeriWall;
    const int JeriBeatsPerLap = 8, JeriLaps = 7;

    public static readonly string[] JerichoStyles = { "AS SETE VOLTAS", "OS ÍDOLOS DE CANAÃ", "O SÉTIMO DIA" };
    static int JerichoNextStyle => PlayerPrefs.GetInt("jericho_plays", 0) % 3;

    float JeriAccuracy => jeriTotal > 0 ? (float)jeriHits / jeriTotal : 0f;
    int JeriLap => Mathf.Clamp(jeriPassed / JeriBeatsPerLap + 1, 1, JeriLaps);

    void ResetMinigames()
    {
        if (goliathGame != null && goliathGame.Active) goliathGame.End();
        goliath = false;
        if (jericho) CleanupJericho();
        jericho = false;
        goliathsCleared = 0;
        jerichosCleared = 0;
    }

    // ================================================================== caixas

    void SpawnJerichoBox(int lane, float z)
    {
        var c = new Color(1f, 0.8f, 0.4f);
        var o = MakeObstacle("JerichoBox", ObType.JerichoBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.55f, 0.45f, 0.3f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), c);
        BuildShofar(t, new Vector3(0f, 0.8f, 0f), 0.6f, c);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.62f), new Vector3(0.9f, 0.15f, 0.05f), c, true);
        o.Init();
    }

    void SpawnGoliathBox(int lane, float z)
    {
        var c = new Color(0.7f, 0.9f, 0.5f);
        var o = MakeObstacle("GoliathBox", ObType.GoliathBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.4f, 0.35f, 0.25f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), c);
        // cinco seixos em cima
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Sphere, t, new Vector3((i - 2) * 0.2f, 0.7f, 0f), Vector3.one * 0.17f, new Color(0.75f, 0.73f, 0.68f));
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.62f), new Vector3(0.12f, 0.8f, 0.05f), c, true);
        o.Init();
    }

    void BuildShofar(Transform parent, Vector3 at, float scale, Color glow)
    {
        var horn = new Color(0.85f, 0.72f, 0.5f);
        for (int k = 0; k < 4; k++)
        {
            float a = k * 0.5f;
            var seg = Prim(PrimitiveType.Cube, parent, at + new Vector3((k - 1.5f) * 0.32f * scale, Mathf.Sin(a) * 0.25f * scale, 0f),
                new Vector3(0.36f * scale, (0.14f + k * 0.05f) * scale, (0.14f + k * 0.05f) * scale), k == 3 ? glow : horn, k == 3);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, 15f + k * 10f);
        }
    }

    // ================================================================== Muralhas de Jericó

    void StartJericho()
    {
        ClearTrackForMinigame();
        int plays = PlayerPrefs.GetInt("jericho_plays", 0);
        jeriStyle = plays % 3;
        PlayerPrefs.SetInt("jericho_plays", plays + 1);
        PlayerPrefs.Save();

        jericho = true;
        jeriGenBeat = jeriPassed = jeriHits = jeriTotal = jeriCombo = jeriBest = 0;
        jeriLastLane = 1;
        jeriPulse = 0f;
        jeriWallSpawned = jeriResolved = false;
        jeriRows.Clear();
        float pz = player.transform.position.z;
        jeriGenZ = pz + 45f;
        player.invuln = Mathf.Max(player.invuln, 1.5f);

        // entardecer em Canaã
        var dusk = new Color(0.55f, 0.33f, 0.2f);
        RenderSettings.fogColor = dusk;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 150f;
        if (cam != null) cam.backgroundColor = dusk;

        // a Arca vai à frente do povo (Js 6:8)
        var ark = new GameObject("ArcaDaAlianca");
        jeriArk = ark.transform;
        jeriDecor.Add(ark);
        var gold = new Color(1f, 0.8f, 0.3f);
        Prim(PrimitiveType.Cube, jeriArk, Vector3.zero, new Vector3(1.6f, 0.9f, 1.0f), gold, true);
        Prim(PrimitiveType.Cube, jeriArk, new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 0.1f, 1.1f), gold * 0.9f);
        for (int s = -1; s <= 1; s += 2)
        {
            var wing = Prim(PrimitiveType.Cube, jeriArk, new Vector3(s * 0.35f, 0.85f, 0f), new Vector3(0.6f, 0.08f, 0.5f), gold, true);
            wing.transform.localRotation = Quaternion.Euler(0f, 0f, s * -30f);
            Prim(PrimitiveType.Cube, jeriArk, new Vector3(0f, -0.2f, s * 0.6f), new Vector3(2.6f, 0.08f, 0.08f), new Color(0.45f, 0.3f, 0.15f));
        }

        bannerText = "AS MURALHAS DE JERICÓ  —  " + JerichoStyles[jeriStyle];
        bannerSub = jeriStyle == 1
            ? "Pegue as trombetas no ritmo e desvie dos ídolos de Canaã. (Js 6:18)"
            : "Fique na faixa da trombeta quando ela passar: sete voltas e as muralhas cairão. (Js 6:4)";
        bannerTime = 4f;
        Play(sLevel, 1f);
    }

    float JeriBpm(int beat)
    {
        int lap = beat / JeriBeatsPerLap;
        return 92f + lap * 6f + (jeriStyle == 2 ? 18f : 0f) + Mathf.Min(jerichosCleared, 3) * 4f;
    }

    void UpdateJericho(float dt)
    {
        float pz = player.transform.position.z;
        if (jeriPulse > 0f) jeriPulse -= dt * 4f;
        if (jeriArk != null)
            jeriArk.position = new Vector3(0f, 4.2f + Mathf.Sin(Time.time * 2f) * 0.2f, pz + 16f);

        // gera as batidas à frente (como a velocidade é fixa, a distância entre elas marca o tempo)
        int totalBeats = JeriBeatsPerLap * JeriLaps;
        while (jeriGenBeat < totalBeats && jeriGenZ < pz + 130f)
        {
            float spacing = jerichoSpeed * 60f / JeriBpm(jeriGenBeat);
            SpawnJerichoRow(jeriGenBeat, jeriGenZ, spacing);
            jeriRows.Add(jeriGenZ);
            jeriGenZ += spacing;
            jeriGenBeat++;
        }
        if (jeriGenBeat >= totalBeats && !jeriWallSpawned)
        {
            jeriWallSpawned = true;
            jeriWallZ = jeriGenZ + 30f;
            BuildJerichoWall(jeriWallZ);
        }

        // metrônomo: cada batida que passa pelo jogador
        while (jeriRows.Count > 0 && pz >= jeriRows[0])
        {
            jeriRows.RemoveAt(0);
            jeriPassed++;
            jeriPulse = 1f;
            // o tambor marca o ritmo: doum no tempo forte, tek nos outros
            bool strong = jeriPassed % 4 == 1;
            Sfx(strong ? "doum" : "tek", strong ? 0.75f : 0.45f, 0.02f, 0.03f);
            if (jeriPassed % JeriBeatsPerLap == 0 && jeriPassed < totalBeats)
                ShowPopup("VOLTA " + (jeriPassed / JeriBeatsPerLap + 1) + " DE " + JeriLaps);
        }

        // trombetas perdidas
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || o.flag || o.type != ObType.Note) continue;
            if (o.transform.position.z > pz - 1.5f) continue;
            o.flag = true;
            jeriCombo = 0;
        }

        // o grande brado
        if (jeriWallSpawned && !jeriResolved && pz >= jeriWallZ - 22f) ResolveJericho();
    }

    void SpawnJerichoRow(int beat, float z, float spacing)
    {
        int inLap = beat % JeriBeatsPerLap;
        // marco de cada volta: duas colunas com tochas
        if (inLap == 0)
        {
            var arch = new GameObject("MarcoDaVolta");
            jeriDecor.Add(arch);
            arch.transform.position = new Vector3(0f, 0f, z - spacing * 0.5f);
            var stone = new Color(0.7f, 0.6f, 0.45f);
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, arch.transform, new Vector3(s * 5.6f, 2f, 0f), new Vector3(0.8f, 4f, 0.8f), stone);
                Prim(PrimitiveType.Cube, arch.transform, new Vector3(s * 5.6f, 4.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), new Color(1f, 0.55f, 0.15f), true);
            }
        }

        // descanso de vez em quando (respiro do ritmo)
        bool rest = inLap == 7 || (inLap == 3 && Random.value < 0.35f);
        if (!rest)
        {
            int lane = Mathf.Clamp(jeriLastLane + Random.Range(-1, 2), 0, 2);
            if (Random.value < 0.15f) lane = Random.Range(0, 3);
            jeriLastLane = lane;
            SpawnNote(lane, z);

            // estilo 2: ídolos falsos em outra faixa
            if (jeriStyle == 1 && Random.value < 0.55f)
            {
                int bad = (lane + Random.Range(1, 3)) % 3;
                SpawnIdolNote(bad, z);
            }
            // estilo 3: contratempo (meia batida) nas voltas finais
            if (jeriStyle == 2 && beat >= JeriBeatsPerLap * 2 && Random.value < 0.4f && inLap != 6)
            {
                int l2 = Mathf.Clamp(lane + (Random.value < 0.5f ? -1 : 1), 0, 2);
                SpawnNote(l2, z + spacing * 0.5f);
            }
        }
    }

    void SpawnNote(int lane, float z)
    {
        var gold = new Color(1f, 0.82f, 0.35f);
        var o = MakeObstacle("Trombeta", ObType.Note, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.8f, 1.1f, 0.7f), 1f, 0, gold);
        BuildShofar(o.transform, Vector3.zero, 1f, gold);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI * 2f / 8f;
            Prim(PrimitiveType.Cube, o.transform, new Vector3(Mathf.Cos(a) * 0.75f, Mathf.Sin(a) * 0.75f, 0f), Vector3.one * 0.12f, gold, true);
        }
        o.Init();
        jeriTotal++;
    }

    void SpawnIdolNote(int lane, float z)
    {
        var dark = new Color(0.18f, 0.12f, 0.1f);
        var o = MakeObstacle("IdoloDeCanaa", ObType.NoteBad, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.6f, 1.1f, 0.5f), 1f, 0, dark);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.4f, 0f), new Vector3(0.9f, 0.5f, 0.7f), dark);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 1.0f, 0.5f), new Color(0.35f, 0.25f, 0.15f));
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.12f, 0.55f, -0.26f), new Vector3(0.1f, 0.06f, 0.03f), new Color(1f, 0.1f, 0.05f), true);
            var h = Prim(PrimitiveType.Cube, t, new Vector3(s * 0.25f, 0.95f, 0f), new Vector3(0.08f, 0.35f, 0.08f), dark);
            h.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
        }
        o.Init();
    }

    void JerichoHit(RunnerObstacle o)
    {
        o.flag = true;
        jeriHits++;
        jeriCombo++;
        jeriBest = Mathf.Max(jeriBest, jeriCombo);
        int pts = Mathf.RoundToInt(40 * Mathf.Min(jeriCombo, 10) * stats.scoreMul);
        killScore += pts;
        AddFloat(o.transform.position + Vector3.up * 1.8f, jeriCombo > 3 ? "x" + jeriCombo + "  +" + pts : "+" + pts, new Color(1f, 0.85f, 0.35f), jeriCombo % 8 == 0);
        FxSphere(o.transform.position, 1.6f, new Color(1f, 0.85f, 0.4f));
        DestroyObstacle(o, false);
        // cada trombeta toca uma nota da escala (sobe com o combo)
        int[] steps = { 0, 1, 4, 5, 7, 8, 10, 12 };
        Sfx("shofar_curto", 0.6f, 0f, 0.05f, Mathf.Pow(2f, steps[(jeriCombo - 1) % steps.Length] / 12f));
    }

    void JerichoIdol(RunnerObstacle o)
    {
        o.flag = true;
        jeriCombo = 0;
        AddFloat(o.transform.position + Vector3.up * 2f, "ÍDOLO! (Js 6:18)", new Color(1f, 0.3f, 0.2f), true);
        Explode(o.transform.position, new Color(0.3f, 0.2f, 0.15f), 10);
        DestroyObstacle(o, false);
        if (player.invuln <= 0f) HurtPlayer(null);
    }

    void BuildJerichoWall(float z)
    {
        var go = new GameObject("MuralhaDeJerico");
        jeriDecor.Add(go);
        jeriWall = go.transform;
        jeriWall.position = new Vector3(0f, 0f, z);
        var a = new Color(0.72f, 0.6f, 0.42f);
        var b = new Color(0.64f, 0.52f, 0.36f);
        for (int y = 0; y < 7; y++)
            for (int x = -7; x <= 7; x++)
                Prim(PrimitiveType.Cube, jeriWall, new Vector3(x * 1.6f + (y % 2) * 0.8f, 0.6f + y * 1.2f, 0f), new Vector3(1.55f, 1.15f, 1.8f), (x + y) % 2 == 0 ? a : b);
        // torres
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, jeriWall, new Vector3(s * 11.5f, 5.5f, 0f), new Vector3(3f, 11f, 3f), b);
        // a janela com o fio escarlate de Raabe (Js 2:18)
        Prim(PrimitiveType.Cube, jeriWall, new Vector3(-9.5f, 6.5f, -0.95f), new Vector3(0.9f, 0.9f, 0.1f), new Color(0.1f, 0.07f, 0.05f));
        Prim(PrimitiveType.Cube, jeriWall, new Vector3(-9.5f, 5.2f, -1.0f), new Vector3(0.08f, 2.4f, 0.05f), new Color(1f, 0.1f, 0.12f), true);
    }

    void ResolveJericho()
    {
        jeriResolved = true;
        float acc = JeriAccuracy;
        bool win = acc >= 0.6f;
        if (jeriWall != null)
        {
            if (win)
            {
                // o grande brado: as muralhas caem
                var bricks = new List<Transform>();
                foreach (Transform c in jeriWall) bricks.Add(c);
                foreach (var br in bricks)
                {
                    br.SetParent(null, true);
                    var d = br.gameObject.AddComponent<RunnerDebris>();
                    d.gravity = true;
                    d.spin = true;
                    d.velocity = new Vector3(Random.Range(-4f, 4f), Random.Range(2f, 9f), Random.Range(4f, 14f));
                    d.life = Random.Range(1.6f, 2.6f);
                }
                Explode(jeriWall.position + Vector3.up * 3f, new Color(0.7f, 0.6f, 0.45f), 50);
                shake = 1.2f;
                PlayBoom();
                Sfx("shofar_longo", 1f, 0f, 1f);
                Sfx("pisao", 1f, 0f, 0.1f, 0.6f);
            }
            else Explode(jeriWall.position + Vector3.up * 2f, new Color(0.7f, 0.6f, 0.45f), 20);
        }
        EndJericho(win, acc);
    }

    void CleanupJericho()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o != null && (o.type == ObType.Note || o.type == ObType.NoteBad))
            {
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        foreach (var d in jeriDecor) if (d != null) Destroy(d);
        jeriDecor.Clear();
        jeriRows.Clear();
        jeriArk = null;
        jeriWall = null;
    }

    void EndJericho(bool win, float acc)
    {
        jericho = false;
        CleanupJericho();
        ApplyAtmosphere();
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        int pct = Mathf.RoundToInt(acc * 100f);
        if (!win)
        {
            ShowPopup("AS MURALHAS RESISTIRAM... (" + pct + "%)");
            Play(sHurt, 0.6f);
            return;
        }
        jerichosCleared++;
        UnlockThemeCard("jerico");   // Trombetas de Jericó entram na coleção
        int pts = Mathf.RoundToInt((1200 + jeriBest * 60) * (1 + jerichosCleared * 0.4f + jeriStyle * 0.25f) * stats.scoreMul);
        killScore += pts;
        Heal(1);
        AddSiclos(15);
        bannerText = "AS MURALHAS CAÍRAM!";
        bannerSub = "\"O povo gritou com grande brado, e o muro caiu\" (Js 6:20)  —  ritmo " + pct + "%";
        bannerTime = 4f;
        ShowPopup("+" + pts);
        bool perfect = acc >= 0.95f;
        OfferGoodCard(perfect ? "RITMO PERFEITO!" : "JERICÓ CAIU!",
            perfect ? "Escolha uma carta (+1 vida). Depois, uma relíquia do despojo." : "Recompensa: escolha uma carta rara, épica ou lendária (+1 vida)",
            perfect ? (System.Action)(() => OpenRelicChoice("O DESPOJO DE JERICÓ", "Escolha uma relíquia", null)) : null);
    }

    void DrawJerichoBar(float s, float W, float H)
    {
        float bw = 620 * s, bh = 16 * s;
        var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
        int totalBeats = JeriBeatsPerLap * JeriLaps;
        Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.55f));
        Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01((float)jeriPassed / totalBeats), bar.height), new Color(1f, 0.75f, 0.35f));
        for (int k = 1; k < JeriLaps; k++) Box(new Rect(bar.x + bar.width * k / JeriLaps - 1, bar.y, 2, bar.height), new Color(0, 0, 0, 0.6f));
        string txt = "JERICÓ — " + JerichoStyles[jeriStyle] + "   •   volta " + JeriLap + "/" + JeriLaps + "   •   ritmo " + Mathf.RoundToInt(JeriAccuracy * 100) + "%" + (jeriCombo > 1 ? "   •   combo x" + jeriCombo : "");
        ShadowLabel(new Rect(0, bar.y + bh + 2 * s, W, 40 * s), txt, Sty(cardSmall), Color.white);
        ShadowLabel(new Rect(0, bar.y + bh + 34 * s, W, 40 * s),
            jeriStyle == 1 ? "Fique na faixa das trombetas  •  desvie dos ídolos escuros  •  60% para derrubar o muro" : "Fique na faixa das trombetas quando passarem  •  60% de ritmo para derrubar o muro",
            Sty(cardSmall), new Color(1f, 0.85f, 0.6f));
        // pulso da batida
        float r = (30 + 14 * Mathf.Clamp01(jeriPulse)) * s;
        Box(new Rect(W / 2 - r / 2, bar.y + bh + 80 * s, r, r), new Color(1f, 0.8f, 0.35f, 0.3f + 0.6f * Mathf.Clamp01(jeriPulse)));
    }

    // ================================================================== Davi e Golias

    void ClearTrackForMinigame()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Destroy(o.gameObject);
        }
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
    }

    void StartGoliath()
    {
        if (goliathGame == null) goliathGame = gameObject.AddComponent<RunnerGoliath>();
        ClearTrackForMinigame();
        goliath = true;
        player.SetVisible(false);
        goliathGame.Begin(this, goliathsCleared);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 170f;
        Sfx("rugido", 0.8f, 0f, 1f, 0.85f);
        bannerText = "DAVI E GOLIAS  —  " + RunnerGoliath.StyleNames[goliathGame.Style];
        bannerSub = "\"Eu venho a ti em nome do Senhor dos Exércitos\" (1Sm 17:45)";
        bannerTime = 4f;
        Play(sLevel, 1f);
    }

    void EndGoliath()
    {
        if (goliathGame != null) goliathGame.End();
        goliath = false;
        player.SetVisible(true);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        ApplyAtmosphere();
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
    }

    public void GoliathWon(Vector3 at, int throwsUsed)
    {
        int style = goliathGame.Style;
        EndGoliath();
        goliathsCleared++;
        UnlockThemeCard("funda");    // Funda de Davi entra na coleção
        bool oneStone = throwsUsed <= 1;
        int pts = Mathf.RoundToInt(1500 * (1 + goliathsCleared * 0.5f + style * 0.25f) * (oneStone ? 1.5f : 1f) * stats.scoreMul);
        killScore += pts;
        kills++;
        Heal(1);
        AddSiclos(oneStone ? 25 : 15);
        shake = 0.8f;
        ShowPopup("O GIGANTE CAIU! +" + pts);
        bannerText = oneStone ? "UMA SÓ PEDRA!" : "O GIGANTE CAIU!";
        bannerSub = "\"Assim Davi prevaleceu contra o filisteu com uma funda e com uma pedra\" (1Sm 17:50)";
        bannerTime = 4f;
        OfferGoodCard("GOLIAS DERROTADO!",
            oneStone ? "Com uma só pedra! Escolha uma carta (+1 vida) e depois uma relíquia." : "Recompensa: escolha uma carta rara, épica ou lendária (+1 vida)",
            oneStone ? (System.Action)(() => OpenRelicChoice("A ESPADA DE GOLIAS", "Escolha uma relíquia (1Sm 21:9)", null)) : null);
    }

    public void GoliathFailed()
    {
        EndGoliath();
        ShowPopup("OS FILISTEUS ZOMBAM... AS PEDRAS ACABARAM");
        Play(sHurt, 0.6f);
    }

    void DrawGoliathHUD(float s, float W, float H)
    {
        var gg = goliathGame;
        ShadowLabel(new Rect(0, 8 * s, W, 44 * s), "DAVI E GOLIAS  —  " + RunnerGoliath.StyleNames[gg.Style], Sty(midStyle, fs: Mathf.RoundToInt(32 * s)), new Color(0.75f, 0.95f, 0.55f));
        string stonesTxt = "Seixos: ";
        for (int i = 0; i < gg.StonesMax; i++) stonesTxt += i < gg.Stones ? "● " : "○ ";
        ShadowLabel(new Rect(0, 50 * s, W, 40 * s), stonesTxt + "   •   distância " + Mathf.RoundToInt(Mathf.Max(0f, gg.Distance)) + " m", Sty(midStyle, fs: Mathf.RoundToInt(28 * s)), Color.white);
        string hint = RunnerTouch.UseTouchUI
            ? "Arraste para mirar  •  segure para girar a funda  •  solte na zona verde"
            : "Mouse ou WASD: mirar  •  segure clique/Espaço para girar  •  solte na zona verde  •  acerte a testa!";
        ShadowLabel(new Rect(0, 88 * s, W, 34 * s), hint, Sty(cardSmall), new Color(1f, 0.9f, 0.7f));

        // mira
        Vector3 sp = WorldToScreen(gg.AimWorld);
        if (sp.z > 0f)
        {
            float cx = sp.x, cy = H - sp.y, r = 22 * s;
            var c = gg.Charging && gg.Charge >= RunnerGoliath.SweetMin && gg.Charge <= RunnerGoliath.SweetMax ? new Color(0.4f, 1f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.85f);
            Box(new Rect(cx - r, cy - 1.5f * s, r * 0.7f, 3 * s), c);
            Box(new Rect(cx + r * 0.3f, cy - 1.5f * s, r * 0.7f, 3 * s), c);
            Box(new Rect(cx - 1.5f * s, cy - r, 3 * s, r * 0.7f), c);
            Box(new Rect(cx - 1.5f * s, cy + r * 0.3f, 3 * s, r * 0.7f), c);
            Box(new Rect(cx - 2 * s, cy - 2 * s, 4 * s, 4 * s), new Color(1f, 0.3f, 0.2f, 0.9f));
        }

        // medidor da funda
        float mw = 460 * s, mh = 22 * s;
        var m = new Rect(W / 2 - mw / 2, H - 120 * s, mw, mh);
        Box(new Rect(m.x - 3, m.y - 3, m.width + 6, m.height + 6), new Color(0, 0, 0, 0.6f));
        Box(m, new Color(0.25f, 0.2f, 0.15f));
        Box(new Rect(m.x + m.width * RunnerGoliath.SweetMin, m.y, m.width * (RunnerGoliath.SweetMax - RunnerGoliath.SweetMin), m.height), new Color(0.3f, 0.8f, 0.3f, 0.8f));
        if (gg.Charging) Box(new Rect(m.x + m.width * gg.Charge - 3 * s, m.y - 6 * s, 6 * s, m.height + 12 * s), Color.white);
        ShadowLabel(new Rect(0, m.yMax + 4 * s, W, 34 * s), gg.Charging ? "GIRANDO A FUNDA..." : "segure para girar", Sty(cardSmall), new Color(0.9f, 0.9f, 0.8f));

        if (gg.TauntTime > 0f && !gg.Won)
            ShadowLabel(new Rect(W * 0.1f, H * 0.22f, W * 0.8f, 50 * s), "GOLIAS: " + gg.Taunt, Sty(midStyle, ww: 1, fs: Mathf.RoundToInt(28 * s)), new Color(1f, 0.55f, 0.45f));
        var big = Sty(bigStyle, fs: Mathf.RoundToInt(48 * s));
        if (gg.ShieldUp) ShadowLabel(new Rect(0, H * 0.3f, W, 60 * s), "ESCUDO ERGUIDO — ESPERE!", big, new Color(1f, 0.7f, 0.3f));
        else if (gg.ShieldWarning) ShadowLabel(new Rect(0, H * 0.3f, W, 60 * s), "ele vai erguer o escudo...", Sty(midStyle), new Color(1f, 0.8f, 0.5f));
        if (gg.Style == 2 && Mathf.Abs(gg.Wind) > 0.3f)
            ShadowLabel(new Rect(0, H * 0.36f, W, 60 * s), gg.Wind > 0f ? "VENTO  >>>" : "<<<  VENTO", big, new Color(0.9f, 0.85f, 0.6f, Mathf.Clamp01(Mathf.Abs(gg.Wind) / 2f)));
    }
}
