using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mini-jogo "Torre de Babel" (Gênesis 11): o profeta escala a torre pulando de laje em laje
/// para derrubar o ídolo no topo antes que o tempo acabe.
/// Plano 2D (x = lado, y = altura) colado na frente da torre; nas bordas você dá a volta na torre.
/// Cada vez que é jogado muda de estilo:
///   0) A Construção     — escalada normal, tijolos caindo dos construtores.
///   1) Vento do Deserto — rajadas empurram para os lados, lajes que se movem.
///   2) Confusão de Línguas — os controles se invertem, a tela gira e treme, o texto embaralha.
/// </summary>
public class RunnerBabel : MonoBehaviour
{
    public float timeLimit = 65f;
    public float halfW = 6.5f;

    public bool Active { get; private set; }
    public int Style { get; private set; }
    public float TimeLeft { get; private set; }
    public float Height => pos.y;
    public float Goal { get; private set; }
    public bool Inverted { get; private set; }
    public bool InvertWarning { get; private set; }
    public float Wind { get; private set; }

    public static readonly string[] StyleNames = { "A CONSTRUÇÃO", "VENTO DO DESERTO", "CONFUSÃO DE LÍNGUAS" };
    public static readonly string[] StyleVerses =
    {
        "\"Edifiquemos uma torre cujo cume toque nos céus\" (Gn 11:4)",
        "\"O vento sopra onde quer\" (Jo 3:8)",
        "\"Confundamos ali a sua língua\" (Gn 11:7)"
    };

    /// Próximo estilo (para mostrar no menu).
    public static int NextStyle => PlayerPrefs.GetInt("babel_plays", 0) % 3;

    class Plat { public Transform t; public float x, y, w, baseX, moveAmp, moveFreq, phase; public bool crumble, broken, spring; public float fall; }
    class Builder { public Transform t; public Plat plat; public float x, dir, hp, throwCd; public bool dead; }
    class Brick { public Transform t; public Vector2 pos, vel; public float r; }
    class Shot { public Transform t; public Vector2 pos, vel; }
    class Tablet { public Transform t; public Vector2 pos; }

    RunnerGame g;
    Transform root, hero, heroBody;
    Vector3 origin;
    Vector2 pos, vel;
    float camY, t, brickCd, fireCd, windCd, windTarget, invertClock, hitShake, squash;
    int tier;
    readonly List<Plat> plats = new List<Plat>();
    readonly List<Builder> builders = new List<Builder>();
    readonly List<Brick> bricks = new List<Brick>();
    readonly List<Shot> shots = new List<Shot>();
    readonly List<Tablet> tablets = new List<Tablet>();
    Transform idol;

    const float Gravity = 32f;
    const float JumpV = 16.5f;
    const float MoveSpeed = 9.5f;

    static readonly Color Mud = new Color(0.72f, 0.56f, 0.38f);
    static readonly Color MudDark = new Color(0.5f, 0.37f, 0.24f);
    static readonly Color Bitumen = new Color(0.12f, 0.1f, 0.09f);
    static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

    Vector3 W(Vector2 p, float z = 0f) => origin + new Vector3(p.x, p.y, z);

    // ================================================================== início / fim

    public void Begin(RunnerGame game, int difficultyTier)
    {
        g = game;
        tier = difficultyTier;
        int plays = PlayerPrefs.GetInt("babel_plays", 0);
        Style = plays % 3;
        PlayerPrefs.SetInt("babel_plays", plays + 1);
        PlayerPrefs.Save();

        var pz = g.player.transform.position.z;
        origin = new Vector3(0f, 0f, pz + 20f);
        root = new GameObject("TorreDeBabel").transform;
        root.position = origin;

        Goal = 72f + Mathf.Min(tier, 3) * 10f;
        TimeLeft = timeLimit + (Style == 2 ? 8f : 0f);
        t = 0f;
        brickCd = 3f;
        fireCd = 0.5f;
        windCd = 2f;
        windTarget = 0f;
        Wind = 0f;
        invertClock = 0f;
        Inverted = InvertWarning = false;
        hitShake = 0f;

        BuildTower();
        BuildPlatforms();
        BuildHero();

        pos = new Vector2(0f, 0.2f);
        vel = new Vector2(0f, JumpV);
        camY = 6f;

        Active = true;
        UpdateCamera();
    }

