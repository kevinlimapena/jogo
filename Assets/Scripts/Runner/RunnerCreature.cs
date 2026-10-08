using UnityEngine;

/// <summary>
/// Criatura de quatro "patas" feita de cubos: corpo de cavalo, e no lugar das pernas
/// esferas com olhos cercadas por anéis girando (inspirada nos "ofanins", os anjos-roda da Bíblia).
/// Tem auréola, asinhas e um suporte nas costas para a arma.
/// Usada pelo jogador e (maior e sombria) pelos chefes.
/// </summary>
public class RunnerCreature : MonoBehaviour
{
    public class Palette
    {
        public Color body, accent, orb, eye, halo, pupil = new Color(0.05f, 0.05f, 0.08f);
        public bool boss;
    }

    public static Palette PlayerPalette => new Palette
    {
        body = new Color(0.95f, 0.94f, 0.9f),
        accent = new Color(0.2f, 0.5f, 1f),
        orb = new Color(1f, 0.82f, 0.3f),
        eye = new Color(0.3f, 1f, 1f),
        halo = new Color(1f, 0.85f, 0.35f)
    };

    public static Palette BossPalette => new Palette
    {
        body = new Color(0.13f, 0.1f, 0.13f),
        accent = new Color(0.85f, 0.1f, 0.15f),
        orb = new Color(0.95f, 0.25f, 0.1f),
        eye = new Color(1f, 0.15f, 0.1f),
        halo = new Color(1f, 0.3f, 0.15f),
        boss = true
    };

    public readonly Transform[] legs = new Transform[4];
    public readonly Transform[] rings = new Transform[4];
    public Transform body, tail, wingL, wingR, halo;
    public Transform weaponMount;

    readonly Vector3[] legBase = new Vector3[4];
    Vector3 bodyBase;
    float phase;
    float rollAngle;

    const float OrbRadius = 0.21f;
    static readonly float[] LegPhase = { 0f, Mathf.PI, Mathf.PI * 0.5f, Mathf.PI * 1.5f };

    /// Cria a criatura como filha de parent. groundY = altura do chão no espaço local do parent.
    public static RunnerCreature Build(Transform parent, float scale, float groundY, Palette p, bool facingBack)
    {
        var g = RunnerGame.I;
        var root = new GameObject("Creature").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, groundY + 0.9f * scale, 0f);
        root.localRotation = facingBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
        root.localScale = Vector3.one * scale;
        var c = root.gameObject.AddComponent<RunnerCreature>();

        // ---------------- pernas: esferas com olhos e anel de "roda"
        int idx = 0;
        for (int sz = 1; sz >= -1; sz -= 2)
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var leg = new GameObject("LegOrb").transform;
            leg.SetParent(root, false);
            leg.localPosition = new Vector3(sx * 0.32f, -0.9f + OrbRadius, sz * 0.45f);

            var orb = g.Prim(PrimitiveType.Sphere, leg, Vector3.zero, Vector3.one * OrbRadius * 2f, p.orb, true).transform;
            // olho virado para fora (e um extra na frente nos chefes)
            g.Prim(PrimitiveType.Sphere, orb, new Vector3(sx * 0.38f, 0.1f, 0.1f), Vector3.one * 0.42f, Color.white);
            g.Prim(PrimitiveType.Sphere, orb, new Vector3(sx * 0.55f, 0.12f, 0.14f), Vector3.one * 0.2f, p.pupil);
            if (p.boss)
            {
                g.Prim(PrimitiveType.Sphere, orb, new Vector3(0f, -0.1f, 0.4f), Vector3.one * 0.35f, Color.white);
                g.Prim(PrimitiveType.Sphere, orb, new Vector3(0f, -0.1f, 0.55f), Vector3.one * 0.17f, p.eye, true);
            }

