using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mini-jogo "Davi e Golias" (1 Samuel 17): visão em primeira pessoa no Vale de Elá.
/// Mire, segure para girar a funda, solte na hora certa (zona verde) e acerte a testa do gigante.
/// Você tem cinco seixos (1Sm 17:40). Estilos que se alternam a cada vez:
///   0) O Desafio      — Golias avança devagar, em linha reta.
///   1) O Escudo       — ele ergue o escudo e protege a cabeça de tempos em tempos.
///   2) O Vento de Elá — ele anda de um lado para o outro e o vento desvia as pedras.
/// </summary>
public class RunnerGoliath : MonoBehaviour
{
    public bool Active { get; private set; }
    public int Style { get; private set; }
    public int Stones { get; private set; }
    public int StonesMax { get; private set; }
    public float Charge { get; private set; }
    public bool Charging { get; private set; }
    public float Wind { get; private set; }
    public bool ShieldUp { get; private set; }
    public bool ShieldWarning { get; private set; }
    public float Distance => goliathZ - origin.z;
    public Vector3 AimWorld { get; private set; }
    public bool Won { get; private set; }

    public const float SweetMin = 0.68f, SweetMax = 0.9f;

    public static readonly string[] StyleNames = { "O DESAFIO", "O ESCUDO", "O VENTO DE ELÁ" };
    public static int NextStyle => PlayerPrefs.GetInt("goliath_plays", 0) % 3;

    class Stone { public Transform t; public Vector3 start; public Vector3 offset; public float time, dur; }

    RunnerGame g;
    Transform root, goliath, shield, spear, sling;
    Vector3 origin;
    float goliathX, goliathZ, t, walkSpeed, shieldClock, windClock, fallT, loseT, tauntCd, stagger;
    Vector2 aim = new Vector2(0.5f, 0.55f);
    Vector2 lastMouse;
    float chargeClock, whirlNext;
    int tier;
    readonly List<Stone> stones = new List<Stone>();
    readonly List<Transform> legs = new List<Transform>();
    int throwsMade;

    static readonly Color Bronze = new Color(0.72f, 0.5f, 0.22f);
    static readonly Color Skin = new Color(0.72f, 0.52f, 0.36f);

    public static readonly string[] Taunts =
    {
        "\"Sou eu algum cão, para vires a mim com paus?\" (1Sm 17:43)",
        "\"Vem a mim, e darei a tua carne às aves do céu!\" (1Sm 17:44)",
        "\"Escolhei dentre vós um homem que venha a mim!\" (1Sm 17:8)",
    };
    public string Taunt { get; private set; }
    public float TauntTime { get; private set; }

    // ================================================================== início / fim

