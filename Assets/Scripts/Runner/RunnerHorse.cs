using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual do cavalo-anjo de cada profeta: pelagens (cores) e adornos.
/// Cada profeta tem a sua "assinatura" (Rodas de Ezequiel, crina de fogo de Elias, coroa de Davi...).
/// As escolhas ficam salvas por profeta.
/// </summary>
public static class Horse
{
    public class Coat { public string name; public Color body, accent, orb, eye, halo; }

    public static readonly string[] AdornNames = { "Assinatura", "Coroa de Ouro", "Chamas", "Sem adorno" };

    public static string SignatureName(string prophet)
    {
        switch (prophet)
        {
            case "elias": return "Crina de Fogo (2Rs 2:11)";
            case "davi": return "Coroa do Pastor (2Sm 5:3)";
            case "sansao": return "Sete Tranças (Jz 16:13)";
            case "daniel": return "Juba de Leão (Dn 6:22)";
            case "debora": return "Ramos de Palmeira (Jz 4:5)";
            case "joao": return "Sete Estrelas (Ap 1:16)";
            default: return "Rodas dentro de Rodas (Ez 1:16)";
        }
    }

    static Coat C(string n, Color b, Color a, Color o, Color e, Color h) => new Coat { name = n, body = b, accent = a, orb = o, eye = e, halo = h };

    static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

