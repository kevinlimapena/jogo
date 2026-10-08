using UnityEngine;

/// <summary>
/// Os três cenários da jornada:
/// 0 = Jerusalém / Babilônia, 3 = Egito (depois do 2º chefe), 1 = Roma (depois do 4º), 2 = Sheol (depois do 6º).
/// Cada tema define cores do céu, chão, construções, obstáculos e os chefes.
/// </summary>
public class BiomeTheme
{
    public int id;
    public string name;
    public string arrivalText;

    public Color sky, fogColor;
    public float fogStart = 40f, fogEnd = 150f;
    public Color sunColor = Color.white;
    public float sunIntensity = 1.1f;

    public Color groundA, groundB, joint, laneMark, sidewalk, curb;
    public bool glowingJoints;
    public Color[] stones;
    public Color accent;      // "azul da Babilônia" / vermelho romano / obsidiana
    public Color metal;       // ouro / ouro / lava
    public Color dark;        // portas e janelas

    public Color barrierMain;
    public Color idol, idolBeard, pedestal, moverIdol;
    public Color tankBody, tankBeard;

    public Color raceRoad, raceCurbA, raceCurbB, raceGround, raceRut;

    public string[] bossNames;
    public RunnerCreature.Palette bossPalette;
}

public static class Biomes
{
    public static readonly BiomeTheme Jerusalem = new BiomeTheme
    {
        id = 0,
        name = "Jerusalém e Babilônia",
        arrivalText = "JERUSALÉM",
        sky = new Color(0.97f, 0.78f, 0.55f),
        fogColor = new Color(0.97f, 0.78f, 0.55f),
        sunColor = new Color(1f, 0.95f, 0.85f),
        groundA = new Color(0.70f, 0.60f, 0.46f), groundB = new Color(0.66f, 0.56f, 0.43f),
        joint = new Color(0.5f, 0.42f, 0.32f), laneMark = new Color(0.42f, 0.34f, 0.26f),
        sidewalk = new Color(0.82f, 0.72f, 0.56f), curb = new Color(0.13f, 0.30f, 0.70f),
        stones = new[]
        {
            new Color(0.92f, 0.87f, 0.76f), new Color(0.86f, 0.74f, 0.55f), new Color(0.80f, 0.66f, 0.47f),
            new Color(0.72f, 0.55f, 0.38f), new Color(0.90f, 0.80f, 0.62f)
        },
        accent = new Color(0.13f, 0.30f, 0.70f),
        metal = new Color(1f, 0.78f, 0.28f),
        dark = new Color(0.12f, 0.09f, 0.08f),
        barrierMain = new Color(0.93f, 0.89f, 0.8f),
        idol = new Color(0.72f, 0.34f, 0.2f), idolBeard = new Color(0.22f, 0.14f, 0.1f),
        pedestal = new Color(0.55f, 0.5f, 0.45f), moverIdol = new Color(0.85f, 0.5f, 0.18f),
        tankBody = new Color(0.78f, 0.7f, 0.56f), tankBeard = new Color(0.15f, 0.15f, 0.25f),
        raceRoad = new Color(0.72f, 0.58f, 0.4f), raceCurbA = new Color(0.88f, 0.83f, 0.72f), raceCurbB = new Color(0.6f, 0.5f, 0.38f),
        raceGround = new Color(0.86f, 0.72f, 0.5f), raceRut = new Color(0.62f, 0.49f, 0.33f),
        bossNames = new[] { "Nabucodonosor", "Bel de Babilônia", "Marduque", "Tamuz", "Gogue de Magogue", "Rei de Tiro", "Baal", "Moloque" },
        bossPalette = RunnerCreature.BossPalette
    };

