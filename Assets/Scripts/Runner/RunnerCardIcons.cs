using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ícones das cartas: Assets/Resources/CardIcons/&lt;id da carta&gt;.png
/// Para trocar pela arte final, basta substituir o PNG mantendo o mesmo nome (quadrado; 256 ou 512 px é o ideal).
/// Se faltar o ícone de alguma carta, aparece o genérico da raridade (_comum, _raro, _epico, _lendario).
/// </summary>
public static class CardIcons
{
    const string Folder = "CardIcons/";
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    static Texture2D Load(string name)
    {
        Texture2D t;
        if (cache.TryGetValue(name, out t)) return t;
        t = Resources.Load<Texture2D>(Folder + name);
        cache[name] = t;
        return t;
    }

    static string RarityKey(Rarity r)
    {
        switch (r)
        {
            case Rarity.Raro: return "_raro";
            case Rarity.Epico: return "_epico";
            case Rarity.Lendario: return "_lendario";
            default: return "_comum";
        }
    }

    public static Texture2D Get(RunnerCard c)
    {
        if (c == null) return null;
        var t = Load(c.id);
        if (t == null) t = Load(c.isEvolution ? "_lendario" : RarityKey(c.rarity));
        return t;
    }

    /// Desenha o ícone (respeita a cor/transparência passada; cinza = bloqueada).
    public static void Draw(Rect r, RunnerCard c, Color tint)
    {
        var t = Get(c);
        if (t == null) return;
        var old = GUI.color;
        GUI.color = tint;
        GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
        GUI.color = old;
    }

    public static void Draw(Rect r, RunnerCard c) => Draw(r, c, Color.white);
}
