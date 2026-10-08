using System.Collections.Generic;
using UnityEngine;

/// Habilidades que um chefe pode ter. Cada chefe sorteia uma "build" própria,
/// espelhando as cartas que o jogador consegue pegar.
public enum BossAbility { TiroMultiplo, Rajada, Teleguiado, Foguetes, Muros, Laser, Investida, Drones, Escudo, Esquiva, Vampirismo,
    ChuvaDeFogo, Enxame, Terremoto, Serpentes, Trevas, Legiao }

/// Chefe que corre de costas à frente do jogador, troca de faixa, pula, desvia e ataca.
public class RunnerBoss : MonoBehaviour
{
    public static readonly string[] Names =
    {
        "Nabucodonosor", "Bel de Babilônia", "Marduque", "Tamuz", "Gogue de Magogue",
        "Faraó do Egito", "Rei de Tiro", "Baal", "Moloque", "Querubim Caído"
    };

    public static string AbilityName(BossAbility a)
    {
        switch (a)
        {
            case BossAbility.TiroMultiplo: return "Tiro Múltiplo";
            case BossAbility.Rajada: return "Rajada";
            case BossAbility.Teleguiado: return "Teleguiado";
            case BossAbility.Foguetes: return "Foguetes";
            case BossAbility.Muros: return "Construtor de Muros";
            case BossAbility.Laser: return "Laser";
            case BossAbility.Investida: return "Investida";
            case BossAbility.Drones: return "Drones";
            case BossAbility.Escudo: return "Escudo";
            case BossAbility.Esquiva: return "Esquiva";
            case BossAbility.ChuvaDeFogo: return ChuvaName();
            case BossAbility.Enxame: return "Enxame de Gafanhotos";
            case BossAbility.Terremoto: return "Terremoto";
            case BossAbility.Serpentes: return "Serpentes";
            case BossAbility.Trevas: return "Trevas";
            case BossAbility.Legiao: return "Legião";
            default: return "Vampirismo";
        }
    }

    // cooldown base de cada habilidade (segundos)
    static float BaseCooldown(BossAbility a)
    {
        switch (a)
        {
            case BossAbility.Rajada: return 5f;
            case BossAbility.Teleguiado: return 4.5f;
            case BossAbility.Foguetes: return 6.5f;
            case BossAbility.Muros: return 7f;
            case BossAbility.Laser: return 8f;
            case BossAbility.Investida: return 9.5f;
            case BossAbility.ChuvaDeFogo: return 7.5f;
            case BossAbility.Enxame: return 8.5f;
            case BossAbility.Terremoto: return 6.5f;
            case BossAbility.Serpentes: return 9f;
            case BossAbility.Trevas: return 15f;
            case BossAbility.Legiao: return 11f;
            default: return 999f;
        }
    }

    [HideInInspector] public string bossName;
    [HideInInspector] public int tier;
    public readonly List<BossAbility> abilities = new List<BossAbility>();
    [HideInInspector] public RunnerObstacle body;
    [HideInInspector] public bool enraged;
    [HideInInspector] public float shield, shieldMax;
    [HideInInspector] public Transform shieldVisual;
    [HideInInspector] public RunnerWeaponModel gun;

    public bool Has(BossAbility a) => abilities.Contains(a);

    // ---------------- chefe final
    public const string HebrewName = "השטן";   // ha-Satan, "o Acusador"
    [HideInInspector] public bool isFinal;
    [HideInInspector] public bool trueForm;      // 3ª fase (abaixo de 25%)
    /// O texto em hebraico é escrito da direita para a esquerda; a UI desenha da esquerda para a direita, então invertemos.
    public static string Rtl(string s)
    {
        var arr = s.ToCharArray();
        System.Array.Reverse(arr);
        return new string(arr);
    }

    float FinalMul => isFinal ? (trueForm ? 0.55f : 0.75f) : 1f;
    float CdMul => FinalMul * (enraged ? 0.65f : 1f) * (1f - Mathf.Min(0.45f, tier * 0.06f + (RunnerGame.I != null ? RunnerGame.I.LevelThreat * 0.06f : 0f)));

