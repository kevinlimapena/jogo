using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carruagem antiga puxada por dois cavalos (usada na corrida pelo jogador e pelos rivais),
/// e a variante "carro de fogo de Elias" (2 Reis 2:11) usada no voo livre:
/// carruagem dourada, cavalos e rodas em chamas, deixando um rastro de fogo.
/// Centro do objeto = centro da carroceria; o chão fica em groundY (local).
/// </summary>
public class RunnerChariot : MonoBehaviour
{
    readonly List<Transform> wheels = new List<Transform>();
    readonly List<Transform> legs = new List<Transform>();
    readonly List<float> legPhase = new List<float>();
    readonly List<Transform> flames = new List<Transform>();
    readonly List<Vector3> flameBase = new List<Vector3>();
    readonly List<Transform> horses = new List<Transform>();
    readonly List<Vector3> horseBase = new List<Vector3>();
    bool fire;
    float phase;
    float spin;
    float trailTimer;
    const float WheelRadius = 0.5f;

    static readonly Color Wood = new Color(0.5f, 0.32f, 0.17f);
    static readonly Color DarkWood = new Color(0.32f, 0.2f, 0.1f);
    static readonly Color Bronze = new Color(0.78f, 0.52f, 0.22f);
    static readonly Color Gold = new Color(1f, 0.78f, 0.28f);
    static readonly Color FireOrange = new Color(1f, 0.5f, 0.08f);
    static readonly Color FireYellow = new Color(1f, 0.85f, 0.3f);
    static readonly Color FireRed = new Color(1f, 0.22f, 0.05f);

    public static RunnerChariot Build(Transform parent, Color team, bool fireChariot, bool isPlayer, float groundY = -0.55f)
    {
        var g = RunnerGame.I;
        var root = new GameObject(fireChariot ? "FireChariot" : "Chariot").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, 0f, -0.6f);   // centraliza carruagem+cavalos na área de colisão
        var c = root.gameObject.AddComponent<RunnerChariot>();
        c.fire = fireChariot;

        Color body = fireChariot ? Gold : Wood;
        Color trim = fireChariot ? FireYellow : Gold;
        Color panel = fireChariot ? FireOrange : team;
        float wheelY = groundY + WheelRadius;

