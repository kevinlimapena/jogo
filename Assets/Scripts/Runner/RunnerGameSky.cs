using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TRAVESSIA DOS CÉUS: ao mudar de região (depois de certos chefes), o cavalo sobe aos céus,
/// atravessa um trecho aéreo bem rápido (desviar de nuvens de tempestade, colunas e portais,
/// com poucos inimigos) e desce já na próxima região.
/// </summary>
public partial class RunnerGame
{
    const float SkyDuration = 22f;       // duração total da travessia (s)
    const float SkyFlashAt = 0.6f;       // momento do clarão da subida

    [HideInInspector] public bool skyTransit;
    float skyT;
    bool skySwapped, skyLanding;
    BiomeTheme pendingBiome;
    Transform skyRoot, skyFloor;
    readonly List<Transform> skyDecor = new List<Transform>();
    float skyDecorTimer, skyStreakTimer;
    float skyFlash;                      // clarão branco na tela (0..1)

    /// Velocidade extra durante a travessia.
    float SkySpeedMul => skyTransit ? 1.7f : 1f;

    void ResetSkyTransit()
    {
        if (skyRoot != null) Destroy(skyRoot.gameObject);
        skyRoot = null;
        skyFloor = null;
        skyDecor.Clear();
        skyTransit = false;
        skyLanding = false;
        skySwapped = false;
        pendingBiome = null;
        skyFlash = 0f;
        if (trackRoot != null) trackRoot.gameObject.SetActive(true);
    }

    /// Começa a travessia: o cavalo sobe com um feixe de luz.
    void StartSkyTransit(BiomeTheme next)
    {
        pendingBiome = next;
        skyTransit = true;
        skyT = 0f;
        skySwapped = false;
        skyLanding = false;
        flightTime = SkyDuration;
        player.StartFlight(true);     // voa com o próprio cavalo (e as armas)
        player.invuln = Mathf.Max(player.invuln, 2.5f);
        skyClearPending = true;   // a pista é limpa no próximo quadro (pode haver laços percorrendo a lista agora)
        nextSpawnZ = player.transform.position.z + 70f;
        CardFx.Pillar(player.transform, new Color(1f, 0.92f, 0.6f), 2.2f, 1.2f);
        CardFx.Ring(player.transform, new Color(1f, 0.9f, 0.55f), 8f, 0.8f);
        Sfx("shofar", 0.9f, 0f, 0.5f);
        Banner("TRAVESSIA DOS CÉUS", "\"Os que esperam no Senhor subirão com asas como águias\" (Is 40:31)  —  rumo a " + next.name);
        bannerTime = 3.5f;
        shake = 0.4f;
    }

    /// Chamado todo quadro enquanto o jogador voa na travessia.
    bool skyClearPending;

    void UpdateSkyTransit(float dt)
    {
        if (skyClearPending)
        {
            skyClearPending = false;
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var o = obstacles[i];
                if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        skyT += dt;
        flightTime = SkyDuration - skyT;
        var pp = player.transform.position;

        // subida: força o cavalo para cima nos primeiros instantes
        if (skyT < 1.2f) player.LiftUp(16f);

        // clarão da subida → troca o cenário pelo céu
        if (!skySwapped)
        {
            skyFlash = Mathf.Max(skyFlash, Mathf.Clamp01(skyT / SkyFlashAt));
            if (skyT >= SkyFlashAt) EnterSky();
        }

        if (skySwapped && !skyLanding)
        {
            // chão de nuvens acompanha o jogador
            if (skyFloor != null) skyFloor.position = new Vector3(0f, -7f, pp.z + 120f);
            if (flightTime > 3.5f) SpawnAhead(true);
            UpdateSkyDecor(dt, pp);
            // clarão da descida
            if (flightTime < 1.2f) skyFlash = Mathf.Max(skyFlash, Mathf.Clamp01((1.2f - flightTime) / 0.8f));
            if (flightTime <= 0.4f) LandFromSky();
        }
    }

    void EnterSky()
    {
        skySwapped = true;
        if (trackRoot != null) trackRoot.gameObject.SetActive(false);   // a cidade fica lá embaixo
        if (roadRoot != null) roadRoot.gameObject.SetActive(false);
        skyRoot = new GameObject("Ceu").transform;
        // mar de nuvens embaixo
        var floor = Prim(PrimitiveType.Cube, skyRoot, Vector3.zero, new Vector3(260f, 1f, 420f), new Color(0.93f, 0.95f, 1f));
        floor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        skyFloor = floor.transform;
        // céu claro e dourado
        var sky = new Color(0.55f, 0.74f, 1f);
        if (cam != null) cam.backgroundColor = sky;
        RenderSettings.fogColor = new Color(0.86f, 0.9f, 1f);
        RenderSettings.fogStartDistance = 45f;
        RenderSettings.fogEndDistance = 190f;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.color = new Color(1f, 0.95f, 0.85f); l.intensity = 1.25f; }
        // nuvens já espalhadas no começo
        var pp = player.transform.position;
        for (int i = 0; i < 14; i++) SpawnDecorCloud(pp.z + Random.Range(10f, 180f));
    }