    // com arma corpo a corpo o chefe fica mais perto (a onda da espada alcança ~20 m)
    float TargetDist => (RunnerGame.I != null && RunnerGame.I.stats.Melee) ? 17f : 28f;
    int lane = 1;
    float x;
    float feetY;
    float vy;
    float distAhead = 95f;
    float laneTimer = 2f;
    float dodgeCd;
    float fireCd = 2f;
    float busy;
    float shieldRecharge;
    readonly Dictionary<BossAbility, float> timers = new Dictionary<BossAbility, float>();

    int burstLeft;
    float burstTimer;

    int laserState;            // 0 nada, 1 aviso, 2 disparando
    float laserTimer;
    float laserX;
    GameObject laserGO;

    int chargeState;           // 0 nada, 1 preparando, 2 avançando, 3 voltando
    float chargeTimer;

    readonly List<RunnerObstacle> drones = new List<RunnerObstacle>();
    readonly List<float> droneFire = new List<float>();
    float droneAngle;
    float droneRespawn;

    bool Arrived => distAhead < TargetDist + 6f;

    /// Sorteia a build do chefe.
    public void Setup(int bossTier, bool final = false)
    {
        isFinal = final;
        tier = final ? Mathf.Max(bossTier, 8) : bossTier;
        var names = RunnerGame.I != null && RunnerGame.I.Theme != null ? RunnerGame.I.Theme.bossNames : Names;
        bossName = final ? "Satanás" : names[Random.Range(0, names.Length)];
        int count = final ? 99 : Mathf.Min(2 + tier, 7);   // o chefe final tem TODAS as habilidades
        var pool = new List<BossAbility>((BossAbility[])System.Enum.GetValues(typeof(BossAbility)));
        // poder-assinatura: pelo nome do chefe ou pela região
        foreach (var sig in Signature(bossName, RunnerGame.I != null && RunnerGame.I.Theme != null ? RunnerGame.I.Theme.id : 0))
            if (abilities.Count < count && pool.Remove(sig)) abilities.Add(sig);
        for (int i = abilities.Count; i < count && pool.Count > 0; i++)
        {
            int k = Random.Range(0, pool.Count);
            abilities.Add(pool[k]);
            pool.RemoveAt(k);
        }
        foreach (var a in abilities) timers[a] = Random.Range(1.5f, BaseCooldown(a) * 0.8f);

        if (Has(BossAbility.Escudo))
        {
            shieldMax = body.maxHp * 0.12f;
            shield = shieldMax;
        }
        droneRespawn = 1.5f;
        x = 0f;
    }

    /// Poderes característicos de cada chefe (pelo nome) ou, se não houver, da região.
    static List<BossAbility> Signature(string name, int biome)
    {
        var l = new List<BossAbility>();
        if (name.Contains("Faraó")) { l.Add(BossAbility.Serpentes); l.Add(BossAbility.Trevas); }
        else if (name.Contains("Janes") || name.Contains("Jambres") || name.Contains("Apep") || name.Contains("Leviatã")) l.Add(BossAbility.Serpentes);
        else if (name.Contains("Rá") || name.Contains("Nabucodonosor") || name.Contains("Nero") || name.Contains("Moloque")) l.Add(BossAbility.ChuvaDeFogo);
        else if (name.Contains("Abadom") || name.Contains("Apoliom") || name.Contains("Belzebu")) l.Add(BossAbility.Enxame);
        else if (name.Contains("Legião") || name.Contains("César")) l.Add(BossAbility.Legiao);
        else if (name.Contains("Beemote") || name.Contains("Gogue")) l.Add(BossAbility.Terremoto);
        else if (name.Contains("Anúbis") || name.Contains("Rei da Morte")) l.Add(BossAbility.Trevas);
        if (l.Count == 0)
        {
            switch (biome)
            {
                case 3: l.Add(Random.value < 0.5f ? BossAbility.Enxame : BossAbility.Serpentes); break;   // pragas do Egito
                case 1: l.Add(Random.value < 0.5f ? BossAbility.Legiao : BossAbility.Terremoto); break;   // legiões de Roma
                case 2: l.Add(Random.value < 0.5f ? BossAbility.ChuvaDeFogo : BossAbility.Terremoto); break; // enxofre do Sheol
                default: l.Add(Random.value < 0.5f ? BossAbility.ChuvaDeFogo : BossAbility.Muros); break;   // fornalha da Babilônia
            }
        }
        return l;
    }