            var ring = new GameObject("Ring").transform;
            ring.SetParent(leg, false);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 2f / 8f;
                g.Prim(PrimitiveType.Cube, ring, new Vector3(0f, Mathf.Cos(a) * 0.3f, Mathf.Sin(a) * 0.3f), Vector3.one * 0.07f, p.halo, true);
            }

            c.legs[idx] = orb;
            c.rings[idx] = ring;
            c.legBase[idx] = leg.localPosition;
            idx++;
        }

        // ---------------- corpo
        var b = new GameObject("Body").transform;
        b.SetParent(root, false);
        c.body = b;
        c.bodyBase = b.localPosition;

        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, -0.2f, 0f), new Vector3(0.62f, 0.42f, 1.15f), p.body);
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, -0.38f, 0f), new Vector3(0.64f, 0.08f, 1.0f), p.accent);          // faixa da barriga
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, -0.15f, 0.58f), new Vector3(0.5f, 0.3f, 0.08f), p.accent, true);  // peitoral
        var neck = g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.12f, 0.5f), new Vector3(0.28f, 0.5f, 0.28f), p.body);
        neck.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.38f, 0.78f), new Vector3(0.3f, 0.3f, 0.6f), p.body);             // cabeça
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.31f, 1.1f), new Vector3(0.24f, 0.2f, 0.16f), p.body * 0.8f);     // focinho
        g.Prim(PrimitiveType.Cube, b, new Vector3(-0.155f, 0.43f, 0.88f), new Vector3(0.04f, 0.08f, 0.1f), p.eye, true);
        g.Prim(PrimitiveType.Cube, b, new Vector3(0.155f, 0.43f, 0.88f), new Vector3(0.04f, 0.08f, 0.1f), p.eye, true);
        g.Prim(PrimitiveType.Cube, b, new Vector3(-0.1f, 0.58f, 0.62f), new Vector3(0.07f, 0.16f, 0.07f), p.body);       // orelhas
        g.Prim(PrimitiveType.Cube, b, new Vector3(0.1f, 0.58f, 0.62f), new Vector3(0.07f, 0.16f, 0.07f), p.body);
        // crina
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.47f, 0.58f), new Vector3(0.08f, 0.14f, 0.2f), p.accent, true);
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.32f, 0.42f), new Vector3(0.08f, 0.14f, 0.2f), p.accent, true);
        g.Prim(PrimitiveType.Cube, b, new Vector3(0f, 0.17f, 0.3f), new Vector3(0.08f, 0.14f, 0.2f), p.accent, true);

        if (p.boss)
        {
            // chifres e muitos olhos no corpo ("cheio de olhos")
            var hl = g.Prim(PrimitiveType.Cube, b, new Vector3(-0.11f, 0.62f, 0.85f), new Vector3(0.06f, 0.3f, 0.06f), p.halo, true);
            hl.transform.localRotation = Quaternion.Euler(-35f, 0f, 20f);
            var hr = g.Prim(PrimitiveType.Cube, b, new Vector3(0.11f, 0.62f, 0.85f), new Vector3(0.06f, 0.3f, 0.06f), p.halo, true);
            hr.transform.localRotation = Quaternion.Euler(-35f, 0f, -20f);
            for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 3; k++)
            {
                var ep = new Vector3(side * 0.32f, -0.15f + (k % 2) * 0.1f, -0.35f + k * 0.35f);
                g.Prim(PrimitiveType.Sphere, b, ep, Vector3.one * 0.14f, Color.white);
                g.Prim(PrimitiveType.Sphere, b, ep + new Vector3(side * 0.05f, 0f, 0f), Vector3.one * 0.07f, p.eye, true);
            }
        }

        // cauda
        var tail = new GameObject("Tail").transform;
        tail.SetParent(b, false);
        tail.localPosition = new Vector3(0f, -0.05f, -0.58f);
        var tc = g.Prim(PrimitiveType.Cube, tail, new Vector3(0f, -0.1f, -0.22f), new Vector3(0.1f, 0.1f, 0.5f), p.accent, true);
        tc.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
        c.tail = tail;

        // asas
        for (int side = -1; side <= 1; side += 2)
        {
            var w = new GameObject(side < 0 ? "WingL" : "WingR").transform;
            w.SetParent(b, false);
            w.localPosition = new Vector3(side * 0.3f, 0.0f, 0.1f);
            g.Prim(PrimitiveType.Cube, w, new Vector3(side * 0.42f, 0f, 0f), new Vector3(0.85f, 0.05f, 0.38f), p.body);
            g.Prim(PrimitiveType.Cube, w, new Vector3(side * 0.62f, 0.01f, -0.12f), new Vector3(0.5f, 0.05f, 0.25f), p.body * 0.9f);
            g.Prim(PrimitiveType.Cube, w, new Vector3(side * 0.82f, 0.02f, -0.05f), new Vector3(0.22f, 0.05f, 0.3f), p.accent, true);
            if (side < 0) c.wingL = w; else c.wingR = w;
        }

        // auréola
        var h = new GameObject("Halo").transform;
        h.SetParent(b, false);
        h.localPosition = new Vector3(0f, 0.75f, 0.72f);
        int hn = p.boss ? 12 : 10;
        for (int k = 0; k < hn; k++)
        {
            float a = k * Mathf.PI * 2f / hn;
            float r = p.boss ? 0.26f : 0.2f;
            g.Prim(PrimitiveType.Cube, h, new Vector3(Mathf.Cos(a) * r, (p.boss && k % 2 == 0) ? 0.05f : 0f, Mathf.Sin(a) * r),
                p.boss ? new Vector3(0.05f, 0.12f, 0.05f) : new Vector3(0.07f, 0.03f, 0.07f), p.halo, true);
        }
        c.halo = h;

        // suporte da arma nas costas
        var mount = new GameObject("WeaponMount").transform;
        mount.SetParent(b, false);
        mount.localPosition = new Vector3(0f, 0.02f, -0.05f);
        c.weaponMount = mount;

        return c;
    }

    void Update()
    {
        var g = RunnerGame.I;
        float spd = g != null ? g.speed : 10f;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        phase += dt * (4f + spd * 0.3f);

        // as esferas rolam como rodas (limitado para não "piscar")
        float omega = Mathf.Min(spd / OrbRadius, 28f) * Mathf.Rad2Deg;
        rollAngle += omega * dt;

        for (int i = 0; i < 4; i++)
        {
            if (legs[i] == null) continue;
            legs[i].localRotation = Quaternion.Euler(rollAngle, 0f, 0f);
            rings[i].localRotation = Quaternion.Euler(-rollAngle * 0.6f, phase * 20f, 0f);
            float hop = Mathf.Max(0f, Mathf.Sin(phase + LegPhase[i])) * 0.12f;
            legs[i].parent.localPosition = legBase[i] + new Vector3(0f, hop, 0f);
        }

        if (body != null)
        {
            body.localPosition = bodyBase + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * 0.06f, 0f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(phase) * 3f, 0f, 0f);
        }
        float flap = Mathf.Sin(phase * 0.8f) * 25f;
        if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, -15f - flap);
        if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, 15f + flap);
        if (tail != null) tail.localRotation = Quaternion.Euler(Mathf.Sin(phase * 1.3f) * 12f, Mathf.Sin(phase) * 18f, 0f);
        if (halo != null) halo.Rotate(0f, 90f * dt, 0f, Space.Self);
    }
}

