using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mini-jogo "bullet hell": a câmera olha de cima, você pilota a Nave de Luz
/// e precisa desviar da chuva de tiros e expulsar o Espírito Maligno.
/// A arena fica bem acima da cidade (não interfere no resto do jogo).
/// Coordenadas da arena: Vector2(x, z) — "para cima" na tela = +z.
/// </summary>
public class RunnerBulletHell : MonoBehaviour
{
    public float arenaHeight = 20f;       // a arena flutua acima dos telhados da cidade (mesmo mundo 3D)
    public float timeLimit = 60f;
    public float scrollSpeed = 12f;       // a cidade passa por baixo enquanto você luta
    public float arenaHalfW = 11f;
    public float arenaHalfH = 8f;

    public bool Active { get; private set; }
    public float SpiritHp { get; private set; }
    public float SpiritMaxHp { get; private set; }
    public float TimeLeft { get; private set; }
    public int Phase => SpiritHp > SpiritMaxHp * 0.66f ? 1 : (SpiritHp > SpiritMaxHp * 0.33f ? 2 : 3);
    public bool Focus { get; private set; }

    class Bullet { public Transform t; public Vector2 pos, vel; public float r; public float dmg; }
    class Minion { public Transform t; public Vector2 pos; public float hp, time, fire, side; }

    RunnerGame g;
    Transform root;
    Vector3 origin;
    Transform ship, shipCore, shipRing;
    Transform spirit, ringA, ringB, tendrils;
    Vector2 shipPos, spiritPos;
    readonly List<Bullet> enemyBullets = new List<Bullet>();
    readonly List<Bullet> playerBullets = new List<Bullet>();
    readonly List<Minion> minions = new List<Minion>();
    float t, fireCd, ringCd, aimCd, spiralCd, spiralAngle, minionCd, rainCd, hitFlash;
    int tier;
    float halfW, halfH;
    int lastPhase;
    int maxBullets;


    static readonly Color Magenta = new Color(1f, 0.2f, 0.8f);
    static readonly Color Crimson = new Color(1f, 0.15f, 0.15f);
    static readonly Color Violet = new Color(0.6f, 0.3f, 1f);
    static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
    // todos os tiros inimigos são "fogo": fáceis de reconhecer
    static readonly Color FireOrange = new Color(1f, 0.5f, 0.08f);
    static readonly Color FireRed = new Color(1f, 0.2f, 0.06f);
    static readonly Color FireYellow = new Color(1f, 0.8f, 0.2f);

    Vector3 W(Vector2 p, float y = 0f) => origin + new Vector3(p.x, y, p.y);

    // ================================================================== início / fim