    /// Nome da chuva conforme a região.
    static string ChuvaName()
    {
        int b = RunnerGame.I != null && RunnerGame.I.Theme != null ? RunnerGame.I.Theme.id : 0;
        return b == 3 ? "Granizo de Fogo" : (b == 2 ? "Enxofre" : (b == 1 ? "Chuva de Pilos" : "Fornalha Ardente"));
    }

    static Color ChuvaColor()
    {
        int b = RunnerGame.I != null && RunnerGame.I.Theme != null ? RunnerGame.I.Theme.id : 0;
        return b == 3 ? new Color(0.6f, 0.9f, 1f) : (b == 2 ? new Color(1f, 0.85f, 0.15f) : (b == 1 ? new Color(0.8f, 0.8f, 0.85f) : new Color(1f, 0.45f, 0.1f)));
    }

    static float LaneX(int l) => (l - 1) * RunnerGame.LaneWidth;

    static int LaneOfX(float px) => Mathf.Clamp(Mathf.RoundToInt(px / RunnerGame.LaneWidth) + 1, 0, 2);

    // ------------------------------------------------------------------ dano

    /// Escudo absorve dano antes da vida.
    public float Absorb(float dmg)
    {
        if (shield <= 0f) return dmg;
        float a = Mathf.Min(shield, dmg);
        shield -= a;
        dmg -= a;
        if (shield <= 0f)
        {
            shieldRecharge = 8f * CdMul;
            RunnerGame.I.BossMessage(transform.position + Vector3.up * 2.5f, "ESCUDO QUEBRADO!", new Color(0.4f, 0.9f, 1f));
        }
        return dmg;
    }

    public void OnPlayerHurt()
    {
        if (!Has(BossAbility.Vampirismo) || body == null) return;
        float heal = body.maxHp * 0.05f;
        body.hp = Mathf.Min(body.maxHp, body.hp + heal);
        RunnerGame.I.BossMessage(transform.position + Vector3.up * 2.5f, "+" + Mathf.RoundToInt(heal) + " VIDA", new Color(1f, 0.3f, 0.4f));
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var g = RunnerGame.I;
        if (g == null || g.state != RunnerState.Playing || body == null || body.dead) return;
        float dt = Time.deltaTime;
        var pp = g.player.transform.position;

        if (!enraged && body.hp < body.maxHp * 0.5f)
        {
            enraged = true;
            g.BossEnraged(this);
            GainPower(g);
        }
        if (isFinal && !trueForm && body.hp < body.maxHp * 0.25f)
        {
            trueForm = true;
            g.FinalBossTrueForm(this);
        }

        // distância até o jogador
        UpdateCharge(dt);
        if (chargeState == 0)
            distAhead = Mathf.Lerp(distAhead, TargetDist, 1f - Mathf.Exp(-1.5f * dt));

        // troca de faixa
        if (chargeState == 0 && laserState == 0)
        {
            laneTimer -= dt;
            if (laneTimer <= 0f)
            {
                lane = OtherLane(lane);
                laneTimer = Random.Range(1.2f, 2.6f) * CdMul;
            }
        }

        // esquiva dos tiros do jogador
        dodgeCd -= dt;
        if (Has(BossAbility.Esquiva) && dodgeCd <= 0f && chargeState == 0 && Arrived) TryDodge();

        // física do pulo
        vy -= 28f * dt;
        feetY += vy * dt;
        if (feetY <= 0f) { feetY = 0f; vy = 0f; }

        x = Mathf.Lerp(x, LaneX(lane), 1f - Mathf.Exp(-9f * dt));
        transform.position = new Vector3(x, feetY + 1.8f, pp.z + distAhead);

        UpdateShield(dt);
        UpdateDrones(g, dt, pp);
        UpdateLaser(g, dt, pp);
        UpdateRain(g, dt, pp);
        UpdateQuake(g);

        if (!Arrived) return;

        // ataques
        busy -= dt;
        foreach (var a in abilities)
            if (timers.ContainsKey(a)) timers[a] -= dt;

        fireCd -= dt;
        if (fireCd <= 0f && chargeState == 0)
        {
            fireCd = 1.4f * CdMul;
            BasicShot(g, pp);
        }

        if (burstLeft > 0)
        {
            burstTimer -= dt;
            if (burstTimer <= 0f)
            {
                burstLeft--;
                burstTimer = 0.11f;
                ShootAt(g, pp, pp.x, 16f);
            }
        }

        if (Ready(BossAbility.Rajada)) { burstLeft = 6 + tier; burstTimer = 0f; busy = 1f; }
        if (Ready(BossAbility.Teleguiado)) FireHoming(g);
        if (Ready(BossAbility.Foguetes)) FireRocket(g, pp);
        if (Ready(BossAbility.Muros)) DropWalls(g);
        if (Ready(BossAbility.Laser)) StartLaser(g, pp);
        if (Ready(BossAbility.Investida)) StartCharge(g);
        if (Ready(BossAbility.ChuvaDeFogo)) StartRain(g);
        if (Ready(BossAbility.Enxame)) Swarm(g, pp);
        if (Ready(BossAbility.Terremoto)) StartQuake(g);
        if (Ready(BossAbility.Serpentes)) Snakes(g);
        if (Ready(BossAbility.Trevas)) Darkness(g);
        if (Ready(BossAbility.Legiao)) Legion(g);
    }

