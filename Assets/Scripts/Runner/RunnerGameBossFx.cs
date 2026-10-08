using UnityEngine;

/// <summary>
/// Ferramentas que os chefes usam nos poderes novos: ondas de choque, serpentes, servos e trevas.
/// </summary>
public partial class RunnerGame
{
    float darknessTime;

    /// Onda de choque que corre pelo chão em todas as faixas — é preciso pular.
    public void SpawnShockwave(float z)
    {
        var c = T.id == 2 ? new Color(1f, 0.4f, 0.08f) : new Color(0.85f, 0.7f, 0.45f);
        var o = MakeObstacle("OndaDeChoque", ObType.Shockwave, new Vector3(0f, 0.3f, z), new Vector3(5.2f, 0.3f, 0.45f), 9999f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(10.4f, 0.5f, 0.7f), c, true);
        for (int i = -4; i <= 4; i++)
        {
            var rock = Prim(PrimitiveType.Cube, t, new Vector3(i * 1.2f, 0.25f, 0.2f), Vector3.one * Random.Range(0.35f, 0.6f), T.stones[Random.Range(0, T.stones.Length)]);
            rock.transform.localRotation = Quaternion.Euler(Random.Range(0f, 45f), Random.Range(0f, 90f), Random.Range(0f, 45f));
        }
        o.velocity = new Vector3(0f, 0f, -13f);
        o.Init();
        Sfx("pisao", 0.9f, 0.05f, 0.1f);
    }

    /// Serpente que rasteja de um lado para o outro (pode ser abatida ou pulada).
    public void SpawnSnake(float z, int tier)
    {
        var c = T.id == 3 ? new Color(0.85f, 0.7f, 0.25f) : new Color(0.25f, 0.6f, 0.2f);
        var o = MakeObstacle("Serpente", ObType.Mover, new Vector3(0f, 0.4f, z), new Vector3(0.9f, 0.4f, 0.6f), (1.5f + tier * 0.5f) * HpMul, 80, c);
        var t = o.transform;
        for (int k = 0; k < 6; k++)
        {
            float a = k * 0.9f;
            Prim(PrimitiveType.Sphere, t, new Vector3(Mathf.Sin(a) * 0.35f, -0.1f, k * 0.35f), Vector3.one * (0.5f - k * 0.05f), k % 2 == 0 ? c : c * 0.7f);
        }
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, -0.3f), new Vector3(0.45f, 0.3f, 0.5f), c * 0.9f);   // cabeça
        for (int sx = -1; sx <= 1; sx += 2)
            Prim(PrimitiveType.Cube, t, new Vector3(sx * 0.13f, 0.15f, -0.5f), new Vector3(0.08f, 0.06f, 0.05f), new Color(1f, 0.2f, 0.05f), true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.65f), new Vector3(0.05f, 0.03f, 0.25f), new Color(0.9f, 0.1f, 0.1f));   // língua
        o.moveFreq = Random.Range(1.4f, 2.2f) + tier * 0.1f;
        o.Init();
    }

    /// Servo invocado pelo chefe.
    public void SpawnMinion(int lane, float z)
    {
        if (Random.value < 0.35f) SpawnMover(z);
        else SpawnTarget(lane, z);
    }

    /// Trevas sobre a terra: a visão fica curta. t = 0 encerra na hora.
    public void BossDarkness(float t)
    {
        if (t <= 0f)
        {
            if (darknessTime > 0f) { darknessTime = 0f; ApplyAtmosphere(); }
            return;
        }
        darknessTime = t;
        var dark = new Color(0.03f, 0.02f, 0.05f);
        RenderSettings.fogColor = dark;
        RenderSettings.fogStartDistance = 5f;
        RenderSettings.fogEndDistance = 36f;
        if (cam != null) cam.backgroundColor = dark;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.intensity = T.sunIntensity * 0.3f;
        bannerText = "TREVAS!";
        bannerSub = "\"Trevas tais que se possam apalpar\" (Êx 10:21)";
        bannerTime = 2.5f;
        Sfx("julgamento", 0.8f, 0f, 0.5f);
    }

    void UpdateDarkness(float dt)
    {
        if (darknessTime <= 0f) return;
        darknessTime -= dt;
        if (darknessTime <= 0f) ApplyAtmosphere();
    }
}
