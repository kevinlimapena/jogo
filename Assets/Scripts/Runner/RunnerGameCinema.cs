using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cenas animadas:
///   • INTRO — os céus se abrem (Ez 1:1), um feixe de luz desce e o cavalo-anjo é enviado à terra.
///   • VITÓRIA — Satanás cai, o céu fica dourado, a Nova Jerusalém desce (Ap 21:2), o resumo da jornada e o MODO INFINITO.
/// Toque / Enter / Espaço pulam a cena.
/// </summary>
public partial class RunnerGame
{
    int cineKind;            // 0 = intro, 1 = vitória
    float cineT;
    float cineLength;
    readonly List<GameObject> cineObjs = new List<GameObject>();
    Transform cineBeam, cineRays, cineCity;
    readonly List<Transform> cineClouds = new List<Transform>();
    bool cineLanded;
    Vector3 cineFocus;
    Color cineSkyFrom;
    float cineSunFrom;

    const float IntroLen = 6.4f;
    const float VictoryLen = 12.5f;

    bool InCinema => state == RunnerState.Cutscene;

    // ================================================================== intro

    void StartIntro()
    {
        CineClear();
        cineKind = 0;
        cineT = 0f;
        cineLength = IntroLen;
        cineLanded = false;
        state = RunnerState.Cutscene;
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);

        var pz = player.transform.position.z;
        cineFocus = new Vector3(0f, 0f, pz);
        player.transform.position = new Vector3(0f, 48f, pz);
        player.SetVisible(true);

