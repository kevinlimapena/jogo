using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Coleção de cartas e baralho de cada profeta (salvos em PlayerPrefs).
/// As armas não entram no baralho: continuam aparecendo de vez em quando nas escolhas.
/// </summary>
public static class Deck
{
    public const int MinSize = 15;
    public const int MaxSize = 30;
    const string DeckKey = "deck_v1_";
    const string OwnKey = "card_own_";

    /// Cartas que todo mundo já tem desde a primeira jornada.
    static readonly HashSet<string> Starter = new HashSet<string>
    {
        "dano", "cadencia", "polvora", "calibre", "coracao", "agil", "sorte", "crit", "ganancia", "fantasma", "pao", "dizimo",
        "multi", "perfura", "escudo", "pulo2", "kit",
    };

    static List<RunnerCard> collectible;
    /// Todas as cartas que podem ir para o baralho (sem armas), em ordem de raridade.
    public static List<RunnerCard> Collectible
    {
        get
        {
            if (collectible != null) return collectible;
            collectible = new List<RunnerCard>();
            for (int r = 0; r < 4; r++)
                foreach (var c in CardDB.All)
                    if (!c.isWeapon && !c.isEvolution && !c.plague && (int)c.rarity == r) collectible.Add(c);
            return collectible;
        }
    }

    public static RunnerCard Find(string id)
    {
        foreach (var c in Collectible) if (c.id == id) return c;
        return null;
    }

    public static bool IsCollectible(RunnerCard c) => c != null && !c.isWeapon && !c.isEvolution && !c.plague;

    // ------------------------------------------------------------------ coleção

    public static bool Owned(string id)
    {
        if (Starter.Contains(id)) return true;
        if (PlayerPrefs.GetInt(OwnKey + id, 0) == 1) return true;
        // as cartas de assinatura vêm junto com o profeta
        foreach (var p in Meta.Prophets)
            if (System.Array.IndexOf(p.startCards, id) >= 0 && Meta.IsUnlocked(p)) return true;
        return false;
    }

    public static int OwnedCount
    {
        get { int n = 0; foreach (var c in Collectible) if (Owned(c.id)) n++; return n; }
    }

    public static int Price(RunnerCard c)
    {
        switch (c.rarity)
        {
            case Rarity.Raro: return 12;
            case Rarity.Epico: return 24;
            case Rarity.Lendario: return 45;
            default: return 6;
        }
    }

    /// Libera a carta para sempre e põe 1 cópia nos baralhos montados que ainda têm espaço.
    public static bool Unlock(string id)
    {
        var c = Find(id);
        if (c == null || Owned(id)) return false;
        PlayerPrefs.SetInt(OwnKey + id, 1);
        foreach (var p in Meta.Prophets)
        {
            if (!IsCustom(p)) continue;
            var d = Get(p);
            if (Size(d) < MaxSize) { d[id] = 1; Save(p, d); }
        }
        PlayerPrefs.Save();
        return true;
    }

    public static bool TryBuy(RunnerCard c)
    {
        if (c == null || Owned(c.id) || Meta.Talents < Price(c)) return false;
        Meta.Talents -= Price(c);
        Unlock(c.id);
        return true;
    }

    // ------------------------------------------------------------------ baralho do profeta

    /// Quantas cópias da carta cabem no baralho (pela raridade e pelo acúmulo máximo).
    public static int MaxCopies(RunnerCard c)
    {
        int byRarity = c.rarity == Rarity.Comum ? 3 : (c.rarity == Rarity.Lendario ? 1 : 2);
        return Mathf.Max(1, Mathf.Min(byRarity, c.maxStacks));
    }

    public static bool IsCustom(Prophet p) => PlayerPrefs.HasKey(DeckKey + p.id);

    public static int Size(Dictionary<string, int> d)
    {
        int n = 0;
        foreach (var kv in d) n += kv.Value;
        return n;
    }

    public static int Count(Dictionary<string, int> d, string id) => d.TryGetValue(id, out var n) ? n : 0;

