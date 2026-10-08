using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camada roguelike entre chefes: siclos (moeda da jornada), relíquias, escolha de caminho,
/// Templo-loja, Altar, eventos com escolhas e os efeitos dos juramentos.
/// </summary>
public partial class RunnerGame
{
    // ================================================================== estado

    [HideInInspector] public int siclos;
    readonly HashSet<string> relics = new HashSet<string>();
    readonly List<Relic> relicList = new List<Relic>();
    float arkCd, vaseTimer;
    bool cordUsed;
    float pathThreat, eventThreat;
    float pathScoreBonus;
    string currentPath = "";
    System.Action pendingAfterCard;
    readonly HashSet<string> shopBought = new HashSet<string>();

    public bool HasRelic(string id) => relics.Contains(id);

    /// Ameaça extra de caminhos, eventos e juramentos (somada ao LevelThreat).
    float ExtraThreat => pathThreat + eventThreat + (Meta.OathOn("fornalha") ? 0.6f : 0f);

    void ResetRogue()
    {
        siclos = 0;
        relics.Clear();
        relicList.Clear();
        arkCd = 0f;
        vaseTimer = 0f;
        cordUsed = false;
        pathThreat = eventThreat = 0f;
        pathScoreBonus = 0f;
        currentPath = "";
        pendingAfterCard = null;
        choiceOpts.Clear();
        ResetMinigames();
        darknessTime = 0f;
        stompChain = 0;
        hintsShown.Clear();
    }

    public void AddSiclos(int n, Vector3? at = null)
    {
        if (n <= 0) return;
        if (HasRelic("oleo")) n *= 2;
        siclos += n;
        Sfx("moeda", 0.45f, 0.08f, 0.06f);
        if (at.HasValue) AddFloat(at.Value, "+" + n + " siclo" + (n > 1 ? "s" : ""), new Color(1f, 0.85f, 0.3f), false);
    }

    /// Chamado em cada abate feito pelo jogador.
    void OnKillReward(RunnerObstacle o, Vector3 pos)
    {
        if (o.type == ObType.EnemyShot) return;
        float chance = o.elite || o.type == ObType.Tank ? 1f : 0.22f;
        if (Random.value < chance) AddSiclos(o.elite || o.type == ObType.Tank ? 3 : 1, pos + Vector3.up * 2.1f);
    }

    public void RelicAddMaxLife()
    {
        maxLives = Mathf.Min(maxLives + 1, hardMaxLives);
        lives = maxLives;
    }

    void GainRelic(Relic r)
    {
        if (r == null || relics.Contains(r.id)) return;
        relics.Add(r.id);
        relicList.Add(r);
        if (r.onGain != null) r.onGain(this);
        maxLives = Mathf.Clamp(maxLives, 1, hardMaxLives);
        lives = Mathf.Min(lives, maxLives);
        bannerText = "RELÍQUIA: " + r.name.ToUpper();
        bannerSub = r.desc + "  (" + r.verse + ")";
        bannerTime = 4f;
        if (player != null) FxSphere(player.transform.position, 3f, r.color);
        Sfx("reliquia", 1f, 0f, 0.5f);
    }

    /// Relíquias contínuas (chamado no Playing normal).
    void UpdateRelics(float dt)
    {
        if (arkCd > 0f) arkCd -= dt;
        if (HasRelic("vaso"))
        {
            vaseTimer += dt;
            if (vaseTimer >= 50f && lives < maxLives)
            {
                vaseTimer = 0f;
                Heal(1);
                AddFloat(player.transform.position + Vector3.up * 2f, "MANÁ DO VASO: +1 VIDA", new Color(1f, 0.95f, 0.7f), true);
            }
        }
    }

