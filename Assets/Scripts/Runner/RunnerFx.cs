using UnityEngine;

/// <summary>
/// Animação simples para efeitos: muda de escala, anda, gira, pode seguir o jogador e some sozinha.
/// </summary>
public class RunnerTween : MonoBehaviour
{
    public Transform follow;          // se definido, o efeito acompanha (o jogador anda rápido)
    public Vector3 offset;
    public Vector3 velocity;
    public Vector3 fromScale, toScale;
    public float life = 1f;
    public float delay;
    public float spinY;
    public bool pingPong;             // cresce e depois encolhe (seno)
    float t;
    bool started;

    void Start()
    {
        if (delay > 0f) transform.localScale = Vector3.zero;
        Place(0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (delay > 0f) { delay -= dt; if (delay > 0f) { Place(0f); return; } }
        started = true;
        t += dt;
        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, life));
        float e = pingPong ? Mathf.Sin(k * Mathf.PI) : k;
        transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, e);
        Place(t);
        if (spinY != 0f) transform.Rotate(0f, spinY * dt, 0f, Space.World);
        if (t >= life) Destroy(gameObject);
    }

    void Place(float time)
    {
        if (follow != null) transform.position = follow.position + offset + velocity * time;
        else if (started) transform.position += velocity * Time.deltaTime;
    }
}

/// <summary>
/// Efeitos visuais das cartas (quando você pega a carta e quando o poder dispara).
/// </summary>
public static class CardFx
{
    static RunnerGame G => RunnerGame.I;

    static GameObject Piece(PrimitiveType type, Color c, bool glow)
    {
        var go = G.Prim(type, null, Vector3.zero, Vector3.one, c, glow);
        return go;
    }

    public static RunnerTween Tween(GameObject go, Transform follow, Vector3 offset, Vector3 from, Vector3 to, float life, bool pingPong = false, Vector3 vel = default(Vector3), float delay = 0f, float spin = 0f)
    {
        var tw = go.AddComponent<RunnerTween>();
        tw.follow = follow; tw.offset = offset; tw.fromScale = from; tw.toScale = to;
        tw.life = life; tw.pingPong = pingPong; tw.velocity = vel; tw.delay = delay; tw.spinY = spin;
        go.transform.position = (follow != null ? follow.position : Vector3.zero) + offset;
        go.transform.localScale = from;
        return tw;
    }

    /// Estouro de faíscas em volta do jogador.
    public static void Burst(Transform p, Color c, int n, float speed, float size)
    {
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var dir = new Vector3(Mathf.Cos(a), Random.Range(0.2f, 1f), Mathf.Sin(a));
            var go = Piece(PrimitiveType.Cube, c, true);
            go.transform.rotation = Random.rotation;
            Tween(go, p, Vector3.up * 0.5f, Vector3.one * size, Vector3.zero, 0.7f, false, dir * speed);
        }
    }

    /// Anel que se expande (trombetas, escudo, evolução).
    public static void Ring(Transform p, Color c, float maxRadius, float life, float delay = 0f, float height = 0.4f)
    {
        var root = new GameObject("FxAnel");
        for (int k = 0; k < 18; k++)
        {
            float a = k * Mathf.PI * 2f / 18f;
            G.Prim(PrimitiveType.Cube, root.transform, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), new Vector3(0.12f, 0.12f, 0.3f), c, true)
                .transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        }
        Tween(root, p, Vector3.up * height, Vector3.one * 0.3f, Vector3.one * maxRadius, life, false, Vector3.zero, delay, 90f);
    }

    /// Coluna de luz/fogo descendo do céu sobre o jogador.
    public static void Pillar(Transform p, Color c, float width, float life)
    {
        var go = Piece(PrimitiveType.Cylinder, c, true);
        Tween(go, p, Vector3.up * 12f, new Vector3(0f, 12f, 0f), new Vector3(width, 12f, width), life, true);
    }

    /// Paredes de água subindo dos dois lados (Êx 14:22).
    public static void WaterWalls(Transform p)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var wall = Piece(PrimitiveType.Cube, new Color(0.15f, 0.45f, 0.85f), false);
            Tween(wall, p, new Vector3(s * 6.5f, 0f, 14f), new Vector3(2.2f, 0f, 34f), new Vector3(2.2f, 14f, 34f), 1.6f, true);
            var foam = Piece(PrimitiveType.Cube, new Color(0.85f, 0.95f, 1f), true);
            Tween(foam, p, new Vector3(s * 6.5f, 0.2f, 14f), new Vector3(2.4f, 0.1f, 34f), new Vector3(2.4f, 0.6f, 34f), 1.6f, true);
            for (int k = 0; k < 10; k++)
            {
                var drop = Piece(PrimitiveType.Cube, new Color(0.6f, 0.85f, 1f), true);
                Tween(drop, p, new Vector3(s * 5.5f, Random.Range(1f, 6f), Random.Range(0f, 28f)), Vector3.one * 0.25f, Vector3.zero, 1.2f, false,
                    new Vector3(-s * Random.Range(1f, 3f), Random.Range(1f, 4f), 0f), Random.Range(0f, 0.6f));
            }
        }
    }

    /// Flocos caindo do céu (maná, pão).
    public static void Flakes(Transform p, Color c, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var go = Piece(PrimitiveType.Cube, c, true);
            go.transform.rotation = Random.rotation;
            Tween(go, p, new Vector3(Random.Range(-3f, 3f), Random.Range(4f, 7f), Random.Range(-1f, 8f)), Vector3.one * 0.18f, Vector3.one * 0.12f,
                1.4f, false, new Vector3(0f, -4f, 0f), Random.Range(0f, 0.5f), 200f);
        }
    }

    /// Corações subindo (vida, cura).
    public static void Hearts(Transform p, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var go = Piece(PrimitiveType.Cube, new Color(1f, 0.25f, 0.35f), true);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Tween(go, p, new Vector3(Random.Range(-1f, 1f), 1f, Random.Range(-0.5f, 1f)), Vector3.one * 0.3f, Vector3.zero, 1.1f, false,
                new Vector3(0f, Random.Range(2f, 3.5f), 0f), i * 0.08f);
        }
    }

    /// Objeto girando acima da cabeça (arma nova).
    public static void Spinner(Transform p, Color c, Vector3 shape)
    {
        var go = Piece(PrimitiveType.Cube, c, true);
        Tween(go, p, Vector3.up * 2.3f, Vector3.zero, shape, 1.1f, true, Vector3.up * 0.4f, 0f, 720f);
    }
}
