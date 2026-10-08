using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Famílias de cartas, efeitos nos inimigos (fogo, água, raio, confusão), cartas que mudam as regras,
/// pragas e unção no baralho, desafio do dia e o Livro da Vida.
/// </summary>
public partial class RunnerGame
{
    // ================================================================== estado da jornada

    readonly HashSet<string> famOn = new HashSet<string>();
    readonly HashSet<string> anointed = new HashSet<string>();
    float fountainTimer, signTimer, idolTimer;
    int idolCount;
    int plentyLeft, famineLeft;
    int lotPrevLane = -1;
    float lotTime = -10f;
    int hitKind;            // 0 normal, 1 fogo, 2 raio (quem chamou o DamageEnemy)
    bool inCombo;

    const int HitFire = 1, HitShock = 2;

    void ResetRules()
    {
        famOn.Clear();
        anointed.Clear();
        fountainTimer = 45f;
        signTimer = 15f;
        idolTimer = 0f;
        idolCount = 0;
        plentyLeft = famineLeft = 0;
        lotPrevLane = -1;
        lotTime = -10f;
        hitKind = 0;
        inCombo = false;
        platforms.Clear();
        platformNextZ = 0f;
        platBumpCd = 0f;
    }

    bool Fam(Family f, int lvl) => famOn.Contains(f.ToString() + lvl);

    /// Quantas cartas DIFERENTES dessa família você já pegou nesta jornada.
    public int FamilyCount(Family f)
    {
        int n = 0;
        foreach (var kv in cardStacks)
        {
            if (kv.Value <= 0) continue;
            var c = FindCard(kv.Key);
            if (c != null && !c.plague && Families.Of(c) == f) n++;
        }
        return n;
    }

    /// Chamado sempre que uma carta é ganha (escolha, profeta, Primícias).
    void OnCardGained(RunnerCard c)
    {
        if (c == null) return;
        Meta.MarkSeen("card", c.id);
        CheckFamilies();
    }

    void CheckFamilies()
    {
        foreach (var f in Families.All)
        {
            int n = FamilyCount(f);
            for (int lvl = 3; lvl <= 5; lvl += 2)
            {
                if (n < lvl || !famOn.Add(f.ToString() + lvl)) continue;
                ApplyFamilyBonus(f, lvl);
                Banner("FAMÍLIA DE " + Families.Name(f) + " (" + lvl + ")", Families.Bonus(f, lvl));
                if (player != null)
                {
                    CardFx.Ring(player.transform, Families.Tint(f), 7f, 0.9f);
                    CardFx.Burst(player.transform, Families.Tint(f), 16, 5f, 0.22f);
                }
                Sfx("evolucao", 0.7f, 0f, 0.5f);
            }
        }
    }

    void ApplyFamilyBonus(Family f, int lvl)
    {
        if (f == Family.Guerra && lvl == 3) { stats.damageMul += 0.15f; stats.critChance += 0.1f; }
        if (f == Family.Guerra && lvl == 5) { stats.extraProjectiles++; stats.extraPierce++; }
        if (f == Family.Fe && lvl == 3) { stats.shieldLevel++; RechargeShieldNow(); }
        if (f == Family.Agua && lvl == 5) RelicAddMaxLife();
        if (f == Family.Sinais && lvl == 3) stats.chain += 1;
    }

    /// Quantas vezes a carta escolhida vale (Gideão e carta ungida dobram).
    int CardTimes(RunnerCard c)
    {
        if (c == null || c.isEvolution || c.isWeapon || c.plague) return 1;
        return 1 + (stats.gideon ? 1 : 0) + (anointed.Contains(c.id) ? 1 : 0);
    }

    /// Aplica as cópias extras de uma carta (sem passar do máximo).
    void ApplyExtraCopies(RunnerCard c)
    {
        int extra = CardTimes(c) - 1, done = 0;
        for (int k = 0; k < extra && Stacks(c) < c.maxStacks; k++)
        {
            cardStacks[c.id] = Stacks(c) + 1;
            history.Add(c);
            c.apply(this);
            done++;
        }
        if (done > 0) AddFloat(player.transform.position + Vector3.up * 2.2f, "VALE EM DOBRO!", new Color(1f, 0.9f, 0.5f), true);
    }