    /// Antes de perder vida: Arca da Aliança. Retorna true se o golpe foi bloqueado.
    bool RelicBlocksHit()
    {
        if (!HasRelic("arca") || arkCd > 0f) return false;
        arkCd = 25f;
        player.invuln = Mathf.Max(player.invuln, 1.2f);
        shake = 0.4f;
        AddFloat(player.transform.position + Vector3.up * 1.6f, "ARCA DA ALIANÇA!", new Color(1f, 0.8f, 0.25f), true);
        Sfx("shofar", 0.8f, 0f, 0.5f);
        if (!bulletHell && !babel && !goliath) Nova(player.transform.position, 18f);
        Play(sShield, 0.9f);
        return true;
    }

    /// Golpe fatal: Fio Escarlate de Raabe. Retorna true se salvou.
    bool RelicSavesLife()
    {
        if (!HasRelic("fio") || cordUsed) return false;
        cordUsed = true;
        lives = 1;
        player.invuln = 2.5f;
        Sfx("ressurreicao", 1f, 0f, 1f);
        bannerText = "O FIO ESCARLATE!";
        bannerSub = "\"Atarás este cordão de fio escarlate à janela\" (Js 2:18)";
        bannerTime = 3f;
        Play(sLevel, 1f);
        return true;
    }

    float BossHpMul()
    {
        float m = 1f;
        if (HasRelic("shofar")) m *= 0.8f;
        if (Meta.OathOn("anaque")) m *= 1.5f;
        return m;
    }

    float DamageMods(RunnerObstacle o, float dmg)
    {
        if (HasRelic("cajado") && (o.type == ObType.Boss || o.type == ObType.BossDrone)) dmg *= 1.3f;
        if (Meta.OathOn("pragas") && o.type != ObType.Boss && o.type != ObType.BossDrone && o.type != ObType.EnemyShot) dmg /= 1.5f;
        return dmg;
    }

    /// Roda o que estava agendado para depois da escolha de carta (relíquia, caminho, voltar à loja...).
    void RunAfterCard()
    {
        if (pendingAfterCard == null) return;
        var a = pendingAfterCard;
        pendingAfterCard = null;
        a();
    }

    // ================================================================== tela de escolhas genérica

    class ChoiceOpt
    {
        public string tag = "", name = "", desc = "";
        public Color color = Color.white;
        public bool enabled = true;
        public System.Action act;
    }

    string choiceTitle = "", choiceSub = "";
    Color choiceColor = Color.white;
    readonly List<ChoiceOpt> choiceOpts = new List<ChoiceOpt>();
    readonly List<Rect> choiceRects = new List<Rect>();
    int choiceSel;
    Vector2 choiceLastMouse;
    float choiceOpenTime;
    bool ChoiceReady => Time.unscaledTime - choiceOpenTime > 0.45f;

    void OpenChoice(string title, string sub, Color color, List<ChoiceOpt> opts)
    {
        choiceTitle = title;
        choiceSub = sub;
        choiceColor = color;
        choiceOpts.Clear();
        choiceOpts.AddRange(opts);
        choiceSel = 0;
        for (int i = 0; i < choiceOpts.Count; i++) if (choiceOpts[i].enabled) { choiceSel = i; break; }
        choiceOpenTime = Time.unscaledTime;
        state = RunnerState.Choice;
        Sfx("abrir_carta", 0.7f, 0.02f, 0.2f);
    }

    void PickChoice(int i)
    {
        if (state != RunnerState.Choice || i < 0 || i >= choiceOpts.Count || !choiceOpts[i].enabled) return;
        var a = choiceOpts[i].act;
        state = RunnerState.Playing;
        player.inputLock = 0.3f;
        player.invuln = Mathf.Max(player.invuln, 0.8f);
        Sfx("carta", 0.8f, 0.03f);
        if (a != null) a();   // pode abrir outra escolha
    }