/// <summary>
/// Modelo 3D da arma. Reconstrói sozinho quando a arma do jogador muda,
/// tem recuo, clarão no cano e canos giratórios na metralhadora.
/// </summary>
public class RunnerWeaponModel : MonoBehaviour
{
    public bool followPlayerWeapon = true;
    [HideInInspector] public string currentId = "";
    [HideInInspector] public Transform muzzle;

    Transform gun;
    Transform spinner;
    GameObject[] flashes = new GameObject[0];
    float recoil;
    float flashT;
    float spinSpeed;
    float swingT;                 // 1 → 0 durante o golpe corpo a corpo
    string meleeKind = "";        // "espada", "lanca", "martelo" ou vazio
    Quaternion restRot = Quaternion.identity;

    public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;

    void Update()
    {
        var g = RunnerGame.I;
        if (followPlayerWeapon && g != null && g.stats != null && g.stats.weapon != null && g.stats.weapon.id != currentId)
            Build(g.stats.weapon.id, g.stats.weapon.color);

        float dt = Time.deltaTime;
        recoil = Mathf.MoveTowards(recoil, 0f, dt * 8f);
        swingT = Mathf.MoveTowards(swingT, 0f, dt * 6.5f);
        if (gun != null)
        {
            if (meleeKind.Length == 0)
            {
                gun.localPosition = new Vector3(0f, 0f, -recoil * 0.12f);
            }
            else
            {
                float k = 1f - swingT;                      // 0 → 1 ao longo do golpe
                float e = Mathf.Sin(k * Mathf.PI);          // vai e volta
                switch (meleeKind)
                {
                    case "lanca":   // estocada para frente
                        gun.localPosition = new Vector3(0f, 0f, swingT > 0f ? e * 0.9f : 0f);
                        gun.localRotation = restRot;
                        break;
                    case "martelo": // pancada de cima para baixo
                        gun.localPosition = Vector3.zero;
                        gun.localRotation = restRot * Quaternion.Euler(swingT > 0f ? Mathf.Lerp(-80f, 40f, k) : 0f, 0f, 0f);
                        break;
                    default:        // espada: corte em arco da direita para a esquerda
                        gun.localPosition = new Vector3(0f, 0f, swingT > 0f ? e * 0.3f : 0f);
                        gun.localRotation = restRot * Quaternion.Euler(0f, swingT > 0f ? Mathf.Lerp(80f, -80f, k) : 0f, 0f);
                        break;
                }
            }
        }

        if (flashT > 0f)
        {
            flashT -= dt;
            if (flashT <= 0f) foreach (var f in flashes) if (f != null) f.SetActive(false);
        }

        spinSpeed = Mathf.MoveTowards(spinSpeed, 0f, dt * 1500f);
        if (spinner != null) spinner.Rotate(0f, 0f, spinSpeed * dt, Space.Self);
    }