    // ================================================================== laço (chamado no Playing)

    void UpdateRules(float dt)
    {
        var pp = player.transform.position;

        // Água 5: Fonte de Água Viva
        if (Fam(Family.Agua, 5))
        {
            fountainTimer -= dt;
            if (fountainTimer <= 0f)
            {
                fountainTimer = 45f;
                if (lives < maxLives)
                {
                    Heal(1);
                    AddFloat(pp + Vector3.up * 2f, "ÁGUA VIVA: +1 VIDA", Families.Tint(Family.Agua), true);
                    CardFx.Flakes(player.transform, new Color(0.6f, 0.85f, 1f), 14);
                }
            }
        }

        // Sinais 5: raio do céu
        if (Fam(Family.Sinais, 5))
        {
            signTimer -= dt;
            if (signTimer <= 0f)
            {
                int hits = 0;
                for (int k = 0; k < 3; k++) if (SkyBolt(pp)) hits++;
                signTimer = hits > 0 ? 15f : 1f;
                if (hits > 0) Sfx("zap", 0.8f, 0.05f, 0.2f);
            }
        }

        // ídolos no baralho (recalcula de vez em quando)
        idolTimer -= dt;
        if (idolTimer <= 0f)
        {
            idolTimer = 0.5f;
            idolCount = 0;
            foreach (var id in drawPile) if (id == "idolo") idolCount++;
            foreach (var id in discardPile) if (id == "idolo") idolCount++;
            foreach (var id in deckHand) if (id == "idolo") idolCount++;
        }

        UpdateStatuses(dt, pp);
        UpdatePlatforms(dt);
    }

