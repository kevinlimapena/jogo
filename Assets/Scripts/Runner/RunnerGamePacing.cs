using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ritmo entre os chefes (trechos: estrada, emboscada, descanso, campeão) e os campeões de elite.
/// </summary>
public partial class RunnerGame
{
    // ================================================================== ritmo entre os chefes

    enum Section { Estrada, Emboscada, Descanso, Campeao }
    static readonly Section[] SectionCycle = { Section.Estrada, Section.Emboscada, Section.Descanso, Section.Estrada, Section.Campeao };
    Section section = Section.Estrada;
    int sectionIdx;
    float sectionEndZ;
    RunnerObstacle champion;
    float fogBlend;

    bool PacingActive => player != null && !racing && !jericho && !passover && boss == null && !bossPending && !player.flying && !bulletHell && !babel && !goliath;

    void ResetPacing()
    {
        section = Section.Estrada;
        sectionIdx = 0;
        sectionEndZ = -1f;   // definido no primeiro quadro da jornada
        champion = null;
    }

    /// Depois do chefe: recomeça com um trecho de estrada.
    void RestartPacingAfterBoss()
    {
        section = Section.Estrada;
        sectionIdx = 0;
        sectionEndZ = player.transform.position.z + 200f;
    }

    /// Espaçamento das fileiras de inimigos no trecho atual.
    float SectionSpacing
    {
        get
        {
            if (!PacingActive) return 1f;
            switch (section)
            {
                case Section.Emboscada: return 0.85f;
                case Section.Descanso: return 1.7f;
                case Section.Campeao: return 1.3f;
                default: return 1f;
            }
        }
    }

    float SectionHazardBonus => PacingActive && section == Section.Emboscada ? 0.1f : 0f;

    void UpdatePacing(float dt)
    {
        // névoa um pouco avermelhada na emboscada
        float want = PacingActive && section == Section.Emboscada ? 1f : 0f;
        if (Mathf.Abs(fogBlend - want) > 0.001f && darknessTime <= 0f && !skyTransit)
        {
            fogBlend = Mathf.MoveTowards(fogBlend, want, dt * 0.8f);
            RenderSettings.fogColor = Color.Lerp(T.fogColor, new Color(0.55f, 0.18f, 0.12f), fogBlend * 0.35f);
        }

        if (!PacingActive) return;
        float pz = player.transform.position.z;
        if (sectionEndZ < 0f) sectionEndZ = pz + 280f;

        if (section == Section.Campeao)
        {
            // o trecho do campeão acaba quando ele morre ou foge
            if (champion == null || champion.dead) NextSection(pz);
            return;
        }
        if (pz >= sectionEndZ) NextSection(pz);
    }

    void NextSection(float pz)
    {
        sectionIdx++;
        section = SectionCycle[sectionIdx % SectionCycle.Length];
        switch (section)
        {
            case Section.Emboscada:
                sectionEndZ = pz + Random.Range(170f, 220f);
                Banner("EMBOSCADA!", "\"Os inimigos cercaram o caminho\"  —  mais inimigos por um trecho");
                bannerTime = 2.2f;
                Sfx("emboscada", 0.9f, 0f, 0.5f);
                break;
            case Section.Descanso:
                sectionEndZ = pz + Random.Range(120f, 150f);
                Banner("ÁGUAS TRANQUILAS", "\"Guia-me mansamente a águas tranquilas\" (Sl 23:2)  —  pegue os siclos");
                bannerTime = 2.2f;
                Sfx("mana", 0.7f, 0f, 0.5f);
                platformNextZ = 0f;   // caravana logo no começo do descanso
                break;
            case Section.Campeao:
                sectionEndZ = float.MaxValue;
                SpawnChampion(pz + 70f);
                break;
            default:
                sectionEndZ = pz + Random.Range(220f, 300f);
                break;
        }
    }