    public void End()
    {
        if (!Active) return;
        Active = false;
        plats.Clear(); builders.Clear(); bricks.Clear(); shots.Clear(); tablets.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    public void UpdateCamera()
    {
        var cam = Camera.main;
        if (cam == null || !Active) return;
        Vector3 shake = hitShake > 0f ? Random.insideUnitSphere * hitShake * 0.5f : Vector3.zero;
        float roll = 0f;
        if (Style == 2)
        {
            roll = Mathf.Sin(t * 0.9f) * 5f + (Inverted ? Mathf.Sin(t * 7f) * 3f : 0f);
            if (Inverted || InvertWarning) shake += Random.insideUnitSphere * 0.12f;
        }
        cam.transform.position = origin + new Vector3(0f, camY + 1.5f, -17f) + shake;
        cam.transform.rotation = Quaternion.Euler(4f, 0f, roll);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f, 0.2f);
    }

    // ================================================================== construção

    void BuildTower()
    {
        // zigurate em degraus: cada andar um pouco mais estreito, com faixas de betume (Gn 11:3)
        float y = -1f;
        int floor = 0;
        while (y < Goal + 4f)
        {
            float h = 12f;
            float w = Mathf.Lerp(halfW * 2f + 6f, halfW * 2f + 1.5f, y / (Goal + 4f));
            var c = floor % 2 == 0 ? Mud : Mud * 0.92f;
            c.a = 1f;
            g.Prim(PrimitiveType.Cube, root, new Vector3(0f, y + h / 2f, 3.2f), new Vector3(w, h, 5f), c);
            g.Prim(PrimitiveType.Cube, root, new Vector3(0f, y + h - 0.2f, 0.9f), new Vector3(w + 0.6f, 0.4f, 0.5f), Bitumen);
            // janelas escuras / tochas
            for (int k = -2; k <= 2; k++)
            {
                bool torch = (k + floor) % 3 == 0;
                g.Prim(PrimitiveType.Cube, root, new Vector3(k * (w / 5.5f), y + h * 0.55f, 0.68f), new Vector3(0.7f, 1.4f, 0.1f),
                    torch ? new Color(1f, 0.6f, 0.2f) : new Color(0.1f, 0.07f, 0.05f), torch);
            }
            y += h;
            floor++;
        }
        // chão (base da torre)
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(halfW * 2f + 8f, 1f, 4f), MudDark);

