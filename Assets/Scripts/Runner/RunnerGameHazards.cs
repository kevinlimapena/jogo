using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Obstáculos e inimigos novos:
///   • só morrem com um PISÃO (tiro e espada ricocheteiam): Escudeiro, Muralha de Barro
///   • INDESTRUTÍVEIS (desvie ou acerte o tempo): Pedra Rolante, Colosso de Daniel 2, Carro de Guerra, Fornalha, Espinhos
///   • inimigos comuns novos: Fundibulário (pedras em arco com sombra no chão) e Saltador (rã / gafanhoto)
/// </summary>
public partial class RunnerGame
{
    int stompChain;
    float immuneHintCd;
    readonly HashSet<ObType> hintsShown = new HashSet<ObType>();

    /// Chance de uma fileira ser de obstáculos novos (cresce com a dificuldade).
    float HazardChance => Mathf.Min(0.7f, 0.22f + 0.28f * Difficulty + LevelThreat * 0.06f + SectionHazardBonus);

    // ================================================================== fileiras

    void SpawnHazardRow(float z, int[] lanes)
    {
        float d = Difficulty;
        float r = Random.value;
        if (r < 0.16f)
        {
            // escudeiros: pise em cima
            int n = d > 0.35f && Random.value < 0.5f ? 2 : 1;
            for (int i = 0; i < n; i++) SpawnShielded(lanes[i], z + i * 3f);
            if (d > 0.3f && Random.value < 0.5f) SpawnTarget(lanes[2], z + 2f);
        }
        else if (r < 0.28f)
        {
            // muralha de barro em todas as faixas: pule (ou pise e quebre)
            for (int l = 0; l < 3; l++) SpawnCrackWall(l, z);
            if (d > 0.4f && Random.value < 0.5f) SpawnShielded(lanes[0], z + 7f);
        }
        else if (r < 0.40f)
        {
            SpawnBoulder(lanes[0], z + 30f);
            if (d > 0.35f && Random.value < 0.5f) SpawnWall(lanes[1], z);
        }
        else if (r < 0.50f && d > 0.15f)
        {
            SpawnColossus(lanes[0], z + 10f);
            if (Random.value < 0.5f) SpawnTarget(lanes[1], z);
        }
        else if (r < 0.60f && d > 0.12f)
        {
            SpawnCharger(lanes[0], z + 20f);
            if (d > 0.4f && Random.value < 0.4f) SpawnCharger(lanes[1], z + 34f);
        }
        else if (r < 0.71f)
        {
            // fornalhas em sequência: as fases se alternam (ache o ritmo)
            int n = d > 0.3f ? 3 : 2;
            for (int i = 0; i < n; i++) SpawnFireJet(lanes[i], z, i * 0.9f);
        }
        else if (r < 0.80f)
        {
            for (int l = 0; l < 3; l++) SpawnSpikes(l, z, l == lanes[0] ? 0.3f : 1.4f);
        }
        else if (r < 0.90f && d > 0.1f)
        {
            SpawnSlinger(lanes[0], z + 8f);
            if (d > 0.35f) SpawnShielded(lanes[1], z);
        }
        else
        {
            int n = d > 0.3f ? 3 : 2;
            for (int i = 0; i < n; i++) SpawnHopper(lanes[i], z + i * 4f);
        }
    }

    // ================================================================== pisão

    /// O jogador caiu em cima de um inimigo "pisável"? Retorna true se pisou.
    bool TryStomp(RunnerObstacle o)
    {
        // estilo "Mario": se os pés estão acima da metade da altura do inimigo, é pisão; abaixo disso, machuca
        if (player.feetY < o.transform.position.y - 0.05f) return false;
        if (player.VerticalSpeed > 4f && player.feetY < o.transform.position.y + o.half.y * 0.5f) return false;   // subindo de raspão por baixo
        stompChain++;
        Vector3 at = o.transform.position;
        DestroyObstacle(o, true);
        int bonus = Mathf.RoundToInt(50 * stompChain * stats.scoreMul);
        killScore += bonus;
        AddFloat(at + Vector3.up * 2f, stompChain > 1 ? "PISÃO x" + stompChain + "!" : "PISÃO!", new Color(1f, 0.85f, 0.3f), true);
        Explode(at + Vector3.up * 0.5f, o.mainColor, 14);
        player.Bounce(player.jumpVelocity * 0.85f, stats.doubleJump);
        player.invuln = Mathf.Max(player.invuln, 0.25f);
        shake = Mathf.Max(shake, 0.3f);
        Sfx("pisao", 0.9f, 0.08f, 0.05f, 1f + Mathf.Min(stompChain, 6) * 0.06f);
        if (stats.stompLevel > 0) OnPlayerLanded(at);   // Pisão do Querubim também explode aqui
        return true;
    }

