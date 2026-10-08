using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carroças de caravana: plataformas compridas com rampa. Suba pela rampa (ou pule em cima),
/// corra lá no alto por um tempo — longe dos inimigos do chão e pegando siclos — e depois desça.
/// Bater na lateral da carroça empurra você de volta para a faixa ao lado.
/// </summary>
public partial class RunnerGame
{
    const float PlatTop = 2.1f;      // altura do teto da carroça
    const float PlatRamp = 6f;       // comprimento da rampa
    float platformNextZ;
    float platBumpCd;

    readonly List<RunnerObstacle> platforms = new List<RunnerObstacle>();

    bool PlatformsAllowed => !racing && !jericho && !passover && boss == null && !bossPending && !player.flying && !bulletHell && !babel && !goliath;

    /// Chamado pelo SpawnRowBase: às vezes vem uma caravana (às vezes duas lado a lado).
    bool TrySpawnPlatforms(float z, int[] lanes)
    {
        if (!PlatformsAllowed || z < platformNextZ || Random.value > 0.09f) return false;
        float len = Random.Range(16f, 28f);
        SpawnPlatform(lanes[0], z, len);
        float end = z + len;
        if (Difficulty > 0.3f && Random.value < 0.35f)
        {
            float z2 = z + Random.Range(2f, 10f), len2 = Random.Range(14f, 24f);
            SpawnPlatform(lanes[1], z2, len2);
            end = Mathf.Max(end, z2 + len2);
        }
        platformNextZ = end + Random.Range(60f, 110f);
        return true;
    }

