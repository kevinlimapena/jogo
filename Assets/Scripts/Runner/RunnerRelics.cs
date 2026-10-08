using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Relíquia: item único da jornada, ganho ao derrotar chefes, no Templo-loja, no Altar ou em eventos.
/// Os efeitos "contínuos" são checados no RunnerGame com HasRelic(id); os imediatos ficam em onGain.
/// </summary>
public class Relic
{
    public string id, name, desc, verse;
    public Color color = new Color(1f, 0.82f, 0.3f);
    public Action<RunnerGame> onGain;
}

public static class Relics
{
    static List<Relic> all;
    public static List<Relic> All => all ?? (all = Build());

    public static Relic Find(string id)
    {
        foreach (var r in All) if (r.id == id) return r;
        return null;
    }

    static List<Relic> Build()
    {
        return new List<Relic>
        {
            new Relic { id = "arca", name = "Arca da Aliança", verse = "1Sm 5:3",
                desc = "Bloqueia um golpe a cada 25 s e derruba os inimigos em volta, como Dagom caiu diante dela.",
                color = new Color(1f, 0.8f, 0.25f) },
            new Relic { id = "urim", name = "Urim e Tumim", verse = "Êx 28:30",
                desc = "+1 carta em cada escolha e +1 rerrolagem.",
                color = new Color(0.6f, 0.9f, 1f),
                onGain = g => { g.stats.choices++; g.stats.rerolls++; } },
            new Relic { id = "cajado", name = "Cajado de Arão", verse = "Êx 7:12",
                desc = "+30% de dano contra chefes: o cajado engoliu os dos magos do Faraó.",
                color = new Color(0.6f, 0.85f, 0.4f) },
            new Relic { id = "tabuas", name = "Tábuas da Lei", verse = "Êx 31:18",
                desc = "+1 vida máxima e cura total.",
                color = new Color(0.85f, 0.85f, 0.8f),
                onGain = g => g.RelicAddMaxLife() },
            new Relic { id = "vaso", name = "Vaso de Maná", verse = "Êx 16:33",
                desc = "Recupera 1 vida a cada 50 s.",
                color = new Color(1f, 0.95f, 0.7f) },
            new Relic { id = "shofar", name = "Trombeta de Gideão", verse = "Jz 7:20",
                desc = "Chefes chegam com 20% a menos de vida.",
                color = new Color(1f, 0.7f, 0.35f) },
            new Relic { id = "coroa", name = "Coroa de Espinhos", verse = "Jo 19:2",
                desc = "Ao sofrer dano, fere todos os inimigos próximos. +0,5 s de invencibilidade.",
                color = new Color(0.75f, 0.3f, 0.25f),
                onGain = g => { g.stats.retaliation = true; g.stats.invulnTime += 0.5f; } },
            new Relic { id = "fio", name = "Fio Escarlate de Raabe", verse = "Js 2:18",
                desc = "Uma vez por jornada, sobrevive a um golpe fatal com 1 vida.",
                color = new Color(1f, 0.15f, 0.2f) },
            new Relic { id = "menora", name = "Menorá de Ouro", verse = "Êx 25:31",
                desc = "+12% de chance de crítico e +0,3 de dano crítico.",
                color = new Color(1f, 0.85f, 0.4f),
                onGain = g => { g.stats.critChance += 0.12f; g.stats.critMul += 0.3f; } },
            new Relic { id = "peitoral", name = "Peitoral do Sumo Sacerdote", verse = "Êx 28:21",
                desc = "Doze pedras: +25% de pontos e +12 siclos.",
                color = new Color(0.5f, 1f, 0.75f),
                onGain = g => { g.stats.scoreMul += 0.25f; g.siclos += 12; } },
            new Relic { id = "seixos", name = "Os Cinco Seixos", verse = "1Sm 17:40",
                desc = "+1 projétil e +10% de dano.",
                color = new Color(0.75f, 0.75f, 0.7f),
                onGain = g => { g.stats.extraProjectiles++; g.stats.damageMul += 0.1f; } },
            new Relic { id = "oleo", name = "Botija de Azeite da Viúva", verse = "2Rs 4:6",
                desc = "O azeite não para de correr: siclos ganhos em dobro.",
                color = new Color(0.9f, 0.85f, 0.3f) },
        };
    }
}