    /// Pelagens disponíveis para cada profeta (a primeira é a padrão).
    public static List<Coat> Coats(string prophet)
    {
        switch (prophet)
        {
            case "elias":
                return new List<Coat> {
                    C("Brasa", new Color(0.95f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.2f), new Color(1f, 0.4f, 0.05f), new Color(1f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.1f)),
                    C("Carvão", new Color(0.15f, 0.12f, 0.12f), new Color(1f, 0.4f, 0.05f), new Color(1f, 0.5f, 0.1f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.35f, 0.05f)),
                    C("Redemoinho", new Color(0.85f, 0.88f, 0.95f), new Color(0.5f, 0.7f, 1f), new Color(1f, 0.6f, 0.2f), new Color(0.6f, 0.9f, 1f), new Color(1f, 0.7f, 0.3f)),
                    C("Monte Carmelo", new Color(0.55f, 0.35f, 0.2f), new Color(1f, 0.6f, 0.15f), new Color(1f, 0.75f, 0.3f), new Color(1f, 0.9f, 0.5f), Gold) };
            case "davi":
                return new List<Coat> {
                    C("Pastor", new Color(0.92f, 0.9f, 0.82f), new Color(0.25f, 0.45f, 0.85f), Gold, new Color(0.5f, 0.85f, 1f), Gold),
                    C("Púrpura Real", new Color(0.45f, 0.2f, 0.55f), Gold, Gold, new Color(1f, 0.9f, 0.5f), Gold),
                    C("Vale de Elá", new Color(0.6f, 0.5f, 0.35f), new Color(0.3f, 0.6f, 0.3f), new Color(0.75f, 0.75f, 0.7f), new Color(0.6f, 1f, 0.6f), new Color(0.9f, 0.9f, 0.85f)),
                    C("Harpa", new Color(0.85f, 0.65f, 0.4f), new Color(0.75f, 0.2f, 0.2f), Gold, new Color(1f, 0.8f, 0.4f), Gold) };
            case "sansao":
                return new List<Coat> {
                    C("Nazireu", new Color(0.55f, 0.35f, 0.2f), new Color(0.2f, 0.12f, 0.08f), Gold, new Color(1f, 0.85f, 0.4f), Gold),
                    C("Leão de Timna", new Color(0.85f, 0.65f, 0.3f), new Color(0.55f, 0.3f, 0.1f), new Color(1f, 0.7f, 0.2f), new Color(1f, 0.9f, 0.4f), Gold),
                    C("Colunas de Dagom", new Color(0.8f, 0.78f, 0.72f), new Color(0.45f, 0.42f, 0.38f), new Color(0.9f, 0.85f, 0.7f), new Color(0.7f, 0.9f, 1f), new Color(0.95f, 0.9f, 0.8f)),
                    C("Queixada", new Color(0.95f, 0.92f, 0.85f), new Color(0.6f, 0.15f, 0.1f), new Color(0.95f, 0.9f, 0.8f), new Color(1f, 0.4f, 0.3f), Gold) };
            case "daniel":
                return new List<Coat> {
                    C("Cova dos Leões", new Color(0.9f, 0.75f, 0.45f), new Color(0.45f, 0.25f, 0.6f), Gold, new Color(0.7f, 0.5f, 1f), Gold),
                    C("Corte da Babilônia", new Color(0.15f, 0.3f, 0.7f), Gold, Gold, new Color(1f, 0.9f, 0.5f), Gold),
                    C("Mão na Parede", new Color(0.92f, 0.9f, 0.88f), new Color(0.15f, 0.12f, 0.12f), new Color(1f, 0.95f, 0.8f), new Color(1f, 1f, 1f), new Color(1f, 0.97f, 0.8f)),
                    C("Ancião de Dias", new Color(1f, 1f, 1f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.8f, 0.3f), new Color(1f, 0.65f, 0.2f)) };
            case "debora":
                return new List<Coat> {
                    C("Palmeira", new Color(0.88f, 0.82f, 0.62f), new Color(0.25f, 0.6f, 0.25f), Gold, new Color(0.6f, 1f, 0.5f), new Color(0.8f, 1f, 0.5f)),
                    C("Monte Tabor", new Color(0.45f, 0.55f, 0.35f), new Color(0.85f, 0.75f, 0.4f), new Color(0.9f, 0.85f, 0.5f), new Color(0.9f, 1f, 0.6f), Gold),
                    C("Juíza", new Color(0.95f, 0.95f, 0.92f), new Color(0.2f, 0.3f, 0.6f), Gold, new Color(0.5f, 0.8f, 1f), Gold),
                    C("Estrelas de Sísera", new Color(0.12f, 0.15f, 0.3f), new Color(0.9f, 0.9f, 1f), new Color(0.85f, 0.9f, 1f), new Color(1f, 1f, 1f), new Color(0.9f, 0.95f, 1f)) };
            case "joao":
                return new List<Coat> {
                    C("Revelação", new Color(0.97f, 0.97f, 1f), Gold, Gold, new Color(1f, 0.9f, 0.4f), Gold),
                    C("Cavalo Branco", new Color(1f, 1f, 1f), new Color(0.8f, 0.1f, 0.15f), Gold, new Color(1f, 0.4f, 0.3f), Gold),
                    C("Mar de Vidro", new Color(0.6f, 0.85f, 0.95f), new Color(0.9f, 1f, 1f), new Color(0.7f, 0.95f, 1f), new Color(1f, 1f, 1f), new Color(0.8f, 1f, 1f)),
                    C("Patmos", new Color(0.45f, 0.4f, 0.35f), new Color(0.2f, 0.45f, 0.75f), Gold, new Color(0.6f, 0.85f, 1f), Gold) };
            default: // ezequiel
                return new List<Coat> {
                    C("Visão", new Color(0.95f, 0.94f, 0.9f), new Color(0.2f, 0.5f, 1f), Gold, new Color(0.3f, 1f, 1f), new Color(1f, 0.85f, 0.35f)),
                    C("Âmbar (Ez 1:27)", new Color(1f, 0.72f, 0.3f), new Color(1f, 0.4f, 0.1f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.95f, 0.6f), new Color(1f, 0.6f, 0.2f)),
                    C("Safira (Ez 1:26)", new Color(0.15f, 0.3f, 0.75f), new Color(0.8f, 0.9f, 1f), new Color(0.5f, 0.75f, 1f), new Color(0.9f, 1f, 1f), new Color(0.7f, 0.85f, 1f)),
                    C("Cristal (Ez 1:22)", new Color(0.85f, 0.95f, 1f), new Color(0.6f, 0.9f, 1f), new Color(0.85f, 1f, 1f), new Color(1f, 1f, 1f), new Color(0.9f, 1f, 1f)) };
        }
    }