    /// Aviso quando o tiro bate num inimigo que não pode ser destruído assim.
    public void ImmuneHint(RunnerObstacle o)
    {
        if (!HintsOn || Time.unscaledTime < immuneHintCd) return;
        immuneHintCd = Time.unscaledTime + 1.2f;
        AddFloat(o.transform.position + Vector3.up * (o.half.y + 0.8f), o.stompable ? "PISE EM CIMA!" : "INDESTRUTÍVEL!", o.stompable ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.35f, 0.3f), false);
    }

    /// Mostra uma dica na primeira vez que cada tipo aparece na jornada.
    void HazardIntro(RunnerObstacle o)
    {
        if (!HintsOn || hintsShown.Contains(o.type)) return;
        float dz = o.transform.position.z - player.transform.position.z;
        if (dz > 45f || dz < 5f) return;
        hintsShown.Add(o.type);
        string t = null, sub = null;
        switch (o.type)
        {
            case ObType.Shielded: t = "ESCUDEIRO"; sub = "Tiros ricocheteiam no escudo: PULE EM CIMA dele!"; break;
            case ObType.CrackWall: t = "MURALHA DE BARRO"; sub = "Pule por cima — ou caia em cima para quebrá-la"; break;
            case ObType.Boulder: t = "PEDRA ROLANTE"; sub = "Indestrutível: troque de faixa!"; break;
            case ObType.Colossus: t = "A ESTÁTUA DO SONHO"; sub = "Cabeça de ouro, pés de barro (Dn 2:32) — indestrutível, desvie"; break;
            case ObType.Charger: t = "CARRO DE GUERRA"; sub = "Quando ele empinar, saia da faixa! (Êx 14:7)"; break;
            case ObType.FireJet: t = "FORNALHA"; sub = "Passe quando o fogo baixar (Dn 3:21)"; break;
            case ObType.Spikes: t = "ESPINHOS"; sub = "Pule ou passe quando estiverem abaixados"; break;
            case ObType.Slinger: t = "FUNDIBULÁRIO"; sub = "Fuja da sombra vermelha: é onde a pedra vai cair (Jz 20:16)"; break;
            case ObType.Platform: t = "CARAVANA"; sub = "Suba pela rampa e corra lá em cima: pegue os siclos, longe dos inimigos do chão"; break;
        }
        if (t == null || bannerTime > 0.5f) return;
        bannerText = t;
        bannerSub = sub;
        bannerTime = 2.6f;
    }

    void UpdateHazards()
    {
        if (player.feetY <= player.groundY + 0.001f) stompChain = 0;
        foreach (var o in obstacles)
            if (o != null && !o.dead && (o.stompable || o.Indestructible || o.type == ObType.Slinger || o.type == ObType.Platform)) HazardIntro(o);
    }

    // ================================================================== criação

    public void SpawnShielded(int lane, float z)
    {
        // escudeiro agachado atrás de um grande escudo (testudo romano, guarda babilônico, guarda egípcio, demônio de pedra)
        Color shieldC = T.id == 1 ? new Color(0.65f, 0.1f, 0.1f) : (T.id == 2 ? new Color(0.2f, 0.16f, 0.16f) : (T.id == 3 ? new Color(0.85f, 0.7f, 0.4f) : T.accent));
        Color skin = T.id == 2 ? new Color(0.35f, 0.08f, 0.06f) : new Color(0.72f, 0.52f, 0.36f);
        var o = MakeObstacle("Escudeiro", ObType.Shielded, new Vector3(LaneX(lane), 0.65f, z), new Vector3(1.0f, 0.65f, 0.6f), 9999f, 150, shieldC);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.35f), new Vector3(1.9f, 1.3f, 0.18f), shieldC);                    // escudo
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.45f), new Vector3(0.45f, 0.45f, 0.08f), T.metal, true);            // umbo dourado
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, -0.45f), new Vector3(1.9f, 0.08f, 0.06f), T.metal);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.1f, 0.15f), new Vector3(1.0f, 1.0f, 0.7f), skin * 0.8f);                // corpo agachado
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.62f, 0.05f), Vector3.one * 0.5f, skin);
        var helm = Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.82f, 0.05f), new Vector3(0.55f, 0.22f, 0.55f), T.id == 2 ? new Color(0.1f, 0.08f, 0.08f) : new Color(0.75f, 0.6f, 0.3f));
        if (T.id == 1) Prim(PrimitiveType.Cube, helm.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.2f, 1.2f, 1.1f), new Color(0.8f, 0.1f, 0.1f));   // crista romana
        if (T.id == 2)
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, t, new Vector3(s * 0.22f, 1.05f, 0.05f), new Vector3(0.08f, 0.3f, 0.08f), new Color(1f, 0.4f, 0.1f), true);
        // seta brilhante apontando para baixo: "pise aqui"
        var arrow = Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.55f, 0f), new Vector3(0.35f, 0.35f, 0.05f), new Color(1f, 0.85f, 0.25f), true);
        arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        o.stompable = true;
        o.baseY = 0.65f;
        o.Init();
    }

    public void SpawnCrackWall(int lane, float z)
    {
        var c = T.id == 2 ? new Color(0.25f, 0.18f, 0.16f) : T.stones[2] * 0.95f;
        var o = MakeObstacle("MuralhaDeBarro", ObType.CrackWall, new Vector3(LaneX(lane), 0.55f, z), new Vector3(1.35f, 0.55f, 0.45f), 9999f, 60, c);
        var t = o.transform;
        for (int row = 0; row < 3; row++)
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, t, new Vector3(-1.05f + k * 0.7f + (row % 2) * 0.35f - 0.17f, -0.37f + row * 0.37f, 0f), new Vector3(0.66f, 0.34f, 0.9f), (k + row) % 2 == 0 ? c : c * 0.88f);
        // rachaduras brilhantes: é frágil por cima
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.56f, 0f), new Vector3(2.6f, 0.05f, 0.92f), new Color(1f, 0.75f, 0.3f), true);
        Prim(PrimitiveType.Cube, t, new Vector3(0.3f, 0.1f, -0.46f), new Vector3(0.05f, 0.7f, 0.02f), T.dark);
        o.stompable = true;
        o.Init();
    }

    public void SpawnBoulder(int lane, float z)
    {
        var c = T.id == 2 ? new Color(0.3f, 0.12f, 0.08f) : T.stones[3];
        var o = MakeObstacle("PedraRolante", ObType.Boulder, new Vector3(LaneX(lane), 1.25f, z), new Vector3(1.15f, 1.25f, 1.1f), 9999f, 0, c);
        var rock = new GameObject("Rocha").transform;
        rock.SetParent(o.transform, false);
        Prim(PrimitiveType.Sphere, rock, Vector3.zero, Vector3.one * 2.5f, c);
        for (int i = 0; i < 6; i++)
            Prim(PrimitiveType.Cube, rock, Random.onUnitSphere * 1.05f, Vector3.one * Random.Range(0.5f, 0.8f), c * Random.Range(0.8f, 1.05f));
        if (T.id == 2) Prim(PrimitiveType.Sphere, rock, Vector3.zero, Vector3.one * 2.3f, new Color(1f, 0.35f, 0.05f), true);
        // trilha de aviso no chão à frente da pedra
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, -1.2f, -6f), new Vector3(1.4f, 0.03f, 10f), new Color(1f, 0.3f, 0.2f), true);
        o.fx = rock;
        o.velocity = new Vector3(0f, 0f, -15f - Difficulty * 6f);
        o.Init();
    }

    public void SpawnColossus(int lane, float z)
    {
        // a estátua do sonho de Nabucodonosor (Dn 2:32-33) — ou a versão de cada região
        Color head, chest, belly, legs, feet;
        if (T.id == 3) { head = new Color(0.1f, 0.09f, 0.1f); chest = T.metal; belly = new Color(0.1f, 0.09f, 0.1f); legs = new Color(0.1f, 0.09f, 0.1f); feet = T.metal; }        // Anúbis
        else if (T.id == 1) { head = T.stones[0]; chest = T.stones[0]; belly = new Color(0.65f, 0.1f, 0.1f); legs = T.stones[1]; feet = T.stones[2]; }                          // centurião de mármore
        else if (T.id == 2) { head = new Color(0.08f, 0.06f, 0.07f); chest = new Color(0.12f, 0.08f, 0.08f); belly = new Color(1f, 0.35f, 0.05f); legs = new Color(0.1f, 0.07f, 0.07f); feet = new Color(0.1f, 0.07f, 0.07f); } // gigante de obsidiana
        else { head = new Color(1f, 0.8f, 0.25f); chest = new Color(0.82f, 0.84f, 0.88f); belly = new Color(0.72f, 0.48f, 0.25f); legs = new Color(0.35f, 0.36f, 0.4f); feet = new Color(0.6f, 0.45f, 0.3f); }
        var o = MakeObstacle("Colosso", ObType.Colossus, new Vector3(LaneX(lane), 2.4f, z), new Vector3(1.2f, 2.4f, 0.8f), 9999f, 0, chest);
        var t = o.transform;
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.4f, -1.3f, 0f), new Vector3(0.5f, 1.6f, 0.55f), legs);      // pernas de ferro
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.4f, -2.2f, -0.1f), new Vector3(0.6f, 0.4f, 0.8f), feet);     // pés de ferro e barro
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.95f, 0.6f, 0f), new Vector3(0.38f, 1.4f, 0.45f), chest);     // braços de prata
        }
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.25f, 0f), new Vector3(1.4f, 0.7f, 0.8f), belly);                // ventre de bronze
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 1.3f, 0.9f), chest);                 // peito de prata
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.8f, 0f), new Vector3(0.75f, 0.8f, 0.75f), head);                 // cabeça de ouro
        if (T.id == 3)
            for (int s = -1; s <= 1; s += 2)
                Prim(PrimitiveType.Cube, t, new Vector3(s * 0.22f, 2.45f, 0f), new Vector3(0.14f, 0.6f, 0.12f), head);   // orelhas de chacal
        for (int s = -1; s <= 1; s += 2)
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.17f, 1.85f, -0.38f), new Vector3(0.12f, 0.08f, 0.04f), new Color(1f, 0.25f, 0.1f), true);
        // rachadura nos pés de barro
        Prim(PrimitiveType.Cube, t, new Vector3(0.4f, -2.2f, -0.51f), new Vector3(0.05f, 0.35f, 0.02f), T.dark);
        o.baseY = 2.4f;
        o.timer = Random.Range(1f, 2f);
        o.Init();
    }

    public void SpawnCharger(int lane, float z)
    {
        var wood = new Color(0.45f, 0.3f, 0.16f);
        var c = T.id == 2 ? new Color(0.2f, 0.08f, 0.06f) : (T.id == 3 ? T.metal : (T.id == 1 ? new Color(0.65f, 0.1f, 0.1f) : T.accent));
        var o = MakeObstacle("CarroDeGuerra", ObType.Charger, new Vector3(LaneX(lane), 1.1f, z), new Vector3(1.1f, 1.1f, 1.8f), 9999f, 0, c);
        var t = o.transform;
        // cavalos à frente (virados para o jogador)
        var horse = T.id == 2 ? new Color(0.1f, 0.05f, 0.05f) : new Color(0.35f, 0.22f, 0.12f);
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.45f, 0f, -1.4f), new Vector3(0.45f, 0.65f, 1.4f), horse);
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.45f, 0.55f, -2.15f), new Vector3(0.3f, 0.55f, 0.5f), horse);
            Prim(PrimitiveType.Cube, t, new Vector3(s * 0.45f, 0.65f, -2.42f), new Vector3(0.08f, 0.08f, 0.04f), new Color(1f, 0.2f, 0.1f), true);
            for (int k = -1; k <= 1; k += 2)
                Prim(PrimitiveType.Cube, t, new Vector3(s * 0.45f + k * 0.12f, -0.7f, -1.4f + k * 0.4f), new Vector3(0.12f, 0.8f, 0.12f), horse);
        }
        // carro com rodas de lâminas
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.1f, 0.6f), new Vector3(1.5f, 0.9f, 1.1f), c);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0.6f), new Vector3(1.55f, 0.1f, 1.15f), T.metal);
        for (int s = -1; s <= 1; s += 2)
        {
            var wheel = Prim(PrimitiveType.Cylinder, t, new Vector3(s * 0.85f, -0.45f, 0.6f), new Vector3(1.1f, 0.06f, 1.1f), wood);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, t, new Vector3(s * 1.05f, -0.45f, 0.6f), new Vector3(0.6f, 0.06f, 0.06f), new Color(0.8f, 0.8f, 0.85f));   // foice no eixo
        }
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.05f, -6f), new Vector3(1.4f, 0.03f, 8f), new Color(1f, 0.3f, 0.2f), true);   // faixa de aviso
        o.Init();
    }

    public void SpawnFireJet(int lane, float z, float phase)
    {
        var o = MakeObstacle("Fornalha", ObType.FireJet, new Vector3(LaneX(lane), 1.6f, z), new Vector3(1.1f, 1.6f, 0.7f), 9999f, 0, new Color(1f, 0.5f, 0.1f));
        var t = o.transform;
        var stone = T.id == 2 ? new Color(0.12f, 0.1f, 0.1f) : T.stones[3] * 0.8f;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.45f, 0f), new Vector3(2.2f, 0.3f, 1.4f), stone);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.3f, 0f), new Vector3(1.4f, 0.04f, 0.8f), new Color(0.15f, 0.05f, 0.02f));
        var flame = new GameObject("Chamas").transform;
        flame.SetParent(t, false);
        flame.localPosition = new Vector3(0f, -1.3f, 0f);
        Prim(PrimitiveType.Cube, flame, new Vector3(0f, 1.6f, 0f), new Vector3(1.6f, 3.2f, 0.9f), new Color(1f, 0.45f, 0.08f), true);
        Prim(PrimitiveType.Cube, flame, new Vector3(0f, 1.3f, 0f), new Vector3(0.9f, 2.6f, 0.5f), new Color(1f, 0.85f, 0.3f), true);
        o.fx = flame;
        o.state = 0;
        o.timer = 0.2f + phase;
        o.hazardOn = false;
        o.Init();
    }

    public void SpawnSpikes(int lane, float z, float phase)
    {
        var o = MakeObstacle("Espinhos", ObType.Spikes, new Vector3(LaneX(lane), 0.35f, z), new Vector3(1.2f, 0.35f, 0.6f), 9999f, 0, new Color(0.7f, 0.7f, 0.75f));
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.33f, 0f), new Vector3(2.5f, 0.06f, 1.3f), T.dark);
        var sp = new GameObject("Pontas").transform;
        sp.SetParent(t, false);
        sp.localPosition = new Vector3(0f, -0.55f, 0f);
        var metal = T.id == 2 ? new Color(1f, 0.4f, 0.1f) : new Color(0.75f, 0.75f, 0.8f);
        for (int i = -2; i <= 2; i++)
            for (int k = -1; k <= 1; k += 2)
            {
                var s = Prim(PrimitiveType.Cube, sp, new Vector3(i * 0.48f, 0f, k * 0.3f), new Vector3(0.16f, 0.65f, 0.16f), metal, T.id == 2);
                s.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
        o.fx = sp;
        o.timer = phase;
        o.hazardOn = false;
        o.Init();
    }

    public void SpawnSlinger(int lane, float z)
    {
        var tunic = new Color(0.55f, 0.35f, 0.2f);
        var o = MakeObstacle("Fundibulario", ObType.Slinger, new Vector3(LaneX(lane), 1.1f, z), new Vector3(0.6f, 1.1f, 0.4f), 2.5f * HpMul, 140, tunic);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.2f, 0f), new Vector3(0.7f, 1.3f, 0.45f), tunic);
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.45f, new Color(0.72f, 0.52f, 0.36f));
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.95f, 0f), new Vector3(0.5f, 0.15f, 0.5f), T.accent);   // turbante / penacho filisteu
        var sling = new GameObject("Funda").transform;
        sling.SetParent(t, false);
        sling.localPosition = new Vector3(0.35f, 1.3f, 0f);
        Prim(PrimitiveType.Cube, sling, new Vector3(0.45f, 0f, 0f), new Vector3(0.9f, 0.03f, 0.03f), new Color(0.4f, 0.28f, 0.15f));
        Prim(PrimitiveType.Sphere, sling, new Vector3(0.9f, 0f, 0f), Vector3.one * 0.18f, new Color(0.7f, 0.68f, 0.62f));
        o.fx = sling;
        o.fireTimer = Random.Range(0.6f, 1.4f);
        o.Init();
    }

    /// Pedra em arco que cai exatamente onde o jogador vai estar; a sombra vermelha marca o ponto.
    public void SpawnSlingStone(Vector3 from)
    {
        var pp = player.transform.position;
        float T0 = 1.15f;
        int lane = Mathf.Clamp(Mathf.RoundToInt(pp.x / LaneWidth) + 1, 0, 2);
        Vector3 land = new Vector3(LaneX(lane), 0.35f, pp.z + speed * T0 + 0.5f);
        var v = new Vector3((land.x - from.x) / T0, 0f, (land.z - from.z) / T0);
        v.y = (land.y - from.y + 0.5f * 22f * T0 * T0) / T0;
        var o = MakeObstacle("PedraDaFunda", ObType.SlingStone, from, new Vector3(0.45f, 0.45f, 0.45f), 9999f, 0, new Color(0.7f, 0.68f, 0.62f));
        Prim(PrimitiveType.Sphere, o.transform, Vector3.zero, Vector3.one * 0.7f, new Color(0.62f, 0.6f, 0.55f));
        o.velocity = v;
        o.airborne = true;
        MeshRenderer mR;
        var m = RunnerGame.NewPrim(PrimitiveType.Cylinder, out mR, false);
        mR.sharedMaterial = Glow(new Color(1f, 0.15f, 0.1f));
        m.transform.position = new Vector3(land.x, 0.04f, land.z);
        m.transform.localScale = new Vector3(1.8f, 0.02f, 1.8f);
        o.marker = m;
        o.Init();
        Sfx("giro", 0.4f, 0.1f, 0.2f);
    }

    public void StoneLanded(RunnerObstacle o)
    {
        Explode(o.transform.position, new Color(0.6f, 0.55f, 0.45f), 8);
        Sfx("pedra", 0.5f, 0.1f, 0.1f);
        DestroyObstacle(o, false);
    }

    public void SpawnHopper(int lane, float z)
    {
        bool frog = T.id == 3;   // a praga das rãs (Êx 8:6)
        var c = frog ? new Color(0.3f, 0.6f, 0.2f) : (T.id == 2 ? new Color(0.3f, 0.1f, 0.08f) : new Color(0.6f, 0.65f, 0.25f));
        var o = MakeObstacle(frog ? "Ra" : "Gafanhoto", ObType.Hopper, new Vector3(LaneX(lane), 0.55f, z), new Vector3(0.7f, 0.55f, 0.6f), 1.5f * HpMul, 90, c);
        var t = o.transform;
        Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0f, 0f), new Vector3(1.1f, 0.8f, 1.2f), c);
        for (int s = -1; s <= 1; s += 2)
        {
            Prim(PrimitiveType.Sphere, t, new Vector3(s * 0.28f, 0.42f, -0.35f), Vector3.one * 0.3f, new Color(1f, 0.9f, 0.3f), true);   // olhos
            var leg = Prim(PrimitiveType.Cube, t, new Vector3(s * 0.55f, -0.15f, 0.25f), new Vector3(0.2f, 0.2f, 0.9f), c * 0.75f);
            leg.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            if (!frog) Prim(PrimitiveType.Cube, t, new Vector3(s * 0.3f, 0.35f, 0.2f), new Vector3(0.5f, 0.04f, 0.9f), c * 1.15f);   // asas
        }
        o.baseY = 0.55f;
        o.timer = Random.Range(0.3f, 1.1f);
        o.Init();
    }
}