    public static Dictionary<string, int> Get(Prophet p)
    {
        if (!IsCustom(p)) return Default(p);
        var d = new Dictionary<string, int>();
        string raw = PlayerPrefs.GetString(DeckKey + p.id, "");
        foreach (var part in raw.Split(';'))
        {
            var kv = part.Split(':');
            if (kv.Length != 2) continue;
            var c = Find(kv[0]);
            int n;
            if (c == null || !Owned(c.id) || !int.TryParse(kv[1], out n) || n <= 0) continue;
            d[c.id] = Mathf.Min(n, MaxCopies(c));
        }
        // baralhos salvos antes do novo limite: corta as cópias extras (das mais comuns primeiro)
        while (Size(d) > MaxSize)
        {
            string cut = null; int best = 0;
            foreach (var kv in d) if (kv.Value > best) { best = kv.Value; cut = kv.Key; }
            if (cut == null) break;
            if (--d[cut] <= 0) d.Remove(cut);
        }
        return d;
    }

    public static void Save(Prophet p, Dictionary<string, int> d)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in d)
        {
            if (kv.Value <= 0) continue;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
        }
        PlayerPrefs.SetString(DeckKey + p.id, sb.ToString());
        PlayerPrefs.Save();
    }

    public static void ResetToDefault(Prophet p)
    {
        PlayerPrefs.DeleteKey(DeckKey + p.id);
        PlayerPrefs.Save();
    }

    /// Baralho sugerido: assinatura do profeta, uma de cada carta liberada e as comuns em dobro (até 30).
    public static Dictionary<string, int> Default(Prophet p)
    {
        var d = new Dictionary<string, int>();
        int size = 0;
        System.Func<RunnerCard, bool> add = c =>
        {
            if (size >= MaxSize || c == null || !Owned(c.id)) return false;
            int have = Count(d, c.id);
            if (have >= MaxCopies(c)) return false;
            d[c.id] = have + 1;
            size++;
            return true;
        };
        foreach (var id in p.startCards) add(Find(id));
        foreach (var c in Collectible) if (c.rarity == Rarity.Comum && Count(d, c.id) == 0) add(c);
        foreach (var c in Collectible) if (c.rarity != Rarity.Comum && Count(d, c.id) == 0 && !c.curse) add(c);   // maldições só se você puser
        foreach (var c in Collectible) if (c.rarity == Rarity.Comum && !c.curse) add(c);
        return d;
    }

    /// Monte da jornada: cada cópia vira uma entrada. Baralho pequeno demais → usa o sugerido.
    public static List<string> BuildPile(Prophet p)
    {
        var d = Get(p);
        if (Size(d) < MinSize) d = Default(p);
        var pile = new List<string>();
        foreach (var kv in d) for (int i = 0; i < kv.Value; i++) pile.Add(kv.Key);
        Shuffle(pile);
        return pile;
    }

    public static void Shuffle(List<string> l)
    {
        for (int i = l.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var t = l[i]; l[i] = l[j]; l[j] = t;
        }
    }
}

/// <summary>
/// Baralho durante a jornada (sacar, descartar, embaralhar) e a tela BARALHO do menu.
/// </summary>
public partial class RunnerGame
{
    // ================================================================== baralho da jornada

    readonly List<string> drawPile = new List<string>();
    readonly List<string> discardPile = new List<string>();
    readonly List<string> deckHand = new List<string>();   // cartas do baralho que estão na mesa agora
    int deckExhausted;                                     // cópias tiradas do jogo (carta no máximo ou queimada)
    readonly List<string> runUnlocks = new List<string>();

    public int DeckTotal => drawPile.Count + discardPile.Count + deckHand.Count;

    void ResetRunDeck()
    {
        drawPile.Clear();
        discardPile.Clear();
        deckHand.Clear();
        deckExhausted = 0;
        runUnlocks.Clear();
        drawPile.AddRange(Deck.BuildPile(Meta.Selected));
    }

    bool CardPlayable(RunnerCard c)
    {
        if (c == null || Stacks(c) >= c.maxStacks) return false;
        if (c.cond != null && !c.cond(this)) return false;
        if (c.isWeapon && prophet != null && prophet.meleeOnly && c.id != "arma_espada" && c.id != "arma_lanca" && c.id != "arma_martelo") return false;
        return true;
    }