    public void Begin(RunnerGame game, int difficultyTier)
    {
        g = game;
        tier = difficultyTier;
        int plays = PlayerPrefs.GetInt("goliath_plays", 0);
        Style = plays % 3;
        PlayerPrefs.SetInt("goliath_plays", plays + 1);
        PlayerPrefs.Save();

        origin = new Vector3(0f, 0f, g.player.transform.position.z + 6f);
        root = new GameObject("ValeDeEla").transform;
        root.position = origin;

        // o "vale": chão de terra e pedras soltas
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.03f, 28f), new Vector3(16f, 0.1f, 60f), new Color(0.55f, 0.47f, 0.33f));
        for (int i = 0; i < 26; i++)
            g.Prim(PrimitiveType.Cube, root, new Vector3(Random.Range(-7f, 7f), 0.12f, Random.Range(3f, 55f)), Vector3.one * Random.Range(0.15f, 0.4f), new Color(0.6f, 0.58f, 0.52f));
        // o ribeiro onde Davi escolheu as pedras
        g.Prim(PrimitiveType.Cube, root, new Vector3(0f, 0.1f, 2.5f), new Vector3(16f, 0.04f, 0.8f), new Color(0.35f, 0.55f, 0.75f), true);

        BuildGoliath();
        BuildSling();

        StonesMax = 5 + g.stats.slingLevel + (g.prophet != null && g.prophet.id == "davi" ? 1 : 0);
        Stones = StonesMax;
        goliathZ = origin.z + 42f;
        goliathX = 0f;
        walkSpeed = 1.6f + Mathf.Min(tier, 4) * 0.25f + g.LevelThreat * 0.2f;
        t = 0f;
        fallT = 0f;
        loseT = 0f;
        stagger = 0f;
        tauntCd = 1.5f;
        Charge = 0f;
        Charging = false;
        Won = false;
        throwsMade = 0;
        aim = new Vector2(0.5f, 0.6f);
        var ms = Mouse.current;
        if (ms != null) lastMouse = ms.position.ReadValue();
        Active = true;
        UpdateCamera();
    }

    public void End()
    {
        if (!Active) return;
        Active = false;
        stones.Clear();
        legs.Clear();
        if (root != null) Destroy(root.gameObject);
        root = null;
    }

    public void UpdateCamera()
    {
        var cam = Camera.main;
        if (cam == null || !Active) return;
        cam.transform.position = origin + new Vector3(0f, 1.6f, 0f) + (stagger > 0f ? Random.insideUnitSphere * stagger * 0.15f : Vector3.zero);
        cam.transform.LookAt(origin + new Vector3(0f, 3.2f, 30f));
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 50f, 0.2f);
    }

    // ================================================================== visuais

    void BuildGoliath()
    {
        goliath = new GameObject("Golias").transform;
        goliath.SetParent(root, false);
        var leather = new Color(0.42f, 0.28f, 0.16f);
        for (int s = -1; s <= 1; s += 2)
        {
            var leg = new GameObject("Perna").transform;
            leg.SetParent(goliath, false);
            leg.localPosition = new Vector3(s * 0.4f, 2.2f, 0f);
            g.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -1.1f, 0f), new Vector3(0.5f, 2.2f, 0.55f), Skin * 0.9f);
            g.Prim(PrimitiveType.Cube, leg, new Vector3(0f, -1.35f, -0.05f), new Vector3(0.56f, 1.3f, 0.6f), Bronze);   // caneleiras de bronze (17:6)
            legs.Add(leg);
        }
        g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 2.45f, 0f), new Vector3(1.5f, 0.6f, 0.95f), leather);
        // couraça de escamas (17:5)
        g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 3.45f, 0f), new Vector3(1.7f, 1.6f, 1.0f), Bronze);
        for (int k = 0; k < 4; k++)
            g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 2.85f + k * 0.38f, -0.51f), new Vector3(1.72f, 0.06f, 0.04f), Bronze * 0.6f);
        // braços
        for (int s = -1; s <= 1; s += 2)
            g.Prim(PrimitiveType.Cube, goliath, new Vector3(s * 1.08f, 3.4f, 0f), new Vector3(0.42f, 1.6f, 0.45f), Skin);
        // cabeça, barba e capacete com a testa exposta
        g.Prim(PrimitiveType.Sphere, goliath, new Vector3(0f, 4.8f, 0f), Vector3.one * 0.95f, Skin);
        g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 4.45f, -0.3f), new Vector3(0.6f, 0.45f, 0.3f), new Color(0.15f, 0.1f, 0.07f));
        g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 5.2f, 0.05f), new Vector3(1.05f, 0.4f, 1.05f), Bronze);
        g.Prim(PrimitiveType.Cube, goliath, new Vector3(0f, 5.5f, 0.1f), new Vector3(0.15f, 0.35f, 1.0f), new Color(0.7f, 0.1f, 0.08f));
        for (int s = -1; s <= 1; s += 2)
        {
            g.Prim(PrimitiveType.Cube, goliath, new Vector3(s * 0.45f, 4.75f, -0.05f), new Vector3(0.12f, 0.6f, 0.8f), Bronze);
            g.Prim(PrimitiveType.Cube, goliath, new Vector3(s * 0.16f, 4.85f, -0.44f), new Vector3(0.12f, 0.07f, 0.05f), Color.black);
        }
        // a lança "como eixo de tecelão" (17:7)
        spear = new GameObject("Lanca").transform;
        spear.SetParent(goliath, false);
        spear.localPosition = new Vector3(1.15f, 3.6f, 0f);
        spear.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        g.Prim(PrimitiveType.Cube, spear, new Vector3(0f, 0f, 0f), new Vector3(0.18f, 0.18f, 5.5f), new Color(0.4f, 0.28f, 0.15f));
        g.Prim(PrimitiveType.Cube, spear, new Vector3(0f, 0f, 3f), new Vector3(0.28f, 0.1f, 0.8f), new Color(0.55f, 0.55f, 0.6f));
        // escudo redondo de bronze
        shield = new GameObject("Escudo").transform;
        shield.SetParent(goliath, false);
        var disc = g.Prim(PrimitiveType.Cylinder, shield, Vector3.zero, new Vector3(1.4f, 0.06f, 1.4f), Bronze);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.Prim(PrimitiveType.Sphere, shield, new Vector3(0f, 0f, -0.06f), Vector3.one * 0.3f, Bronze * 1.2f);
        shield.localPosition = ShieldDown;
    }

    static readonly Vector3 ShieldDown = new Vector3(-1.3f, 3.1f, -0.35f);
    static readonly Vector3 ShieldRaised = new Vector3(0f, 4.85f, -0.75f);

    void BuildSling()
    {
        sling = new GameObject("Funda").transform;
        sling.SetParent(root, false);
        g.Prim(PrimitiveType.Cube, sling, new Vector3(0f, 0f, 0.35f), new Vector3(0.03f, 0.03f, 0.7f), new Color(0.45f, 0.3f, 0.15f));
        g.Prim(PrimitiveType.Sphere, sling, new Vector3(0f, 0f, 0.72f), Vector3.one * 0.13f, new Color(0.65f, 0.63f, 0.58f));
    }

    // ================================================================== loop

    public void Tick(float dt)
    {
        if (!Active) return;
        var cam = Camera.main;
        float rdt = Time.unscaledDeltaTime;
        t += dt;
        if (stagger > 0f) stagger -= dt * 2f;
        if (g.player.invuln > 0f) g.player.invuln -= dt;
        if (TauntTime > 0f) TauntTime -= dt;

        // ---------------- queda do gigante (vitória)
        if (Won)
        {
            fallT += dt;
            float a = Mathf.Clamp01(fallT / 1.1f);
            goliath.localRotation = Quaternion.Euler(-90f * a * a, 0f, 0f);
            if (fallT > 1.2f && fallT - dt <= 1.2f) { g.BossShake(1f); g.Explode(goliath.position + new Vector3(0f, 0.3f, -3f), new Color(0.6f, 0.5f, 0.35f), 30); g.PlayBoom(); }
            if (fallT > 2.2f) g.GoliathWon(goliath.position + Vector3.up * 2f, throwsMade);
            return;
        }

        // ---------------- entrada: mira
        RunnerTouch.Update();
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var ms = Mouse.current;
        Vector2 axis = Vector2.zero;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis.x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) axis.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis.y -= 1f;
        }
        if (gp != null) axis += gp.leftStick.ReadValue() + gp.dpad.ReadValue();
        aim += Vector2.ClampMagnitude(axis, 1f) * 0.45f * rdt;
        if (ms != null && !RunnerTouch.UseTouchUI)
        {
            Vector2 mp = ms.position.ReadValue();
            if ((mp - lastMouse).sqrMagnitude > 1f)
                aim = new Vector2(mp.x / Mathf.Max(1, Screen.width), mp.y / Mathf.Max(1, Screen.height));
            lastMouse = mp;
        }
        if (RunnerTouch.Holding)
            aim += new Vector2(RunnerTouch.DragDelta.x / Mathf.Max(1, Screen.width), RunnerTouch.DragDelta.y / Mathf.Max(1, Screen.height)) * 0.8f;
        aim.x = Mathf.Clamp(aim.x, 0.05f, 0.95f);
        aim.y = Mathf.Clamp(aim.y, 0.08f, 0.95f);

        // balanço leve da mão (no estilo 3, o vento balança mais)
        float swayAmp = Style == 2 ? 0.012f : 0.006f;
        Vector2 sway = new Vector2(Mathf.Sin(t * 1.7f), Mathf.Sin(t * 2.3f + 1f)) * swayAmp * (Charging ? 1.6f : 1f);
        Vector2 finalAim = aim + sway;

        // ---------------- funda: segurar para girar, soltar para arremessar
        bool hold = (ms != null && ms.leftButton.isPressed && !RunnerTouch.UseTouchUI)
                 || (kb != null && (kb.spaceKey.isPressed || kb.jKey.isPressed))
                 || (gp != null && (gp.buttonSouth.isPressed || gp.rightTrigger.isPressed))
                 || RunnerTouch.Holding;
        if (Stones <= 0) hold = false;
        if (hold)
        {
            if (!Charging) { Charging = true; chargeClock = 0f; whirlNext = 0f; }
            chargeClock += rdt;
            if (chargeClock >= whirlNext) { whirlNext += 0.375f; g.Sfx("giro", 0.35f, 0.05f, 0.1f); }
            Charge = Mathf.PingPong(chargeClock / 0.75f, 1f);
        }
        else if (Charging)
        {
            Charging = false;
            if (Charge > 0.08f && cam != null) Throw(cam, finalAim);
            Charge = 0f;
        }

        if (cam != null)
        {
            var ray = cam.ViewportPointToRay(new Vector3(finalAim.x, finalAim.y, 0f));
            AimWorld = ray.origin + ray.direction * 20f;
            // funda girando ao lado da câmera
            sling.position = cam.transform.position + cam.transform.right * 0.45f - cam.transform.up * 0.35f + cam.transform.forward * 0.8f;
            sling.rotation = cam.transform.rotation * Quaternion.Euler(0f, Charging ? chargeClock * 1100f : 0f, 0f);
        }

        UpdateGoliath(dt);
        UpdateStones(dt);

        // sem pedras e nenhuma no ar: Golias venceu este duelo
        if (Stones <= 0 && stones.Count == 0 && !Won)
        {
            loseT += dt;
            if (loseT > 1.2f)
            {
                g.TryHurtPlayer();
                if (g.state != RunnerState.Playing || !Active) return;
                g.GoliathFailed();
            }
        }
    }

    void UpdateGoliath(float dt)
    {
        // ---------------- escudo (estilo 2)
        if (Style == 1)
        {
            shieldClock += dt;
            float c = Mathf.Repeat(shieldClock, 4.4f);
            ShieldWarning = c >= 2.4f && c < 2.8f;
            ShieldUp = c >= 2.8f;
        }
        else ShieldUp = ShieldWarning = false;
        Vector3 target = ShieldUp ? ShieldRaised : (ShieldWarning ? Vector3.Lerp(ShieldDown, ShieldRaised, 0.35f) : ShieldDown);
        shield.localPosition = Vector3.Lerp(shield.localPosition, target, 1f - Mathf.Exp(-10f * dt));

        // ---------------- vento (estilo 3)
        if (Style == 2)
        {
            windClock -= dt;
            if (windClock <= 0f) { windClock = Random.Range(2.5f, 4f); Wind = Random.Range(-1f, 1f) * (2f + tier * 0.3f); }
            goliathX = Mathf.Sin(t * 0.7f) * 3f;
        }
        else Wind = 0f;

        // ---------------- avança
        goliathZ -= walkSpeed * dt;
        goliath.position = new Vector3(origin.x + goliathX, origin.y, goliathZ);
        float step = Mathf.Sin(t * walkSpeed * 2.2f);
        for (int i = 0; i < legs.Count; i++) legs[i].localRotation = Quaternion.Euler((i == 0 ? step : -step) * 18f, 0f, 0f);
        goliath.localRotation = Quaternion.Euler(0f, 0f, step * 2.5f);

        tauntCd -= dt;
        if (tauntCd <= 0f)
        {
            tauntCd = Random.Range(6f, 9f);
            Taunt = Taunts[Random.Range(0, Taunts.Length)];
            TauntTime = 3f;
        }

        // chegou em Davi: golpe de lança, e ele recua para provocar de novo
        if (Distance < 5f)
        {
            g.BossShake(0.8f);
            g.Sfx("pisao", 0.9f);
            stagger = 1f;
            g.TryHurtPlayer();
            if (g.state != RunnerState.Playing || !Active) return;
            goliathZ = origin.z + 24f;
            Taunt = "\"Ha! Mais um passo e acabo contigo!\"";
            TauntTime = 2.5f;
        }
    }

    void Throw(Camera cam, Vector2 finalAim)
    {
        Stones--;
        throwsMade++;
        var ray = cam.ViewportPointToRay(new Vector3(finalAim.x, finalAim.y, 0f));
        float dz = goliathZ - ray.origin.z;
        float k = ray.direction.z > 0.01f ? dz / ray.direction.z : 40f;
        Vector3 hit = ray.origin + ray.direction * k;

        // fora da zona verde a pedra sai torta
        float err = 0f;
        if (Charge < SweetMin) err = (SweetMin - Charge) * 2.6f;
        else if (Charge > SweetMax) err = (Charge - SweetMax) * 6f;
        Vector2 dev = Random.insideUnitCircle * err;
        hit += new Vector3(dev.x, dev.y, 0f);
        hit.x += Wind * Mathf.Clamp01(dz / 40f) * 0.9f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = g.Mat(new Color(0.7f, 0.68f, 0.62f));
        go.transform.SetParent(root, false);
        go.transform.localScale = Vector3.one * 0.16f;
        var start = cam.transform.position + cam.transform.right * 0.4f - cam.transform.up * 0.2f + cam.transform.forward * 0.7f;
        stones.Add(new Stone { t = go.transform, start = start, offset = hit - goliath.position, dur = Mathf.Max(0.25f, dz / 42f) });
        g.Sfx("funda", 0.8f);
        if (Charge >= SweetMin && Charge <= SweetMax) g.ShowFloat(cam.transform.position + cam.transform.forward * 4f + Vector3.up * 0.6f, "PERFEITO!", new Color(0.5f, 1f, 0.5f), false);
    }

    void UpdateStones(float dt)
    {
        for (int i = stones.Count - 1; i >= 0; i--)
        {
            var s = stones[i];
            s.time += dt;
            float a = Mathf.Clamp01(s.time / s.dur);
            Vector3 end = goliath.position + s.offset;
            Vector3 p = Vector3.Lerp(s.start, end, a);
            p.y += Mathf.Sin(a * Mathf.PI) * 0.8f;
            s.t.position = p;
            if (a < 1f) continue;

            Resolve(s.offset, end);
            Destroy(s.t.gameObject);
            stones.RemoveAt(i);
            if (Won) return;
        }
    }

    void Resolve(Vector3 off, Vector3 at)
    {
        var head = new Vector3(0f, 4.8f, 0f);
        float dx = off.x - head.x, dy = off.y - head.y;
        bool headHit = dx * dx + dy * dy < 0.5f * 0.5f;
        if (headHit && ShieldUp)
        {
            g.PlayClank();
            g.Explode(at, new Color(1f, 0.8f, 0.4f), 8);
            g.ShowFloat(at + Vector3.up, "ESCUDO!", new Color(0.8f, 0.6f, 0.3f), true);
            return;
        }
        if (headHit)
        {
            Won = true;
            fallT = 0f;
            g.Explode(at, new Color(0.9f, 0.2f, 0.15f), 20);
            g.ShowFloat(at + Vector3.up * 1.2f, "NA TESTA!", new Color(1f, 0.9f, 0.3f), true);
            g.Sfx("pedra", 1f, 0f, 0.1f);
            g.Sfx("vitoria", 0.9f, 0f, 1f);
            g.BossShake(0.6f);
            return;
        }
        if (Mathf.Abs(off.x) < 1.1f && off.y > 0f && off.y < 4.3f)
        {
            // bateu na armadura: ele só cambaleia para trás
            g.PlayClank();
            g.Sfx("espada_hit", 0.6f, 0.1f, 0.05f, 0.7f);
            g.Explode(at, new Color(1f, 0.75f, 0.35f), 6);
            g.AddBonus(30, at + Vector3.up);
            goliathZ += 3f;
            g.ShowFloat(at + Vector3.up * 0.4f, "CLANG!", new Color(0.85f, 0.65f, 0.35f), false);
            return;
        }
        g.Explode(goliath.position + new Vector3(off.x, 0.1f, off.z), new Color(0.6f, 0.5f, 0.35f), 4);
    }
}