        // o ídolo no topo
        idol = new GameObject("Idolo").transform;
        idol.SetParent(root, false);
        idol.localPosition = new Vector3(0f, Goal + 0.4f, 0.4f);
        g.Prim(PrimitiveType.Cube, idol, new Vector3(0f, 0f, 0f), new Vector3(3.2f, 0.8f, 1.6f), MudDark);
        g.Prim(PrimitiveType.Cube, idol, new Vector3(0f, 1.6f, 0f), new Vector3(1.1f, 2.4f, 0.8f), Gold, true);
        g.Prim(PrimitiveType.Sphere, idol, new Vector3(0f, 3.1f, 0f), Vector3.one * 0.9f, Gold, true);
        for (int s = -1; s <= 1; s += 2)
        {
            var horn = g.Prim(PrimitiveType.Cube, idol, new Vector3(s * 0.45f, 3.6f, 0f), new Vector3(0.18f, 0.7f, 0.18f), Gold, true);
            horn.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 25f);
            g.Prim(PrimitiveType.Sphere, idol, new Vector3(s * 0.2f, 3.15f, -0.42f), Vector3.one * 0.18f, new Color(1f, 0.1f, 0.05f), true);
        }
    }

    void BuildPlatforms()
    {
        // laje do chão (largura total)
        AddPlat(0f, 0f, halfW * 2f, false, false, 0f);
        float y = 2.6f;
        float lastX = 0f;
        int n = 0;
        float crumbleChance = Style == 2 ? 0.3f : 0.18f;
        float moveChance = Style == 1 ? 0.35f : (Style == 2 ? 0.2f : 0.1f);
        while (y < Goal - 1.5f)
        {
            float w = Random.Range(2.2f, 3.4f) - Mathf.Min(0.6f, tier * 0.12f);
            float x = Mathf.Clamp(lastX + Random.Range(-6f, 6f), -halfW + w / 2f, halfW - w / 2f);
            bool crumble = n > 2 && Random.value < crumbleChance;
            bool spring = !crumble && n > 4 && Random.value < 0.07f;
            float amp = (!crumble && n > 3 && Random.value < moveChance) ? Random.Range(1.5f, 3f) : 0f;
            var p = AddPlat(x, y, w, crumble, spring, amp);

            // garante uma laje firme logo depois de uma que desmorona
            if (crumble)
            {
                float y2 = y + Random.Range(0.6f, 1.2f);
                float x2 = Mathf.Clamp(-x * 0.6f + Random.Range(-1.5f, 1.5f), -halfW + 1.3f, halfW - 1.3f);
                AddPlat(x2, y2, 2.6f, false, false, 0f);
                y = y2;
                x = x2;
            }
            else if (y > 8f && amp == 0f && !spring && w >= 2.6f && Random.value < 0.22f + tier * 0.04f)
                AddBuilder(p);

            if (Random.value < 0.3f) AddTablet(new Vector2(Random.Range(-halfW + 1f, halfW - 1f), y + Random.Range(1.2f, 2.2f)));

            lastX = x;
            y += Random.Range(2.1f, 3.3f) + (Style == 1 ? 0.1f : 0f);
            n++;
        }
        // laje final larga, logo abaixo do ídolo
        AddPlat(0f, Goal - 0.2f, 5f, false, false, 0f);
    }

    Plat AddPlat(float x, float y, float w, bool crumble, bool spring, float amp)
    {
        var tr = new GameObject(crumble ? "LajeRachada" : "Laje").transform;
        tr.SetParent(root, false);
        var top = crumble ? new Color(0.55f, 0.42f, 0.3f) : new Color(0.8f, 0.64f, 0.45f);
        g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, -0.2f, 0f), new Vector3(w, 0.4f, 1.6f), top);
        g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, -0.45f, 0.1f), new Vector3(w - 0.2f, 0.1f, 1.4f), Bitumen);
        if (crumble)
            for (int k = 0; k < 3; k++)
                g.Prim(PrimitiveType.Cube, tr, new Vector3(Random.Range(-w / 2f + 0.3f, w / 2f - 0.3f), -0.01f, -0.81f), new Vector3(0.06f, 0.42f, 0.04f), Bitumen);
        if (spring)
        {
            g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.15f, 0.9f), Gold, true);
            g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 0.25f, -0.4f), new Vector3(0.12f, 0.35f, 0.04f), Gold, true);
        }
        if (amp > 0f)
            for (int s = -1; s <= 1; s += 2)
                g.Prim(PrimitiveType.Cube, tr, new Vector3(s * (w / 2f - 0.15f), -0.2f, -0.82f), new Vector3(0.15f, 0.25f, 0.04f), new Color(0.4f, 0.8f, 1f), true);
        var p = new Plat { t = tr, x = x, baseX = x, y = y, w = w, crumble = crumble, spring = spring, moveAmp = amp, moveFreq = Random.Range(0.6f, 1.1f), phase = Random.Range(0f, 6.28f) };
        tr.position = W(new Vector2(x, y));
        plats.Add(p);
        return p;
    }

    void AddBuilder(Plat p)
    {
        var tr = new GameObject("Construtor").transform;
        tr.SetParent(root, false);
        var skin = new Color(0.7f, 0.5f, 0.35f);
        g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.8f, 0.45f), new Color(0.45f, 0.2f, 0.15f));
        g.Prim(PrimitiveType.Sphere, tr, new Vector3(0f, 1.1f, 0f), Vector3.one * 0.45f, skin);
        g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 1.32f, 0f), new Vector3(0.5f, 0.12f, 0.5f), new Color(0.9f, 0.85f, 0.7f));
        g.Prim(PrimitiveType.Cube, tr, new Vector3(0.38f, 0.75f, -0.1f), new Vector3(0.45f, 0.25f, 0.3f), new Color(0.75f, 0.35f, 0.2f));   // tijolo nas mãos
        var b = new Builder { t = tr, plat = p, x = p.x, dir = Random.value < 0.5f ? -1f : 1f, hp = Mathf.Max(2f, g.stats.Damage * 2.5f), throwCd = Random.Range(1.5f, 3f) };
        builders.Add(b);
    }

    void AddTablet(Vector2 at)
    {
        var tr = new GameObject("Tabua").transform;
        tr.SetParent(root, false);
        g.Prim(PrimitiveType.Cube, tr, Vector3.zero, new Vector3(0.5f, 0.7f, 0.12f), new Color(0.85f, 0.75f, 0.55f), true);
        for (int k = 0; k < 3; k++)
            g.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 0.2f - k * 0.18f, -0.07f), new Vector3(0.35f, 0.04f, 0.02f), Bitumen);
        tr.position = W(at, -0.3f);
        tablets.Add(new Tablet { t = tr, pos = at });
    }

    void BuildHero()
    {
        var p = Meta.Selected;
        hero = new GameObject("Profeta").transform;
        hero.SetParent(root, false);
        heroBody = new GameObject("Corpo").transform;
        heroBody.SetParent(hero, false);
        var robe = p.color;
        g.Prim(PrimitiveType.Cube, heroBody, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.95f, 0.45f), robe);
        g.Prim(PrimitiveType.Cube, heroBody, new Vector3(0f, 0.12f, 0f), new Vector3(0.7f, 0.25f, 0.5f), robe * 0.85f);
        g.Prim(PrimitiveType.Sphere, heroBody, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.45f, new Color(0.85f, 0.65f, 0.48f));
        g.Prim(PrimitiveType.Cube, heroBody, new Vector3(0f, 1.0f, -0.18f), new Vector3(0.3f, 0.25f, 0.12f), new Color(0.55f, 0.55f, 0.55f));   // barba
        g.Prim(PrimitiveType.Cube, heroBody, new Vector3(0f, 1.35f, 0f), new Vector3(0.5f, 0.14f, 0.5f), new Color(0.95f, 0.94f, 0.9f));
        // auréola
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI * 2f / 8f;
            g.Prim(PrimitiveType.Cube, heroBody, new Vector3(Mathf.Cos(a) * 0.32f, 1.62f, Mathf.Sin(a) * 0.32f), Vector3.one * 0.08f, Gold, true);
        }
        // cajado
        g.Prim(PrimitiveType.Cube, heroBody, new Vector3(0.45f, 0.7f, -0.05f), new Vector3(0.07f, 1.5f, 0.07f), new Color(0.45f, 0.3f, 0.15f));
    }

    // ================================================================== loop

    public void Tick(float dt)
    {
        if (!Active) return;
        float mdt = g.BulletTimeActive ? Time.unscaledDeltaTime : dt;   // o profeta ignora o tempo bala
        t += dt;

        // ---------------- entrada
        RunnerTouch.Update();
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var ms = Mouse.current;
        float axis = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis += 1f;
        }
        if (gp != null)
        {
            axis += gp.leftStick.ReadValue().x;
            axis += gp.dpad.ReadValue().x;
        }
        if (RunnerTouch.Holding) axis += RunnerTouch.Stick.x * 1.4f;
        axis = Mathf.Clamp(axis, -1f, 1f);

        if ((kb != null && (kb.qKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame)) || (ms != null && ms.rightButton.wasPressedThisFrame)
            || (gp != null && gp.leftTrigger.wasPressedThisFrame) || RunnerTouch.Pressed("ability"))
            g.TryBulletTime();

        // ---------------- estilos
        if (Style == 1) UpdateWind(dt);
        if (Style == 2) UpdateConfusion(dt);
        if (Inverted) axis = -axis;

        // ---------------- física do pulo
        vel.x = Mathf.MoveTowards(vel.x, axis * MoveSpeed, 60f * mdt);
        Vector2 prev = pos;
        pos.x += (vel.x + Wind) * mdt;
        vel.y -= Gravity * mdt;
        pos.y += vel.y * mdt;
        // dá a volta na torre
        if (pos.x > halfW + 0.4f) pos.x = -halfW - 0.3f;
        else if (pos.x < -halfW - 0.4f) pos.x = halfW + 0.3f;

        UpdatePlatforms(dt, mdt);
        if (vel.y <= 0f) Land(prev);

        // câmera só sobe
        camY = Mathf.Max(camY, Mathf.Lerp(camY, pos.y + 2.5f, 1f - Mathf.Exp(-5f * dt)));
        camY = Mathf.Min(camY, Goal + 2f);

        // visual do profeta
        squash = Mathf.MoveTowards(squash, 0f, dt * 4f);
        hero.position = W(pos, -0.6f);
        heroBody.localScale = new Vector3(1f + squash * 0.35f, 1f - squash * 0.35f, 1f);
        heroBody.localRotation = Quaternion.Euler(0f, 0f, -vel.x * 1.6f);
        if (g.player.invuln > 0f)
        {
            g.player.invuln -= dt;
            bool vis = g.player.invuln <= 0f || Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            foreach (var r in hero.GetComponentsInChildren<Renderer>()) r.enabled = vis;
        }
        if (hitShake > 0f) hitShake -= dt * 2f;
        idol.Rotate(0f, 40f * dt, 0f);

        UpdateTablets(dt);
        UpdateShots(dt, mdt);
        if (UpdateBuilders(dt)) return;
        if (UpdateBricks(dt)) return;

        // caiu para fora da tela: perde uma vida e volta para a laje mais próxima
        if (pos.y < camY - 11f)
        {
            Hurt();
            if (g.state != RunnerState.Playing || !Active) return;
            Respawn();
        }

        TimeLeft -= dt;
        if (pos.y >= Goal - 0.3f && Mathf.Abs(pos.x) < 3f) { Won(); return; }
        if (TimeLeft <= 0f) { g.BabelFailed(); return; }
    }

    void UpdatePlatforms(float dt, float mdt)
    {
        foreach (var p in plats)
        {
            if (p.broken)
            {
                p.fall += 20f * dt;
                p.y -= p.fall * dt;
                p.t.position = W(new Vector2(p.x, p.y));
                p.t.Rotate(0f, 0f, 90f * dt);
                continue;
            }
            if (p.moveAmp > 0f)
            {
                p.x = Mathf.Clamp(p.baseX + Mathf.Sin(t * p.moveFreq + p.phase) * p.moveAmp, -halfW + p.w / 2f, halfW - p.w / 2f);
                p.t.position = W(new Vector2(p.x, p.y));
            }
        }
    }

    void Land(Vector2 prev)
    {
        foreach (var p in plats)
        {
            if (p.broken) continue;
            if (prev.y < p.y - 0.05f || pos.y > p.y) continue;
            if (Mathf.Abs(pos.x - p.x) > p.w / 2f + 0.3f) continue;
            pos.y = p.y;
            vel.y = p.spring ? JumpV * 1.55f : JumpV;
            squash = 1f;
            if (p.spring) { g.ShowFloat(W(pos + Vector2.up * 1.5f), "SALTO!", Gold, false); g.Sfx("mola", 0.7f); }
            else g.Sfx("pulo", 0.35f, 0.1f, 0.05f);
            if (p.crumble)
            {
                p.broken = true;
                g.Explode(W(new Vector2(p.x, p.y)), new Color(0.55f, 0.42f, 0.3f), 8);
            }
            return;
        }
    }

    void Respawn()
    {
        Plat best = null;
        foreach (var p in plats)
        {
            if (p.broken || p.y > camY + 2f) continue;
            if (best == null || p.y > best.y) best = p;
        }
        if (best == null) best = plats[0];
        pos = new Vector2(best.x, best.y + 0.1f);
        vel = new Vector2(0f, JumpV);
        camY = Mathf.Max(best.y + 2.5f, 6f);
        if (g.player.invuln < 2f) g.player.invuln = 2f;
    }

    void UpdateWind(float dt)
    {
        windCd -= dt;
        if (windCd <= 0f)
        {
            windCd = Random.Range(3f, 5f);
            windTarget = Random.value < 0.25f ? 0f : (Random.value < 0.5f ? -1f : 1f) * Random.Range(3.5f, 6f + tier * 0.6f);
        }
        Wind = Mathf.MoveTowards(Wind, windTarget, 4f * dt);
        // areia voando
        if (Mathf.Abs(Wind) > 1f && Random.value < (Application.isMobilePlatform ? 0.35f : 0.7f))
        {
            MeshRenderer goR;
            var go = RunnerGame.NewPrim(PrimitiveType.Cube, out goR, false);
            goR.sharedMaterial = g.Mat(new Color(0.9f, 0.78f, 0.55f));
            go.transform.position = W(new Vector2(-Mathf.Sign(Wind) * (halfW + 6f), camY + Random.Range(-9f, 9f)), Random.Range(-3f, -1f));
            go.transform.localScale = new Vector3(Random.Range(0.6f, 1.6f), 0.05f, 0.05f);
            var d = go.AddComponent<RunnerDebris>();
            d.gravity = false;
            d.velocity = new Vector3(Wind * 5f, Random.Range(-0.5f, 0.5f), 0f);
            d.life = 0.9f;
        }
    }

    void UpdateConfusion(float dt)
    {
        // ciclo: 5 s normal → 1,2 s aviso → 4 s invertido
        invertClock += dt;
        float cycle = 10.2f;
        float c = Mathf.Repeat(invertClock, cycle);
        bool wasInv = Inverted;
        InvertWarning = c >= 5f && c < 6.2f;
        Inverted = c >= 6.2f;
        if (Inverted && !wasInv)
        {
            g.BossShake(0.4f);
            g.PlayClank();
        }
    }

    void UpdateTablets(float dt)
    {
        for (int i = tablets.Count - 1; i >= 0; i--)
        {
            var tb = tablets[i];
            tb.t.Rotate(0f, 120f * dt, 0f);
            if ((tb.pos - (pos + Vector2.up * 0.6f)).sqrMagnitude < 0.9f * 0.9f)
            {
                g.AddBonus(50, W(tb.pos, -1f));
                g.Explode(W(tb.pos, -0.5f), new Color(1f, 0.9f, 0.6f), 6);
                Destroy(tb.t.gameObject);
                tablets.RemoveAt(i);
            }
        }
    }

    bool UpdateBuilders(float dt)
    {
        for (int i = builders.Count - 1; i >= 0; i--)
        {
            var b = builders[i];
            if (b.plat.broken) b.dead = true;
            if (b.dead) { Kill(i, false); continue; }
            // anda de um lado para o outro na laje
            b.x += b.dir * 1.6f * dt;
            float lim = b.plat.w / 2f - 0.4f;
            if (b.x > b.plat.x + lim) { b.x = b.plat.x + lim; b.dir = -1f; }
            if (b.x < b.plat.x - lim) { b.x = b.plat.x - lim; b.dir = 1f; }
            b.t.position = W(new Vector2(b.x, b.plat.y), -0.2f);
            b.t.localRotation = Quaternion.Euler(0f, b.dir > 0f ? 0f : 180f, 0f);

            // arremessa tijolos quando o profeta está abaixo
            float dy = b.plat.y - pos.y;
            if (dy > 1f && dy < 12f)
            {
                b.throwCd -= dt;
                if (b.throwCd <= 0f)
                {
                    b.throwCd = Random.Range(2.2f, 3.4f) / (1f + tier * 0.15f);
                    var from = new Vector2(b.x, b.plat.y + 1f);
                    float time = 1.1f;
                    SpawnBrick(from, new Vector2((pos.x - from.x) / time, 3f));
                }
            }

            // contato
            Vector2 c = new Vector2(b.x, b.plat.y + 0.6f);
            if (Mathf.Abs(pos.x - c.x) < 0.65f && Mathf.Abs(pos.y + 0.6f - c.y) < 1.1f)
            {
                if (vel.y < 0f && pos.y > b.plat.y + 0.5f)
                {
                    // pisou na cabeça
                    vel.y = JumpV * 1.1f;
                    squash = 1f;
                    Kill(i, true);
                }
                else if (g.player.invuln <= 0f)
                {
                    Hurt();
                    if (g.state != RunnerState.Playing || !Active) return true;
                }
            }
        }
        return false;
    }

    void Kill(int i, bool reward)
    {
        var b = builders[i];
        if (reward)
        {
            g.AddBonus(120, W(new Vector2(b.x, b.plat.y + 2f), -1f));
            g.Explode(W(new Vector2(b.x, b.plat.y + 0.6f), -0.2f), new Color(0.75f, 0.35f, 0.2f), 12);
            g.PlayBoom();
        }
        Destroy(b.t.gameObject);
        builders.RemoveAt(i);
    }

    void SpawnBrick(Vector2 at, Vector2 v)
    {
        var tr = new GameObject("Tijolo").transform;
        tr.SetParent(root, false);
        g.Prim(PrimitiveType.Cube, tr, Vector3.zero, new Vector3(0.9f, 0.45f, 0.55f), new Color(0.8f, 0.38f, 0.2f));
        g.Prim(PrimitiveType.Cube, tr, Vector3.zero, new Vector3(0.95f, 0.1f, 0.6f), new Color(1f, 0.5f, 0.15f), true);
        tr.position = W(at, -0.4f);
        bricks.Add(new Brick { t = tr, pos = at, vel = v, r = 0.45f });
    }

    bool UpdateBricks(float dt)
    {
        // tijolos caindo do alto da obra
        brickCd -= dt;
        if (brickCd <= 0f)
        {
            float rate = 1f + tier * 0.2f + g.ThreatSoft * 0.15f + (Style == 0 ? 0.15f : 0f);
            brickCd = Random.Range(1.8f, 2.8f) / rate;
            float x = Mathf.Clamp(pos.x + Random.Range(-3f, 3f), -halfW, halfW);
            SpawnBrick(new Vector2(x, camY + 12f), new Vector2(Wind * 0.5f, -4f));
        }

        for (int i = bricks.Count - 1; i >= 0; i--)
        {
            var b = bricks[i];
            b.vel.y -= 9f * dt;
            b.pos += b.vel * dt;
            b.t.position = W(b.pos, -0.4f);
            b.t.Rotate(200f * dt, 0f, 150f * dt);
            if (b.pos.y < camY - 14f)
            {
                Destroy(b.t.gameObject);
                bricks.RemoveAt(i);
                continue;
            }
            Vector2 d = b.pos - (pos + Vector2.up * 0.6f);
            if (g.player.invuln <= 0f && Mathf.Abs(d.x) < b.r + 0.3f && Mathf.Abs(d.y) < b.r + 0.6f)
            {
                g.Explode(W(b.pos, -0.4f), new Color(0.8f, 0.38f, 0.2f), 8);
                Destroy(b.t.gameObject);
                bricks.RemoveAt(i);
                Hurt();
                if (g.state != RunnerState.Playing || !Active) return true;
            }
        }
        return false;
    }

    /// Tiro automático: a arma do profeta derruba tijolos e construtores à frente.
    void UpdateShots(float dt, float mdt)
    {
        var st = g.stats;
        fireCd -= mdt;
        if (fireCd <= 0f)
        {
            Vector2 from = pos + Vector2.up * 0.9f;
            Vector2? target = null;
            float best = 11f * 11f;
            foreach (var b in bricks)
            {
                float d = (b.pos - from).sqrMagnitude;
                if (b.pos.y > from.y - 1f && d < best) { best = d; target = b.pos; }
            }
            foreach (var b in builders)
            {
                var bp = new Vector2(b.x, b.plat.y + 0.6f);
                float d = (bp - from).sqrMagnitude;
                if (bp.y > from.y - 1f && d < best) { best = d; target = bp; }
            }
            if (target.HasValue)
            {
                fireCd = Mathf.Max(0.12f, st.Cooldown * 1.4f);
                var dir = (target.Value - from).normalized;
                MeshRenderer goR;
                var go = RunnerGame.NewPrim(PrimitiveType.Sphere, out goR, false);
                goR.sharedMaterial = g.Glow(st.weapon.color);
                go.transform.SetParent(root, false);
                go.transform.localScale = Vector3.one * 0.3f;
                shots.Add(new Shot { t = go.transform, pos = from, vel = dir * 26f });
                g.PlayShoot();
            }
            else fireCd = 0.1f;
        }

        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var s = shots[i];
            s.pos += s.vel * dt;
            s.t.position = W(s.pos, -0.5f);
            bool remove = (s.pos - pos).sqrMagnitude > 16f * 16f;
            if (!remove)
                for (int k = bricks.Count - 1; k >= 0; k--)
                {
                    if ((bricks[k].pos - s.pos).sqrMagnitude < 0.6f * 0.6f)
                    {
                        g.Explode(W(bricks[k].pos, -0.4f), new Color(0.8f, 0.38f, 0.2f), 6);
                        g.AddBonus(25, W(bricks[k].pos + Vector2.up, -1f));
                        Destroy(bricks[k].t.gameObject);
                        bricks.RemoveAt(k);
                        remove = true;
                        break;
                    }
                }
            if (!remove)
                for (int k = builders.Count - 1; k >= 0; k--)
                {
                    var b = builders[k];
                    if ((new Vector2(b.x, b.plat.y + 0.6f) - s.pos).sqrMagnitude < 0.6f)
                    {
                        float dmg = st.Damage * (Random.value < st.critChance ? st.critMul : 1f);
                        b.hp -= dmg;
                        g.PlayClank();
                        if (b.hp <= 0f) Kill(k, true);
                        remove = true;
                        break;
                    }
                }
            if (remove)
            {
                Destroy(s.t.gameObject);
                shots.RemoveAt(i);
            }
        }
    }

    void Hurt()
    {
        if (g.player.invuln > 0f) return;
        hitShake = 1f;
        g.TryHurtPlayer();
        if (g.state != RunnerState.Playing) return;
        if (g.player.invuln < 1.5f) g.player.invuln = 1.5f;
    }

    void Won()
    {
        Vector3 at = idol.position + Vector3.up * 2f;
        g.BabelWon(at, Style);
    }

    // ================================================================== texto embaralhado (confusão de línguas)

    static readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

    /// Embaralha as letras de cada palavra (muda algumas vezes por segundo).
    public static string Garble(string s, float time)
    {
        var rng = new System.Random(Mathf.FloorToInt(time * 3f) * 7919 + s.Length);
        sb.Length = 0;
        var words = s.Split(' ');
        for (int w = 0; w < words.Length; w++)
        {
            var chars = words[w].ToCharArray();
            for (int i = chars.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                char tmp = chars[i]; chars[i] = chars[j]; chars[j] = tmp;
            }
            if (w > 0) sb.Append(' ');
            sb.Append(chars);
        }
        return sb.ToString();
    }
}