    public static readonly BiomeTheme Rome = new BiomeTheme
    {
        id = 1,
        name = "Roma",
        arrivalText = "ROMA",
        sky = new Color(0.70f, 0.82f, 0.96f),
        fogColor = new Color(0.78f, 0.85f, 0.95f),
        sunColor = new Color(1f, 0.98f, 0.94f),
        sunIntensity = 1.15f,
        groundA = new Color(0.45f, 0.44f, 0.43f), groundB = new Color(0.50f, 0.49f, 0.47f),   // pedras da Via Ápia
        joint = new Color(0.28f, 0.27f, 0.27f), laneMark = new Color(0.82f, 0.8f, 0.74f),
        sidewalk = new Color(0.86f, 0.83f, 0.75f), curb = new Color(0.62f, 0.1f, 0.1f),
        stones = new[]
        {
            new Color(0.95f, 0.94f, 0.9f), new Color(0.88f, 0.85f, 0.77f), new Color(0.82f, 0.78f, 0.68f),
            new Color(0.80f, 0.5f, 0.35f), new Color(0.92f, 0.9f, 0.84f)
        },
        accent = new Color(0.62f, 0.1f, 0.1f),     // vermelho romano
        metal = new Color(1f, 0.78f, 0.28f),
        dark = new Color(0.1f, 0.08f, 0.08f),
        barrierMain = new Color(0.96f, 0.95f, 0.92f),
        idol = new Color(0.92f, 0.9f, 0.85f), idolBeard = new Color(0.75f, 0.72f, 0.66f),        // estátuas de mármore
        pedestal = new Color(0.62f, 0.1f, 0.1f), moverIdol = new Color(0.78f, 0.6f, 0.3f),
        tankBody = new Color(0.62f, 0.45f, 0.25f), tankBeard = new Color(0.4f, 0.3f, 0.18f),       // touro de bronze
        raceRoad = new Color(0.62f, 0.55f, 0.45f), raceCurbA = new Color(0.95f, 0.93f, 0.88f), raceCurbB = new Color(0.62f, 0.1f, 0.1f),
        raceGround = new Color(0.42f, 0.6f, 0.3f), raceRut = new Color(0.5f, 0.44f, 0.36f),        // Circo Máximo, gramado
        bossNames = new[] { "Nero", "Calígula", "Domiciano", "Legião", "Besta do Mar", "Herodes", "César Imortal" },
        bossPalette = new RunnerCreature.Palette
        {
            body = new Color(0.9f, 0.88f, 0.82f), accent = new Color(0.65f, 0.08f, 0.1f),
            orb = new Color(1f, 0.75f, 0.25f), eye = new Color(1f, 0.15f, 0.1f), halo = new Color(1f, 0.8f, 0.3f), boss = true
        }
    };

    public static readonly BiomeTheme Sheol = new BiomeTheme
    {
        id = 2,
        name = "Sheol",
        arrivalText = "SHEOL",
        sky = new Color(0.20f, 0.05f, 0.04f),
        fogColor = new Color(0.24f, 0.06f, 0.04f),
        fogStart = 25f, fogEnd = 115f,
        sunColor = new Color(1f, 0.42f, 0.25f),
        sunIntensity = 0.95f,
        groundA = new Color(0.13f, 0.09f, 0.09f), groundB = new Color(0.16f, 0.11f, 0.1f),       // cinzas
        joint = new Color(1f, 0.35f, 0.05f), laneMark = new Color(0.45f, 0.08f, 0.04f),
        glowingJoints = true,                                                                    // rachaduras de lava
        sidewalk = new Color(0.08f, 0.07f, 0.08f), curb = new Color(1f, 0.35f, 0.05f),
        stones = new[]
        {
            new Color(0.16f, 0.13f, 0.14f), new Color(0.22f, 0.17f, 0.17f), new Color(0.12f, 0.1f, 0.11f),
            new Color(0.3f, 0.2f, 0.18f), new Color(0.2f, 0.16f, 0.16f)
        },
        accent = new Color(0.1f, 0.07f, 0.09f),    // obsidiana
        metal = new Color(1f, 0.4f, 0.08f),        // lava
        dark = new Color(0.02f, 0.01f, 0.01f),
        barrierMain = new Color(0.9f, 0.88f, 0.8f), // ossos
        idol = new Color(0.45f, 0.06f, 0.06f), idolBeard = new Color(0.1f, 0.02f, 0.02f),        // ídolos demoníacos
        pedestal = new Color(0.12f, 0.1f, 0.1f), moverIdol = new Color(0.6f, 0.15f, 0.05f),
        tankBody = new Color(0.18f, 0.14f, 0.15f), tankBeard = new Color(0.5f, 0.05f, 0.05f),
        raceRoad = new Color(0.2f, 0.14f, 0.13f), raceCurbA = new Color(1f, 0.4f, 0.08f), raceCurbB = new Color(0.1f, 0.07f, 0.08f),
        raceGround = new Color(0.1f, 0.07f, 0.07f), raceRut = new Color(0.35f, 0.08f, 0.04f),
        bossNames = new[] { "Leviatã", "Abadom", "Belzebu", "Beemote", "Rei da Morte", "Estrela da Manhã Caída", "Apoliom" },
        bossPalette = new RunnerCreature.Palette
        {
            body = new Color(0.05f, 0.03f, 0.04f), accent = new Color(1f, 0.35f, 0.05f),
            orb = new Color(1f, 0.3f, 0.05f), eye = new Color(1f, 0.85f, 0.2f), halo = new Color(1f, 0.25f, 0.05f), boss = true
        }
    };