        // nuvens que se abrem
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.PI * 2f / 14f;
            var c = Prim(PrimitiveType.Sphere, null, new Vector3(Mathf.Cos(a) * 6f, 52f + Random.Range(-1.5f, 1.5f), pz + Mathf.Sin(a) * 6f),
                new Vector3(Random.Range(7f, 11f), Random.Range(2.5f, 4f), Random.Range(7f, 11f)), new Color(0.95f, 0.93f, 0.9f));
            cineObjs.Add(c);
            cineClouds.Add(c.transform);
        }
        // feixe de luz do céu
        var beam = Prim(PrimitiveType.Cylinder, null, new Vector3(0f, 26f, pz), new Vector3(0.1f, 26f, 0.1f), new Color(1f, 0.92f, 0.6f), true);
        cineObjs.Add(beam);
        cineBeam = beam.transform;
        // raios girando em volta do feixe
        var rays = new GameObject("RaiosDeLuz");
        cineObjs.Add(rays);
        cineRays = rays.transform;
        cineRays.position = new Vector3(0f, 0f, pz);
        for (int i = 0; i < 8; i++)
        {
            var ray = Prim(PrimitiveType.Cube, cineRays, new Vector3(0f, 25f, 0f), new Vector3(0.12f, 50f, 0.12f), new Color(1f, 0.85f, 0.45f), true);
            ray.transform.localRotation = Quaternion.Euler(0f, i * 45f, 12f);
        }
        Sfx("reliquia", 0.8f, 0f, 1f);
    }

    // ================================================================== vitória

    void StartVictory(Vector3 at)
    {
        CineClear();
        cineKind = 1;
        cineT = 0f;
        cineLength = VictoryLen;
        state = RunnerState.Cutscene;
        cineFocus = at;
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        for (int i = obstacles.Count - 1; i >= 0; i--)
            if (obstacles[i] != null && obstacles[i].type == ObType.EnemyShot) { Destroy(obstacles[i].gameObject); obstacles.RemoveAt(i); }

        cineSkyFrom = RenderSettings.fogColor;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) cineSunFrom = l.intensity;

        // a Nova Jerusalém: cidade quadrada de ouro com doze portas de pérola (Ap 21:12-21)
        var city = new GameObject("NovaJerusalem");
        cineObjs.Add(city);
        cineCity = city.transform;
        cineCity.position = new Vector3(0f, 90f, player.transform.position.z + 70f);
        var gold = new Color(1f, 0.85f, 0.4f);
        var jasper = new Color(0.7f, 0.95f, 0.85f);
        var pearl = new Color(0.98f, 0.96f, 0.92f);
        float half = 14f;
        Prim(PrimitiveType.Cube, cineCity, Vector3.zero, new Vector3(half * 2f, 1.2f, half * 2f), gold, true);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            Prim(PrimitiveType.Cube, cineCity, new Vector3(0f, 3f, sd * half), new Vector3(half * 2f, 6f, 0.8f), jasper);   // muralhas de jaspe
            Prim(PrimitiveType.Cube, cineCity, new Vector3(sd * half, 3f, 0f), new Vector3(0.8f, 6f, half * 2f), jasper);
            for (int g = -1; g <= 1; g++)
            {
                // doze portas, cada uma de uma só pérola (Ap 21:21)
                Prim(PrimitiveType.Cube, cineCity, new Vector3(g * 8f, 2.5f, sd * (half + 0.45f)), new Vector3(2.4f, 4.5f, 1f), pearl, true);
                Prim(PrimitiveType.Cube, cineCity, new Vector3(sd * (half + 0.45f), 2.5f, g * 8f), new Vector3(1f, 4.5f, 2.4f), pearl, true);
            }
        }
        // torres e o trono no centro, com a luz da glória (Ap 21:23)
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
            Prim(PrimitiveType.Cube, cineCity, new Vector3(Mathf.Cos(a) * half, 6f, Mathf.Sin(a) * half), new Vector3(2.5f, 12f, 2.5f), gold, true);
        }
        Prim(PrimitiveType.Cube, cineCity, new Vector3(0f, 5f, 0f), new Vector3(8f, 10f, 8f), gold);
        Prim(PrimitiveType.Sphere, cineCity, new Vector3(0f, 13f, 0f), Vector3.one * 5f, new Color(1f, 0.97f, 0.85f), true);
        // rio da água da vida (Ap 22:1)
        Prim(PrimitiveType.Cube, cineCity, new Vector3(0f, 0.65f, 0f), new Vector3(2f, 0.1f, half * 2f), new Color(0.5f, 0.85f, 1f), true);
        // raios
        var rays = new GameObject("RaiosDaGloria");
        cineObjs.Add(rays);
        cineRays = rays.transform;
        cineRays.position = cineCity.position;
        for (int i = 0; i < 12; i++)
        {
            var ray = Prim(PrimitiveType.Cube, cineRays, Vector3.zero, new Vector3(0.25f, 0.25f, 120f), new Color(1f, 0.9f, 0.5f), true);
            ray.transform.localRotation = Quaternion.Euler(Random.Range(-60f, 10f), i * 30f, 0f);
        }

        Sfx("shofar_longo", 1f, 0f, 1f);
        Sfx("vitoria", 1f, 0f, 1f);
        shake = 1.2f;
    }

    // ================================================================== loop

    void UpdateCinema(float udt)
    {
        cineT += udt;
        bool skip = cineT > (cineKind == 0 ? 0.4f : 2f) && (ConfirmPressed() || PausePressed());
        if (cineKind == 0) UpdateIntro(udt);
        else UpdateVictory(udt);
        if (skip || cineT >= cineLength) EndCinema();
    }

    void UpdateIntro(float dt)
    {
        float pz = cineFocus.z;
        // nuvens se afastando
        float open = Mathf.Clamp01(cineT / 1.6f);
        for (int i = 0; i < cineClouds.Count; i++)
        {
            var c = cineClouds[i];
            Vector3 dir = new Vector3(c.position.x, 0f, c.position.z - pz).normalized;
            c.position += dir * dt * (4f + open * 14f);
        }
        // feixe crescendo
        float bw = Mathf.Lerp(0.1f, 3.2f, Mathf.SmoothStep(0f, 1f, cineT / 1.2f));
        if (cineT > 4.2f) bw = Mathf.Lerp(3.2f, 0f, (cineT - 4.2f) / 1.2f);
        cineBeam.localScale = new Vector3(bw, 26f, bw);
        cineRays.Rotate(0f, 40f * dt, 0f);
        cineRays.localScale = Vector3.one * Mathf.Clamp01(bw / 3.2f);

        // descida (1,2 s → 4,0 s), girando devagar
        float k = Mathf.Clamp01((cineT - 1.2f) / 2.8f);
        float y = Mathf.Lerp(48f, 0.9f, EaseOutCubic(k));
        player.transform.position = new Vector3(0f, y, pz);
        player.transform.rotation = Quaternion.Euler(0f, (1f - k) * 540f, 0f);
        if (k < 1f && Random.value < 0.5f)
            Explode(player.transform.position + Random.insideUnitSphere * 0.8f, new Color(1f, 0.9f, 0.5f), 1);

        // pouso
        if (!cineLanded && k >= 1f)
        {
            cineLanded = true;
            player.transform.rotation = Quaternion.identity;
            FxSphere(new Vector3(0f, 0.5f, pz), 7f, new Color(1f, 0.9f, 0.5f));
            Explode(new Vector3(0f, 0.3f, pz), new Color(1f, 0.85f, 0.45f), 40);
            shake = 0.9f;
            Sfx("pisao", 1f, 0f, 1f, 0.8f);
            Sfx("shofar_longo", 0.9f, 0f, 1f);
        }

        // câmera: começa olhando o céu, acompanha a descida e termina atrás do jogador
        var cam0 = new Vector3(0f, 1.6f, pz - 7f);
        var camEnd = new Vector3(0f, 4.2f, pz - 8f);
        Vector3 look;
        Vector3 pos;
        if (cineT < 1.2f) { pos = cam0; look = new Vector3(0f, 40f, pz + 6f); }
        else if (cineT < 4.2f) { pos = Vector3.Lerp(cam0, new Vector3(3f, 3f, pz - 9f), k); look = player.transform.position + Vector3.up * 0.5f; }
        else { float e = Mathf.SmoothStep(0f, 1f, (cineT - 4.2f) / 1.6f); pos = Vector3.Lerp(new Vector3(3f, 3f, pz - 9f), camEnd, e); look = Vector3.Lerp(player.transform.position, new Vector3(0f, 1.2f, pz + 10f), e); }
        cam.transform.position = pos + (shake > 0f ? Random.insideUnitSphere * shake * 0.3f : Vector3.zero);
        cam.transform.LookAt(look);
        if (shake > 0f) shake -= dt * 1.5f;
    }

    void UpdateVictory(float dt)
    {
        float pz = player.transform.position.z;
        // explosões em cadeia de Satanás nos primeiros segundos
        if (cineT < 2.2f && Random.value < 0.35f)
        {
            Explode(cineFocus + Random.insideUnitSphere * 2.5f, Random.value < 0.5f ? new Color(1f, 0.2f, 0.05f) : new Color(0.1f, 0.02f, 0.02f), 12);
            if (Random.value < 0.3f) PlayBoom();
        }
        if (cineT > 1.8f && cineT - dt <= 1.8f) { FxSphere(cineFocus, 14f, Color.white); Sfx("ressurreicao", 1f, 0f, 1f); }

        // o céu fica dourado
        float g = Mathf.Clamp01((cineT - 1.8f) / 2.5f);
        var goldSky = new Color(1f, 0.9f, 0.68f);
        RenderSettings.fogColor = Color.Lerp(cineSkyFrom, goldSky, g);
        RenderSettings.fogStartDistance = Mathf.Lerp(T.fogStart, 70f, g);
        RenderSettings.fogEndDistance = Mathf.Lerp(T.fogEnd, 260f, g);
        if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.intensity = Mathf.Lerp(cineSunFrom, 1.4f, g); l.color = Color.Lerp(l.color, new Color(1f, 0.95f, 0.85f), g * 0.1f); }

        // a cidade desce do céu
        float d = Mathf.Clamp01((cineT - 2.2f) / 5f);
        cineCity.position = new Vector3(0f, Mathf.Lerp(90f, 16f, EaseOutCubic(d)), pz + 70f);
        cineCity.Rotate(0f, 6f * dt, 0f);
        cineRays.position = cineCity.position + Vector3.up * 13f;
        cineRays.Rotate(0f, 10f * dt, 0f);

        // câmera: do chão olhando a explosão para o alto, vendo a cidade
        float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((cineT - 1.5f) / 4f));
        var pos = Vector3.Lerp(new Vector3(0f, 3.5f, pz - 8f), new Vector3(0f, 1.5f, pz - 6f), c);
        var look = Vector3.Lerp(cineFocus, cineCity.position, c);
        cam.transform.position = pos + (shake > 0f ? Random.insideUnitSphere * shake * 0.3f : Vector3.zero);
        cam.transform.LookAt(look);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 70f, dt);
        if (shake > 0f) shake -= dt;
    }

    static float EaseOutCubic(float k) { float u = 1f - Mathf.Clamp01(k); return 1f - u * u * u; }

    void EndCinema()
    {
        int kind = cineKind;
        CineClear();
        player.transform.rotation = Quaternion.identity;
        player.transform.position = new Vector3(player.transform.position.x, 0.9f, player.transform.position.z);
        player.feetY = 0f;
        state = RunnerState.Playing;
        player.inputLock = 0.3f;
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        if (cam != null) cam.fieldOfView = 60f;
        if (kind == 1)
        {
            ApplyAtmosphere();
            bannerText = "MODO INFINITO";
            bannerSub = "A jornada continua — os inimigos ficam cada vez mais fortes";
            bannerTime = 4f;
            AfterBossRewards();
            OpenCardChoice(true);
        }
    }

    void CineClear()
    {
        foreach (var o in cineObjs) if (o != null) Destroy(o);
        cineObjs.Clear();
        cineClouds.Clear();
        cineBeam = cineRays = cineCity = null;
    }

    // ================================================================== textos das cenas

    void DrawCinema(float s, float W, float H)
    {
        // faixas de cinema
        float bar = 90 * s;
        Box(new Rect(0, 0, W, bar), Color.black);
        Box(new Rect(0, H - bar, W, bar), Color.black);
        var big = new GUIStyle(bigStyle) { fontSize = Mathf.RoundToInt(110 * s) };
        var verse = new GUIStyle(midStyle) { fontSize = Mathf.RoundToInt(30 * s), wordWrap = true, fontStyle = FontStyle.Italic };
        if (cineKind == 0)
        {
            float a1 = Fade(cineT, 0.2f, 3.6f);
            ShadowLabel(new Rect(W * 0.1f, bar + 20 * s, W * 0.8f, 90 * s), "\"Abriram-se os céus, e eu tive visões de Deus.\"  (Ez 1:1)", verse, new Color(1f, 1f, 1f, a1));
            float a2 = Fade(cineT, 4.0f, 6.4f);
            ShadowLabel(new Rect(0, H * 0.3f, W, 130 * s), "EZEQUIEL", big, new Color(1f, 0.85f, 0.25f, a2));
            ShadowLabel(new Rect(W * 0.1f, H * 0.3f + 125 * s, W * 0.8f, 60 * s), "\"Filho do homem, eu te envio.\"  (Ez 2:3)", verse, new Color(1f, 1f, 1f, a2));
        }
        else
        {
            float a1 = Fade(cineT, 1.8f, 6.5f);
            ShadowLabel(new Rect(0, H * 0.2f, W, 130 * s), "VITÓRIA!", big, new Color(1f, 0.85f, 0.25f, a1));
            ShadowLabel(new Rect(W * 0.1f, H * 0.2f + 125 * s, W * 0.8f, 60 * s), "\"O acusador de nossos irmãos foi lançado fora.\"  (Ap 12:10)", verse, new Color(1f, 1f, 1f, a1));
            float a2 = Fade(cineT, 4.5f, 9.2f);
            ShadowLabel(new Rect(W * 0.1f, H * 0.62f, W * 0.8f, 60 * s), "\"Eis que faço novas todas as coisas.\"  (Ap 21:5)", verse, new Color(1f, 0.95f, 0.8f, a2));
            // resumo da jornada
            float a3 = Fade(cineT, 6.5f, 9.6f);
            if (a3 > 0f)
            {
                var r = new Rect(W / 2 - 320 * s, H * 0.36f, 640 * s, 150 * s);
                Box(r, new Color(0f, 0f, 0f, 0.5f * a3));
                string summary = "Profeta: " + (prophet != null ? prophet.name : "Ezequiel") + "\nPontos: " + Score + "   •   Chefes: " + bossesDefeated + "\nTempo de jornada: " + Mathf.FloorToInt(runTime / 60f) + "min " + Mathf.FloorToInt(runTime % 60f) + "s   •   Abates: " + kills;
                ShadowLabel(r, summary, new GUIStyle(midStyle) { fontSize = Mathf.RoundToInt(30 * s) }, new Color(1f, 1f, 1f, a3));
            }
            float a4 = Fade(cineT, 9.4f, 12.5f);
            ShadowLabel(new Rect(0, H * 0.3f, W, 160 * s), "∞", new GUIStyle(bigStyle) { fontSize = Mathf.RoundToInt(170 * s) }, new Color(1f, 0.85f, 0.3f, a4));
            ShadowLabel(new Rect(0, H * 0.3f + 150 * s, W, 90 * s), "MODO INFINITO", big, new Color(1f, 0.95f, 0.8f, a4));
        }
        if (cineT > (cineKind == 0 ? 0.4f : 2f))
            ShadowLabel(new Rect(0, H - bar + 20 * s, W - 30 * s, 50 * s), RunnerTouch.UseTouchUI ? "toque para pular" : "Enter / clique para pular",
                new GUIStyle(cardSmall) { alignment = TextAnchor.MiddleRight }, new Color(1f, 1f, 1f, 0.6f));
    }

    static float Fade(float t, float a, float b)
    {
        if (t < a || t > b) return 0f;
        return Mathf.Clamp01((t - a) / 0.5f) * Mathf.Clamp01((b - t) / 0.5f);
    }
}
