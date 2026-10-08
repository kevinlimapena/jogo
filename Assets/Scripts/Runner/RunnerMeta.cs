using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Profeta jogável: cada um começa a jornada com arma e bênçãos diferentes.
/// </summary>
public class Prophet
{
    public string id, name, title, desc;
    public int cost;                      // em Talentos (0 = liberado desde o início)
    public Func<RunnerWeapon> weapon;
    public string[] startCards = new string[0];
    public int extraLives;
    public float damageBonus;
    public float critBonus;
    public int extraRerolls;
    public bool meleeOnly;                // só aceita armas corpo a corpo
    public bool freeAfterFinal;           // liberado de graça depois de derrotar Satanás
    public Color color = Color.white;
}

/// <summary>
/// Melhoria permanente comprada no Templo com Talentos.
/// </summary>
public class TempleUpgrade
{
    public string id, name, desc;
    public int[] costs;
    public int MaxLevel => costs.Length;
}

/// <summary>
/// Juramento de dificuldade (escolhido no Templo): deixa a jornada mais dura e multiplica os Talentos.
/// </summary>
public class Oath
{
    public string id, name, desc, verse;
    public float bonus;   // +% de talentos (0,2 = +20%)
}

/// <summary>
/// Progressão entre jogatinas: Talentos (Mt 25:14-30), profetas e o Templo. Tudo salvo em PlayerPrefs.
/// </summary>
public static class Meta
{
    // ------------------------------------------------------------------ talentos