    /// Fileira do trecho de descanso: siclos, caravanas e quase nenhum perigo.
    bool SpawnCalmRow(float z, int[] lanes)
    {
        if (!PacingActive || section != Section.Descanso) return false;
        if (TrySpawnPlatformsCalm(z, lanes)) return true;
        float r = Random.value;
        if (r < 0.55f)
        {
            int lane = lanes[0];
            for (int i = 0; i < 5; i++) SpawnCoin(new Vector3(LaneX(lane), 1.1f, z + i * 2.4f));
        }
        else if (r < 0.65f && lives < maxLives && !Meta.OathOn("jejum")) SpawnHealth(lanes[0], z);
        else if (r < 0.8f) SpawnTarget(lanes[0], z);
        return true;
    }

    bool TrySpawnPlatformsCalm(float z, int[] lanes)
    {
        if (z < platformNextZ || Random.value > 0.4f) return false;
        float len = Random.Range(18f, 28f);
        SpawnPlatform(lanes[0], z, len);
        platformNextZ = z + len + Random.Range(25f, 45f);
        return true;
    }

    // ================================================================== campeões de elite

    static readonly string[] AffixNames = { "", "BLINDADO", "VELOZ", "DIVISOR", "ATIRADOR", "REGENERA" };
    static readonly string[] AffixDesc =
    {
        "",
        "Leva metade do dano — fogo, raio e críticos atravessam a armadura",
        "Troca de faixa o tempo todo",
        "Ao morrer, se divide em três",
        "Atira com o dobro da frequência",
        "Recupera vida — o fogo impede a cura",
    };
    static readonly Color[] AffixColors =
    {
        Color.white, new Color(0.6f, 0.75f, 0.9f), new Color(0.5f, 1f, 0.6f), new Color(0.85f, 0.5f, 1f), new Color(1f, 0.45f, 0.3f), new Color(0.4f, 1f, 0.8f),
    };