    // ------------------------------------------------------------------ novos poderes

    /// Na fúria, o chefe revela um poder que ainda não tinha.
    void GainPower(RunnerGame g)
    {
        var pool = new List<BossAbility>();
        foreach (BossAbility a in System.Enum.GetValues(typeof(BossAbility)))
            if (!Has(a) && a != BossAbility.Escudo && a != BossAbility.Vampirismo) pool.Add(a);
        if (pool.Count == 0) return;
        var p = pool[Random.Range(0, pool.Count)];
        abilities.Add(p);
        timers[p] = 2f;
        g.BossMessage(transform.position + Vector3.up * 3.5f, "NOVO PODER: " + AbilityName(p).ToUpper(), new Color(1f, 0.4f, 0.2f));
    }

    // ---- chuva (fogo / granizo / enxofre / pilos): marca 2 faixas, uma fica livre
    class Strike { public float x, timer; public GameObject marker; }
    readonly List<Strike> strikes = new List<Strike>();
    int rainWaves;
    float rainWaveTimer;

    void StartRain(RunnerGame g)
    {
        rainWaves = enraged ? 3 : 2;
        rainWaveTimer = 0f;
        busy = 1.1f * rainWaves + 0.5f;
        g.BossMessage(transform.position + Vector3.up * 3f, ChuvaName().ToUpper() + "!", ChuvaColor());
    }

    void UpdateRain(RunnerGame g, float dt, Vector3 pp)
    {
        if (rainWaves > 0)
        {
            rainWaveTimer -= dt;
            if (rainWaveTimer <= 0f)
            {
                rainWaves--;
                rainWaveTimer = 1.1f;
                int safe = Random.Range(0, 3);
                for (int l = 0; l < 3; l++)
                {
                    if (l == safe) continue;
                    var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Destroy(m.GetComponent<Collider>());
                    m.GetComponent<Renderer>().sharedMaterial = g.Glow(ChuvaColor());
                    strikes.Add(new Strike { x = LaneX(l), timer = 1.05f, marker = m });
                }
            }
        }
        for (int i = strikes.Count - 1; i >= 0; i--)
        {
            var s = strikes[i];
            s.timer -= dt;
            float pulse = 1.6f + Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.8f;
            s.marker.transform.position = new Vector3(s.x, 0.06f, pp.z + 3f);
            s.marker.transform.localScale = new Vector3(pulse, 0.04f, pulse);
            if (s.timer > 0f) continue;
            // o golpe cai do céu
            var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(col.GetComponent<Collider>());
            col.GetComponent<Renderer>().sharedMaterial = g.Glow(ChuvaColor());
            col.transform.position = new Vector3(s.x, 7f, pp.z + 3f);
            col.transform.localScale = new Vector3(1.1f, 14f, 1.1f);
            var d = col.AddComponent<RunnerDebris>();
            d.gravity = false; d.spin = false; d.life = 0.25f;
            g.Explode(new Vector3(s.x, 0.5f, pp.z + 3f), ChuvaColor(), 10);
            if (Mathf.Abs(pp.x - s.x) < 1.4f) g.TryHurtPlayer();
            Destroy(s.marker);
            strikes.RemoveAt(i);
        }
        if (strikes.Count > 0 && Random.value < 0.15f) g.BossShake(0.15f);
    }