    public static int Talents
    {
        get => PlayerPrefs.GetInt("runner_talents", 0);
        set { PlayerPrefs.SetInt("runner_talents", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    /// Talentos ganhos ao fim de uma jogatina.
    public static int TalentsForRun(int score, int bossesDefeated, int level, bool finalBeaten)
    {
        int baseT = score / 1500 + bossesDefeated * 3 + level / 5 + (finalBeaten ? 20 : 0);
        return Mathf.RoundToInt(baseT * OathTalentMul);
    }

    // ------------------------------------------------------------------ profetas

    static List<Prophet> prophets;
    public static List<Prophet> Prophets => prophets ?? (prophets = BuildProphets());

    static List<Prophet> BuildProphets()
    {
        return new List<Prophet>
        {
            new Prophet { id = "ezequiel", name = "Ezequiel", title = "o profeta das visões", cost = 0,
                desc = "Espada. +1 rerrolagem: ele já viu o que vem pela frente. (Ez 1)",
                weapon = Weapons.Sword, extraRerolls = 1, color = new Color(0.95f, 0.94f, 0.9f) },
            new Prophet { id = "elias", name = "Elias", title = "o profeta do fogo", cost = 15,
                desc = "Pistola + Fogo do Céu desde o início. (1Rs 18:38)",
                weapon = Weapons.Pistol, startCards = new[] { "fogoceu" }, color = new Color(1f, 0.55f, 0.15f) },
            new Prophet { id = "davi", name = "Davi", title = "o pastor guerreiro", cost = 20,
                desc = "Funda (crítico alto) + Funda de Davi. +15% de crítico. (1Sm 17)",
                weapon = Weapons.Sling, startCards = new[] { "funda" }, critBonus = 0.15f, color = new Color(0.6f, 0.85f, 1f) },
            new Prophet { id = "sansao", name = "Sansão", title = "o nazireu", cost = 25,
                desc = "Martelo de Guerra, +2 vidas e +25% de dano, mas só aceita armas corpo a corpo. (Jz 16)",
                weapon = Weapons.Hammer, extraLives = 2, damageBonus = 0.25f, meleeOnly = true, color = new Color(0.85f, 0.55f, 0.3f) },
            new Prophet { id = "daniel", name = "Daniel", title = "na cova dos leões", cost = 30,
                desc = "Lança + Escudo de Energia desde o início: Deus fechou a boca dos leões. (Dn 6:22)",
                weapon = Weapons.Spear, startCards = new[] { "escudo" }, color = new Color(0.5f, 0.85f, 1f) },
            new Prophet { id = "debora", name = "Débora", title = "juíza e profetisa", cost = 40,
                desc = "Arco Longo + Tempo Bala desde o início. (Jz 4)",
                weapon = Weapons.Longbow, startCards = new[] { "tempo" }, color = new Color(0.75f, 1f, 0.55f) },
            new Prophet { id = "joao", name = "João", title = "o revelador", cost = 60, freeAfterFinal = true,
                desc = "Espada + Tiro Múltiplo + Corrente Elétrica: o caminho dos Sete Selos. Grátis após derrotar Satanás. (Ap 1)",
                weapon = Weapons.Sword, startCards = new[] { "multi", "corrente" }, color = new Color(1f, 0.85f, 0.4f) },
        };
    }

    public static bool IsUnlocked(Prophet p)
    {
        if (p.cost <= 0) return true;
        if (p.freeAfterFinal && PlayerPrefs.GetInt("runner_final_win", 0) == 1) return true;
        return PlayerPrefs.GetInt("prophet_" + p.id, 0) == 1;
    }

    public static bool TryUnlock(Prophet p)
    {
        if (IsUnlocked(p) || Talents < p.cost) return false;
        Talents -= p.cost;
        PlayerPrefs.SetInt("prophet_" + p.id, 1);
        PlayerPrefs.Save();
        return true;
    }

    public static Prophet Selected
    {
        get
        {
            string id = PlayerPrefs.GetString("runner_prophet", "ezequiel");
            foreach (var p in Prophets) if (p.id == id && IsUnlocked(p)) return p;
            return Prophets[0];
        }
        set { PlayerPrefs.SetString("runner_prophet", value.id); PlayerPrefs.Save(); }
    }

    /// Próximo/anterior profeta LIBERADO.
    public static Prophet Cycle(int dir)
    {
        var list = Prophets;
        int i = list.IndexOf(Selected);
        for (int k = 0; k < list.Count; k++)
        {
            i = (i + dir + list.Count) % list.Count;
            if (IsUnlocked(list[i])) { Selected = list[i]; break; }
        }
        return Selected;
    }

    // ------------------------------------------------------------------ juramentos

    static List<Oath> oaths;
    public static List<Oath> Oaths => oaths ?? (oaths = new List<Oath>
    {
        new Oath { id = "nazireu", name = "Voto de Nazireu", desc = "-1 vida máxima", verse = "Nm 6:2", bonus = 0.2f },
        new Oath { id = "jejum", name = "Jejum de Quarenta Dias", desc = "rolos de cura não aparecem na pista", verse = "Mt 4:2", bonus = 0.15f },
        new Oath { id = "pragas", name = "Pragas do Egito", desc = "inimigos comuns 50% mais resistentes", verse = "Êx 7–12", bonus = 0.25f },
        new Oath { id = "fornalha", name = "Fornalha Sete Vezes Mais Quente", desc = "a ameaça começa mais alta", verse = "Dn 3:19", bonus = 0.2f },
        new Oath { id = "anaque", name = "Filhos de Anaque", desc = "chefes com +50% de vida", verse = "Nm 13:33", bonus = 0.25f },
        new Oath { id = "exilio", name = "Exílio na Babilônia", desc = "as bênçãos do Templo não valem nesta jornada", verse = "Sl 137:1", bonus = 0.3f },
    });

    public static bool OathOn(string id) => PlayerPrefs.GetInt("oath_" + id, 0) == 1;

    public static void ToggleOath(Oath o)
    {
        PlayerPrefs.SetInt("oath_" + o.id, OathOn(o.id) ? 0 : 1);
        PlayerPrefs.Save();
    }

    public static int ActiveOaths
    {
        get { int n = 0; foreach (var o in Oaths) if (OathOn(o.id)) n++; return n; }
    }

    /// Multiplicador de talentos pelos juramentos ativos.
    public static float OathTalentMul
    {
        get { float m = 1f; foreach (var o in Oaths) if (OathOn(o.id)) m += o.bonus; return m; }
    }

    // ------------------------------------------------------------------ Templo

    static List<TempleUpgrade> upgrades;
    public static List<TempleUpgrade> Upgrades => upgrades ?? (upgrades = new List<TempleUpgrade>
    {
        new TempleUpgrade { id = "vida", name = "Bênção da Vida", desc = "+1 vida máxima no início", costs = new[] { 10, 25 } },
        new TempleUpgrade { id = "uncao", name = "Unção", desc = "+10% de dano no início", costs = new[] { 8, 16, 30 } },
        new TempleUpgrade { id = "primicias", name = "Primícias", desc = "começa com 1 carta aleatória", costs = new[] { 12, 30 } },
        new TempleUpgrade { id = "sabedoria", name = "Sabedoria de Salomão", desc = "+1 rerrolagem", costs = new[] { 8, 20 } },
        new TempleUpgrade { id = "heranca", name = "Herança", desc = "+15% de pontos", costs = new[] { 6, 12, 24 } },
    });

    public static int Level(TempleUpgrade u) => PlayerPrefs.GetInt("temple_" + u.id, 0);
    public static int Level(string id) => PlayerPrefs.GetInt("temple_" + id, 0);

    public static bool TryBuy(TempleUpgrade u)
    {
        int lvl = Level(u);
        if (lvl >= u.MaxLevel || Talents < u.costs[lvl]) return false;
        Talents -= u.costs[lvl];
        PlayerPrefs.SetInt("temple_" + u.id, lvl + 1);
        PlayerPrefs.Save();
        return true;
    }
}