    void HandleChoiceInput()
    {
        if (!ChoiceReady) return;
        if (RunnerTouch.Tap)
        {
            Vector2 tp = RunnerTouch.TapPos - guiOffset;
            for (int i = 0; i < choiceRects.Count && i < choiceOpts.Count; i++)
                if (choiceRects[i].Contains(tp)) { PickChoice(i); return; }
        }
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if (kb != null)
        {
            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key };
            for (int i = 0; i < choiceOpts.Count && i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame) { PickChoice(i); return; }
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) MoveChoice(-1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) MoveChoice(1);
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { PickChoice(choiceSel); return; }
        }
        if (gp != null)
        {
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) MoveChoice(-1);
            if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) MoveChoice(1);
            if (gp.buttonSouth.wasPressedThisFrame) PickChoice(choiceSel);
        }
    }

    void MoveChoice(int d)
    {
        int n = choiceOpts.Count;
        Sfx("clique", 0.4f, 0.1f, 0.03f);
        for (int k = 0; k < n; k++)
        {
            choiceSel = (choiceSel + d + n) % n;
            if (choiceOpts[choiceSel].enabled) return;
        }
    }

    void DrawChoice(float s, float W, float H)
    {
        Box(new Rect(0, 0, W, H), new Color(0.03f, 0.02f, 0.01f, 0.75f));
        ShadowLabel(new Rect(0, H * 0.07f, W, 80 * s), choiceTitle, new GUIStyle(bigStyle) { fontSize = Mathf.RoundToInt(60 * s) }, choiceColor);
        ShadowLabel(new Rect(W * 0.08f, H * 0.07f + 72 * s, W * 0.84f, 70 * s), choiceSub, new GUIStyle(midStyle) { wordWrap = true, fontSize = Mathf.RoundToInt(28 * s) }, Color.white);
        ShadowLabel(new Rect(0, H * 0.07f + 138 * s, W, 40 * s), "Siclos: " + siclos + "   •   Vidas: " + lives + "/" + maxLives + "   •   Relíquias: " + relicList.Count,
            new GUIStyle(cardSmall), new Color(1f, 0.85f, 0.35f));

        int n = choiceOpts.Count;
        float cw = 330 * s, ch = 380 * s, gap = 28 * s;
        float totalW = n * cw + (n - 1) * gap;
        if (totalW > W - 40 * s)
        {
            float k = (W - 40 * s) / totalW;
            cw *= k; ch *= k; gap *= k; totalW = W - 40 * s;
        }
        float cs = cw / 330f;
        float maxH = H * 0.5f;
        if (ch > maxH) { float k2 = maxH / ch; cw *= k2; ch *= k2; gap *= k2; cs *= k2; totalW = n * cw + (n - 1) * gap; }
        var title = new GUIStyle(cardTitle) { fontSize = Mathf.RoundToInt(30 * cs), wordWrap = true };
        var desc = new GUIStyle(cardDesc) { fontSize = Mathf.RoundToInt(23 * cs), wordWrap = true };
        var small = new GUIStyle(cardSmall) { fontSize = Mathf.RoundToInt(20 * cs) };
        float x0 = W / 2 - totalW / 2, y0 = H * 0.33f;
        choiceRects.Clear();

        Event e = Event.current;
        for (int i = 0; i < n; i++)
        {
            var o = choiceOpts[i];
            var rect = new Rect(x0 + i * (cw + gap), y0, cw, ch);
            choiceRects.Add(rect);
            if (o.enabled && e.type == EventType.Repaint && rect.Contains(e.mousePosition) && (e.mousePosition - choiceLastMouse).sqrMagnitude > 1f) choiceSel = i;
            if (ChoiceReady && o.enabled && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                PickChoice(i);
                return;
            }
            bool sel = i == choiceSel && o.enabled;
            if (sel) rect.y -= 14 * cs;
            Color rc = o.enabled ? o.color : new Color(0.35f, 0.35f, 0.35f);
            float border = sel ? 7 * cs : 4 * cs;
            Box(new Rect(rect.x + 6 * cs, rect.y + 8 * cs, rect.width, rect.height), new Color(0, 0, 0, 0.5f));
            Box(rect, sel ? Color.Lerp(rc, Color.white, 0.35f) : rc);
            var inner = new Rect(rect.x + border, rect.y + border, rect.width - border * 2, rect.height - border * 2);
            Box(inner, new Color(0.1f, 0.08f, 0.06f));
            var band = new Rect(inner.x, inner.y, inner.width, 40 * cs);
            Box(band, new Color(rc.r * 0.45f, rc.g * 0.45f, rc.b * 0.45f));
            ShadowLabel(band, o.tag, small, Color.white);
            ShadowLabel(new Rect(inner.x + 8 * cs, inner.y + 48 * cs, inner.width - 16 * cs, 86 * cs), o.name, title, rc);
            Box(new Rect(inner.x + 30 * cs, inner.y + 138 * cs, inner.width - 60 * cs, 2 * cs), new Color(rc.r, rc.g, rc.b, 0.5f));
            ShadowLabel(new Rect(inner.x + 14 * cs, inner.y + 150 * cs, inner.width - 28 * cs, inner.height - 196 * cs), o.desc, desc, o.enabled ? new Color(0.93f, 0.92f, 0.88f) : new Color(0.6f, 0.6f, 0.6f));
            ShadowLabel(new Rect(inner.x, inner.yMax - 40 * cs, inner.width, 34 * cs), "[" + (i + 1) + "]", small, sel ? Color.white : new Color(0.6f, 0.6f, 0.6f));
        }
        if (e.type == EventType.Repaint) choiceLastMouse = e.mousePosition;
        string hint = RunnerTouch.UseTouchUI ? "Toque numa opção" : "Clique, aperte 1-" + n + ", ou use ← → e Enter";
        ShadowLabel(new Rect(0, y0 + ch + 24 * s, W, 44 * s), hint, midStyle, ChoiceReady ? Color.white : new Color(1, 1, 1, 0.4f));
    }

    static ChoiceOpt Opt(string tag, string name, string desc, Color c, System.Action act, bool enabled = true)
        => new ChoiceOpt { tag = tag, name = name, desc = desc, color = c, act = act, enabled = enabled };

    /// Abre a escolha de carta boa (rara+) com título próprio; depois dela roda "then".
    void OfferGoodCard(string title, string sub, System.Action then)
    {
        offerTitle = title;
        offerSub = sub;
        pendingAfterCard = then;
        OpenCardChoice(true);
    }

    // ================================================================== relíquias

    void OpenRelicChoice(string title, string sub, System.Action then)
    {
        var pool = new List<Relic>();
        foreach (var r in Relics.All) if (!relics.Contains(r.id)) pool.Add(r);
        if (pool.Count == 0) { AddSiclos(25); if (then != null) then(); return; }
        var opts = new List<ChoiceOpt>();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            var r = pool[Random.Range(0, pool.Count)];
            pool.Remove(r);
            var rr = r;
            opts.Add(Opt("RELÍQUIA • " + r.verse, r.name, r.desc, r.color, () => { GainRelic(rr); if (then != null) then(); }));
        }
        OpenChoice(title, sub, new Color(1f, 0.82f, 0.3f), opts);
    }

    // ================================================================== caminhos entre chefes

    /// Depois de derrotar um chefe: recompensa → relíquia → escolha do caminho.
    void AfterBossRewards()
    {
        pendingAfterCard = () => OpenRelicChoice("RELÍQUIA DO CHEFE", "O inimigo deixou um tesouro para trás. Escolha uma relíquia.", OpenPathChoice);
    }

    void EndPath()
    {
        if (currentPath == "deserto") AddSiclos(15, player.transform.position + Vector3.up * 2.5f);
        stats.scoreMul -= pathScoreBonus;
        pathScoreBonus = 0f;
        pathThreat = 0f;
        eventThreat = 0f;
        currentPath = "";
    }

    void OpenPathChoice()
    {
        var all = new List<ChoiceOpt>
        {
            Opt("CAMINHO • Dt 8:2", "O Deserto da Provação", "Inimigos mais fortes até o próximo chefe, mas +30% de pontos e +15 siclos ao chegar nele.", new Color(1f, 0.6f, 0.25f),
                () => { currentPath = "deserto"; pathThreat = 0.5f; pathScoreBonus = 0.3f; stats.scoreMul += 0.3f; Banner("O DESERTO DA PROVAÇÃO", "\"Para te humilhar e te provar\" (Dt 8:2)"); }),
            Opt("CAMINHO • Êx 15:27", "O Oásis de Elim", "Doze fontes e setenta palmeiras: cura total, escudo pronto e um trecho mais calmo.", new Color(0.4f, 0.9f, 0.8f),
                () => { currentPath = "oasis"; lives = maxLives; shieldReady = true; pathThreat = -0.3f; Banner("O OÁSIS DE ELIM", "\"Doze fontes de água e setenta palmeiras\" (Êx 15:27)"); }),
            Opt("CAMINHO • 1Rs 6", "O Templo de Salomão", "Gaste seus siclos: cura, cartas, relíquias e bênçãos.", new Color(1f, 0.85f, 0.35f),
                () => { currentPath = "templo"; shopBought.Clear(); OpenShop(); }),
            Opt("CAMINHO • Gn 22:8", "O Altar de Moriá", "Ofereça algo de valor em troca de um poder maior.", new Color(0.9f, 0.3f, 0.25f),
                () => { currentPath = "altar"; OpenAltar(); }),
            Opt("CAMINHO • ?", "Um Encontro no Caminho", "Alguém (ou algo) cruza o seu caminho. Uma escolha te espera.", new Color(0.7f, 0.6f, 1f),
                () => { currentPath = "evento"; OpenRandomEvent(); }),
            Opt("CAMINHO • 1Sm 17", "O Vale de Elá", "Enfrente o gigante Golias com a funda e cinco seixos.", new Color(0.65f, 0.8f, 0.5f),
                () => { currentPath = "ela"; StartGoliath(); }),
            Opt("CAMINHO • Js 6", "As Muralhas de Jericó", "Dê as sete voltas no ritmo das trombetas e derrube os muros.", new Color(0.95f, 0.75f, 0.45f),
                () => { currentPath = "jerico"; StartJericho(); }),
        };
        // sorteia 3 caminhos diferentes
        var opts = new List<ChoiceOpt>();
        while (opts.Count < 3 && all.Count > 0)
        {
            var o = all[Random.Range(0, all.Count)];
            all.Remove(o);
            opts.Add(o);
        }
        OpenChoice("ESCOLHA O CAMINHO", "\"Eu sou o caminho\" (Jo 14:6)  —  o que te espera até o próximo chefe?", new Color(1f, 0.85f, 0.4f), opts);
    }

    void Banner(string title, string sub)
    {
        bannerText = title;
        bannerSub = sub;
        bannerTime = 3.5f;
    }

    // ================================================================== Templo-loja

    void OpenShop()
    {
        var gold = new Color(1f, 0.85f, 0.35f);
        var opts = new List<ChoiceOpt>
        {
            Opt("8 SICLOS", "Oferta Pacífica", "Recupera 1 vida. (Lv 3)", new Color(1f, 0.4f, 0.4f),
                () => { siclos -= 8; Heal(1); OpenShop(); }, siclos >= 8 && lives < maxLives),
            Opt("20 SICLOS", "Rolo dos Profetas", "Escolha uma carta rara, épica ou lendária.", new Color(0.7f, 0.5f, 1f),
                () => { siclos -= 20; shopBought.Add("rolo"); OfferGoodCard("ROLO DOS PROFETAS", "Escolha uma carta", OpenShop); }, siclos >= 20 && !shopBought.Contains("rolo")),
            Opt("35 SICLOS", "Tesouro do Templo", "Escolha uma relíquia.", gold,
                () => { siclos -= 35; shopBought.Add("reliquia"); OpenRelicChoice("TESOURO DO TEMPLO", "Escolha uma relíquia", OpenShop); }, siclos >= 35 && !shopBought.Contains("reliquia")),
            Opt("12 SICLOS", "Azeite da Unção", "+10% de dano pelo resto da jornada. (Sl 23:5)", new Color(0.9f, 0.9f, 0.4f),
                () => { siclos -= 12; shopBought.Add("azeite"); stats.damageMul += 0.1f; OpenShop(); }, siclos >= 12 && !shopBought.Contains("azeite")),
            Opt("SAIR", "Seguir Viagem", "Deixar o Templo e voltar à estrada.", new Color(0.5f, 0.55f, 0.6f), null),
        };
        OpenChoice("O TEMPLO DE SALOMÃO", "\"A minha casa será chamada casa de oração\" (Is 56:7)  —  ofertas compradas com siclos", gold, opts);
    }

    // ================================================================== Altar

    void OpenAltar()
    {
        var red = new Color(0.9f, 0.3f, 0.25f);
        var opts = new List<ChoiceOpt>
        {
            Opt("SACRIFÍCIO", "Oferecer Sangue", "Perca 1 vida máxima. Escolha uma carta épica ou lendária.", red,
                () =>
                {
                    maxLives--; lives = Mathf.Min(lives, maxLives);
                    OfferGoodCard("O FOGO DESCEU SOBRE O ALTAR", "Escolha uma carta (1Rs 18:38)", null);
                }, maxLives > 1),
            Opt("OFERTA", "Oferecer 20 Siclos", "Escolha uma relíquia.", new Color(1f, 0.82f, 0.3f),
                () => { siclos -= 20; OpenRelicChoice("A OFERTA FOI ACEITA", "Escolha uma relíquia", null); }, siclos >= 20),
            Opt("HOLOCAUSTO", "Queimar Metade dos Pontos", "Perca metade dos pontos de abates. +20% de dano permanente.", new Color(1f, 0.55f, 0.2f),
                () => { killScore /= 2; stats.damageMul += 0.2f; Banner("HOLOCAUSTO", "\"Aroma agradável ao Senhor\" (Lv 1:9)  —  +20% de dano"); }, killScore > 1000),
            Opt("RECUSAR", "Deus Proverá", "Não oferecer nada. Você encontra um carneiro preso no mato: +5 siclos. (Gn 22:13)", new Color(0.5f, 0.55f, 0.6f),
                () => AddSiclos(5)),
        };
        OpenChoice("O ALTAR DE MORIÁ", "\"Deus proverá para si o cordeiro\" (Gn 22:8)", red, opts);
    }

    // ================================================================== eventos

    void OpenRandomEvent()
    {
        int n = 10;
        int pick = Random.Range(0, n);
        var purple = new Color(0.7f, 0.6f, 1f);
        var grey = new Color(0.5f, 0.55f, 0.6f);
        var gold = new Color(1f, 0.82f, 0.3f);
        var red = new Color(1f, 0.4f, 0.35f);
        var green = new Color(0.5f, 0.9f, 0.55f);
        switch (pick)
        {
            case 0:
                OpenChoice("A VIÚVA DE SAREPTA", "Uma viúva junta gravetos para o último pão. \"Faze-me dele primeiro um bolo\" (1Rs 17:13)", purple, new List<ChoiceOpt>
                {
                    Opt("10 SICLOS", "Ajudar a Viúva", "A farinha da panela não se acaba: +1 vida máxima.", green,
                        () => { siclos -= 10; RelicAddMaxLife(); Banner("A FARINHA NÃO SE ACABOU", "+1 vida máxima (1Rs 17:16)"); }, siclos >= 10),
                    Opt("SEGUIR", "Passar Adiante", "Você segue viagem sem parar.", grey, null),
                });
                break;
            case 1:
                OpenChoice("JACÓ E O ANJO", "Um homem misterioso luta com você até o romper do dia. (Gn 32:24)", purple, new List<ChoiceOpt>
                {
                    Opt("-1 VIDA", "Não te Deixarei Ir", "Lute até o fim: perca 1 vida e receba uma carta épica ou lendária.", red,
                        () => { lives--; OfferGoodCard("NÃO TE DEIXAREI IR!", "\"...se não me abençoares\" (Gn 32:26)  —  escolha sua bênção", null); }, lives > 1),
                    Opt("+8 SICLOS", "Deixá-lo Partir", "Ele te deixa uma pequena bolsa ao partir.", grey, () => AddSiclos(8)),
                });
                break;
            case 2:
                OpenChoice("O PRATO DE LENTILHAS", "Esaú chega faminto do campo e oferece um negócio. (Gn 25:34)", purple, new List<ChoiceOpt>
                {
                    Opt("+40 SICLOS", "Vender a Primogenitura", "Ganhe 40 siclos agora, mas perca 20% dos pontos pelo resto da jornada.", red,
                        () => { siclos += 40; stats.scoreMul = Mathf.Max(0.3f, stats.scoreMul - 0.2f); }),
                    Opt("+1 VIDA", "Comer e Seguir", "Só a sopa: recupera 1 vida.", green, () => Heal(1)),
                });
                break;
            case 3:
                OpenChoice("O BEZERRO DE OURO", "O povo dança diante de um ídolo de ouro aos pés do Sinai. (Êx 32:19)", purple, new List<ChoiceOpt>
                {
                    Opt("TENTAÇÃO", "Pegar o Ouro", "+35 siclos, mas o caminho fica mais perigoso até o próximo chefe.", red,
                        () => { siclos += 35; eventThreat += 0.5f; Banner("O OURO PESA", "A ameaça aumentou até o próximo chefe"); }),
                    Opt("ZELO", "Moer o Ídolo", "Reduza-o a pó, como Moisés: +15% de dano. (Êx 32:20)", gold,
                        () => { stats.damageMul += 0.15f; Banner("O ÍDOLO VIROU PÓ", "+15% de dano (Êx 32:20)"); }),
                });
                break;
            case 4:
                OpenChoice("A SARÇA ARDENTE", "Um arbusto pega fogo, mas não se consome. (Êx 3:2)", purple, new List<ChoiceOpt>
                {
                    Opt("REVERÊNCIA", "Tirar as Sandálias", "\"O lugar em que estás é terra santa\": cura total.", green,
                        () => { lives = maxLives; Banner("TERRA SANTA", "Cura total (Êx 3:5)"); }),
                    Opt("-1 VIDA", "Aproximar-se do Fogo", "Perca 1 vida e escolha uma relíquia.", gold,
                        () => { lives--; OpenRelicChoice("\"EU SOU O QUE SOU\"", "Êx 3:14  —  escolha uma relíquia", null); }, lives > 1),
                });
                break;
            case 5:
                OpenChoice("O ENIGMA DE SANSÃO", "Do comedor saiu comida, e do forte saiu doçura. (Jz 14:14)", purple, new List<ChoiceOpt>
                {
                    Opt("+1 VIDA", "Comer o Mel do Leão", "Mel na carcaça do leão: recupera 1 vida.", green, () => Heal(1)),
                    Opt("APOSTA 10", "Apostar no Enigma", "50%: ganhe 30 siclos. 50%: perca 10.", gold,
                        () =>
                        {
                            if (Random.value < 0.5f) { siclos += 30; Banner("ENIGMA RESOLVIDO!", "+30 siclos"); }
                            else { siclos -= 10; Banner("ARARAM COM A SUA NOVILHA...", "-10 siclos (Jz 14:18)"); }
                        }, siclos >= 10),
                });
                break;
            case 6:
                OpenChoice("OS ESPIAS DE CANAÃ", "\"Subamos e possuamos a terra, porque certamente prevaleceremos\" (Nm 13:30)", purple, new List<ChoiceOpt>
                {
                    Opt("FÉ", "Confiar como Calebe", "Ganhe uma relíquia agora, mas o caminho fica mais difícil até o próximo chefe.", gold,
                        () => { eventThreat += 0.5f; OpenRelicChoice("A TERRA QUE MANA LEITE E MEL", "Escolha uma relíquia", null); }),
                    Opt("MEDO", "Voltar com os Dez", "\"Éramos como gafanhotos\": nada acontece.", grey, null),
                });
                break;
            case 7:
                OpenChoice("NAAMÃ NO JORDÃO", "\"Vai, lava-te sete vezes no Jordão\" (2Rs 5:10)", purple, new List<ChoiceOpt>
                {
                    Opt("HUMILDADE", "Mergulhar Sete Vezes", "Cura total e +0,5 s de invencibilidade após dano.", green,
                        () => { lives = maxLives; stats.invulnTime += 0.5f; Banner("A PELE FICOU COMO A DE UM MENINO", "2Rs 5:14"); }),
                    Opt("ORGULHO", "Os Rios de Damasco São Melhores", "Recuse e ganhe 10 siclos dos presentes que você trouxe.", grey, () => AddSiclos(10)),
                });
                break;
            case 8:
                OpenChoice("ELIAS E OS CORVOS", "Corvos trazem pão e carne junto ao ribeiro de Querite. (1Rs 17:6)", purple, new List<ChoiceOpt>
                {
                    Opt("PÃO", "Aceitar o Pão", "Recupera 1 vida e ganha 5 siclos.", green, () => { Heal(1); AddSiclos(5); }),
                    Opt("JEJUM", "Jejuar e Orar", "+1 rerrolagem nas escolhas de carta.", gold, () => stats.rerolls++),
                });
                break;
            default:
                OpenChoice("O SONHO DE SALOMÃO", "\"Pede o que queres que eu te dê\" (1Rs 3:5)", purple, new List<ChoiceOpt>
                {
                    Opt("SABEDORIA", "Um Coração Entendido", "+2 rerrolagens e +1 carta em cada escolha. (1Rs 3:9)", gold,
                        () => { stats.rerolls += 2; stats.choices = Mathf.Min(stats.choices + 1, 5); }),
                    Opt("RIQUEZA", "Riquezas", "+40 siclos.", new Color(1f, 0.85f, 0.3f), () => siclos += 40),
                    Opt("VIDA LONGA", "Muitos Dias", "+1 vida máxima.", green, () => RelicAddMaxLife()),
                });
                break;
        }
    }

    // ================================================================== HUD / pausa

    string RelicSummary()
    {
        if (relicList.Count == 0) return "";
        var sb = new System.Text.StringBuilder("\n\nRELÍQUIAS:");
        foreach (var r in relicList) sb.Append("\n• ").Append(r.name);
        return sb.ToString();
    }

    void DrawRogueHUD(float s, float W, float H)
    {
        // moeda + número de siclos
        float y = (highScore > 0 && Score > highScore) ? 96 * s : 72 * s;
        var coin = new Rect(26 * s, y + 4 * s, 22 * s, 22 * s);
        Box(coin, new Color(0.55f, 0.4f, 0.1f));
        Box(new Rect(coin.x + 3 * s, coin.y + 3 * s, coin.width - 6 * s, coin.height - 6 * s), new Color(1f, 0.82f, 0.3f));
        ShadowLabel(new Rect(coin.xMax + 8 * s, y, 200 * s, 30 * s), siclos.ToString(), new GUIStyle(cardSmall) { alignment = TextAnchor.MiddleLeft, fontSize = Mathf.RoundToInt(24 * s) }, new Color(1f, 0.88f, 0.45f));

        // relíquias como ícones coloridos (nome completo na pausa)
        float x = 26 * s;
        float ry = y + 36 * s;
        foreach (var r in relicList)
        {
            var rr = new Rect(x, ry, 24 * s, 24 * s);
            Box(rr, new Color(0f, 0f, 0f, 0.55f));
            Color c = r.color;
            if (r.id == "arca" && arkCd > 0f) c = Color.Lerp(new Color(0.3f, 0.3f, 0.3f), c, 1f - arkCd / 25f);
            Box(new Rect(rr.x + 3 * s, rr.y + 3 * s, rr.width - 6 * s, rr.height - 6 * s), c);
            x += 30 * s;
        }
    }
}