    // ---- enxame de gafanhotos (Ap 9:3): fileiras em zigue-zague, sempre com uma faixa livre
    void Swarm(RunnerGame g, Vector3 pp)
    {
        int rows = enraged ? 4 : 3;
        int safe = Random.Range(0, 3);
        for (int row = 0; row < rows; row++)
        {
            for (int l = 0; l < 3; l++)
            {
                if (l == safe) continue;
                for (int k = 0; k < 2; k++)
                {
                    var o = g.SpawnEnemyShot(new Vector3(LaneX(l) + (k - 0.5f) * 0.9f, 1.1f + k * 0.4f, transform.position.z - 3f - row * 7f), new Vector3(0f, 0f, -7f), 0f);
                    if (o != null) { o.mainColor = new Color(0.6f, 0.8f, 0.2f); o.hp = o.maxHp = 0.5f; }
                }
            }
            safe = Mathf.Clamp(safe + (Random.value < 0.5f ? -1 : 1), 0, 2);
        }
        busy = 1.2f;
        g.BossMessage(transform.position + Vector3.up * 3f, "GAFANHOTOS!", new Color(0.7f, 0.9f, 0.3f));
    }

    // ---- terremoto: pula e, ao cair, manda ondas de choque pelo chão (pule por cima)
    bool quakePending;
    int quakeWaves;

    void StartQuake(RunnerGame g)
    {
        quakePending = true;
        quakeWaves = enraged ? 2 : 1;
        vy = 13f;
        busy = 2f;
        g.BossMessage(transform.position + Vector3.up * 3f, "TERREMOTO! PULE!", new Color(0.9f, 0.7f, 0.4f));
    }

    void UpdateQuake(RunnerGame g)
    {
        if (!quakePending || vy > 0f || feetY > 0.01f) return;
        g.BossShake(0.7f);
        for (int k = 0; k < quakeWaves; k++) g.SpawnShockwave(transform.position.z - 2f - k * 9f);
        quakePending = false;
    }

    // ---- serpentes (Êx 7:12 / Nm 21:6): rastejam de um lado para o outro pela pista
    void Snakes(RunnerGame g)
    {
        int n = enraged ? 4 : 3;
        for (int i = 0; i < n; i++) g.SpawnSnake(transform.position.z - 4f - i * 8f, tier);
        busy = 1f;
        g.BossMessage(transform.position + Vector3.up * 3f, "SERPENTES!", new Color(0.4f, 0.9f, 0.3f));
    }

    // ---- trevas (Êx 10:21): a visão fica curta por alguns segundos
    void Darkness(RunnerGame g)
    {
        g.BossDarkness(4.5f + Mathf.Min(tier, 4) * 0.4f);
        g.BossMessage(transform.position + Vector3.up * 3f, "TREVAS!", new Color(0.6f, 0.5f, 0.9f));
    }

    // ---- legião (Mc 5:9): invoca servos na pista
    void Legion(RunnerGame g)
    {
        int n = enraged ? 5 : 3;
        int[] lanes = { 0, 1, 2 };
        for (int i = 0; i < n; i++) g.SpawnMinion(lanes[Random.Range(0, 3)], transform.position.z - 5f - i * 6f);
        busy = 1f;
        g.BossMessage(transform.position + Vector3.up * 3f, "\"MEU NOME É LEGIÃO\"", new Color(1f, 0.35f, 0.3f));
    }

    bool Ready(BossAbility a)
    {
        if (!Has(a) || busy > 0f || chargeState != 0 || laserState != 0) return false;
        if (timers[a] > 0f) return false;
        timers[a] = BaseCooldown(a) * CdMul * Random.Range(0.85f, 1.15f);
        return true;
    }