    public static int CoatIndex(string prophet) => Mathf.Clamp(PlayerPrefs.GetInt("horse_coat_" + prophet, 0), 0, 3);
    public static int AdornIndex(string prophet) => Mathf.Clamp(PlayerPrefs.GetInt("horse_adorn_" + prophet, 0), 0, AdornNames.Length - 1);

    public static void Cycle(string prophet, bool coat, int dir)
    {
        string key = (coat ? "horse_coat_" : "horse_adorn_") + prophet;
        int n = coat ? Coats(prophet).Count : AdornNames.Length;
        int v = (PlayerPrefs.GetInt(key, 0) + dir + n) % n;
        PlayerPrefs.SetInt(key, v);
        PlayerPrefs.Save();
    }

    public static string AdornLabel(string prophet)
    {
        int a = AdornIndex(prophet);
        return a == 0 ? SignatureName(prophet) : AdornNames[a];
    }

    public static RunnerCreature.Palette PaletteFor(string prophet)
    {
        var c = Coats(prophet)[CoatIndex(prophet)];
        return new RunnerCreature.Palette { body = c.body, accent = c.accent, orb = c.orb, eye = c.eye, halo = c.halo };
    }

    // ================================================================== adornos

    /// Coloca o adorno escolhido na criatura.
    public static void Adorn(RunnerCreature cr, string prophet, RunnerCreature.Palette p)
    {
        var g = RunnerGame.I;
        int a = AdornIndex(prophet);
        var b = cr.body;
        if (a == 3) return;
        if (a == 1) { Crown(g, b, new Color(1f, 0.82f, 0.3f)); return; }
        if (a == 2) { Flames(g, b, cr.tail); return; }

        switch (prophet)
        {
            case "elias": Flames(g, b, cr.tail); break;
            case "davi":
                Crown(g, b, new Color(1f, 0.82f, 0.3f));
                // funda pendurada no pescoço
                g.Prim(PrimitiveType.Cube, b, new Vector3(0.17f, 0.05f, 0.45f), new Vector3(0.03f, 0.3f, 0.03f), new Color(0.45f, 0.3f, 0.15f));
                g.Prim(PrimitiveType.Sphere, b, new Vector3(0.17f, -0.12f, 0.45f), Vector3.one * 0.08f, new Color(0.7f, 0.68f, 0.62f));
                break;
            case "sansao":
                // sete tranças longas (Jz 16:13)
                for (int k = 0; k < 7; k++)
                {
                    var br = g.Prim(PrimitiveType.Cube, b, new Vector3((k - 3) * 0.035f, 0.25f - k * 0.05f, 0.62f - k * 0.08f), new Vector3(0.045f, 0.5f, 0.045f), p.accent);
                    br.transform.localRotation = Quaternion.Euler(-20f, 0f, (k - 3) * 6f);
                }
                break;
            case "daniel":
                // juba de leão em volta da cabeça
                for (int k = 0; k < 12; k++)
                {
                    float ang = k * Mathf.PI * 2f / 12f;
                    var m = g.Prim(PrimitiveType.Cube, b, new Vector3(Mathf.Cos(ang) * 0.24f, 0.38f + Mathf.Sin(ang) * 0.24f, 0.7f), new Vector3(0.12f, 0.12f, 0.2f), new Color(0.85f, 0.55f, 0.2f));
                    m.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
                }
                break;
            case "debora":
                // ramos de palmeira (coroa de folhas) e pontas das asas verdes
                for (int k = -2; k <= 2; k++)
                {
                    var leaf = g.Prim(PrimitiveType.Cube, b, new Vector3(k * 0.06f, 0.62f, 0.72f), new Vector3(0.05f, 0.03f, 0.35f), new Color(0.25f, 0.65f, 0.25f));
                    leaf.transform.localRotation = Quaternion.Euler(-50f, k * 25f, 0f);
                }
                foreach (var w in new[] { cr.wingL, cr.wingR })
                    if (w != null) g.Prim(PrimitiveType.Cube, w, new Vector3(w == cr.wingL ? -0.95f : 0.95f, 0.03f, -0.05f), new Vector3(0.25f, 0.04f, 0.45f), new Color(0.3f, 0.75f, 0.3f));
                break;
            case "joao":
            {
                // sete estrelas girando (Ap 1:16)
                var ring = new GameObject("SeteEstrelas").transform;
                ring.SetParent(b, false);
                ring.localPosition = new Vector3(0f, 0.95f, 0.5f);
                for (int k = 0; k < 7; k++)
                {
                    float ang = k * Mathf.PI * 2f / 7f;
                    var st = g.Prim(PrimitiveType.Cube, ring, new Vector3(Mathf.Cos(ang) * 0.42f, 0f, Mathf.Sin(ang) * 0.42f), Vector3.one * 0.09f, new Color(1f, 0.95f, 0.6f), true);
                    st.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
                }
                ring.gameObject.AddComponent<RunnerSpin>().speed = 70f;
                break;
            }
            default:
            {
                // ezequiel: rodas dentro de rodas, cheias de olhos (Ez 1:16-18)
                for (int r = 0; r < 2; r++)
                {
                    var pivot = new GameObject("Roda").transform;
                    pivot.SetParent(b, false);
                    pivot.localPosition = new Vector3(0f, 0f, 0f);
                    pivot.localRotation = Quaternion.Euler(r == 0 ? 0f : 90f, 0f, 90f);
                    var spin = new GameObject("Giro").transform;
                    spin.SetParent(pivot, false);
                    for (int k = 0; k < 14; k++)
                    {
                        float ang = k * Mathf.PI * 2f / 14f;
                        g.Prim(PrimitiveType.Cube, spin, new Vector3(Mathf.Cos(ang) * 0.95f, 0f, Mathf.Sin(ang) * 0.95f), new Vector3(0.07f, 0.05f, 0.18f), p.halo, true)
                            .transform.localRotation = Quaternion.Euler(0f, -ang * Mathf.Rad2Deg, 0f);
                        if (k % 3 == 0) g.Prim(PrimitiveType.Sphere, spin, new Vector3(Mathf.Cos(ang) * 0.95f, 0.06f, Mathf.Sin(ang) * 0.95f), Vector3.one * 0.08f, Color.white);
                    }
                    spin.gameObject.AddComponent<RunnerSpin>().speed = r == 0 ? 60f : -45f;
                }
                break;
            }
        }
    }

    static void Crown(RunnerGame g, Transform b, Color gold)
    {
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.56f, 0.8f), new Vector3(0.3f, 0.06f, 0.3f), gold, true);
        for (int s = -1; s <= 1; s++)
            for (int t = -1; t <= 1; t += 2)
                g.Prim(PrimitiveType.Cube, b, new Vector3(s * 0.12f, 0.64f, 0.8f + t * 0.12f), new Vector3(0.05f, 0.12f, 0.05f), gold, true);
    }

    static void Flames(RunnerGame g, Transform b, Transform tail)
    {
        var fire = new Color(1f, 0.45f, 0.08f);
        var yellow = new Color(1f, 0.85f, 0.25f);
        for (int k = 0; k < 4; k++)
            g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.52f - k * 0.13f, 0.62f - k * 0.12f), new Vector3(0.1f, 0.22f, 0.16f), k % 2 == 0 ? fire : yellow, true);
        if (tail != null)
            for (int k = 0; k < 3; k++)
                g.Prim(PrimitiveType.Cube, tail, new Vector3(0f, -0.15f - k * 0.04f, -0.35f - k * 0.16f), new Vector3(0.14f, 0.14f, 0.2f), k % 2 == 0 ? fire : yellow, true);
    }
}