    public void Begin(RunnerGame game, int difficultyTier)
    {
        g = game;
        tier = difficultyTier;
        var pz = g.player.transform.position.z;
        origin = new Vector3(0f, arenaHeight, pz);
        root = new GameObject("BulletHellArena").transform;
        root.position = origin;

        halfH = arenaHalfH;
        halfW = arenaHalfW;
        maxBullets = Application.isMobilePlatform ? 160 : 220;

        // "nuvem escura" do espírito sob a arena: dá contraste para enxergar o fogo,
        // e a cidade continua visível em volta (mesmo mundo 3D)
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.6f, 0f), new Vector3(halfW * 2f + 1f, 0.2f, halfH * 2f + 1f), new Color(0.11f, 0.05f, 0.14f));
        for (int sx = -1; sx <= 1; sx += 2)
        {
            g.Prim(PrimitiveType.Cube, root, new Vector3(sx * (halfW + 0.5f), -0.45f, 0f), new Vector3(0.15f, 0.1f, halfH * 2f + 1f), Violet, true);
            g.Prim(PrimitiveType.Cube, root, new Vector3(0f, -0.45f, sx * (halfH + 0.5f)), new Vector3(halfW * 2f + 1f, 0.1f, 0.15f), Violet, true);
        }

        BuildShip();
        BuildSpirit();

        shipPos = new Vector2(0f, -halfH + 2.5f);
        spiritPos = new Vector2(0f, halfH - 3.2f);

        SpiritMaxHp = Mathf.Max(35f, g.stats.EstimatedDps * 0.7f * (22f + 5f * tier));
        SpiritHp = SpiritMaxHp;
        TimeLeft = timeLimit;
        t = 0f;
        fireCd = 0.5f;
        ringCd = 1.5f;
        aimCd = 2.2f;
        spiralCd = 0f;
        minionCd = 4f;
        rainCd = 0f;
        lastPhase = 1;

        Active = true;
        UpdateCamera();
    }

    public void End()
    {
        if (!Active) return;
        Active = false;
        enemyBullets.Clear();
        playerBullets.Clear();
        minions.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    public void UpdateCamera()
    {
        var cam = Camera.main;
        if (cam == null || !Active) return;
        Vector3 shake = hitFlash > 0f ? Random.insideUnitSphere * hitFlash * 0.4f : Vector3.zero;
        // câmera em perspectiva, inclinada atrás da arena: dá para ver a cidade passando lá embaixo
        cam.transform.position = origin + new Vector3(0f, 17f, -12.5f) + shake;
        cam.transform.LookAt(origin + new Vector3(0f, 0f, 0.8f));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 58f, 0.2f);
    }

    // ================================================================== visuais

    void BuildShip()
    {
        ship = new GameObject("NaveDeLuz").transform;
        ship.SetParent(root, false);
        var d = g.Prim(PrimitiveType.Cube, ship, Vector3.zero, new Vector3(0.6f, 0.3f, 0.6f), Gold, true);
        d.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        g.Prim(PrimitiveType.Cube, ship, new Vector3(0f, 0f, 0.45f), new Vector3(0.18f, 0.25f, 0.6f), Gold, true);
        for (int s = -1; s <= 1; s += 2)
        {
            var wing = g.Prim(PrimitiveType.Cube, ship, new Vector3(s * 0.65f, -0.05f, -0.15f), new Vector3(0.9f, 0.08f, 0.35f), new Color(0.95f, 0.94f, 0.9f));
            wing.transform.localRotation = Quaternion.Euler(0f, s * 15f, 0f);
            g.Prim(PrimitiveType.Cube, ship, new Vector3(s * 1.1f, 0f, -0.25f), new Vector3(0.18f, 0.1f, 0.4f), new Color(0.3f, 0.7f, 1f), true);
        }
        shipRing = new GameObject("Ring").transform;
        shipRing.SetParent(ship, false);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.PI * 2f / 8f;
            g.Prim(PrimitiveType.Cube, shipRing, new Vector3(Mathf.Cos(a) * 0.85f, 0f, Mathf.Sin(a) * 0.85f), Vector3.one * 0.1f, Gold, true);
        }
        shipCore = g.Prim(PrimitiveType.Sphere, ship, new Vector3(0f, 0.35f, 0f), Vector3.one * 0.26f, Color.white, true).transform;
    }

    void BuildSpirit()
    {
        spirit = new GameObject("EspiritoMaligno").transform;
        spirit.SetParent(root, false);
        var dark = new Color(0.07f, 0.02f, 0.09f);
        g.Prim(PrimitiveType.Sphere, spirit, Vector3.zero, Vector3.one * 3.2f, dark);
        g.Prim(PrimitiveType.Sphere, spirit, new Vector3(0f, 0.3f, 0f), new Vector3(2.2f, 2.4f, 2.2f), new Color(0.2f, 0.04f, 0.2f));
        // olhos vermelhos (visíveis de cima)
        Vector3[] eyes = { new Vector3(-0.55f, 1.55f, -0.2f), new Vector3(0.55f, 1.55f, -0.2f), new Vector3(0f, 1.6f, 0.45f), new Vector3(-0.9f, 1.2f, 0.6f), new Vector3(0.9f, 1.2f, 0.6f) };
        foreach (var e in eyes)
        {
            g.Prim(PrimitiveType.Sphere, spirit, e, Vector3.one * 0.42f, Crimson, true);
            g.Prim(PrimitiveType.Sphere, spirit, e + new Vector3(0f, 0.18f, -0.04f), Vector3.one * 0.16f, Color.black);
        }
        // chifres
        for (int s = -1; s <= 1; s += 2)
        {
            var h = g.Prim(PrimitiveType.Cube, spirit, new Vector3(s * 0.9f, 1.2f, 1.2f), new Vector3(0.2f, 0.2f, 1.2f), new Color(0.35f, 0.3f, 0.1f));
            h.transform.localRotation = Quaternion.Euler(0f, s * 25f, 0f);
        }
        ringA = new GameObject("RingA").transform;
        ringA.SetParent(spirit, false);
        for (int k = 0; k < 10; k++)
        {
            float a = k * Mathf.PI * 2f / 10f;
            g.Prim(PrimitiveType.Cube, ringA, new Vector3(Mathf.Cos(a) * 2.3f, 0.6f, Mathf.Sin(a) * 2.3f), new Vector3(0.35f, 0.2f, 0.35f), Violet, true);
        }
        ringB = new GameObject("RingB").transform;
        ringB.SetParent(spirit, false);
        for (int k = 0; k < 14; k++)
        {
            float a = k * Mathf.PI * 2f / 14f;
            g.Prim(PrimitiveType.Cube, ringB, new Vector3(Mathf.Cos(a) * 3.0f, 0.2f, Mathf.Sin(a) * 3.0f), new Vector3(0.25f, 0.15f, 0.5f), new Color(0.5f, 0.05f, 0.1f), true);
        }
        tendrils = new GameObject("Tendrils").transform;
        tendrils.SetParent(spirit, false);
        for (int k = 0; k < 6; k++)
        {
            var arm = new GameObject("Arm").transform;
            arm.SetParent(tendrils, false);
            arm.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
            g.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -0.3f, 2.6f), new Vector3(0.45f, 0.15f, 2.4f), new Color(0.15f, 0.04f, 0.18f));
            g.Prim(PrimitiveType.Cube, arm, new Vector3(0f, -0.25f, 3.9f), new Vector3(0.3f, 0.12f, 0.5f), Magenta, true);
        }
    }

    Transform MakeBulletVisual(Color c, float diameter, bool elongated)
    {
        var go = GameObject.CreatePrimitive(elongated ? PrimitiveType.Cube : PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = elongated ? g.Mat(c) : g.Glow(c);   // só o fogo inimigo brilha
        go.transform.SetParent(root, false);
        go.transform.localScale = elongated ? new Vector3(diameter * 0.35f, 0.12f, diameter * 1.6f) : Vector3.one * diameter;
        return go.transform;
    }

    // ================================================================== disparos

    void EnemyShot(Vector2 from, float angleDeg, float speed, float radius, Color c)
    {
        if (enemyBullets.Count >= maxBullets) return;
        float a = angleDeg * Mathf.Deg2Rad;
        var b = new Bullet { pos = from, vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed, r = radius };
        b.t = MakeBulletVisual(c, radius * 2f, false);
        b.t.position = W(b.pos, 0.5f);
        enemyBullets.Add(b);
    }

    float AngleTo(Vector2 from, Vector2 to)
    {
        var d = to - from;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    void Ring(int n, float speed, float offset, Color c)
    {
        for (int i = 0; i < n; i++) EnemyShot(spiritPos, offset + i * 360f / n, speed, 0.24f, c);
    }

    void AimedFan(Vector2 from, int n, float spread, float speed, Color c)
    {
        float baseA = AngleTo(from, shipPos);
        for (int i = 0; i < n; i++)
        {
            float a = n == 1 ? baseA : baseA - spread / 2f + spread * i / (n - 1);
            EnemyShot(from, a, speed, 0.28f, c);
        }
    }

    // ================================================================== loop

    public void Tick(float dt)
    {
        if (!Active) return;
        var st = g.stats;
        float mdt = g.BulletTimeActive ? Time.unscaledDeltaTime : dt;   // a nave ignora o tempo bala
        float diff = 1f + tier * 0.15f + g.Difficulty * 0.3f + g.LevelThreat * 0.12f;
        t += dt;

        // a arena avança junto com a cidade
        origin.z += scrollSpeed * dt;
        root.position = origin;
        var pp = g.player.transform.position;
        g.player.transform.position = new Vector3(pp.x, pp.y, origin.z);

        // ---------------- movimento da nave
        RunnerTouch.Update();
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        Vector2 axis = Vector2.zero;
        bool focus = false;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis.x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis.y -= 1f;
            focus |= kb.leftShiftKey.isPressed || kb.kKey.isPressed;
        }
        if (gp != null)
        {
            axis += gp.leftStick.ReadValue();
            axis += gp.dpad.ReadValue();
            focus |= gp.leftShoulder.isPressed || gp.buttonEast.isPressed;
        }
        axis = Vector2.ClampMagnitude(axis, 1f);
        Focus = focus;

        // Tempo Bala continua funcionando aqui (Q / botão direito / gatilho esquerdo / botão na tela)
        var ms = Mouse.current;
        if ((kb != null && kb.qKey.wasPressedThisFrame) || (ms != null && ms.rightButton.wasPressedThisFrame)
            || (gp != null && gp.leftTrigger.wasPressedThisFrame) || RunnerTouch.Pressed("ability"))
            g.TryBulletTime();
        shipPos += axis * (focus ? 4f : 9.5f) * mdt;

        // toque: a nave acompanha o arrasto do dedo (1:1, mais preciso)
        if (RunnerTouch.Holding)
        {
            float worldPerPixel = (halfH * 2f) / Mathf.Max(1, Screen.height);
            shipPos += RunnerTouch.DragDelta * worldPerPixel * 1.25f;
        }
        shipPos.x = Mathf.Clamp(shipPos.x, -halfW + 0.8f, halfW - 0.8f);
        shipPos.y = Mathf.Clamp(shipPos.y, -halfH + 0.8f, halfH - 1.2f);

        ship.position = W(shipPos, 1f);
        ship.rotation = Quaternion.Euler(0f, 0f, -axis.x * 20f);
        shipRing.Rotate(0f, 180f * mdt, 0f);
        shipCore.localScale = Vector3.one * (focus ? 0.4f : 0.26f);

        // invencibilidade (o RunnerPlayer fica pausado neste modo)
        if (g.player.invuln > 0f)
        {
            g.player.invuln -= dt;
            bool vis = g.player.invuln <= 0f || Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
            foreach (var r in ship.GetComponentsInChildren<Renderer>()) r.enabled = vis;
        }
        if (hitFlash > 0f) hitFlash -= dt * 2f;

        // ---------------- tiro automático
        fireCd -= mdt;
        if (fireCd <= 0f)
        {
            fireCd = Mathf.Max(0.05f, st.Cooldown * 0.9f);
            int n = Mathf.Max(1, st.Pellets);
            float spread = focus ? 2f : 5f;
            for (int i = 0; i < n; i++)
            {
                float a = 90f + (n == 1 ? 0f : -spread * (n - 1) / 2f + spread * i);
                float ar = a * Mathf.Deg2Rad;
                var b = new Bullet { pos = shipPos + new Vector2(0f, 0.6f), vel = new Vector2(Mathf.Cos(ar), Mathf.Sin(ar)) * 28f, r = 0.2f, dmg = st.Damage };
                b.t = MakeBulletVisual(new Color(0.85f, 0.85f, 0.8f), 0.3f, true);
                b.t.rotation = Quaternion.Euler(0f, 90f - a, 0f);
                b.t.position = W(b.pos, 0.8f);
                playerBullets.Add(b);
            }
            g.PlayShoot();
        }

        UpdatePlayerBullets(dt);
        UpdateSpirit(dt, diff);
        UpdateMinions(dt, diff);
        if (UpdateEnemyBullets(dt)) return;   // levou dano e morreu

        TimeLeft -= dt;
        if (SpiritHp <= 0f) { g.HellWon(W(spiritPos, 1f)); return; }
        if (TimeLeft <= 0f) { g.HellFailed(); return; }
    }

    void UpdatePlayerBullets(float dt)
    {
        var st = g.stats;
        for (int i = playerBullets.Count - 1; i >= 0; i--)
        {
            var b = playerBullets[i];
            b.pos += b.vel * dt;
            bool remove = b.pos.y > halfH + 1f || Mathf.Abs(b.pos.x) > halfW + 1f;

            if (!remove && (b.pos - spiritPos).sqrMagnitude < 1.7f * 1.7f)
            {
                float dmg = b.dmg;
                bool crit = Random.value < st.critChance;
                if (crit) dmg *= st.critMul;
                SpiritHp -= dmg;
                g.PlayClank();
                remove = true;
            }
            if (!remove)
            {
                for (int m = minions.Count - 1; m >= 0; m--)
                {
                    var mi = minions[m];
                    if ((b.pos - mi.pos).sqrMagnitude < 0.75f * 0.75f)
                    {
                        mi.hp -= b.dmg;
                        remove = true;
                        if (mi.hp <= 0f)
                        {
                            g.PlayBoom();
                            g.AddBonus(60, W(mi.pos, 2f));
                            Destroy(mi.t.gameObject);
                            minions.RemoveAt(m);
                        }
                        break;
                    }
                }
            }

            if (remove)
            {
                Destroy(b.t.gameObject);
                playerBullets.RemoveAt(i);
            }
            else b.t.position = W(b.pos, 0.8f);
        }
    }

    void UpdateSpirit(float dt, float diff)
    {
        // movimento em "oito"
        spiritPos = new Vector2(Mathf.Sin(t * 0.55f) * (halfW * 0.55f), halfH - 3.2f + Mathf.Sin(t * 1.1f) * 0.9f);
        spirit.position = W(spiritPos, 0f);
        ringA.Rotate(0f, 70f * dt, 0f);
        ringB.Rotate(0f, -45f * dt, 0f);
        tendrils.Rotate(0f, 20f * dt, 0f);
        float pulse = 1f + Mathf.Sin(t * 4f) * 0.06f;
        spirit.localScale = Vector3.one * pulse;

        int ph = Phase;
        if (ph != lastPhase)
        {
            lastPhase = ph;
            g.BossShake(0.5f);
            // limpa a tela na troca de fase (respiro para o jogador)
            ClearBullets(Vector2.zero, 999f);
            Ring(16, 3.2f, t * 30f, FireRed);
        }

        float rate = diff;
        switch (ph)
        {
            case 1:
                ringCd -= dt * rate;
                if (ringCd <= 0f) { ringCd = 1.6f; Ring(10 + tier * 2, 4.2f, t * 40f, FireOrange); }
                aimCd -= dt * rate;
                if (aimCd <= 0f) { aimCd = 2.0f; AimedFan(spiritPos, 3, 16f, 6f, FireRed); }
                break;

            case 2:
                spiralCd -= dt * rate;
                if (spiralCd <= 0f)
                {
                    spiralCd = 0.12f;
                    spiralAngle += 13f;
                    EnemyShot(spiritPos, spiralAngle, 4.4f, 0.24f, FireOrange);
                    EnemyShot(spiritPos, spiralAngle + 180f, 4.4f, 0.24f, FireOrange);
                }
                aimCd -= dt * rate;
                if (aimCd <= 0f) { aimCd = 2.6f; AimedFan(spiritPos, 5, 36f, 5.5f, FireRed); }
                break;

            default:
                spiralCd -= dt * rate;
                if (spiralCd <= 0f)
                {
                    spiralCd = 0.16f;
                    spiralAngle += 10f;
                    for (int k = 0; k < 2; k++)
                    {
                        EnemyShot(spiritPos, spiralAngle + k * 180f, 4.2f, 0.22f, FireOrange);
                        EnemyShot(spiritPos, -spiralAngle + k * 180f + 90f, 4.2f, 0.22f, FireYellow);
                    }
                }
                ringCd -= dt * rate;
                if (ringCd <= 0f) { ringCd = 2.6f; Ring(14, 3.6f, Random.Range(0f, 30f), FireRed); }
                rainCd -= dt * rate;
                if (rainCd <= 0f)
                {
                    rainCd = 0.55f;
                    EnemyShot(new Vector2(Random.Range(-halfW, halfW), halfH + 0.5f), -90f + Random.Range(-8f, 8f), 5f, 0.3f, FireRed);
                }
                break;
        }

        minionCd -= dt;
        if (minionCd <= 0f)
        {
            minionCd = ph >= 2 ? 5f : 7f;
            for (int s = -1; s <= 1; s += 2) SpawnMinion(s);
        }
    }

    void SpawnMinion(float side)
    {
        var tr = new GameObject("Sombra").transform;
        tr.SetParent(root, false);
        g.Prim(PrimitiveType.Sphere, tr, Vector3.zero, Vector3.one * 1.0f, new Color(0.1f, 0.03f, 0.12f));
        g.Prim(PrimitiveType.Sphere, tr, new Vector3(0f, 0.45f, 0f), Vector3.one * 0.35f, Crimson, true);
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, tr, new Vector3(s * 0.65f, 0f, 0.1f), new Vector3(0.7f, 0.05f, 0.35f), new Color(0.25f, 0.05f, 0.3f));
        var m = new Minion { t = tr, side = side, hp = Mathf.Max(2f, g.stats.Damage * 4f), fire = Random.Range(0.6f, 1.2f) };
        m.pos = new Vector2(side * (halfW + 1f), halfH - 1f);
        minions.Add(m);
    }

    void UpdateMinions(float dt, float diff)
    {
        for (int i = minions.Count - 1; i >= 0; i--)
        {
            var m = minions[i];
            m.time += dt;
            // entra pelo canto, faz uma curva e sai pelo outro lado
            m.pos += new Vector2(-m.side * 4.5f, -2.2f + Mathf.Cos(m.time * 1.5f) * 2f) * dt;
            m.t.position = W(m.pos, 0.6f);
            m.t.Rotate(0f, 200f * dt, 0f);
            m.fire -= dt * diff;
            if (m.fire <= 0f)
            {
                m.fire = 1.8f;
                AimedFan(m.pos, 1, 0f, 5.5f, FireOrange);
            }
            if (m.pos.y < -halfH - 2f || Mathf.Abs(m.pos.x) > halfW + 3f)
            {
                Destroy(m.t.gameObject);
                minions.RemoveAt(i);
            }
        }
    }

    /// Retorna true se o jogador morreu (o jogo acabou).
    bool UpdateEnemyBullets(float dt)
    {
        const float shipRadius = 0.17f;   // hitbox pequena, no estilo bullet hell
        bool hit = false;
        for (int i = enemyBullets.Count - 1; i >= 0; i--)
        {
            var b = enemyBullets[i];
            b.pos += b.vel * dt;
            if (b.pos.y < -halfH - 2f || b.pos.y > halfH + 3f || Mathf.Abs(b.pos.x) > halfW + 2f)
            {
                Destroy(b.t.gameObject);
                enemyBullets.RemoveAt(i);
                continue;
            }
            b.t.position = W(b.pos, 0.5f);
            float rr = b.r + shipRadius;
            if (!hit && g.player.invuln <= 0f && (b.pos - shipPos).sqrMagnitude < rr * rr) hit = true;
        }
        // encostar nos lacaios também machuca
        if (!hit && g.player.invuln <= 0f)
            foreach (var m in minions)
                if ((m.pos - shipPos).sqrMagnitude < 0.7f * 0.7f) { hit = true; break; }

        if (hit)
        {
            hitFlash = 1f;
            ClearBullets(shipPos, 4f);     // limpa em volta para não tomar dano em sequência
            g.TryHurtPlayer();
            if (g.state != RunnerState.Playing) return true;
            if (g.player.invuln < 1.5f) g.player.invuln = 1.5f;
        }
        return false;
    }

    void ClearBullets(Vector2 center, float radius)
    {
        for (int i = enemyBullets.Count - 1; i >= 0; i--)
        {
            var b = enemyBullets[i];
            if ((b.pos - center).sqrMagnitude <= radius * radius)
            {
                Destroy(b.t.gameObject);
                enemyBullets.RemoveAt(i);
            }
        }
    }
}