    /// Pousa na nova região e entrega as recompensas do chefe.
    void LandFromSky()
    {
        skyLanding = true;
        // tira o que sobrou do céu
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o != null && o.airborne && !o.dead)
            {
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        if (skyRoot != null) Destroy(skyRoot.gameObject);
        skyRoot = null;
        skyFloor = null;
        skyDecor.Clear();
        skyTransit = false;
        player.EndFlight();
        player.invuln = Mathf.Max(player.invuln, 2f);
        var next = pendingBiome;
        pendingBiome = null;
        if (next != null) SetBiome(next, true);   // nova cidade
        if (trackRoot != null) trackRoot.gameObject.SetActive(true);
        nextSpawnZ = player.transform.position.z + 45f;
        planeBoxTimer = planeBoxInterval;
        skyFlash = 1f;
        Explode(player.transform.position, new Color(1f, 0.9f, 0.6f), 16);
        Sfx("pisao", 0.9f, 0f, 0.2f);
        shake = 0.5f;
        // agora sim: carta → relíquia → caminho
        AfterBossRewards();
        OpenCardChoice(true);
    }

    // ---------------------------------------------------------------- obstáculos do céu

    /// Fileira aérea: principalmente coisas para desviar, poucos inimigos.
    void SpawnSkyRow(float z)
    {
        float r = Random.value;
        if (r < 0.4f)
        {
            int n = Random.Range(2, Difficulty > 0.4f ? 5 : 4);
            for (int i = 0; i < n; i++) SpawnStormCloud(RandomSkyPos(z + Random.Range(-4f, 4f)));
        }
        else if (r < 0.55f) SpawnGate(z);
        else if (r < 0.7f)
        {
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++) SpawnPillar(Random.Range(-6.5f, 6.5f), Random.Range(4f, 10f), z + i * 6f);
        }
        else if (r < 0.85f)
        {
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++) SpawnFlyer(RandomSkyPos(z + i * 5f));
        }
        else
        {
            var p = RandomSkyPos(z);
            int n = Random.Range(3, 6);
            for (int i = 0; i < n; i++)
            {
                SpawnRing(p + new Vector3(0f, 0f, i * 7f));
                p.x = Mathf.Clamp(p.x + Random.Range(-1.5f, 1.5f), -6.5f, 6.5f);
                p.y = Mathf.Clamp(p.y + Random.Range(-1f, 1f), 1.6f, 9f);
            }
        }
    }

    /// Nuvem de tempestade: desvie (não dá para destruir).
    void SpawnStormCloud(Vector3 pos)
    {
        var dark = new Color(0.32f, 0.33f, 0.4f);
        var o = MakeObstacle("NuvemTempestade", ObType.Wall, pos, new Vector3(1.5f, 0.9f, 1.3f), 9999f, 0, dark);
        var t = o.transform;
        Prim(PrimitiveType.Sphere, t, new Vector3(-0.7f, 0f, 0f), new Vector3(1.8f, 1.4f, 1.8f), dark);
        Prim(PrimitiveType.Sphere, t, new Vector3(0.6f, 0.1f, 0.1f), new Vector3(2f, 1.6f, 2f), dark * 1.1f);
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.5f, -0.2f), new Vector3(1.6f, 1.3f, 1.6f), dark * 1.2f);
        // raiozinho brilhando embaixo
        var bolt = Prim(PrimitiveType.Cube, t, new Vector3(0.1f, -0.9f, 0f), new Vector3(0.12f, 0.8f, 0.12f), new Color(1f, 0.95f, 0.5f), true);
        bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
        o.airborne = true;
        o.Init();
    }

    // ---------------------------------------------------------------- nuvens e riscos de velocidade (só visual)

    void SpawnDecorCloud(float z)
    {
        if (skyRoot == null) return;
        var c = new GameObject("NuvemDecor").transform;
        c.SetParent(skyRoot, false);
        float side = Random.value < 0.5f ? -1f : 1f;
        c.position = new Vector3(side * Random.Range(11f, 40f), Random.Range(-4f, 16f), z);
        float sc = Random.Range(2.5f, 6f);
        var white = new Color(0.97f, 0.98f, 1f);
        for (int i = 0; i < 4; i++)
        {
            var p = Prim(PrimitiveType.Sphere, c, new Vector3(Random.Range(-1f, 1f) * sc * 0.5f, Random.Range(-0.2f, 0.4f) * sc * 0.4f, Random.Range(-1f, 1f) * sc * 0.4f),
                new Vector3(sc, sc * 0.6f, sc), white);
            p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        skyDecor.Add(c);
    }

    void UpdateSkyDecor(float dt, Vector3 pp)
    {
        skyDecorTimer -= dt;
        if (skyDecorTimer <= 0f && skyDecor.Count < 30)
        {
            skyDecorTimer = 0.18f;
            SpawnDecorCloud(pp.z + Random.Range(150f, 190f));
        }
        // riscos de luz passando (sensação de velocidade)
        skyStreakTimer -= dt;
        if (skyStreakTimer <= 0f && RunnerDebris.Live < DebrisBudget)
        {
            skyStreakTimer = QualityTier == 0 ? 0.12f : 0.06f;
            MeshRenderer sr;
            var s = NewPrim(PrimitiveType.Cube, out sr, false);
            sr.sharedMaterial = Glow(new Color(1f, 1f, 1f));
            float side = Random.value < 0.5f ? -1f : 1f;
            s.transform.position = new Vector3(side * Random.Range(5f, 12f), pp.y + Random.Range(-5f, 6f), pp.z + Random.Range(20f, 45f));
            s.transform.localScale = new Vector3(0.05f, 0.05f, Random.Range(2.5f, 5f));
            var d = s.AddComponent<RunnerDebris>();
            d.gravity = false;
            d.spin = false;
            d.life = 0.5f;
            d.velocity = new Vector3(0f, 0f, -speed * 0.6f);
        }
        // tira as nuvens que já passaram
        for (int i = skyDecor.Count - 1; i >= 0; i--)
        {
            var c = skyDecor[i];
            if (c == null) { skyDecor.RemoveAt(i); continue; }
            if (c.position.z < pp.z - 20f) { Destroy(c.gameObject); skyDecor.RemoveAt(i); }
        }
    }

    // ---------------------------------------------------------------- tela

    void DrawSkyFlash(float W, float H)
    {
        if (skyFlash <= 0.01f) return;
        Box(new Rect(0, 0, W, H), new Color(1f, 0.98f, 0.9f, Mathf.Clamp01(skyFlash)));
    }

    void UpdateSkyFlash(float udt)
    {
        // o clarão some sozinho quando ninguém está "segurando" ele aceso
        if (skyFlash > 0f && (!skyTransit || (skySwapped && flightTime > 1.2f))) skyFlash = Mathf.Max(0f, skyFlash - udt * 1.6f);
    }

    void DrawSkyHUD(float s, float W)
    {
        float bw = 520 * s, bh = 16 * s;
        var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
        Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.45f));
        Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(1f - flightTime / SkyDuration), bar.height), new Color(1f, 0.88f, 0.5f));
        string dest = pendingBiome != null ? pendingBiome.name.ToUpper() : "";
        string txt = "TRAVESSIA DOS CÉUS  →  " + dest + "   (" + (RunnerTouch.UseTouchUI ? "arraste o dedo para voar" : "WASD / setas para voar") + ")";
        ShadowLabel(new Rect(0, bar.y + bh + 2 * s, W, 40 * s), txt, Sty(cardSmall), Color.white);
    }
}