    int OtherLane(int current)
    {
        int n = Random.Range(0, 2);
        int l = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i == current) continue;
            if (n == 0) { l = i; break; }
            n--;
        }
        return l;
    }

    void TryDodge()
    {
        foreach (var b in RunnerBullet.All)
        {
            if (b == null) continue;
            Vector3 bp = b.transform.position;
            float dz = transform.position.z - bp.z;
            if (dz < 0f || dz > 11f) continue;
            if (Mathf.Abs(bp.x - x) > 1.4f) continue;

            dodgeCd = (enraged ? 0.6f : 0.95f);
            if (feetY <= 0.01f && Random.value < 0.45f) vy = 11f;
            else lane = OtherLane(lane);
            return;
        }
    }

    // ------------------------------------------------------------------ tiros

    Vector3 Muzzle => new Vector3(x, 1.3f, transform.position.z - 1.5f);

    void ShootAt(RunnerGame g, Vector3 pp, float targetX, float shotSpeed)
    {
        if (g.stats.Melee) shotSpeed *= 0.55f;   // mais perto = tiros mais lentos, para dar tempo de desviar
        Vector3 o = Muzzle;
        float closing = g.speed + shotSpeed;
        float time = Mathf.Max(0.2f, (o.z - pp.z) / closing);
        float vx = (targetX - o.x) / time;
        g.SpawnEnemyShot(o, new Vector3(vx, 0f, -shotSpeed), 0f);
        if (gun != null) gun.OnFire();
    }

    void BasicShot(RunnerGame g, Vector3 pp)
    {
        int pl = LaneOfX(pp.x);
        ShootAt(g, pp, LaneX(pl), 14f);
        if (Has(BossAbility.TiroMultiplo))
        {
            // atira em mais uma faixa, mas sempre deixa uma livre
            ShootAt(g, pp, LaneX(OtherLane(pl)), 14f);
        }
    }

    void FireHoming(RunnerGame g)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var o = g.SpawnEnemyShot(Muzzle + new Vector3(s * 0.8f, 0.4f, 0f), new Vector3(s * 4f, 0f, -9f), 1f + tier * 0.2f);
            o.mainColor = new Color(1f, 0.3f, 0.8f);
        }
    }

    void FireRocket(RunnerGame g, Vector3 pp)
    {
        int pl = LaneOfX(pp.x);
        Vector3 o = Muzzle;
        float closing = g.speed + 6f;
        float time = Mathf.Max(0.2f, (o.z - pp.z) / closing);
        float vx = (LaneX(pl) - o.x) / time;
        g.SpawnRocket(o, new Vector3(vx, 0f, -6f));
        if (gun != null) gun.OnFire();
    }

    void DropWalls(RunnerGame g)
    {
        int[] lanes = { 0, 1, 2 };
        for (int i = 2; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int t = lanes[i]; lanes[i] = lanes[j]; lanes[j] = t;
        }
        float z = transform.position.z - 3f;
        g.SpawnWall(lanes[0], z);
        if (tier >= 1 || enraged) g.SpawnBarrierLane(lanes[1], z);
        g.BossMessage(transform.position + Vector3.up * 3f, "MUROS!", new Color(1f, 0.6f, 0.2f));
    }

    // ------------------------------------------------------------------ laser

    void StartLaser(RunnerGame g, Vector3 pp)
    {
        laserState = 1;
        laserTimer = 1.1f;
        laserX = LaneX(LaneOfX(pp.x));
        busy = 2f;
        if (laserGO == null)
        {
            laserGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(laserGO.GetComponent<Collider>());
            laserGO.name = "BossLaser";
        }
        laserGO.GetComponent<Renderer>().sharedMaterial = g.Glow(new Color(1f, 0.15f, 0.15f));
        laserGO.SetActive(true);
    }

    void UpdateLaser(RunnerGame g, float dt, Vector3 pp)
    {
        if (laserState == 0) return;
        laserTimer -= dt;
        float zStart = pp.z - 6f;
        float zEnd = transform.position.z - 1f;
        float len = Mathf.Max(1f, zEnd - zStart);
        var t = laserGO.transform;

        if (laserState == 1)
        {
            float pulse = 0.25f + Mathf.Abs(Mathf.Sin(Time.time * 18f)) * 0.35f;
            t.position = new Vector3(laserX, 0.04f, zStart + len / 2f);
            t.localScale = new Vector3(pulse, 0.05f, len);
            if (laserTimer <= 0f)
            {
                laserState = 2;
                laserTimer = 0.6f;
                g.BossShake(0.3f);
            }
        }
        else
        {
            t.position = new Vector3(laserX, 1.6f, zStart + len / 2f);
            t.localScale = new Vector3(1.8f, 3.2f, len);
            if (Mathf.Abs(pp.x - laserX) < 1.3f) g.TryHurtPlayer();
            if (laserTimer <= 0f)
            {
                laserState = 0;
                laserGO.SetActive(false);
            }
        }
    }

    // ------------------------------------------------------------------ investida

    void StartCharge(RunnerGame g)
    {
        chargeState = 1;
        chargeTimer = 0.85f;
        busy = 3f;
        body.Flash(0.85f);
        g.BossMessage(transform.position + Vector3.up * 3f, "!!", new Color(1f, 0.2f, 0.2f));
    }

    void UpdateCharge(float dt)
    {
        switch (chargeState)
        {
            case 1:
                chargeTimer -= dt;
                if (chargeTimer <= 0f) chargeState = 2;
                break;
            case 2:
                distAhead -= 48f * dt;
                if (distAhead <= -5f) chargeState = 3;
                break;
            case 3:
                distAhead += 20f * dt;
                if (distAhead >= TargetDist - 1f) chargeState = 0;
                break;
        }
    }

    // ------------------------------------------------------------------ escudo e drones

    void UpdateShield(float dt)
    {
        if (!Has(BossAbility.Escudo)) { if (shieldVisual != null) shieldVisual.gameObject.SetActive(false); return; }
        if (shield <= 0f)
        {
            shieldRecharge -= dt;
            if (shieldRecharge <= 0f) shield = shieldMax;
        }
        if (shieldVisual != null)
        {
            shieldVisual.gameObject.SetActive(shield > 0f);
            shieldVisual.Rotate(0f, 200f * dt, 0f);
        }
    }

    void UpdateDrones(RunnerGame g, float dt, Vector3 pp)
    {
        if (!Has(BossAbility.Drones)) return;

        for (int i = drones.Count - 1; i >= 0; i--)
        {
            if (drones[i] == null || drones[i].dead) { drones.RemoveAt(i); droneFire.RemoveAt(i); }
        }

        if (drones.Count == 0 && Arrived)
        {
            droneRespawn -= dt;
            if (droneRespawn <= 0f)
            {
                int n = tier >= 2 ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    drones.Add(g.SpawnBossDrone(transform.position));
                    droneFire.Add(Random.Range(1f, 2.5f));
                }
                droneRespawn = 12f * CdMul;
            }
        }

        droneAngle += dt * 1.8f;
        for (int i = 0; i < drones.Count; i++)
        {
            float a = droneAngle + i * Mathf.PI * 2f / drones.Count;
            drones[i].transform.position = transform.position + new Vector3(Mathf.Cos(a) * 3.2f, 1.2f + Mathf.Sin(a * 2f) * 0.4f, Mathf.Sin(a) * 1.5f - 1f);
            if (!Arrived || chargeState != 0) continue;
            droneFire[i] -= dt;
            if (droneFire[i] <= 0f)
            {
                droneFire[i] = 2.4f * CdMul;
                Vector3 o = drones[i].transform.position;
                o.y = 1.3f;
                float closing = g.speed + 12f;
                float time = Mathf.Max(0.2f, (o.z - pp.z) / closing);
                g.SpawnEnemyShot(o, new Vector3((pp.x - o.x) / time, 0f, -12f), 0f);
            }
        }
    }

    /// Chamado quando o chefe morre: remove drones e efeitos.
    public void CleanupAll(bool explode)
    {
        var g = RunnerGame.I;
        foreach (var d in drones)
        {
            if (d == null || d.dead) continue;
            if (explode && g != null) g.DestroyObstacle(d, false);
            else Destroy(d.gameObject);
        }
        drones.Clear();
        droneFire.Clear();
        if (laserGO != null) Destroy(laserGO);
        laserGO = null;
        foreach (var st in strikes) if (st.marker != null) Destroy(st.marker);
        strikes.Clear();
        rainWaves = 0;
        if (g != null) g.BossDarkness(0f);
    }

    void OnDestroy()
    {
        if (laserGO != null) Destroy(laserGO);
        foreach (var st in strikes) if (st.marker != null) Destroy(st.marker);
    }
}