        // ---------------- carroceria
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY - 0.05f, -0.45f), new Vector3(1.3f, 0.12f, 1.1f), body);              // piso
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.35f, 0.1f), new Vector3(1.3f, 0.75f, 0.12f), panel, fireChariot); // frente
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, root, new Vector3(s * 0.65f, wheelY + 0.2f, -0.4f), new Vector3(0.1f, 0.5f, 1.0f), panel, fireChariot);
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.74f, 0.1f), new Vector3(1.36f, 0.07f, 0.18f), trim, true);       // borda
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, root, new Vector3(s * 0.65f, wheelY + 0.47f, -0.4f), new Vector3(0.14f, 0.06f, 1.04f), trim, true);
        // emblema na frente (sol / roda)
        g.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, wheelY + 0.38f, 0.17f), new Vector3(0.36f, 0.02f, 0.36f), trim, true).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // eixo
        g.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, wheelY, -0.5f), new Vector3(0.1f, 0.95f, 0.1f), DarkWood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // lança que liga aos cavalos
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.05f, 0.95f), new Vector3(0.09f, 0.09f, 1.7f), fireChariot ? Gold : DarkWood);
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.45f, 1.6f), new Vector3(1.0f, 0.08f, 0.08f), fireChariot ? Gold : DarkWood); // canga

        // ---------------- rodas raiadas
        for (int s = -1; s <= 1; s += 2)
        {
            var w = new GameObject("Wheel").transform;
            w.SetParent(root, false);
            w.localPosition = new Vector3(s * 0.82f, wheelY, -0.5f);
            Color rim = fireChariot ? FireOrange : DarkWood;
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI * 2f / 12f;
                var seg = g.Prim(PrimitiveType.Cube, w, new Vector3(0f, Mathf.Cos(a) * WheelRadius, Mathf.Sin(a) * WheelRadius), new Vector3(0.1f, 0.12f, 0.27f), rim, fireChariot);
                seg.transform.localRotation = Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f);
            }
            for (int k = 0; k < 3; k++)
            {
                var spoke = g.Prim(PrimitiveType.Cube, w, Vector3.zero, new Vector3(0.05f, WheelRadius * 2f, 0.05f), fireChariot ? FireYellow : Wood, fireChariot);
                spoke.transform.localRotation = Quaternion.Euler(k * 60f, 0f, 0f);
            }
            g.Prim(PrimitiveType.Cylinder, w, Vector3.zero, new Vector3(0.2f, 0.08f, 0.2f), fireChariot ? Gold : Bronze, fireChariot).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            c.wheels.Add(w);
        }

        // ---------------- condutor (o jogador tem auréola)
        Color robe = fireChariot ? new Color(0.95f, 0.92f, 0.85f) : (isPlayer ? new Color(0.95f, 0.94f, 0.9f) : team * 0.8f);
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.6f, -0.45f), new Vector3(0.36f, 0.6f, 0.26f), robe);
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 1.02f, -0.45f), new Vector3(0.24f, 0.24f, 0.24f), new Color(0.8f, 0.62f, 0.45f));
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.95f, -0.58f), new Vector3(0.22f, 0.18f, 0.06f), new Color(0.85f, 0.85f, 0.85f)); // barba (Elias!)
        if (isPlayer || fireChariot)
            g.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, wheelY + 1.25f, -0.45f), new Vector3(0.34f, 0.015f, 0.34f), Gold, true);
        // braços segurando as rédeas
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, wheelY + 0.72f, -0.2f), new Vector3(0.4f, 0.08f, 0.35f), robe * 0.9f);
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, root, new Vector3(s * 0.2f, wheelY + 0.65f, 0.75f), new Vector3(0.02f, 0.02f, 1.6f), DarkWood);
        if (!fireChariot)
        {
            // estandarte da equipe
            g.Prim(PrimitiveType.Cube, root, new Vector3(0.55f, wheelY + 0.9f, -0.85f), new Vector3(0.04f, 1.1f, 0.04f), DarkWood);
            g.Prim(PrimitiveType.Cube, root, new Vector3(0.55f, wheelY + 1.25f, -1.05f), new Vector3(0.02f, 0.38f, 0.38f), team);
        }

        // ---------------- cavalos
        for (int s = -1; s <= 1; s += 2)
        {
            var h = new GameObject("Horse").transform;
            h.SetParent(root, false);
            h.localPosition = new Vector3(s * 0.38f, groundY + 0.95f, 2.15f);
            Color hc = fireChariot ? (s < 0 ? FireOrange : FireRed) : (isPlayer ? new Color(0.92f, 0.9f, 0.86f) : new Color(0.45f + Random.value * 0.2f, 0.3f, 0.18f));
            Color mane = fireChariot ? FireYellow : (isPlayer ? new Color(0.2f, 0.5f, 1f) : new Color(0.15f, 0.1f, 0.07f));
            bool glow = fireChariot;
            g.Prim(PrimitiveType.Cube, h, Vector3.zero, new Vector3(0.36f, 0.4f, 0.95f), hc, glow);
            var neck = g.Prim(PrimitiveType.Cube, h, new Vector3(0f, 0.32f, 0.45f), new Vector3(0.2f, 0.45f, 0.22f), hc, glow);
            neck.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            g.Prim(PrimitiveType.Cube, h, new Vector3(0f, 0.55f, 0.65f), new Vector3(0.2f, 0.22f, 0.42f), hc, glow);
            g.Prim(PrimitiveType.Cube, h, new Vector3(0f, 0.5f, 0.38f), new Vector3(0.08f, 0.32f, 0.26f), mane, true);
            g.Prim(PrimitiveType.Cube, h, new Vector3(0f, 0.08f, -0.55f), new Vector3(0.1f, 0.35f, 0.12f), mane, true);
            g.Prim(PrimitiveType.Cube, h, new Vector3(0f, -0.05f, 0f), new Vector3(0.38f, 0.1f, 0.5f), fireChariot ? Gold : (isPlayer ? Gold : team), fireChariot); // manta
            int li = 0;
            for (int lz = 1; lz >= -1; lz -= 2)
            for (int lx = -1; lx <= 1; lx += 2)
            {
                var pivot = new GameObject("Leg").transform;
                pivot.SetParent(h, false);
                pivot.localPosition = new Vector3(lx * 0.12f, -0.18f, lz * 0.34f);
                g.Prim(PrimitiveType.Cube, pivot, new Vector3(0f, -0.3f, 0f), new Vector3(0.09f, 0.6f, 0.1f), hc, glow);
                c.legs.Add(pivot);
                c.legPhase.Add((li % 2 == 0 ? 0f : Mathf.PI) + (lz > 0 ? 0f : Mathf.PI * 0.5f) + s * 0.4f);
                li++;
            }
            c.horses.Add(h);
            c.horseBase.Add(h.localPosition);

            if (fireChariot)
            {
                // chamas no dorso e na cabeça do cavalo
                AddFlame(c, h, new Vector3(0f, 0.35f, -0.1f), 0.3f, FireYellow);
                AddFlame(c, h, new Vector3(0f, 0.85f, 0.6f), 0.22f, FireYellow);
                AddFlame(c, h, new Vector3(0f, 0.25f, -0.6f), 0.25f, FireRed);
            }
        }

        if (fireChariot)
        {
            // chamas saindo da carruagem
            for (int s = -1; s <= 1; s += 2)
            {
                AddFlame(c, root, new Vector3(s * 0.65f, wheelY + 0.85f, -0.6f), 0.35f, FireOrange);
                AddFlame(c, root, new Vector3(s * 0.65f, wheelY + 0.8f, 0.0f), 0.28f, FireYellow);
                AddFlame(c, root, new Vector3(s * 0.82f, wheelY + 0.6f, -0.5f), 0.3f, FireRed);
            }
            AddFlame(c, root, new Vector3(0f, wheelY + 0.95f, 0.1f), 0.4f, FireOrange);
            AddFlame(c, root, new Vector3(0f, wheelY + 0.3f, -1.05f), 0.45f, FireRed);
        }
        return c;
    }

    static void AddFlame(RunnerChariot c, Transform parent, Vector3 pos, float size, Color col)
    {
        var f = RunnerGame.I.Prim(PrimitiveType.Cube, parent, pos, new Vector3(size, size * 1.6f, size), col, true).transform;
        f.localRotation = Quaternion.Euler(0f, 45f, 0f);
        c.flames.Add(f);
        c.flameBase.Add(f.localScale);
    }

    void Update()
    {
        var g = RunnerGame.I;
        float spd = g != null ? g.speed : 10f;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        phase += dt * (5f + spd * 0.25f);
        spin += Mathf.Min(spd / WheelRadius, 30f) * Mathf.Rad2Deg * dt;

        foreach (var w in wheels) w.localRotation = Quaternion.Euler(spin, 0f, 0f);

        for (int i = 0; i < legs.Count; i++)
            legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase + legPhase[i]) * 35f, 0f, 0f);
        for (int i = 0; i < horses.Count; i++)
            horses[i].localPosition = horseBase[i] + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase + i)) * 0.08f, 0f);

        if (!fire) return;

        // chamas tremulando
        for (int i = 0; i < flames.Count; i++)
        {
            float k = 0.75f + Mathf.PerlinNoise(Time.time * 6f, i * 1.7f) * 0.6f;
            var b = flameBase[i];
            flames[i].localScale = new Vector3(b.x * (1.1f - k * 0.2f), b.y * k, b.z * (1.1f - k * 0.2f));
            flames[i].Rotate(0f, 240f * dt, 0f, Space.Self);
        }

        // rastro de fogo
        trailTimer -= dt;
        if (trailTimer <= 0f && g != null && gameObject.activeInHierarchy)
        {
            trailTimer = Application.isMobilePlatform ? 0.08f : 0.04f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = g.Glow(Random.value < 0.5f ? FireOrange : (Random.value < 0.5f ? FireYellow : FireRed));
            go.transform.position = transform.TransformPoint(new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.6f), Random.Range(-1.1f, 2.2f)));
            go.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);
            var d = go.AddComponent<RunnerDebris>();
            d.gravity = false;
            d.velocity = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(1f, 2.5f), Random.Range(-1f, 0f));
            d.life = Random.Range(0.35f, 0.6f);
        }
    }
}