    void SpawnChampion(float z)
    {
        int lane = Random.Range(0, 3);
        SpawnTank(lane, z);
        var o = obstacles[obstacles.Count - 1];
        o.name = "Champion";
        o.elite = true;
        o.champion = true;
        o.affix = Random.Range(1, AffixNames.Length);
        o.eliteTime = 16f;
        float hp = Mathf.Max(40f, stats.EstimatedDps * 6.5f) * (1f + bossesDefeated * 0.15f);
        o.hp = o.maxHp = hp;
        o.points = 1000;
        o.transform.localScale = Vector3.one * 1.3f;
        o.half *= 1.3f;
        Color ac = AffixColors[o.affix];
        // aura do modificador + coroa de campeão
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, -1.15f, 0f), new Vector3(2.6f, 0.03f, 2.6f), ac, true);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 1.65f, -0.7f), new Vector3(0.5f, 0.25f, 0.5f), ac, true);
        o.Init();
        o.mainColor = ac;
        champion = o;
        Banner("CAMPEÃO " + AffixNames[o.affix] + "!", AffixDesc[o.affix] + "  —  derrote-o antes que fuja");
        bannerTime = 3f;
        Sfx("rugido", 0.8f, 0.05f, 0.5f);
        shake = Mathf.Max(shake, 0.4f);
    }

    /// Campeão: fica à frente do jogador por um tempo e depois foge.
    void UpdateChampion(float dt)
    {
        var o = champion;
        if (o == null || o.dead || player == null) return;
        var p = o.transform.position;
        var pp = player.transform.position;
        o.eliteTime -= dt;
        // fica ao alcance da sua arma (corpo a corpo: a onda de luz alcança) e, depois do tempo, foge para frente
        float hold = o.eliteTime > 0f ? Mathf.Clamp(stats.Range * 0.7f, 12f, 26f) : 200f;
        float targetZ = pp.z + hold;
        if (p.z > targetZ) p.z = Mathf.MoveTowards(p.z, targetZ, (speed + 6f) * dt);   // chega até a distância
        else p.z = Mathf.MoveTowards(p.z, targetZ, (speed + (o.eliteTime > 0f ? 2f : 30f)) * dt);
        if (o.affix == 2) p.x = Mathf.Sin(Time.time * 1.6f) * LaneWidth;   // veloz
        if (o.affix == 5 && o.burnT <= 0f) o.hp = Mathf.Min(o.maxHp, o.hp + o.maxHp * 0.035f * dt);   // regenera
        o.transform.position = p;
        ChampionShoot(dt);
        if (o.eliteTime <= 0f && p.z - pp.z > 150f)
        {
            AddFloat(pp + new Vector3(0f, 2.5f, 8f), "O CAMPEÃO FUGIU...", new Color(1f, 0.7f, 0.5f), true);
            o.dead = true;
            obstacles.Remove(o);
            Destroy(o.gameObject);
            champion = null;
        }
    }

    float championShotT;

    /// O campeão atira de tempos em tempos (o ATIRADOR, com o dobro da frequência).
    void ChampionShoot(float dt)
    {
        var o = champion;
        if (o == null || o.dead || o.eliteTime <= 0f || player == null) return;
        championShotT -= dt;
        if (championShotT > 0f) return;
        championShotT = o.affix == 4 ? 0.9f : 1.8f;
        var pp = player.transform.position;
        Vector3 from = o.transform.position + new Vector3(0f, 0.2f, -1.5f);
        float time = Mathf.Max(0.3f, (from.z - pp.z) / (speed + 14f));
        SpawnEnemyShot(from, new Vector3((pp.x - from.x) / time, 0f, -14f), 0f);
    }

    /// Blindado: metade do dano, exceto fogo, raio, combos e críticos.
    float ChampionDamageMod(RunnerObstacle o, float dmg, bool crit)
    {
        if (o.affix == 1 && !crit && hitKind == 0 && !inCombo) dmg *= 0.5f;
        return dmg;
    }

    /// Quando um campeão morre.
    void OnChampionKilled(RunnerObstacle o, Vector3 pos)
    {
        if (!o.champion) return;
        Sfx("impacto", 1f, 0f, 0.1f);
        shake = Mathf.Max(shake, 0.6f);
        FxSphere(pos, 6f, AffixColors[o.affix]);
        AddSiclos(15, pos + Vector3.up * 2.5f);
        if (lives < maxLives && Random.value < 0.4f) { Heal(1); AddFloat(player.transform.position + Vector3.up * 2f, "+1 VIDA", new Color(1f, 0.3f, 0.4f), true); }
        Banner("CAMPEÃO DERROTADO!", "+15 siclos");
        bannerTime = 2f;
        if (o.affix == 3)
        {
            // divisor: três pedaços menores
            for (int lane = 0; lane < 3; lane++) SpawnTarget(lane, pos.z + 4f + lane * 2f);
        }
        if (champion == o) champion = null;
    }

    /// Nome e barra de vida do campeão.
    void DrawChampionLabel(float s, float H)
    {
        var o = champion;
        if (o == null || o.dead) return;
        Vector3 sp = WorldToScreen(o.transform.position + Vector3.up * (o.half.y + 1.1f));
        if (sp.z <= 0f || sp.z > 120f) return;
        float w = 160 * s, h = 10 * s;
        var r = new Rect(sp.x - w / 2, H - sp.y, w, h);
        Color ac = AffixColors[o.affix];
        floatStyle.fontSize = Mathf.RoundToInt(22 * s);
        ShadowLabel(new Rect(sp.x - 150 * s, r.y - 30 * s, 300 * s, 28 * s), "CAMPEÃO " + AffixNames[o.affix], floatStyle, ac);
        Box(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0, 0, 0, 0.75f));
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(o.hp / o.maxHp), r.height), ac);
        if (o.eliteTime > 0f)
            Box(new Rect(r.x, r.yMax + 2 * s, r.width * Mathf.Clamp01(o.eliteTime / 16f), 3 * s), new Color(1f, 1f, 1f, 0.6f));
    }
}