    public void SpawnPlatform(int lane, float z0, float len)
    {
        float z1 = z0 + len;
        var wood = new Color(0.5f, 0.32f, 0.17f);
        var woodDark = new Color(0.33f, 0.2f, 0.1f);
        var plank = new Color(0.68f, 0.48f, 0.28f);
        var cloth = T.accent;
        var o = MakeObstacle("Caravana", ObType.Platform, new Vector3(LaneX(lane), 0f, z1), Vector3.one * 0.01f, 9999f, 0, wood);
        o.platZ0 = z0;
        o.platZ1 = z1;
        o.platTop = PlatTop;
        o.platRamp = PlatRamp;
        o.state = lane;
        var t = o.transform;
        float H = PlatTop, R = PlatRamp, body = len - R;

        // rampa (tábua inclinada) + calços
        float rampLen = Mathf.Sqrt(R * R + H * H);
        float ang = Mathf.Atan2(H, R) * Mathf.Rad2Deg;
        var ramp = Prim(PrimitiveType.Cube, t, new Vector3(0f, H / 2f - 0.05f, -len + R / 2f), new Vector3(2.3f, 0.14f, rampLen), plank);
        ramp.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
        for (int k = 1; k <= 4; k++)
        {
            float f = k / 5f;
            Prim(PrimitiveType.Cube, t, new Vector3(0f, H * f / 2f, -len + R * f), new Vector3(2.0f, H * f, 0.2f), woodDark);
        }
        for (int k = 0; k < 6; k++)   // ripas da rampa
        {
            var rip = Prim(PrimitiveType.Cube, t, new Vector3(0f, H * (k + 0.5f) / 6f + 0.04f, -len + R * (k + 0.5f) / 6f), new Vector3(2.35f, 0.05f, 0.12f), woodDark);
            rip.transform.localRotation = Quaternion.Euler(-ang, 0f, 0f);
        }

        // corpo da carroça
        Prim(PrimitiveType.Cube, t, new Vector3(0f, (0.45f + H) / 2f, -body / 2f), new Vector3(2.5f, H - 0.45f, body), wood);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, H, -body / 2f), new Vector3(2.6f, 0.12f, body + 0.1f), plank);           // assoalho de cima
        for (int sx = -1; sx <= 1; sx += 2)
        {
            Prim(PrimitiveType.Cube, t, new Vector3(sx * 1.26f, H * 0.62f, -body / 2f), new Vector3(0.04f, 0.45f, body - 0.4f), cloth);      // faixa colorida
            Prim(PrimitiveType.Cube, t, new Vector3(sx * 1.27f, H * 0.62f, -body / 2f), new Vector3(0.03f, 0.08f, body - 0.4f), Gold, true);
            int wheels = Mathf.Max(2, Mathf.RoundToInt(body / 4.5f));
            for (int w = 0; w < wheels; w++)
            {
                float wz = -body + 1.2f + w * (body - 2.4f) / Mathf.Max(1, wheels - 1);
                var wh = Prim(PrimitiveType.Cylinder, t, new Vector3(sx * 1.3f, 0.48f, wz), new Vector3(0.95f, 0.08f, 0.95f), woodDark);
                wh.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var hub = Prim(PrimitiveType.Cylinder, t, new Vector3(sx * 1.36f, 0.48f, wz), new Vector3(0.3f, 0.05f, 0.3f), Gold);
                hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }
        // frente com borda dourada (de onde o jogador desce)
        Prim(PrimitiveType.Cube, t, new Vector3(0f, H + 0.08f, -0.05f), new Vector3(2.6f, 0.1f, 0.1f), Gold, true);
        o.Init();
        platforms.Add(o);

        // siclos em cima (e às vezes um rolo de cura no fim)
        for (float cz = z0 + R + 1.5f; cz < z1 - 1f; cz += 2.6f)
            SpawnCoin(new Vector3(LaneX(lane), H + 1.0f, cz));
        if (lives < maxLives && !Meta.OathOn("jejum") && Random.value < 0.15f)
        {
            SpawnHealth(lane, z1 - 0.5f);
            var hp = obstacles[obstacles.Count - 1];
            hp.transform.position = new Vector3(LaneX(lane), H + 1.2f, z1 - 0.5f);
        }
    }

    void SpawnCoin(Vector3 pos)
    {
        var c = new Color(1f, 0.82f, 0.3f);
        var o = MakeObstacle("Siclo", ObType.Coin, pos, new Vector3(0.55f, 0.55f, 0.4f), 1f, 0, c);
        var disc = Prim(PrimitiveType.Cylinder, o.transform, Vector3.zero, new Vector3(0.6f, 0.05f, 0.6f), c, true);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, 0f, -0.03f), new Vector3(0.35f, 0.06f, 0.35f), new Color(0.75f, 0.55f, 0.15f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        o.gameObject.AddComponent<RunnerSpin>().speed = 200f;
        o.airborne = true;
        o.Init();
    }

    /// Altura do "chão" em (x, z): o teto da carroça ou a rampa, se o jogador estiver em cima (ou chegando por cima).
    public float GroundAt(float x, float z, float feetY)
    {
        float best = 0f;
        for (int i = platforms.Count - 1; i >= 0; i--)
        {
            var o = platforms[i];
            if (o == null || o.dead) { platforms.RemoveAt(i); continue; }
            if (z < o.platZ0 || z > o.platZ1) continue;
            if (Mathf.Abs(x - o.transform.position.x) > 1.25f) continue;
            bool onRamp = z < o.platZ0 + o.platRamp;
            float h = onRamp ? o.platTop * (z - o.platZ0) / o.platRamp : o.platTop;
            // na rampa você sempre sobe; no corpo, só conta se vier por cima (senão é a lateral da carroça)
            if ((onRamp || feetY >= h - 0.6f) && h > best) best = h;
        }
        return best;
    }

    /// Batidas na lateral + limpeza do que nasceu dentro da carroça.
    void UpdatePlatforms(float dt)
    {
        if (platBumpCd > 0f) platBumpCd -= dt;
        if (platforms.Count == 0) return;
        var pp = player.transform.position;
        foreach (var pl in platforms)
        {
            if (pl == null || pl.dead) continue;
            float px = pl.transform.position.x;

            // lateral: o jogador entrou de lado, por baixo do teto
            if (platBumpCd <= 0f && pp.z > pl.platZ0 + pl.platRamp * 0.7f && pp.z < pl.platZ1
                && Mathf.Abs(pp.x - px) < 1.6f && player.feetY < pl.platTop - 0.6f && player.lane == pl.state)
            {
                int back = pp.x > px ? pl.state + 1 : pl.state - 1;
                if (back < 0 || back > 2) back = pl.state == 0 ? 1 : (pl.state == 2 ? 1 : (pp.x > px ? 2 : 0));
                player.lane = back;
                platBumpCd = 0.4f;
                shake = Mathf.Max(shake, 0.25f);
                PlayClank();
                AddFloat(pp + Vector3.up * 1.6f, "BUM!", new Color(1f, 0.8f, 0.5f), false);
            }
        }

        // nada nasce dentro da carroça (só o que fica por cima)
        var snap = SnapshotObstacles();
        foreach (var o in snap)
        {
            if (o == null || o.dead || o.type == ObType.Platform || o.type == ObType.Boss || o.type == ObType.BossDrone || o.type == ObType.EnemyShot || o.type == ObType.Flyer) continue;
            var c = o.transform.position;
            if (c.z - pp.z < 25f) continue;
            if (c.y - o.half.y > PlatTop - 0.2f) continue;   // está em cima: fica
            foreach (var pl in platforms)
            {
                if (pl == null || pl.dead) continue;
                if (c.z + o.half.z < pl.platZ0 - 1.5f || c.z - o.half.z > pl.platZ1 + 0.5f) continue;
                if (Mathf.Abs(c.x - pl.transform.position.x) > 1.3f + o.half.x) continue;
                o.dead = true;
                obstacles.Remove(o);
                Destroy(o.gameObject);
                break;
            }
        }
        ReleaseList(snap);
    }

    /// Pegou um siclo.
    void PickCoin(RunnerObstacle o)
    {
        Vector3 at = o.transform.position;
        o.dead = true;
        obstacles.Remove(o);
        Destroy(o.gameObject);
        AddSiclos(1);   // sem texto flutuante: só o som (tela limpa)
    }
}