    public static readonly BiomeTheme Egypt = new BiomeTheme
    {
        id = 3,
        name = "Egito",
        arrivalText = "EGITO",
        sky = new Color(0.42f, 0.7f, 0.9f),
        fogColor = new Color(0.93f, 0.8f, 0.56f),       // poeira dourada do deserto
        fogStart = 45f, fogEnd = 160f,
        sunColor = new Color(1f, 0.92f, 0.75f),
        sunIntensity = 1.25f,
        groundA = new Color(0.87f, 0.73f, 0.48f), groundB = new Color(0.83f, 0.69f, 0.45f),
        joint = new Color(0.68f, 0.53f, 0.33f), laneMark = new Color(0.15f, 0.55f, 0.58f),     // faixas turquesa
        sidewalk = new Color(0.93f, 0.87f, 0.72f), curb = new Color(0.12f, 0.3f, 0.7f),       // lápis-lazúli
        stones = new[]
        {
            new Color(0.93f, 0.86f, 0.68f), new Color(0.88f, 0.76f, 0.52f), new Color(0.82f, 0.68f, 0.45f),
            new Color(0.75f, 0.6f, 0.38f), new Color(0.95f, 0.9f, 0.78f)
        },
        accent = new Color(0.15f, 0.6f, 0.62f),        // turquesa
        metal = new Color(1f, 0.8f, 0.28f),            // ouro
        dark = new Color(0.1f, 0.07f, 0.05f),
        barrierMain = new Color(0.95f, 0.9f, 0.78f),
        idol = new Color(0.1f, 0.09f, 0.1f), idolBeard = new Color(1f, 0.78f, 0.25f),          // ídolos de basalto com barba dourada
        pedestal = new Color(0.82f, 0.68f, 0.45f), moverIdol = new Color(1f, 0.78f, 0.28f),
        tankBody = new Color(0.86f, 0.72f, 0.48f), tankBeard = new Color(0.12f, 0.3f, 0.7f),   // esfinge com toucado azul
        raceRoad = new Color(0.84f, 0.7f, 0.46f), raceCurbA = new Color(1f, 0.8f, 0.28f), raceCurbB = new Color(0.12f, 0.3f, 0.7f),
        raceGround = new Color(0.92f, 0.78f, 0.52f), raceRut = new Color(0.7f, 0.55f, 0.35f),
        bossNames = new[] { "Faraó do Êxodo", "Janes, o Mago", "Jambres, o Feiticeiro", "Rá, o Sol Falso", "Apep, a Serpente do Caos", "Anúbis", "Capataz de Pitom" },
        bossPalette = new RunnerCreature.Palette
        {
            body = new Color(0.08f, 0.07f, 0.09f), accent = new Color(1f, 0.78f, 0.25f),
            orb = new Color(0.2f, 0.9f, 0.9f), eye = new Color(0.3f, 1f, 0.9f), halo = new Color(1f, 0.8f, 0.3f), boss = true
        }
    };