    public void OnFire()
    {
        swingT = 1f;
        recoil = 1f;
        flashT = 0.05f;
        spinSpeed = 1400f;
        foreach (var f in flashes)
        {
            if (f == null) continue;
            f.SetActive(true);
            f.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        }
    }

    public void Build(string id, Color c)
    {
        var g = RunnerGame.I;
        currentId = id;
        if (gun != null) Destroy(gun.gameObject);
        spinner = null;
        meleeKind = (id == "espada" || id == "lanca" || id == "martelo") ? id : "";
        restRot = Quaternion.identity;

        gun = new GameObject("Gun").transform;
        gun.SetParent(transform, false);

        var dark = new Color(0.2f, 0.2f, 0.24f);
        var metal = new Color(0.45f, 0.47f, 0.52f);
        var wood = new Color(0.45f, 0.28f, 0.14f);
        var rotX = Quaternion.Euler(90f, 0f, 0f);
        var muzzleLocal = new Vector3(0f, 0.15f, 0.8f);
        var flashPoints = new System.Collections.Generic.List<Vector3>();

        switch (id)
        {
            case "escopeta":
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.08f, -0.12f), new Vector3(0.16f, 0.18f, 0.35f), wood);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.12f, 0.22f), new Vector3(0.2f, 0.2f, 0.4f), dark);
                for (int s = -1; s <= 1; s += 2)
                    g.Prim(PrimitiveType.Cylinder, gun, new Vector3(s * 0.05f, 0.17f, 0.72f), new Vector3(0.09f, 0.36f, 0.09f), metal).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.07f, 0.62f), new Vector3(0.18f, 0.12f, 0.26f), wood);
                for (int k = 0; k < 3; k++)
                    g.Prim(PrimitiveType.Cube, gun, new Vector3(0.11f, 0.12f, 0.1f + k * 0.09f), new Vector3(0.03f, 0.07f, 0.05f), c, true);
                muzzleLocal = new Vector3(0f, 0.17f, 1.1f);
                flashPoints.Add(muzzleLocal);
                break;

            case "metralhadora":
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.13f, 0.08f), new Vector3(0.3f, 0.26f, 0.5f), dark);
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0.24f, 0.1f, 0.05f), new Vector3(0.34f, 0.08f, 0.34f), c, true).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.27f, 0.08f), new Vector3(0.31f, 0.03f, 0.4f), c, true);
                spinner = new GameObject("Barrels").transform;
                spinner.SetParent(gun, false);
                spinner.localPosition = new Vector3(0f, 0.15f, 0.35f);
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI * 2f / 6f;
                    g.Prim(PrimitiveType.Cylinder, spinner, new Vector3(Mathf.Cos(a) * 0.08f, Mathf.Sin(a) * 0.08f, 0.38f), new Vector3(0.05f, 0.36f, 0.05f), metal).transform.localRotation = rotX;
                }
                g.Prim(PrimitiveType.Cylinder, spinner, new Vector3(0f, 0f, 0.55f), new Vector3(0.25f, 0.02f, 0.25f), dark).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Cylinder, spinner, new Vector3(0f, 0f, 0.15f), new Vector3(0.25f, 0.02f, 0.25f), dark).transform.localRotation = rotX;
                muzzleLocal = new Vector3(0f, 0.15f, 1.12f);
                flashPoints.Add(muzzleLocal);
                break;

            case "duplas":
                for (int s = -1; s <= 1; s += 2)
                {
                    var p = new Vector3(s * 0.38f, -0.02f, 0f);
                    BuildPistol(g, gun, p, c, dark, metal, rotX);
                    flashPoints.Add(p + new Vector3(0f, 0.14f, 0.78f));
                }
                muzzleLocal = new Vector3(0f, 0.12f, 0.78f);
                break;

            case "railgun":
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.1f, -0.02f), new Vector3(0.28f, 0.2f, 0.45f), dark);
                for (int s = -1; s <= 1; s += 2)
                    g.Prim(PrimitiveType.Cube, gun, new Vector3(s * 0.09f, 0.15f, 0.5f), new Vector3(0.06f, 0.09f, 1.3f), metal);
                for (int k = 0; k < 3; k++)
                    g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.15f, 0.3f + k * 0.3f), new Vector3(0.34f, 0.03f, 0.34f), c, true).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Sphere, gun, new Vector3(0f, 0.15f, -0.08f), Vector3.one * 0.2f, c, true);
                muzzleLocal = new Vector3(0f, 0.15f, 1.2f);
                flashPoints.Add(muzzleLocal);
                break;

            case "bazuca":
                var olive = new Color(0.32f, 0.4f, 0.22f);
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.2f, 0.25f), new Vector3(0.3f, 0.6f, 0.3f), olive).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.2f, 0.85f), new Vector3(0.36f, 0.04f, 0.36f), dark).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.2f, -0.36f), new Vector3(0.4f, 0.06f, 0.4f), dark).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Sphere, gun, new Vector3(0f, 0.2f, 0.88f), Vector3.one * 0.2f, c, true);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0.17f, 0.36f, 0.2f), new Vector3(0.07f, 0.1f, 0.25f), dark);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.37f, 0.25f), new Vector3(0.1f, 0.03f, 0.9f), c, true);
                muzzleLocal = new Vector3(0f, 0.2f, 0.98f);
                flashPoints.Add(muzzleLocal);
                break;

            case "espada":
            {
                // espada reluzente com guarda dourada
                var gold = new Color(1f, 0.78f, 0.28f);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, -0.15f), new Vector3(0.08f, 0.08f, 0.3f), wood);          // cabo
                g.Prim(PrimitiveType.Sphere, gun, new Vector3(0f, 0.2f, -0.32f), Vector3.one * 0.11f, gold);                     // pomo
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.02f), new Vector3(0.42f, 0.07f, 0.07f), gold);           // guarda
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.62f), new Vector3(0.12f, 0.035f, 1.15f), new Color(0.85f, 0.88f, 0.95f));
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.22f, 0.62f), new Vector3(0.03f, 0.03f, 1.1f), c, true);       // fio brilhante
                var tip = g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 1.22f), new Vector3(0.085f, 0.035f, 0.085f), new Color(0.85f, 0.88f, 0.95f));
                tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                muzzleLocal = new Vector3(0f, 0.2f, 1.25f);
                break;
            }

            case "lanca":
            {
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.18f, 0.35f), new Vector3(0.06f, 0.95f, 0.06f), wood).transform.localRotation = rotX;
                var tip = g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.18f, 1.38f), new Vector3(0.16f, 0.04f, 0.16f), new Color(0.8f, 0.55f, 0.25f));
                tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.18f, 1.3f), new Vector3(0.05f, 0.05f, 0.3f), c, true);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.18f, 1.1f), new Vector3(0.12f, 0.12f, 0.06f), new Color(1f, 0.78f, 0.28f));
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.08f, 1.05f), new Vector3(0.03f, 0.18f, 0.12f), new Color(0.75f, 0.1f, 0.1f)); // fita
                muzzleLocal = new Vector3(0f, 0.18f, 1.5f);
                break;
            }

            case "martelo":
            {
                g.Prim(PrimitiveType.Cylinder, gun, new Vector3(0f, 0.2f, 0.25f), new Vector3(0.08f, 0.5f, 0.08f), wood).transform.localRotation = rotX;
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.8f), new Vector3(0.6f, 0.36f, 0.36f), new Color(0.5f, 0.45f, 0.4f));
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.8f), new Vector3(0.62f, 0.38f, 0.08f), new Color(1f, 0.78f, 0.28f));
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.99f), new Vector3(0.3f, 0.2f, 0.03f), c, true);
                muzzleLocal = new Vector3(0f, 0.2f, 1.0f);
                break;
            }

            case "arco":
            {
                // arco longo curvado (segmentos) com corda e flecha
                for (int k = -3; k <= 3; k++)
                {
                    float a = k * 0.28f;
                    var seg = g.Prim(PrimitiveType.Cube, gun, new Vector3(Mathf.Sin(a) * 0.75f, 0.2f, 0.3f + Mathf.Cos(a) * 0.25f - 0.25f), new Vector3(0.24f, 0.05f, 0.06f), wood);
                    seg.transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                }
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.12f), new Vector3(1.3f, 0.015f, 0.015f), new Color(0.9f, 0.9f, 0.85f));
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.22f, 0.45f), new Vector3(0.03f, 0.03f, 0.9f), wood);
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.22f, 0.92f), new Vector3(0.07f, 0.03f, 0.1f), c, true);
                muzzleLocal = new Vector3(0f, 0.22f, 0.95f);
                flashPoints.Add(muzzleLocal);
                break;
            }

            case "funda_arma":
            {
                // funda de pastor: tiras de couro e a pedra
                var leather = new Color(0.45f, 0.3f, 0.15f);
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    var strap = g.Prim(PrimitiveType.Cube, gun, new Vector3(sx * 0.1f, 0.2f, 0.3f), new Vector3(0.03f, 0.03f, 0.6f), leather);
                    strap.transform.localRotation = Quaternion.Euler(0f, -sx * 10f, 0f);
                }
                g.Prim(PrimitiveType.Cube, gun, new Vector3(0f, 0.2f, 0.62f), new Vector3(0.22f, 0.05f, 0.14f), leather);
                g.Prim(PrimitiveType.Sphere, gun, new Vector3(0f, 0.25f, 0.62f), Vector3.one * 0.14f, new Color(0.6f, 0.58f, 0.55f));
                muzzleLocal = new Vector3(0f, 0.25f, 0.7f);
                flashPoints.Add(muzzleLocal);
                break;
            }

            default: // pistola
                BuildPistol(g, gun, Vector3.zero, c, dark, metal, rotX);
                muzzleLocal = new Vector3(0f, 0.14f, 0.78f);
                flashPoints.Add(muzzleLocal);
                break;
        }

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(gun, false);
        muzzle.localPosition = muzzleLocal;

        flashes = new GameObject[flashPoints.Count];
        for (int i = 0; i < flashPoints.Count; i++)
        {
            var f = g.Prim(PrimitiveType.Cube, gun, flashPoints[i], new Vector3(0.22f, 0.22f, 0.12f), new Color(1f, 0.85f, 0.4f), true);
            f.name = "Flash";
            f.SetActive(false);
            flashes[i] = f;
        }
    }

    static void BuildPistol(RunnerGame g, Transform parent, Vector3 o, Color c, Color dark, Color metal, Quaternion rotX)
    {
        g.Prim(PrimitiveType.Cube, parent, o + new Vector3(0f, 0.1f, 0.15f), new Vector3(0.16f, 0.2f, 0.55f), dark);
        g.Prim(PrimitiveType.Cylinder, parent, o + new Vector3(0f, 0.14f, 0.55f), new Vector3(0.08f, 0.2f, 0.08f), metal).transform.localRotation = rotX;
        g.Prim(PrimitiveType.Cube, parent, o + new Vector3(0f, 0.21f, 0.15f), new Vector3(0.17f, 0.04f, 0.42f), c, true);
        g.Prim(PrimitiveType.Cube, parent, o + new Vector3(0f, 0.25f, 0.36f), new Vector3(0.05f, 0.06f, 0.05f), metal);
        g.Prim(PrimitiveType.Cube, parent, o + new Vector3(0f, 0.12f, -0.12f), new Vector3(0.12f, 0.12f, 0.1f), c, true);
    }
}