    bool SkyBolt(Vector3 pp)
    {
        RunnerObstacle best = null;
        int seen = 0;
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || !o.Shootable || o.type == ObType.EnemyShot) continue;
            float dz = o.transform.position.z - pp.z;
            if (dz < 6f || dz > 55f) continue;
            seen++;
            if (Random.Range(0, seen) == 0) best = o;
        }
        if (best == null) return false;
        Vector3 c = best.transform.position;
        Zap(c + new Vector3(Random.Range(-2f, 2f), 14f, 0f), c);
        hitKind = HitShock;
        DamageEnemy(best, stats.Damage * 3f, true, c);
        hitKind = 0;
        return true;
    }

    // ================================================================== efeitos nos inimigos

    /// Dano extra por efeitos (molhado, eletrificado) e pelos ídolos.
    float StatusDamageMods(RunnerObstacle o, float dmg)
    {
        if (o.wetT > 0f && (Fam(Family.Agua, 3) || stats.flood)) dmg *= 1.2f;
        if (o.shockT > 0f) dmg *= 1.1f;
        if (idolCount > 0) dmg *= Mathf.Max(0.5f, 1f - 0.1f * idolCount);
        return dmg;
    }

    /// Depois de um acerto que não matou: aplica fogo, água, raio e confusão.
    void OnEnemyHit(RunnerObstacle o, bool weaponHit)
    {
        if (o == null || o.dead || o.type == ObType.EnemyShot || inCombo) return;
        int kind = hitKind;
        if (weaponHit)
        {
            bool fire = Fam(Family.Fogo, 3), water = Fam(Family.Agua, 3);
            if (fire && water) { if (Random.value < 0.5f) fire = false; else water = false; }   // os dois juntos: um de cada vez
            if (fire) ApplyStatus(o, 1);
            if (water) ApplyStatus(o, 2);
            if (stats.igniteChance > 0f && Random.value < stats.igniteChance) ApplyStatus(o, 1);
            else if (stats.wetChance > 0f && Random.value < stats.wetChance) ApplyStatus(o, 2);
            if (stats.babelConfusion && o.type != ObType.Boss && o.type != ObType.BossDrone && o.confuseT <= 0f && Random.value < 0.2f)
            {
                o.confuseT = 4f;
                AddFloat(o.transform.position + Vector3.up * (o.half.y + 1f), "CONFUSO!", new Color(0.8f, 0.55f, 1f), false);
            }
        }
        if (kind == HitFire) ApplyStatus(o, 1);
        if (kind == HitShock) ApplyStatus(o, 3);
    }

    /// 1 = fogo, 2 = água, 3 = raio. Combinações: fogo+água = vapor, água+raio = eletrocutado.
    void ApplyStatus(RunnerObstacle o, int kind)
    {
        if (o == null || o.dead) return;
        Vector3 at = o.transform.position;
        if ((kind == 1 && o.wetT > 0f && !stats.flood) || (kind == 2 && o.burnT > 0f))
        {
            // VAPOR: apaga as duas coisas, atordoa e machuca um pouco
            o.wetT = 0f; o.burnT = 0f;
            o.fireTimer += 2f;
            AddFloat(at + Vector3.up * (o.half.y + 1f), "VAPOR!", new Color(0.85f, 0.9f, 0.95f), false);
            Explode(at, new Color(0.9f, 0.92f, 0.95f), 8);
            inCombo = true;
            DamageEnemy(o, stats.Damage * 1.2f, false, at);
            inCombo = false;
            return;
        }
        if (kind == 3 && o.wetT > 0f)
        {
            Electrocute(o);
            return;
        }
        if (kind == 1) { o.burnT = 3f; o.burnDps = Mathf.Max(o.burnDps, stats.Damage * 0.4f); }
        if (kind == 2) o.wetT = Mathf.Max(o.wetT, 4f);
        if (kind == 3) o.shockT = 1.5f;
    }

    /// ÁGUA + RAIO: o choque passa por todos os inimigos molhados por perto.
    void Electrocute(RunnerObstacle from)
    {
        Vector3 c = from.transform.position;
        AddFloat(c + Vector3.up * (from.half.y + 1.2f), "ELETROCUTADO!", new Color(0.6f, 0.95f, 1f), false);
        Sfx("zap", 0.8f, 0.1f, 0.1f);
        var targets = new List<RunnerObstacle>();
        foreach (var o in obstacles)
            if (o != null && !o.dead && o.Shootable && o.type != ObType.EnemyShot && o.wetT > 0f && (o.transform.position - c).sqrMagnitude < 100f)
                targets.Add(o);
        if (!targets.Contains(from)) targets.Add(from);
        inCombo = true;
        foreach (var o in targets)
        {
            if (o == null || o.dead) continue;
            o.wetT = 0f;
            o.shockT = 1.5f;
            if (o != from) Zap(c, o.transform.position);
            DamageEnemy(o, stats.Damage * (o == from ? 2f : 1.2f), false, o.transform.position);
        }
        inCombo = false;
    }

    void UpdateStatuses(float dt, Vector3 pp)
    {
        var list = SnapshotObstacles();
        try
        {
        foreach (var o in list)
        {
            if (o == null || o.dead) continue;

            // Dilúvio: muralhas afundam, inimigos ficam molhados
            if (stats.flood && !racing && !jericho)
            {
                if ((o.type == ObType.Wall || o.type == ObType.Barrier) && o.transform.position.z - pp.z > 25f)
                {
                    // afunda em silêncio, longe da vista
                    o.dead = true;
                    obstacles.Remove(o);
                    Destroy(o.gameObject);
                    continue;
                }
                if (o.Shootable && o.type != ObType.EnemyShot) o.wetT = Mathf.Max(o.wetT, 0.5f);
            }

            if (o.burnT > 0f || o.wetT > 0f || o.shockT > 0f || o.confuseT > 0f)
            {
                if (o.wetT > 0f) o.wetT -= dt;
                if (o.shockT > 0f) o.shockT -= dt;
                if (o.burnT > 0f)
                {
                    o.burnT -= dt;
                    o.statusTick -= dt;
                    if (o.statusTick <= 0f)
                    {
                        o.statusTick = 0.5f;
                        DamageEnemy(o, o.burnDps * 0.5f, false, o.transform.position);
                        if (o == null || o.dead) continue;
                    }
                    if (o.burnT <= 0f) o.burnDps = 0f;
                }
                if (o.confuseT > 0f)
                {
                    o.confuseT -= dt;
                    o.confuseTick -= dt;
                    if (o.confuseTick <= 0f)
                    {
                        o.confuseTick = 0.8f;
                        var tgt = NearestAround(o.transform.position, 9f, new HashSet<RunnerObstacle> { o });
                        if (tgt != null && tgt.type != ObType.EnemyShot)
                        {
                            Zap(o.transform.position + Vector3.up * 0.5f, tgt.transform.position);
                            DamageEnemy(tgt, stats.Damage * 1.2f, false, tgt.transform.position);
                        }
                    }
                }
            }
            UpdateStatusFx(o);
        }
        }
        finally { ReleaseList(list); }
    }

    /// Bolinha brilhante em cima do inimigo com a cor do efeito.
    void UpdateStatusFx(RunnerObstacle o)
    {
        Color c;
        bool any = true;
        if (o.shockT > 0f) c = new Color(0.7f, 0.95f, 1f);
        else if (o.burnT > 0f) c = new Color(1f, 0.5f, 0.12f);
        else if (o.confuseT > 0f) c = new Color(0.75f, 0.45f, 1f);
        else if (o.wetT > 0f) c = new Color(0.25f, 0.6f, 1f);
        else { c = Color.clear; any = false; }

        if (!any)
        {
            if (o.statusFx != null) { Destroy(o.statusFx); o.statusFx = null; }
            return;
        }
        if (o.statusFx == null)
        {
            o.statusFx = Prim(PrimitiveType.Sphere, o.transform, Vector3.zero, Vector3.one * 0.35f, c, true);
            o.statusColor = c;
        }
        else if (o.statusColor != c)
        {
            o.statusFx.GetComponent<Renderer>().sharedMaterial = Glow(c);
            o.statusColor = c;
        }
        // posição em coordenadas do mundo (o inimigo pode ter escala diferente)
        float bob = Mathf.Sin(Time.time * 8f + o.GetInstanceID()) * 0.12f;
        o.statusFx.transform.position = o.transform.position + Vector3.up * (o.half.y + 0.6f + bob);
        float pulse = 0.32f + 0.08f * Mathf.Sin(Time.time * 12f);
        var ls = o.transform.lossyScale;
        o.statusFx.transform.localScale = new Vector3(pulse / Mathf.Max(0.01f, ls.x), pulse / Mathf.Max(0.01f, ls.y), pulse / Mathf.Max(0.01f, ls.z));
    }

    /// Fogo 5: quem morre queimando espalha o fogo.
    void OnEnemyKilled(RunnerObstacle o, Vector3 pos)
    {
        if (o.burnT <= 0f || !Fam(Family.Fogo, 5) || o.type == ObType.EnemyShot) return;
        FxSphere(pos, 3f, new Color(1f, 0.5f, 0.12f));
        foreach (var n in new List<RunnerObstacle>(obstacles))
        {
            if (n == null || n.dead || n == o || !n.Shootable || n.type == ObType.EnemyShot) continue;
            if ((n.transform.position - pos).sqrMagnitude > 36f) continue;
            n.burnT = 3f;
            n.burnDps = Mathf.Max(n.burnDps, stats.Damage * 0.4f);
        }
    }

    // ================================================================== Fé 5 (milagre) e Mulher de Ló

    bool FaithMiracle()
    {
        if (!Fam(Family.Fe, 5) || Random.value >= 0.25f) return false;
        player.invuln = Mathf.Max(player.invuln, 1f);
        AddFloat(player.transform.position + Vector3.up * 1.8f, "MILAGRE!", Families.Tint(Family.Fe), true);
        Sfx("shofar", 0.6f, 0.05f, 0.3f);
        CardFx.Ring(player.transform, Families.Tint(Family.Fe), 5f, 0.6f);
        return true;
    }

    /// Chamado pelo jogador ao trocar de faixa.
    public void OnLaneChanged(int from, int to)
    {
        if (stats.lotsWife && to == lotPrevLane && Time.time - lotTime < 1f && player.invuln <= 0f && state == RunnerState.Playing)
        {
            AddFloat(player.transform.position + Vector3.up * 2f, "NÃO OLHE PARA TRÁS!", new Color(1f, 0.95f, 0.85f), true);
            Explode(player.transform.position, new Color(0.95f, 0.95f, 0.92f), 16);
            HurtPlayer(null);
        }
        lotPrevLane = from;
        lotTime = Time.time;
    }

    // ================================================================== cartas de regra

    public void StartFlood()
    {
        stats.flood = true;
        stats.gravityMul *= 0.6f;
        Banner("O DILÚVIO!", "\"Prevaleceram as águas sobre a terra\" (Gn 7:24)  —  muralhas afundam, você flutua");
        if (player != null) CardFx.WaterWalls(player.transform);
        Sfx("mar", 0.9f, 0.03f, 0.5f);
    }

    public void StartGideon()
    {
        stats.gideon = true;
        var all = new List<string>();
        all.AddRange(drawPile);
        all.AddRange(discardPile);
        // Gideão manda embora primeiro os medrosos: as pragas saem antes
        all.RemoveAll(id => { var c = FindCard(id); return c != null && c.plague; });
        Deck.Shuffle(all);
        if (all.Count > 10) { deckExhausted += all.Count - 10; all.RemoveRange(10, all.Count - 10); }
        drawPile.Clear();
        discardPile.Clear();
        drawPile.AddRange(all);
        Banner("OS 300 DE GIDEÃO", "Seu baralho ficou com " + drawPile.Count + " cartas  —  e cada carta vale em dobro (Jz 7:7)");
    }

    public void StartPlenty()
    {
        plentyLeft = 7;
        famineLeft = 7;
        Banner("SETE ANOS DE FARTURA", "Os próximos 7 níveis oferecem 5 cartas  —  depois vêm 7 de fome (Gn 41:29)");
    }

    /// Quantas cartas aparecem na escolha agora.
    int ChoiceCount()
    {
        if (offerIsBoss) return stats.choices;
        if (plentyLeft > 0) return Mathf.Max(5, stats.choices);
        if (famineLeft > 0) return 1;
        return stats.choices;
    }

    void TickPlenty()
    {
        if (plentyLeft > 0)
        {
            plentyLeft--;
            if (plentyLeft == 0 && famineLeft > 0) Banner("SETE ANOS DE FOME", "Os próximos 7 níveis oferecem só 1 carta (Gn 41:30)");
        }
        else if (famineLeft > 0)
        {
            famineLeft--;
            if (famineLeft == 0) Banner("A FOME ACABOU", "As escolhas voltam ao normal");
        }
    }

    string PlentyText()
    {
        if (offerIsBoss) return "";
        if (plentyLeft > 0) return "FARTURA: mais " + plentyLeft + " nível(is) com 5 cartas";
        if (famineLeft > 0) return "FOME: mais " + famineLeft + " nível(is) com 1 carta";
        return "";
    }

    // ================================================================== pragas e unção

    /// Põe pragas no baralho desta jornada (embaralhadas no monte).
    public void AddPlague(string id, int n = 1)
    {
        var c = FindCard(id);
        if (c == null) return;
        for (int i = 0; i < n; i++) drawPile.Insert(Random.Range(0, drawPile.Count + 1), id);
        AddFloat(player.transform.position + Vector3.up * 2.4f, "PRAGA NO BARALHO: " + c.name.ToUpper(), new Color(1f, 0.4f, 0.35f), true);
    }

    void OnPlagueDrawn(RunnerCard c)
    {
        if (c.id == "praga_gafanhotos" && siclos > 0)
        {
            int lost = Mathf.Min(5, siclos);
            siclos -= lost;
            AddFloat(player.transform.position + Vector3.up * 2.2f, "GAFANHOTOS: -" + lost + " SICLOS", new Color(1f, 0.45f, 0.35f), true);
        }
    }

    /// Põe uma carta no baralho desta jornada (Gideão limita a 10).
    void AddToRunDeck(string id, int n = 1)
    {
        for (int i = 0; i < n; i++)
        {
            if (stats.gideon && DeckTotal >= 10) { deckExhausted++; continue; }
            discardPile.Add(id);
        }
    }

    /// Unção: escolha 1 de 3 cartas do baralho para valer em dobro sempre que for escolhida.
    void OpenAnoint(System.Action back)
    {
        var ids = new List<string>();
        foreach (var id in drawPile) if (!ids.Contains(id)) ids.Add(id);
        foreach (var id in discardPile) if (!ids.Contains(id)) ids.Add(id);
        ids.RemoveAll(id => { var c = FindCard(id); return c == null || c.plague || c.maxStacks <= 1 || anointed.Contains(id); });
        var opts = new List<ChoiceOpt>();
        while (opts.Count < 3 && ids.Count > 0)
        {
            var id = ids[Random.Range(0, ids.Count)];
            ids.Remove(id);
            var c = FindCard(id);
            var cid = id;
            var op = Opt(CardDB.RarityName(c.rarity) + " • UNGIR", c.name + "+", "Sempre que você escolher esta carta, ela vale em dobro.\n" + c.desc, new Color(1f, 0.85f, 0.35f), () =>
            {
                anointed.Add(cid);
                Banner("CARTA UNGIDA", FindCard(cid).name + "+ agora vale em dobro (Sl 23:5)");
                Sfx("reliquia", 0.7f, 0f, 0.5f);
                if (back != null) back();
            });
            op.card = c;
            opts.Add(op);
        }
        if (opts.Count == 0) { AddSiclos(18); if (back != null) back(); return; }
        OpenChoice("O AZEITE DA UNÇÃO", "\"Unges a minha cabeça com óleo\" (Sl 23:5)  —  escolha a carta", new Color(1f, 0.85f, 0.35f), opts);
    }

    public bool IsAnointed(RunnerCard c) => c != null && anointed.Contains(c.id);

    // ================================================================== desafio do dia

    void StartDaily()
    {
        menuPanel = 0;
        Meta.DailyMode = true;
        StartRun();
        StartIntro();
    }

    /// Guarda o recorde do desafio do dia (fim de jogo ou sair pela pausa).
    void RecordDaily()
    {
        if (!Meta.DailyMode) return;
        if (Score > Meta.DailyBest)
        {
            Meta.DailyBest = Score;
            menuNote = "Novo recorde do desafio do dia: " + Score;
            menuNoteTime = 4f;
        }
    }

    // ================================================================== Livro da Vida

    int codexTab;            // 0 cartas, 1 relíquias, 2 chefes
    int codexPage;
    string codexSel = "";

    static List<string> bossList;
    static List<string> BossList
    {
        get
        {
            if (bossList != null) return bossList;
            bossList = new List<string>();
            foreach (var b in new[] { Biomes.Jerusalem, Biomes.Egypt, Biomes.Rome, Biomes.Sheol })
                foreach (var n in b.bossNames) if (!bossList.Contains(n)) bossList.Add(n);
            bossList.Add("Satanás");
            return bossList;
        }
    }

    static string BossRegion(string name)
    {
        if (name == "Satanás") return "O Acusador, no fim da jornada (Ap 12:9)";
        foreach (var b in new[] { Biomes.Jerusalem, Biomes.Egypt, Biomes.Rome, Biomes.Sheol })
            if (System.Array.IndexOf(b.bossNames, name) >= 0) return "Chefe de " + b.name;
        return "";
    }

    static List<RunnerCard> codexCards;
    static List<RunnerCard> CodexCards
    {
        get
        {
            if (codexCards != null) return codexCards;
            codexCards = new List<RunnerCard>();
            foreach (var c in Deck.Collectible) codexCards.Add(c);
            foreach (var c in CardDB.All) if (c.isWeapon) codexCards.Add(c);
            foreach (var e in Evolutions.All) codexCards.Add(e.Card);
            return codexCards;
        }
    }

    void OpenCodex()
    {
        menuPanel = 0;
        codexTab = 0;
        codexPage = 0;
        codexSel = "";
        state = RunnerState.Codex;
    }

    void UpdateCodex()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame)) state = RunnerState.Menu;
    }

    static bool CardSeen(RunnerCard c) => Meta.Seen("card", c.id) || (c.isEvolution && PlayerPrefs.GetInt("evo_" + c.id, 0) == 1);

    void DrawCodex(float s, float W, float H)
    {
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.03f, 0.94f));
        ShadowLabel(new Rect(0, 14 * s, W, 70 * s), "LIVRO DA VIDA", Sty(bigStyle, fs: Mathf.RoundToInt(58 * s)), new Color(1f, 0.85f, 0.3f));
        ShadowLabel(new Rect(0, 78 * s, W, 34 * s), "\"Escreve num livro tudo o que viste\" (Ap 1:11)", Sty(midStyle, fs: Mathf.RoundToInt(22 * s), fst: FontStyle.Italic), new Color(1f, 0.95f, 0.85f, 0.85f));

        int seenCards = 0; foreach (var c in CodexCards) if (CardSeen(c)) seenCards++;
        int seenRel = 0; foreach (var r in Relics.All) if (Meta.Seen("relic", r.id)) seenRel++;
        int seenBoss = 0; foreach (var b in BossList) if (Meta.Seen("boss", b)) seenBoss++;

        float gridW = Mathf.Min(1220 * s, W - 30 * s);
        float gx = W / 2 - gridW / 2;
        float tabY = 120 * s, tabH = 46 * s, tgap = 8 * s;
        float tabW = (gridW - tgap * 2) / 3f;
        string[] tabs = { "CARTAS  " + seenCards + "/" + CodexCards.Count, "RELÍQUIAS  " + seenRel + "/" + Relics.All.Count, "CHEFES  " + seenBoss + "/" + BossList.Count };
        for (int t = 0; t < 3; t++)
        {
            int tt = t;
            ActionButton("cx_tab" + t, new Rect(gx + t * (tabW + tgap), tabY, tabW, tabH), tabs[t],
                codexTab == t ? new Color(0.45f, 0.33f, 0.1f, 0.95f) : new Color(0.17f, 0.15f, 0.2f, 0.9f), s, () => { codexTab = tt; codexPage = 0; codexSel = ""; });
        }

        int count = codexTab == 0 ? CodexCards.Count : (codexTab == 1 ? Relics.All.Count : BossList.Count);
        const int cols = 6, rows = 3, per = cols * rows;
        int pages = Mathf.Max(1, (count + per - 1) / per);
        codexPage = Mathf.Clamp(codexPage, 0, pages - 1);
        float top = tabY + tabH + 12 * s, footH = 150 * s, cgap = 10 * s;
        float tw = (gridW - cgap * (cols - 1)) / cols;
        float th = Mathf.Min(150 * s, (H - top - footH - cgap * (rows - 1)) / rows);
        var nameSt = Sty(cardSmall, fs: Mathf.RoundToInt(17 * s), ww: 1, al: TextAnchor.UpperCenter);

        for (int k = 0; k < per; k++)
        {
            int idx = codexPage * per + k;
            if (idx >= count) break;
            var r = new Rect(gx + (k % cols) * (tw + cgap), top + (k / cols) * (th + cgap), tw, th);
            string key, name;
            bool seen;
            Color col;
            RunnerCard card = null;
            if (codexTab == 0)
            {
                card = CodexCards[idx];
                key = card.id; name = card.name; seen = CardSeen(card);
                col = card.isEvolution ? CardDB.EvolutionColor : CardDB.RarityColor(card.rarity);
            }
            else if (codexTab == 1)
            {
                var rel = Relics.All[idx];
                key = rel.id; name = rel.name; seen = Meta.Seen("relic", rel.id); col = rel.color;
            }
            else
            {
                key = BossList[idx]; name = key; seen = Meta.Seen("boss", key); col = key == "Satanás" ? new Color(1f, 0.25f, 0.2f) : new Color(0.9f, 0.55f, 0.35f);
            }
            bool sel = codexSel == key;
            if (sel) Box(new Rect(r.x - 3 * s, r.y - 3 * s, r.width + 6 * s, r.height + 6 * s), Color.Lerp(col, Color.white, 0.3f));
            var kk = key;
            ActionButton("cx_" + codexTab + "_" + idx, r, "", seen ? new Color(col.r * 0.25f, col.g * 0.25f, col.b * 0.25f, 0.95f) : new Color(0.08f, 0.08f, 0.09f, 0.95f), s, () => codexSel = kk);
            float isz = Mathf.Min(r.height - 46 * s, r.width - 20 * s);
            var ir = new Rect(r.center.x - isz / 2, r.y + 8 * s, isz, isz);
            if (card != null) CardIcons.Draw(ir, card, seen ? Color.white : new Color(0.15f, 0.15f, 0.17f, 0.9f));
            else
            {
                Box(ir, seen ? col : new Color(0.18f, 0.18f, 0.2f));
                Box(new Rect(ir.x + isz * 0.2f, ir.y + isz * 0.2f, isz * 0.6f, isz * 0.6f), seen ? Color.Lerp(col, Color.white, 0.45f) : new Color(0.12f, 0.12f, 0.14f));
                if (codexTab == 2 && seen) ShadowLabel(ir, "♛", Sty(bigStyle, fs: Mathf.RoundToInt(isz * 0.55f)), new Color(0.25f, 0.1f, 0.05f));
            }
            ShadowLabel(new Rect(r.x + 4 * s, ir.yMax + 2 * s, r.width - 8 * s, r.yMax - ir.yMax - 2 * s), seen ? name : "???", nameSt, seen ? Color.white : new Color(0.5f, 0.5f, 0.55f));
        }

        float footY = H - footH + 4 * s;
        if (pages > 1)
        {
            ActionButton("cx_pp", new Rect(gx, footY, 70 * s, 42 * s), "◀", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => codexPage = Mathf.Max(0, codexPage - 1), codexPage > 0);
            ShadowLabel(new Rect(gx + 70 * s, footY, 90 * s, 42 * s), (codexPage + 1) + "/" + pages, Sty(cardSmall), Color.white);
            ActionButton("cx_pn", new Rect(gx + 160 * s, footY, 70 * s, 42 * s), "▶", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => codexPage = Mathf.Min(pages - 1, codexPage + 1), codexPage < pages - 1);
        }

        // texto do item escolhido
        string info = "Toque num item para ler. Cartas, relíquias e chefes entram no livro quando você os encontra numa jornada.";
        Color infoCol = new Color(0.9f, 0.9f, 0.92f);
        if (codexSel != "")
        {
            if (codexTab == 0)
            {
                foreach (var c in CodexCards)
                    if (c.id == codexSel)
                    {
                        var f = Families.Of(c);
                        info = CardSeen(c) ? c.name + (f != Family.Nenhuma ? "  [" + Families.Name(f) + "]" : "") + ":  " + c.desc : "Ainda não descoberta.";
                        infoCol = CardSeen(c) ? Color.Lerp(c.isEvolution ? CardDB.EvolutionColor : CardDB.RarityColor(c.rarity), Color.white, 0.55f) : infoCol;
                    }
            }
            else if (codexTab == 1)
            {
                foreach (var r in Relics.All)
                    if (r.id == codexSel) info = Meta.Seen("relic", r.id) ? r.name + ":  " + r.desc + "  (" + r.verse + ")" : "Ainda não encontrada.";
            }
            else info = Meta.Seen("boss", codexSel) ? codexSel + "  —  " + BossRegion(codexSel) : "Ainda não derrotado.";
        }
        float ix = gx + (pages > 1 ? 240 * s : 0f);
        ShadowLabel(new Rect(ix, footY - 4 * s, gridW - (ix - gx), 60 * s), info, Sty(cardDesc, fs: Mathf.RoundToInt(19 * s), ww: 1, al: TextAnchor.MiddleCenter), infoCol);
        ActionButton("cx_back", new Rect(W / 2 - 150 * s, H - 70 * s, 300 * s, 58 * s), "VOLTAR" + (RunnerTouch.UseTouchUI ? "" : "  (Esc)"), new Color(0.45f, 0.33f, 0.08f, 0.95f), s, () => state = RunnerState.Menu);
    }
}

public partial class RunnerGame
{
    /// Sonhos de José: nomes das próximas 3 cartas do monte.
    string JosephPreview()
    {
        if (!stats.josephDreams || offerIsBoss || drawPile.Count == 0) return "";
        var names = new List<string>();
        for (int i = drawPile.Count - 1; i >= 0 && names.Count < 3; i--)
        {
            var c = FindCard(drawPile[i]);
            if (c != null) names.Add(c.name);
        }
        return "   •   próximas: " + string.Join(", ", names.ToArray());
    }
}