    /// Carta que saiu do jogo vai para o descarte, ou some se já estiver no máximo.
    void DiscardCard(string id)
    {
        var c = FindCard(id);
        if (c != null && Stacks(c) >= c.maxStacks) { deckExhausted++; return; }
        if (stats.gideon && drawPile.Count + discardPile.Count >= 10) { deckExhausted++; return; }   // Gideão: no máximo 10
        discardPile.Add(id);
    }

    void ReturnHand()
    {
        foreach (var id in deckHand) DiscardCard(id);
        deckHand.Clear();
    }

    void ReshuffleDeck()
    {
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Deck.Shuffle(drawPile);
        if (state == RunnerState.LevelUp || state == RunnerState.Playing) Sfx("embaralhar", 0.7f, 0.05f, 0.3f);
    }

    /// Saca até n cartas diferentes que dá para pegar agora. As que não servem vão para o descarte.
    List<RunnerCard> DrawCards(int n)
    {
        var result = new List<RunnerCard>();
        int budget = drawPile.Count + discardPile.Count;
        while (result.Count < n && budget-- > 0)
        {
            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) break;
                ReshuffleDeck();
            }
            string id = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            var c = FindCard(id);
            if (c == null) continue;
            if (!CardPlayable(c) || result.Contains(c)) { DiscardCard(id); continue; }
            result.Add(c);
            deckHand.Add(id);
            if (c.plague) OnPlagueDrawn(c);
        }
        return result;
    }

    /// Escolha normal de nível: cartas do baralho e, às vezes, uma arma.
    List<RunnerCard> DrawOffer(int n)
    {
        ReturnHand();
        var o = DrawCards(n);
        var weapon = RollWeapon();
        bool onlyPlagues = o.Count > 0 && !o.Exists(c => !c.plague);
        if (weapon != null && (o.Count < n || onlyPlagues || Random.value < 0.18f))
        {
            if (o.Count >= n)
            {
                int ri = o.Count - 1;
                if (onlyPlagues || !o[ri].plague) RemoveFromOffer(o, ri);
                else if (o.Count > 1) RemoveFromOffer(o, 0);   // deixa a praga à vista
            }
            o.Add(weapon);
        }
        return o;
    }

    /// Tira a carta da oferta; se veio do baralho, volta para o descarte.
    void RemoveFromOffer(List<RunnerCard> o, int idx)
    {
        var c = o[idx];
        o.RemoveAt(idx);
        int h = deckHand.IndexOf(c.id);
        if (h >= 0) { deckHand.RemoveAt(h); DiscardCard(c.id); }
    }

    RunnerCard RollWeapon()
    {
        var pool = new List<RunnerCard>();
        float total = 0f;
        foreach (var c in CardDB.All)
            if (c.isWeapon && CardPlayable(c)) { pool.Add(c); total += WeaponWeight(c); }
        if (pool.Count == 0) return null;
        float roll = Random.value * total;
        foreach (var c in pool) { roll -= WeaponWeight(c); if (roll <= 0f) return c; }
        return pool[pool.Count - 1];
    }

    static float WeaponWeight(RunnerCard c) => c.rarity == Rarity.Comum ? 3f : (c.rarity == Rarity.Raro ? 2f : 1f);

    /// Escolha boa (chefe, loja, altar, mini-jogos): cartas raras+ da coleção e, se houver, uma carta NOVA.
    List<RunnerCard> RollGoodOffer(int n)
    {
        ReturnHand();
        var owned = new List<RunnerCard>();
        var locked = new List<RunnerCard>();
        foreach (var c in Deck.Collectible)
        {
            if (c.rarity == Rarity.Comum || !CardPlayable(c)) continue;
            if (Deck.Owned(c.id)) owned.Add(c); else locked.Add(c);
        }
        var result = new List<RunnerCard>();
        // carta NOVA só às vezes, na recompensa de chefe (o resto se compra no BARALHO com Talentos)
        bool nova = offerHasNova;
        if (nova && locked.Count > 0)
        {
            float[] lw = { 0f, 50f, 35f, 15f };
            float total = 0f;
            foreach (var c in locked) total += lw[(int)c.rarity];
            float roll = Random.value * total;
            RunnerCard pick = locked[locked.Count - 1];
            foreach (var c in locked) { roll -= lw[(int)c.rarity]; if (roll <= 0f) { pick = c; break; } }
            result.Add(pick);
        }
        float[] weights = { 0f, 15f, 55f, 30f + bossesDefeated * 5f };
        while (result.Count < n && owned.Count > 0)
        {
            float total = 0f;
            foreach (var c in owned) total += weights[(int)c.rarity];
            float roll = Random.value * total;
            RunnerCard pick = owned[owned.Count - 1];
            foreach (var c in owned) { roll -= weights[(int)c.rarity]; if (roll <= 0f) { pick = c; break; } }
            result.Add(pick);
            owned.Remove(pick);
        }
        // a carta nova fica no meio, para chamar atenção
        if (result.Count >= 3 && !Deck.Owned(result[0].id)) { var t = result[0]; result[0] = result[1]; result[1] = t; }
        return result;
    }

    /// Depois de escolher: a carta escolhida e as outras do baralho voltam ao descarte.
    /// Carta de recompensa entra no baralho desta jornada; se era nova, fica liberada para sempre.
    void ResolveDeckChoice(RunnerCard chosen, bool fromGoodOffer)
    {
        ReturnHand();
        if (!Deck.IsCollectible(chosen)) return;
        if (!Deck.Owned(chosen.id) && Deck.Unlock(chosen.id))
        {
            runUnlocks.Add(chosen.id);
            Banner("CARTA NOVA DESBLOQUEADA!", chosen.name + " agora faz parte da sua coleção. Monte o baralho no menu BARALHO.");
            Sfx("reliquia", 0.8f, 0f, 0.5f);
        }
        if (fromGoodOffer) AddToRunDeck(chosen.id);
    }

    /// Mini-jogo vencido pela primeira vez: libera a carta do tema.
    void UnlockThemeCard(string id)
    {
        var c = Deck.Find(id);
        if (c == null || Deck.Owned(id) || !Deck.Unlock(id)) return;
        runUnlocks.Add(id);
        discardPile.Add(id);
        AddFloat(player.transform.position + Vector3.up * 2.6f, "CARTA NOVA: " + c.name.ToUpper(), CardDB.RarityColor(c.rarity), true);
    }

    // ---------------------------------------------------------------- loja e altar

    /// Templo: escolha 1 de 3 cartas da sua coleção para pôr no baralho desta jornada.
    void OpenScrollShop(System.Action back)
    {
        var pool = new List<RunnerCard>();
        foreach (var c in Deck.Collectible) if (Deck.Owned(c.id) && CardPlayable(c) && !c.curse) pool.Add(c);
        var opts = new List<ChoiceOpt>();
        for (int i = 0; i < 3 && pool.Count > 0; i++)
        {
            var c = pool[Random.Range(0, pool.Count)];
            pool.Remove(c);
            var cc = c;
            var op = Opt(CardDB.RarityName(c.rarity) + " • +2 NO BARALHO", c.name, c.desc, CardDB.RarityColor(c.rarity), () =>
            {
                AddToRunDeck(cc.id, 2);
                Banner("PERGAMINHO COPIADO", cc.name + (stats.gideon ? " entrou no seu baralho (Gideão: máximo 10)" : " entrou 2x no seu baralho desta jornada"));
                if (back != null) back();
            });
            op.card = c;
            opts.Add(op);
        }
        if (opts.Count == 0) { if (back != null) back(); return; }
        OpenChoice("O PERGAMINHO", "\"Escreve num livro o que vês\" (Ap 1:11)  —  a carta entra no baralho e aparece nos próximos níveis", new Color(0.7f, 0.5f, 1f), opts);
    }

    /// Altar: queime uma carta do baralho desta jornada (para sacar mais as que você quer).
    void OpenBurnCard(System.Action then)
    {
        var ids = new List<string>();
        foreach (var id in drawPile) if (!ids.Contains(id)) ids.Add(id);
        foreach (var id in discardPile) if (!ids.Contains(id)) ids.Add(id);
        // as pragas aparecem primeiro: é para isso que serve o fogo do altar
        var plagues = ids.FindAll(x => { var pc = FindCard(x); return pc != null && pc.plague; });
        var opts = new List<ChoiceOpt>();
        while (opts.Count < 4 && ids.Count > 0)
        {
            var id = plagues.Count > 0 ? plagues[0] : ids[Random.Range(0, ids.Count)];
            plagues.Remove(id);
            ids.Remove(id);
            var c = FindCard(id);
            if (c == null) continue;
            int copies = 0;
            foreach (var x in drawPile) if (x == id) copies++;
            foreach (var x in discardPile) if (x == id) copies++;
            var cid = id;
            var op = Opt(CardDB.RarityName(c.rarity) + " • " + copies + "x NO BARALHO", c.name, c.desc, CardDB.RarityColor(c.rarity), () =>
            {
                int n = drawPile.RemoveAll(x => x == cid) + discardPile.RemoveAll(x => x == cid);
                deckExhausted += n;
                AddSiclos(5);
                Banner("A CARTA VIROU CINZA", c.name + " saiu do baralho  (+5 siclos)");
                if (then != null) then();
            });
            op.card = c;
            opts.Add(op);
        }
        opts.Add(Opt("VOLTAR", "Guardar Tudo", "Não queimar nada.", new Color(0.5f, 0.55f, 0.6f), then));
        OpenChoice("QUEIMAR UMA CARTA", "Todas as cópias da carta escolhida saem do baralho desta jornada", new Color(0.9f, 0.3f, 0.25f), opts);
    }

    // ================================================================== tela BARALHO (menu)

    int deckTab;          // 0 comum, 1 rara, 2 épica, 3 lendária
    int deckPage;
    string deckSel = "";
    string deckMsg = "";
    bool offerHasNova;    // a próxima escolha boa traz uma carta nova (decidido na hora do chefe)
    float deckMsgTime;

    void OpenDeckEditor()
    {
        menuPanel = 0;
        deckTab = 0;
        deckPage = 0;
        deckSel = "";
        state = RunnerState.Deck;
    }

    void UpdateDeckEditor(float udt)
    {
        if (deckMsgTime > 0f) deckMsgTime -= udt;
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame)) state = RunnerState.Menu;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame) Meta.Cycle(-1);
            if (kb.eKey.wasPressedThisFrame) Meta.Cycle(1);
            if (kb.digit1Key.wasPressedThisFrame) { deckTab = 0; deckPage = 0; }
            if (kb.digit2Key.wasPressedThisFrame) { deckTab = 1; deckPage = 0; }
            if (kb.digit3Key.wasPressedThisFrame) { deckTab = 2; deckPage = 0; }
            if (kb.digit4Key.wasPressedThisFrame) { deckTab = 3; deckPage = 0; }
        }
    }

    void DeckNote(string msg)
    {
        deckMsg = msg;
        deckMsgTime = 2.2f;
    }

    void DeckChange(Prophet p, RunnerCard c, int delta)
    {
        var d = Deck.Get(p);
        int have = Deck.Count(d, c.id);
        int size = Deck.Size(d);
        if (delta > 0)
        {
            if (have >= Deck.MaxCopies(c)) { DeckNote("Máximo de " + Deck.MaxCopies(c) + " cópia(s) dessa carta"); return; }
            if (size >= Deck.MaxSize) { DeckNote("O baralho já tem " + Deck.MaxSize + " cartas"); return; }
        }
        else if (have <= 0) return;
        d[c.id] = have + delta;
        if (d[c.id] <= 0) d.Remove(c.id);
        Deck.Save(p, d);
        Sfx("carta", 0.4f, 0.08f, 0.03f);
    }

    void DrawDeckEditor(float s, float W, float H)
    {
        var p = Meta.Selected;
        var d = Deck.Get(p);
        int size = Deck.Size(d);
        bool tooSmall = size < Deck.MinSize;

        Box(new Rect(0, 0, W, H), new Color(0.04f, 0.035f, 0.06f, 0.93f));
        ShadowLabel(new Rect(0, 14 * s, W, 70 * s), "BARALHO", Sty(bigStyle, fs: Mathf.RoundToInt(60 * s)), new Color(1f, 0.85f, 0.3f));

        // profeta + tamanho do baralho
        float hw = Mathf.Min(760 * s, W - 30 * s);
        var hr = new Rect(W / 2 - hw / 2, 82 * s, hw, 56 * s);
        Box(hr, new Color(0f, 0f, 0f, 0.45f));
        ShadowLabel(new Rect(hr.x + 70 * s, hr.y, hr.width - 140 * s, hr.height),
            p.name.ToUpper() + "   •   " + size + " cartas" + (RunnerTouch.UseTouchUI ? "" : "   [Q / E]"),
            Sty(midStyle, fs: Mathf.RoundToInt(28 * s)), tooSmall ? new Color(1f, 0.5f, 0.4f) : p.color);
        ActionButton("dk_pp", new Rect(hr.x + 6 * s, hr.y + 6 * s, 56 * s, hr.height - 12 * s), "◀", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => Meta.Cycle(-1));
        ActionButton("dk_pn", new Rect(hr.xMax - 62 * s, hr.y + 6 * s, 56 * s, hr.height - 12 * s), "▶", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => Meta.Cycle(1));
        string rule = tooSmall
            ? "Mínimo " + Deck.MinSize + " cartas — enquanto isso, a jornada usa o baralho sugerido"
            : "Mín " + Deck.MinSize + "  •  máx " + Deck.MaxSize + "  •  baralho enxuto = suas cartas favoritas aparecem mais";
        ShadowLabel(new Rect(0, hr.yMax + 2 * s, W, 30 * s), rule, Sty(cardSmall, fs: Mathf.RoundToInt(18 * s)), tooSmall ? new Color(1f, 0.6f, 0.45f) : new Color(0.8f, 0.8f, 0.9f));

        // abas por raridade
        float gridW = Mathf.Min(1220 * s, W - 30 * s);
        float gx = W / 2 - gridW / 2;
        float tabY = hr.yMax + 36 * s, tabH = 46 * s, tgap = 8 * s;
        float tabW = (gridW - tgap * 3) / 4f;
        string[] tabNames = { "COMUNS", "RARAS", "ÉPICAS", "LENDÁRIAS" };
        for (int t = 0; t < 4; t++)
        {
            int own = 0, all = 0, inDeck = 0;
            foreach (var c in Deck.Collectible)
                if ((int)c.rarity == t) { all++; if (Deck.Owned(c.id)) own++; inDeck += Deck.Count(d, c.id); }
            Color rc = CardDB.RarityColor((Rarity)t);
            Color bg = deckTab == t ? new Color(rc.r * 0.55f, rc.g * 0.55f, rc.b * 0.55f, 0.95f) : new Color(0.16f, 0.15f, 0.2f, 0.9f);
            int tt = t;
            ActionButton("dk_tab" + t, new Rect(gx + t * (tabW + tgap), tabY, tabW, tabH), tabNames[t] + "  " + own + "/" + all + "  (" + inDeck + ")", bg, s, () => { deckTab = tt; deckPage = 0; });
        }

        // grade de cartas da aba
        var list = new List<RunnerCard>();
        foreach (var c in Deck.Collectible) if ((int)c.rarity == deckTab) list.Add(c);
        const int cols = 5, rows = 3, perPage = cols * rows;
        int pages = Mathf.Max(1, (list.Count + perPage - 1) / perPage);
        deckPage = Mathf.Clamp(deckPage, 0, pages - 1);
        float gridTop = tabY + tabH + 12 * s;
        float footH = 150 * s;
        float cgap = 10 * s;
        float tileW = (gridW - cgap * (cols - 1)) / cols;
        float tileH = Mathf.Min(170 * s, (H - gridTop - footH - cgap * (rows - 1)) / rows);
        float ts = tileH / (170 * s) * s;   // escala do texto dentro do bloco
        var nameSt = Sty(cardTitle, fs: Mathf.RoundToInt(19 * ts), ww: 1, al: TextAnchor.MiddleLeft);
        var smallSt = Sty(cardSmall, fs: Mathf.RoundToInt(17 * ts));

        for (int k = 0; k < perPage; k++)
        {
            int idx = deckPage * perPage + k;
            if (idx >= list.Count) break;
            var c = list[idx];
            int col = k % cols, row = k / cols;
            var r = new Rect(gx + col * (tileW + cgap), gridTop + row * (tileH + cgap), tileW, tileH);
            bool own = Deck.Owned(c.id);
            int have = Deck.Count(d, c.id);
            Color rc = c.curse ? new Color(0.9f, 0.3f, 0.35f) : CardDB.RarityColor(c.rarity);
            bool sel = deckSel == c.id;
            var cc = c;

            // corpo do bloco (toque mostra a descrição embaixo)
            var body = new Rect(r.x, r.y, r.width, r.height - 44 * ts);
            Color bodyCol = own ? (have > 0 ? new Color(rc.r * 0.35f, rc.g * 0.35f, rc.b * 0.35f, 0.95f) : new Color(0.12f, 0.12f, 0.16f, 0.95f)) : new Color(0.07f, 0.07f, 0.08f, 0.95f);
            if (sel) Box(new Rect(r.x - 3 * s, r.y - 3 * s, r.width + 6 * s, r.height + 6 * s), Color.Lerp(rc, Color.white, 0.3f));
            ActionButton("dk_c_" + c.id, body, "", bodyCol, s, () => deckSel = cc.id);
            Box(new Rect(body.x, body.y, body.width, 5 * ts), own ? rc : new Color(0.3f, 0.3f, 0.3f));
            float isz = Mathf.Min(body.height - 36 * ts, body.width * 0.42f);
            CardIcons.Draw(new Rect(body.x + 6 * ts, body.y + 8 * ts, isz, isz), c, own ? (have > 0 ? Color.white : new Color(1f, 1f, 1f, 0.75f)) : new Color(0.35f, 0.35f, 0.38f, 0.8f));
            ShadowLabel(new Rect(body.x + isz + 10 * ts, body.y + 6 * ts, body.width - isz - 14 * ts, body.height - 32 * ts), c.name, nameSt, own ? (have > 0 ? Color.white : new Color(0.75f, 0.75f, 0.8f)) : new Color(0.45f, 0.45f, 0.5f));
            string sub = !own ? "BLOQUEADA" : (have > 0 ? "no baralho: " + have + "/" + Deck.MaxCopies(c) : "fora do baralho");
            var fam = Families.Of(c);
            if (fam != Family.Nenhuma)
            {
                // faixa da família no rodapé do bloco
                Color fc = Families.Tint(fam);
                Box(new Rect(body.x, body.yMax - 26 * ts, body.width, 24 * ts), new Color(fc.r * 0.25f, fc.g * 0.25f, fc.b * 0.25f, own ? 0.9f : 0.5f));
                Box(new Rect(body.x, body.yMax - 26 * ts, 5 * ts, 24 * ts), own ? fc : new Color(0.3f, 0.3f, 0.3f));
                sub = Families.Name(fam) + "  •  " + sub;
            }
            ShadowLabel(new Rect(body.x, body.yMax - 26 * ts, body.width, 24 * ts), sub, smallSt, own ? (have > 0 ? new Color(1f, 0.9f, 0.55f) : new Color(0.6f, 0.6f, 0.7f)) : new Color(0.6f, 0.45f, 0.4f));

            // linha de botões
            var bar = new Rect(r.x, r.yMax - 40 * ts, r.width, 40 * ts);
            if (own)
            {
                float bw = (bar.width - 6 * s) / 2f;
                ActionButton("dk_m_" + c.id, new Rect(bar.x, bar.y, bw, bar.height), "−", new Color(0.35f, 0.18f, 0.15f, 0.92f), s, () => DeckChange(p, cc, -1), have > 0);
                ActionButton("dk_p_" + c.id, new Rect(bar.x + bw + 6 * s, bar.y, bw, bar.height), "+", new Color(0.15f, 0.35f, 0.2f, 0.92f), s, () => DeckChange(p, cc, 1), have < Deck.MaxCopies(c) && size < Deck.MaxSize);
            }
            else
            {
                int price = Deck.Price(c);
                ActionButton("dk_b_" + c.id, bar, "LIBERAR (" + price + ")", new Color(0.4f, 0.3f, 0.08f, 0.92f), s, () =>
                {
                    if (Deck.TryBuy(cc)) { deckSel = cc.id; DeckNote(cc.name + " liberada!"); Sfx("reliquia", 0.7f, 0f, 0.5f); }
                    else DeckNote("Talentos insuficientes: faltam " + (Deck.Price(cc) - Meta.Talents));
                }, Meta.Talents >= price);
            }
        }

        // páginas (se a aba tiver mais de 15 cartas)
        float footY = H - footH + 6 * s;
        if (pages > 1)
        {
            ActionButton("dk_pgp", new Rect(gx, footY - 4 * s, 70 * s, 40 * s), "◀", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => deckPage = Mathf.Max(0, deckPage - 1), deckPage > 0);
            ShadowLabel(new Rect(gx + 70 * s, footY - 4 * s, 90 * s, 40 * s), (deckPage + 1) + "/" + pages, smallSt, Color.white);
            ActionButton("dk_pgn", new Rect(gx + 160 * s, footY - 4 * s, 70 * s, 40 * s), "▶", new Color(0.2f, 0.2f, 0.3f, 0.9f), s, () => deckPage = Mathf.Min(pages - 1, deckPage + 1), deckPage < pages - 1);
        }

        // descrição da carta escolhida (ou dica)
        var info = Deck.Find(deckSel);
        string infoTxt;
        Color infoCol = new Color(0.9f, 0.9f, 0.95f);
        if (info != null)
        {
            var ifam = Families.Of(info);
            infoTxt = info.name + (ifam != Family.Nenhuma ? "  [" + Families.Name(ifam) + "]" : "") + ":  " + info.desc + (Deck.Owned(info.id) ? "" : "   (ou encontre numa recompensa de chefe)");
            if (ifam != Family.Nenhuma) infoTxt += "\n" + Families.Name(ifam) + " — 3 cartas: " + Families.Bonus(ifam, 3) + ".  5 cartas: " + Families.Bonus(ifam, 5) + ".";
            infoCol = info.curse ? new Color(1f, 0.6f, 0.6f) : Color.Lerp(CardDB.RarityColor(info.rarity), Color.white, 0.55f);
        }
        else infoTxt = "Toque numa carta para ler o que ela faz.  Cartas novas aparecem com o selo NOVA! nas recompensas de chefe, na loja e nos mini-jogos.";
        float infoX = gx + (pages > 1 ? 240 * s : 0f);
        if (deckMsgTime > 0f) { infoTxt = deckMsg; infoCol = new Color(1f, 1f, 0.6f); }
        ShadowLabel(new Rect(infoX, footY - 8 * s, gridW - (infoX - gx), 72 * s), infoTxt, Sty(cardDesc, fs: Mathf.RoundToInt(18 * s), ww: 1, al: TextAnchor.MiddleCenter), infoCol);

        // botões do rodapé
        float bw2 = 250 * s, bh2 = 56 * s, by2 = H - bh2 - 12 * s;
        ActionButton("dk_reset", new Rect(W / 2 - bw2 - 8 * s, by2, bw2, bh2), "SUGERIDO", new Color(0.25f, 0.22f, 0.32f, 0.92f), s, () =>
        {
            Deck.ResetToDefault(p);
            DeckNote("Baralho sugerido de " + p.name + " (" + Deck.Size(Deck.Default(p)) + " cartas)");
        }, Deck.IsCustom(p));
        ActionButton("dk_back", new Rect(W / 2 + 8 * s, by2, bw2, bh2), "PRONTO" + (RunnerTouch.UseTouchUI ? "" : "  (Esc)"), new Color(0.45f, 0.33f, 0.08f, 0.95f), s, () => state = RunnerState.Menu);
        ShadowLabel(new Rect(gx, by2, W / 2 - bw2 - 20 * s - gx, bh2), "Talentos: " + Meta.Talents, Sty(cardSmall, al: TextAnchor.MiddleLeft, fs: Mathf.RoundToInt(22 * s)), new Color(0.7f, 1f, 0.75f));
        ShadowLabel(new Rect(W / 2 + bw2 + 20 * s, by2, gx + gridW - (W / 2 + bw2 + 20 * s), bh2), "Coleção " + Deck.OwnedCount + "/" + Deck.Collectible.Count, Sty(cardSmall, al: TextAnchor.MiddleRight, fs: Mathf.RoundToInt(22 * s)), new Color(0.85f, 0.85f, 0.95f));
    }
}