    public static BiomeTheme ForBosses(int bossesDefeated, int egyptAfter, int romeAfter, int sheolAfter)
    {
        if (bossesDefeated >= sheolAfter) return Sheol;
        if (bossesDefeated >= romeAfter) return Rome;
        if (bossesDefeated >= egyptAfter) return Egypt;
        return Jerusalem;
    }

    // ================================================================== construções do Egito

    static void Nemes(RunnerGame g, Transform t, Vector3 at, float sc, float face)
    {
        // toucado listrado de azul e ouro
        var gold = Egypt.metal; var blue = Egypt.curb;
        g.Prim(PrimitiveType.Cube, t, at, new Vector3(1.05f, 1.0f, 1.05f) * sc, Egypt.stones[1]);                   // rosto
        for (int k = 0; k < 4; k++)
            g.Prim(PrimitiveType.Cube, t, at + new Vector3(0f, (0.55f - k * 0.25f) * sc, 0f), new Vector3(1.25f, 0.12f, 1.2f) * sc, k % 2 == 0 ? gold : blue);
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, t, at + new Vector3(face * 0.1f * sc, -0.55f * sc, s * 0.62f * sc), new Vector3(0.5f, 1.1f, 0.15f) * sc, s > 0 ? blue : gold);
        g.Prim(PrimitiveType.Cube, t, at + new Vector3(face * 0.53f * sc, -0.6f * sc, 0f), new Vector3(0.12f, 0.5f, 0.18f) * sc, gold);   // barba
        g.Prim(PrimitiveType.Cube, t, at + new Vector3(face * 0.55f * sc, 0.62f * sc, 0f), new Vector3(0.12f, 0.2f, 0.12f) * sc, gold);  // uraeus
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, t, at + new Vector3(face * 0.53f * sc, 0.12f * sc, s * 0.2f * sc), new Vector3(0.04f, 0.08f, 0.2f) * sc, Egypt.dark);
    }

    static void Palm(RunnerGame g, Transform t, Vector3 at, float h)
    {
        var trunk = new Color(0.55f, 0.4f, 0.24f);
        for (int k = 0; k < 5; k++)
            g.Prim(PrimitiveType.Cylinder, t, at + new Vector3(Mathf.Sin(k * 0.4f) * 0.15f, h * (k + 0.5f) / 5f, 0f), new Vector3(0.38f - k * 0.03f, h / 10f, 0.38f - k * 0.03f), trunk * (k % 2 == 0 ? 1f : 0.85f));
        for (int i = 0; i < 6; i++)
        {
            var leaf = g.Prim(PrimitiveType.Cube, t, at + new Vector3(0f, h + 0.1f, 0f), new Vector3(0.35f, 0.06f, 2.6f), new Color(0.2f, 0.5f, 0.18f));
            leaf.transform.localRotation = Quaternion.Euler(25f, i * 60f, 0f);
            leaf.transform.localPosition += leaf.transform.localRotation * new Vector3(0f, 0f, 1.1f);
        }
        g.Prim(PrimitiveType.Sphere, t, at + new Vector3(0f, h - 0.15f, 0f), Vector3.one * 0.5f, new Color(0.45f, 0.25f, 0.1f));   // tâmaras
    }

    /// Retorna a meia-largura da construção (eixo X). face = direção da rua.
    public static float BuildEgypt(RunnerGame g, Transform t, float side)
    {
        var T = Egypt;
        float face = -side;
        float r = Random.value;
        Color sand = T.stones[Random.Range(0, 3)];

        if (r < 0.2f)
        {
            // pirâmide em degraus com o topo de ouro
            float b = Random.Range(7f, 10f);
            int steps = 7;
            for (int k = 0; k < steps; k++)
            {
                float w = b * (1f - k / (float)steps);
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, k * 1.1f + 0.55f, 0f), new Vector3(w, 1.1f, w), k % 2 == 0 ? sand : sand * 0.95f);
            }
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, steps * 1.1f + 0.4f, 0f), new Vector3(0.9f, 0.8f, 0.9f), T.metal, true);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * (b / 2f + 0.01f), 1f, 0f), new Vector3(0.05f, 2f, 1.2f), T.dark);   // entrada
            return b / 2f;
        }
        if (r < 0.36f)
        {
            // esfinge deitada olhando para a rua
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1f, 7.5f), T.stones[2]);                          // base
            g.Prim(PrimitiveType.Cube, t, new Vector3(-face * 0.2f, 1.7f, 0.6f), new Vector3(2.4f, 1.6f, 4.8f), sand);                  // corpo
            for (int s = -1; s <= 1; s += 2)
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.2f, 1.25f, -2.4f + s * 0.55f), new Vector3(2.2f, 0.6f, 0.7f), sand);   // patas
            Nemes(g, t, new Vector3(face * 0.55f, 3.1f, -1.6f), 1.5f, face);
            return 1.7f;
        }
        if (r < 0.52f)
        {
            // pilone de templo (Karnak): duas torres e o portão, com faixas pintadas e mastros
            float d = 9f;
            for (int s = -1; s <= 1; s += 2)
            {
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 4.5f, s * 2.8f), new Vector3(2.4f, 9f, 3.4f), sand);
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.21f, 6.5f, s * 2.8f), new Vector3(0.04f, 2.2f, 2.6f), T.accent);
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.22f, 3.2f, s * 2.8f), new Vector3(0.04f, 1.2f, 2.6f), new Color(0.7f, 0.3f, 0.18f));
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(face * 1.4f, 6f, s * 1.4f), new Vector3(0.15f, 6f, 0.15f), new Color(0.5f, 0.35f, 0.2f));
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.4f, 11.5f, s * 1.4f + 0.4f), new Vector3(0.05f, 0.6f, 0.8f), T.curb);
            }
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 3.5f, 0f), new Vector3(2.2f, 7f, 2.2f), sand * 0.9f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.11f, 2.2f, 0f), new Vector3(0.05f, 4.4f, 1.6f), T.dark);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.12f, 5.2f, 0f), new Vector3(0.05f, 0.6f, 1.4f), T.metal, true);   // disco solar alado
            return 1.4f;
        }
        if (r < 0.66f)
        {
            // obelisco com hieróglifos
            float h = Random.Range(8f, 12f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.4f, 0f), new Vector3(2f, 0.8f, 2f), T.stones[3]);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f + 0.8f, 0f), new Vector3(1.1f, h, 1.1f), new Color(0.75f, 0.4f, 0.32f));   // granito rosa
            for (int k = 0; k < 6; k++)
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.56f, 1.8f + k * (h - 2f) / 6f, 0f), new Vector3(0.03f, 0.35f, 0.4f), T.dark);
            var tip = g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 1.1f, 0f), new Vector3(0.8f, 0.8f, 0.8f), T.metal, true);
            tip.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            return 1f;
        }
        if (r < 0.8f)
        {
            // colosso sentado (Abu Simbel)
            float sc = Random.Range(1.6f, 2.1f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.2f * sc, 0f), new Vector3(2.2f, 2.4f, 2.4f) * sc, sand * 0.9f);                     // trono
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.4f * sc, 2.9f * sc, 0f), new Vector3(1.2f, 1.8f, 1.6f) * sc, sand);              // tronco
            for (int s = -1; s <= 1; s += 2)
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.2f * sc, 1.5f * sc, s * 0.45f * sc), new Vector3(1f, 0.5f, 0.5f) * sc, sand); // joelhos
            Nemes(g, t, new Vector3(face * 0.45f * sc, 4.3f * sc, 0f), sc, face);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.4f * sc, 5.2f * sc, 0f), new Vector3(0.5f, 0.7f, 0.5f) * sc, new Color(0.9f, 0.88f, 0.8f));   // coroa
            return 1.3f * sc;
        }
        if (r < 0.9f)
        {
            // colunata de papiro
            int n = 4;
            for (int i = 0; i < n; i++)
            {
                float z = -4.5f + i * 3f;
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 3f, z), new Vector3(0.9f, 3f, 0.9f), sand);
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 2.5f, z), new Vector3(0.95f, 0.15f, 0.95f), T.accent);
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 6.2f, z), new Vector3(1.4f, 0.25f, 1.4f), new Color(0.3f, 0.6f, 0.35f));   // capitel de papiro
            }
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 6.7f, 0f), new Vector3(1.6f, 0.6f, 10.5f), sand * 0.95f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.81f, 6.7f, 0f), new Vector3(0.03f, 0.3f, 10f), T.curb);
            return 0.9f;
        }
        // margem do Nilo: água, juncos e palmeiras
        g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.04f, 0f), new Vector3(3.5f, 0.08f, 9f), new Color(0.2f, 0.45f, 0.55f), false);
        for (int i = 0; i < 10; i++)
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * Random.Range(1.2f, 1.8f), 0.6f, Random.Range(-4f, 4f)), new Vector3(0.06f, Random.Range(0.8f, 1.6f), 0.06f), new Color(0.35f, 0.55f, 0.2f));
        Palm(g, t, new Vector3(-face * 0.8f, 0f, -2.5f), Random.Range(5f, 7f));
        Palm(g, t, new Vector3(-face * 0.4f, 0f, 2.8f), Random.Range(5f, 7f));
        return 1.8f;
    }

    // ================================================================== construções de Roma

    /// Retorna a meia-largura da construção (eixo X). face = direção da rua.
    public static float BuildRome(RunnerGame g, Transform t, float side)
    {
        var T = Rome;
        float face = -side;
        float r = Random.value;
        Color marble = T.stones[Random.Range(0, 3)];

        if (r < 0.25f)
        {
            // templo com colunas e frontão
            float w = Random.Range(5f, 7f), d = Random.Range(7f, 9f), h = Random.Range(5f, 7f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.4f, 0f), new Vector3(w + 0.8f, 0.8f, d + 0.8f), marble * 0.92f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(-face * w * 0.15f, h / 2f + 0.8f, 0f), new Vector3(w * 0.6f, h, d * 0.9f), marble * 0.95f);
            int n = 5;
            for (int i = 0; i < n; i++)
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(face * (w / 2f - 0.4f), 0.8f + h / 2f, -d / 2f + 0.6f + i * (d - 1.2f) / (n - 1)), new Vector3(0.55f, h / 2f, 0.55f), marble);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 1.05f, 0f), new Vector3(w + 0.4f, 0.5f, d + 0.4f), marble);
            var ped = g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 1.3f, 0f), new Vector3(w * 0.72f, w * 0.72f, d + 0.3f), T.accent);
            ped.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            ped.transform.localScale = new Vector3(w * 0.5f, w * 0.5f, d + 0.3f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 1.32f, 0f), new Vector3(w + 0.5f, 0.12f, d + 0.5f), T.metal);
            return w / 2f + 0.4f;
        }
        if (r < 0.45f)
        {
            // trecho do Coliseu: 3 andares de arcos
            float d = Random.Range(8f, 9.5f);
            for (int lvl = 0; lvl < 3; lvl++)
            {
                float y = lvl * 3f;
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, y + 1.5f, 0f), new Vector3(2.4f, 3f, d), T.stones[2]);
                for (int i = 0; i < 4; i++)
                    g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.21f, y + 1.3f, -d / 2f + 1.2f + i * (d - 2.4f) / 3f), new Vector3(0.05f, 1.9f, 1.2f), T.dark);
            }
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 9.3f, 0f), new Vector3(2.5f, 0.6f, d), T.stones[1]);
            return 1.25f;
        }
        if (r < 0.6f)
        {
            // arco do triunfo
            float d = 4f;
            for (int s = -1; s <= 1; s += 2)
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 3f, s * 1.6f), new Vector3(2.4f, 6f, 1.6f), marble);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 6.9f, 0f), new Vector3(2.5f, 1.8f, d + 1.2f), marble);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 1.26f, 6.9f, 0f), new Vector3(0.04f, 0.6f, d), T.metal);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 8.2f, 0f), new Vector3(0.9f, 0.9f, 1.8f), T.metal);     // quadriga dourada
            return 1.25f;
        }
        if (r < 0.78f)
        {
            // ínsula: casa de tijolo com telhado de telhas
            float w = Random.Range(4f, 6f), h = Random.Range(4f, 8f), d = Random.Range(6f, 8.5f);
            Color brick = new Color(0.82f, 0.52f, 0.36f) * Random.Range(0.9f, 1.05f);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), brick);
            for (int i = -1; i <= 1; i++)
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.01f), h * 0.65f, i * d * 0.28f), new Vector3(0.06f, 0.8f, 0.6f), T.dark);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.01f), 1f, 0f), new Vector3(0.06f, 2f, 1.3f), T.dark);
            var roof = g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 0.5f, 0f), new Vector3(w * 0.75f, w * 0.75f, d + 0.3f), new Color(0.65f, 0.25f, 0.15f));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            roof.transform.localScale = new Vector3(w * 0.72f, w * 0.72f, d + 0.3f);
            return w / 2f;
        }
        if (r < 0.9f)
        {
            // estátua num pedestal alto
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.5f, 0f), new Vector3(1.8f, 3f, 1.8f), T.stones[2]);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 4.2f, 0f), new Vector3(0.9f, 2.4f, 0.6f), marble);
            g.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 5.8f, 0f), Vector3.one * 0.7f, marble);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.6f, 4.8f, 0f), new Vector3(0.25f, 1.4f, 0.25f), marble).transform.localRotation = Quaternion.Euler(0f, 0f, face * -40f);
            g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 6.15f, 0f), new Vector3(0.75f, 0.06f, 0.75f), new Color(0.3f, 0.6f, 0.2f)); // louros
            return 0.9f;
        }
        // ciprestes
        int trees = Random.Range(2, 4);
        for (int i = 0; i < trees; i++)
        {
            float pz = -3f + i * 3f + Random.Range(-0.5f, 0.5f);
            float th = Random.Range(5f, 8f);
            g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.6f, pz), new Vector3(0.3f, 0.6f, 0.3f), new Color(0.4f, 0.28f, 0.18f));
            g.Prim(PrimitiveType.Sphere, t, new Vector3(0f, th / 2f + 0.8f, pz), new Vector3(1.2f, th, 1.2f), new Color(0.12f, 0.32f, 0.15f));
        }
        return 0.8f;
    }

    // ================================================================== construções do Sheol

    public static float BuildSheol(RunnerGame g, Transform t, float side)
    {
        var T = Sheol;
        float face = -side;
        float r = Random.value;
        Color rock = T.stones[Random.Range(0, T.stones.Length)];

        if (r < 0.3f)
        {
            // pilares de basalto com rachaduras de lava
            int n = Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                float h = Random.Range(4f, 14f);
                float x = Random.Range(-1.2f, 1.2f), z = Random.Range(-3.5f, 3.5f);
                var p = g.Prim(PrimitiveType.Cube, t, new Vector3(x, h / 2f, z), new Vector3(1.2f, h, 1.2f), rock);
                p.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 90f), Random.Range(-6f, 6f));
                g.Prim(PrimitiveType.Cube, p.transform, new Vector3(0.51f, 0f, 0f), new Vector3(0.02f, 0.8f, 0.1f), T.metal, true);
            }
            return 1.8f;
        }
        if (r < 0.5f)
        {
            // poça de lava com rochas em volta
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, 0f), new Vector3(4f, 0.1f, 6f), T.metal, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                var rk = g.Prim(PrimitiveType.Cube, t, new Vector3(Mathf.Cos(a) * 2.3f, 0.5f, Mathf.Sin(a) * 3.2f), Vector3.one * Random.Range(0.8f, 1.6f), rock);
                rk.transform.localRotation = Quaternion.Euler(Random.Range(0f, 40f), Random.Range(0f, 90f), Random.Range(0f, 40f));
            }
            return 2.6f;
        }
        if (r < 0.68f)
        {
            // pilha de ossos com crânio
            var bone = new Color(0.88f, 0.85f, 0.75f);
            for (int i = 0; i < 9; i++)
            {
                var b = g.Prim(PrimitiveType.Cube, t, new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.1f, 0.8f), Random.Range(-2f, 2f)), new Vector3(0.15f, 0.15f, Random.Range(0.8f, 1.6f)), bone);
                b.transform.localRotation = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 180f), Random.Range(-30f, 30f));
            }
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.2f, 0f), new Vector3(1.1f, 1f, 1f), bone);
            for (int s = -1; s <= 1; s += 2)
                g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.51f, 1.35f, s * 0.22f), new Vector3(0.05f, 0.25f, 0.22f), T.metal, true);
            g.Prim(PrimitiveType.Cube, t, new Vector3(face * 0.51f, 0.95f, 0f), new Vector3(0.05f, 0.15f, 0.5f), T.dark);
            return 1.4f;
        }
        if (r < 0.85f)
        {
            // árvore morta em chamas
            var trunk = new Color(0.1f, 0.07f, 0.06f);
            float h = Random.Range(4f, 7f);
            g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h / 2f, 0f), new Vector3(0.45f, h / 2f, 0.45f), trunk);
            for (int i = 0; i < 4; i++)
            {
                var br = g.Prim(PrimitiveType.Cube, t, new Vector3(0f, h * Random.Range(0.6f, 0.95f), 0f), new Vector3(0.15f, 0.15f, Random.Range(1.5f, 2.5f)), trunk);
                br.transform.localRotation = Quaternion.Euler(-35f, i * 90f + Random.Range(-20f, 20f), 0f);
                br.transform.localPosition += br.transform.localRotation * Vector3.forward * 0.8f;
            }
            for (int i = 0; i < 3; i++)
                g.Prim(PrimitiveType.Cube, t, new Vector3(Random.Range(-0.5f, 0.5f), h + Random.Range(-0.3f, 0.5f), Random.Range(-0.5f, 0.5f)), new Vector3(0.4f, 0.8f, 0.4f), i % 2 == 0 ? T.metal : new Color(1f, 0.8f, 0.2f), true);
            return 1.2f;
        }
        // portão de pedra negra com chamas
        float d2 = 6f;
        for (int s = -1; s <= 1; s += 2)
        {
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 4f, s * 2.2f), new Vector3(1.6f, 8f, 1.4f), rock);
            g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 8.5f, s * 2.2f), new Vector3(0.6f, 1f, 0.6f), T.metal, true);
        }
        g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 7.3f, 0f), new Vector3(1.7f, 1.2f, d2), rock * 0.8f);
        g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 3f, 0f), new Vector3(0.3f, 6f, 3f), T.dark);
        return 0.9f;
    }

    // ================================================================== beira da pista (corrida)

    /// Árvore / decoração da beira da pista na corrida, conforme o bioma.
    public static void BuildRaceSide(RunnerGame g, Transform t, BiomeTheme T)
    {
        if (T.id == 3)
        {
            if (Random.value < 0.6f) Palm(g, t, new Vector3(0f, -2f, 0f), 4.5f);
            else
            {
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.8f, 4f, 0.8f), new Color(0.75f, 0.4f, 0.32f));
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 2.2f, 0f), new Vector3(0.5f, 0.5f, 0.5f), T.metal, true);
            }
            return;
        }
        if (T.id == 1)
        {
            if (Random.value < 0.6f)
            {
                g.Prim(PrimitiveType.Cylinder, t, new Vector3(0f, -1.5f, 0f), new Vector3(0.3f, 0.5f, 0.3f), new Color(0.4f, 0.28f, 0.18f));
                g.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.6f, 0f), new Vector3(1.2f, 4f, 1.2f), new Color(0.12f, 0.32f, 0.15f));
            }
            else
            {
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.2f, 0f), new Vector3(1.2f, 1.6f, 1.2f), T.stones[2]);
                g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 2f, 0.45f), T.stones[0]);
                g.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.9f, 0f), Vector3.one * 0.55f, T.stones[0]);
            }
            return;
        }
        // Sheol: espinhos de rocha e chamas
        var rock = T.stones[Random.Range(0, T.stones.Length)];
        var sp = g.Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.2f, 0f), new Vector3(1f, 4.2f, 1f), rock);
        sp.transform.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), 45f, Random.Range(-10f, 10f));
        g.Prim(PrimitiveType.Cube, t, new Vector3(0f, 2.2f, 0f), new Vector3(0.45f, 0.9f, 0.45f), T.metal, true);
    }
}
