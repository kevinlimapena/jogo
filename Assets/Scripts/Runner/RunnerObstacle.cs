using UnityEngine;

public enum ObType { Wall, Barrier, Target, Mover, Turret, EnemyShot, Health, Tank, Boss, BossDrone, Ring, Mine, Flyer, PlaneBox, CarBox, Rival, Cone, Boost, ShipBox, AngelBox, HouseOpen, HouseBlood, BabelBox, JerichoBox, GoliathBox, Note, NoteBad, Shockwave,
    Shielded, CrackWall, Boulder, Colossus, Charger, FireJet, Spikes, Slinger, SlingStone, Hopper }

public class RunnerObstacle : MonoBehaviour
{
    public ObType type;
    public Vector3 half = Vector3.one * 0.5f;   // meia-extensão da caixa de colisão (centro = transform.position)
    public float hp = 1f;
    public float maxHp = 1f;
    public int points;
    public Color mainColor = Color.white;
    public bool elite;

    [HideInInspector] public bool dead;
    [HideInInspector] public float moveFreq = 2f;
    [HideInInspector] public float fireTimer = 1f;
    [HideInInspector] public Transform head;
    [HideInInspector] public Vector3 velocity = new Vector3(0f, 0f, -12f);   // tiros inimigos
    [HideInInspector] public float homingShot;
    [HideInInspector] public float baseX, baseY;
    [HideInInspector] public bool airborne;
    [HideInInspector] public bool flag;       // casa já julgada / já contada   // objeto de modo especial (voo/corrida), removido ao final
    [HideInInspector] public float rivalSpeed, rivalOffset, rivalStun, rivalTimer, bumpCd;
    float rivalVx;                                 // objeto da fase de voo                            // inimigos voadores                              // tiros teleguiados

    public bool Shootable => type == ObType.Target || type == ObType.Mover || type == ObType.Turret
                          || type == ObType.EnemyShot || type == ObType.Tank
                          || type == ObType.Boss || type == ObType.BossDrone
                          || type == ObType.Mine || type == ObType.Flyer
                          || type == ObType.Slinger || type == ObType.Hopper;

    /// Obstáculos que nada destrói: é preciso desviar ou acertar o tempo.
    public bool Indestructible => type == ObType.Boulder || type == ObType.Colossus || type == ObType.Charger
                               || type == ObType.FireJet || type == ObType.Spikes;

    [HideInInspector] public bool stompable;      // só morre quando o jogador pula em cima
    [HideInInspector] public bool hazardOn = true; // fornalha / espinhos: só machuca quando ativo
    [HideInInspector] public Transform fx;         // parte animada (chamas, espinhos, rodas, pedra)
    [HideInInspector] public GameObject marker;    // sombra de onde a pedra da funda vai cair
    [HideInInspector] public float timer;
    [HideInInspector] public int state;
    [HideInInspector] public float targetX;

    void OnDestroy()
    {
        if (marker != null) Destroy(marker);
    }

    float t;
    float phase;
    float flash;
    Renderer[] rends;
    MaterialPropertyBlock mpb;

    public void Init()
    {
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        phase = Random.Range(0f, Mathf.PI * 2f);
        maxHp = hp;
    }

    public bool Overlaps(Vector3 center, Vector3 h)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(center.x - c.x) < h.x + half.x
            && Mathf.Abs(center.y - c.y) < h.y + half.y
            && Mathf.Abs(center.z - c.z) < h.z + half.z;
    }

    public bool ContainsPoint(Vector3 p, float r)
    {
        Vector3 c = transform.position;
        return Mathf.Abs(p.x - c.x) < half.x + r
            && Mathf.Abs(p.y - c.y) < half.y + r
            && Mathf.Abs(p.z - c.z) < half.z + r;
    }

    void Update()
    {
        var g = RunnerGame.I;
        if (g == null || g.state != RunnerState.Playing) return;
        float dt = Time.deltaTime;
        t += dt;
        var p = transform.position;

        switch (type)
        {
            case ObType.Mover:
                p.x = Mathf.Sin(t * moveFreq + phase) * RunnerGame.LaneWidth;
                transform.position = p;
                break;

            case ObType.Turret:
            {
                var pp = g.player.transform.position;
                float dz = p.z - pp.z;
                if (head != null)
                {
                    var dir = pp - head.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.01f)
                        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(-dir), 1f - Mathf.Exp(-6f * dt));
                }
                if (dz > 14f && dz < 80f)
                {
                    fireTimer -= dt;
                    if (fireTimer <= 0f)
                    {
                        fireTimer = Mathf.Lerp(1.7f, 0.8f, g.Difficulty) / (1f + g.LevelThreat * 0.3f);
                        g.SpawnEnemyShot(p + new Vector3(0f, 0.4f, -1.0f));
                    }
                }
                break;
            }

            case ObType.EnemyShot:
            {
                if (homingShot > 0f)
                {
                    var pp = g.player.transform.position;
                    if (p.z - pp.z > 9f)
                    {
                        float desired = Mathf.Clamp((pp.x - p.x) * 2.5f, -9f, 9f);
                        velocity.x = Mathf.MoveTowards(velocity.x, desired, homingShot * 14f * dt);
                    }
                }
                p += velocity * dt;
                transform.position = p;
                transform.Rotate(0f, 0f, 360f * dt);
                break;
            }

            case ObType.Health:
                transform.Rotate(0f, 120f * dt, 0f);
                p.y = (baseY != 0f ? baseY : 1.2f) + Mathf.Sin(t * 4f) * 0.15f;
                transform.position = p;
                break;

            case ObType.Tank:
                // avança lentamente na direção do jogador
                p.z -= 3f * dt;
                transform.position = p;
                break;

            case ObType.CarBox:
            case ObType.ShipBox:
            case ObType.AngelBox:
            case ObType.BabelBox:
            case ObType.JerichoBox:
            case ObType.GoliathBox:
            case ObType.PlaneBox:
                transform.Rotate(0f, 90f * dt, 0f);
                p.y = 1.2f + Mathf.Sin(t * 3f) * 0.2f;
                transform.position = p;
                break;

            case ObType.Rival:
            {
                var pz = g.player.transform.position.z;
                float dz = p.z - pz;
                float spd = rivalSpeed;
                if (dz < -25f) spd += 10f;          // "elástico": quem fica pra trás acelera
                else if (dz > 70f) spd -= 6f;
                if (rivalStun > 0f) { rivalStun -= dt; spd *= 0.5f; }
                if (bumpCd > 0f) bumpCd -= dt;
                p.z += spd * dt;

                rivalTimer -= dt;
                if (rivalTimer <= 0f)
                {
                    rivalTimer = Random.Range(1f, 2.5f);
                    rivalOffset = Random.Range(-4f, 4f);
                }
                float tx = g.RoadX(p.z) + rivalOffset;
                rivalVx = Mathf.MoveTowards(rivalVx, Mathf.Clamp((tx - p.x) * 3f, -18f, 18f), 50f * dt);
                p.x += rivalVx * dt;
                transform.position = p;
                float yaw = rivalStun > 0f ? t * 900f : Mathf.Atan2(rivalVx, Mathf.Max(5f, spd)) * Mathf.Rad2Deg * 1.6f;
                transform.rotation = Quaternion.Euler(0f, yaw, -rivalVx * 0.5f);
                break;
            }

            case ObType.Boost:
                break;

            // ---------------------------------------------------------- novos obstáculos
            case ObType.Shielded:
                // escudeiro agachado avança devagar
                p.z -= 1.6f * dt;
                p.y = baseY + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.05f;
                transform.position = p;
                break;

            case ObType.Boulder:
                p.z += velocity.z * dt;
                transform.position = p;
                if (fx != null) fx.Rotate(-velocity.z * dt * 45f, 0f, 0f, Space.World);
                if (Random.value < 0.25f) g.Explode(p + new Vector3(0f, -1.1f, 0.8f), mainColor * 0.8f, 1);
                break;

            case ObType.Colossus:
            {
                // estátua gigante: anda, pisca e troca de faixa
                var pz = g.player.transform.position.z;
                p.z -= 2.2f * dt;
                timer -= dt;
                if (state == 0 && timer <= 0f && p.z - pz > 16f)
                {
                    int lane = Mathf.Clamp(Mathf.RoundToInt(p.x / RunnerGame.LaneWidth) + 1, 0, 2);
                    int nl = lane == 0 ? 1 : (lane == 2 ? 1 : (Random.value < 0.5f ? 0 : 2));
                    targetX = (nl - 1) * RunnerGame.LaneWidth;
                    state = 1; timer = 0.75f;
                    Flash(0.75f);
                }
                else if (state == 1 && timer <= 0f) state = 2;
                else if (state == 2)
                {
                    p.x = Mathf.MoveTowards(p.x, targetX, 6f * dt);
                    if (Mathf.Abs(p.x - targetX) < 0.01f) { state = 0; timer = Random.Range(1.8f, 3.2f); }
                }
                p.y = baseY + Mathf.Abs(Mathf.Sin(t * 3f)) * 0.12f;
                transform.position = p;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 3f);
                break;
            }

            case ObType.Charger:
            {
                // carro de guerra: espera, empina (aviso) e dispara contra o jogador
                var pz = g.player.transform.position.z;
                if (state == 0 && p.z - pz < 58f) { state = 1; timer = 0.9f; Flash(0.9f); }
                else if (state == 1) { timer -= dt; if (timer <= 0f) state = 2; }
                else if (state == 2) p.z -= 24f * dt;
                transform.position = p;
                if (fx != null && state == 2) fx.Rotate(0f, 0f, 0f);
                transform.rotation = Quaternion.Euler(state == 1 ? -8f + Mathf.Sin(t * 30f) * 3f : 0f, 0f, 0f);
                break;
            }

            case ObType.FireJet:
            case ObType.Spikes:
            {
                // ciclo: desligado → aviso → ativo
                timer -= dt;
                if (timer <= 0f)
                {
                    state = (state + 1) % 3;
                    timer = state == 0 ? 1.3f : (state == 1 ? 0.55f : 0.9f);
                }
                hazardOn = state == 2;
                if (fx != null)
                {
                    if (type == ObType.FireJet)
                    {
                        float h = state == 2 ? 1f : (state == 1 ? 0.12f + Mathf.Abs(Mathf.Sin(t * 30f)) * 0.08f : 0.02f);
                        fx.localScale = new Vector3(1f + (state == 2 ? Mathf.Sin(t * 25f) * 0.1f : 0f), h, 1f);
                    }
                    else
                    {
                        float y = state == 2 ? 0f : (state == 1 ? -0.3f + Mathf.Sin(t * 40f) * 0.04f : -0.55f);
                        fx.localPosition = new Vector3(0f, Mathf.MoveTowards(fx.localPosition.y, y, 6f * dt), 0f);
                    }
                }
                break;
            }

            case ObType.Slinger:
            {
                var pp = g.player.transform.position;
                float dz = p.z - pp.z;
                if (fx != null) fx.Rotate(0f, 900f * dt, 0f);   // funda girando
                if (dz > 18f && dz < 65f)
                {
                    fireTimer -= dt;
                    if (fireTimer <= 0f)
                    {
                        fireTimer = Mathf.Lerp(2.6f, 1.5f, g.Difficulty) / (1f + g.LevelThreat * 0.25f);
                        g.SpawnSlingStone(p + new Vector3(0f, 1.2f, -0.5f));
                    }
                }
                break;
            }

            case ObType.SlingStone:
                velocity.y -= 22f * dt;
                p += velocity * dt;
                transform.position = p;
                transform.Rotate(400f * dt, 0f, 300f * dt);
                if (p.y <= 0.35f && velocity.y < 0f) g.StoneLanded(this);
                break;

            case ObType.Hopper:
            {
                // salta de faixa em faixa
                p.z -= 2.5f * dt;
                timer -= dt;
                if (state == 0 && timer <= 0f)
                {
                    int lane = Mathf.Clamp(Mathf.RoundToInt(p.x / RunnerGame.LaneWidth) + 1, 0, 2);
                    int nl = lane == 0 ? 1 : (lane == 2 ? 1 : (Random.value < 0.5f ? 0 : 2));
                    baseX = p.x; targetX = (nl - 1) * RunnerGame.LaneWidth;
                    state = 1; timer = 0.55f;
                }
                if (state == 1)
                {
                    float k = 1f - Mathf.Clamp01(timer / 0.55f);
                    p.x = Mathf.Lerp(baseX, targetX, k);
                    p.y = baseY + Mathf.Sin(k * Mathf.PI) * 2.2f;
                    if (timer <= 0f) { state = 0; timer = Random.Range(0.7f, 1.4f); p.y = baseY; }
                }
                transform.position = p;
                break;
            }

            case ObType.Shockwave:
                p += velocity * dt;
                transform.position = p;
                transform.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 30f) * 0.15f, 1f);
                break;

            case ObType.Note:
                transform.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 3f) * 25f, Mathf.Sin(t * 6f) * 8f);
                p.y = 1.2f + Mathf.Sin(t * 5f + phase) * 0.12f;
                transform.position = p;
                break;

            case ObType.Ring:
                transform.Rotate(0f, 0f, 90f * dt);
                break;

            case ObType.Mine:
                p.y = baseY + Mathf.Sin(t * 2.5f + phase) * 0.3f;
                transform.position = p;
                transform.Rotate(40f * dt, 70f * dt, 0f);
                break;

            case ObType.Flyer:
            {
                // caça inimigo: voa em oito e atira no avião do jogador
                p.x = Mathf.Clamp(baseX + Mathf.Sin(t * moveFreq + phase) * 2.5f, -7f, 7f);
                p.y = Mathf.Clamp(baseY + Mathf.Cos(t * moveFreq * 0.7f + phase) * 1.5f, 1f, 10f);
                p.z += 4f * dt;   // voa um pouco na mesma direção do jogador
                transform.position = p;
                var pp = g.player.transform.position;
                float dz = p.z - pp.z;
                if (dz > 16f && dz < 70f)
                {
                    fireTimer -= dt;
                    if (fireTimer <= 0f)
                    {
                        fireTimer = Mathf.Lerp(2.2f, 1.2f, g.Difficulty) / (1f + g.LevelThreat * 0.3f);
                        Vector3 o = p + new Vector3(0f, 0f, -1f);
                        float time = Mathf.Max(0.3f, (o.z - pp.z) / (g.speed + 14f));
                        g.SpawnEnemyShot(o, new Vector3((pp.x - o.x) / time, (pp.y - o.y) / time, -14f), 0f);
                    }
                }
                break;
            }
        }

        if (flash > 0f)
        {
            flash -= dt;
            if (flash <= 0f) SetFlash(false);
        }
    }

    public void Flash(float time)
    {
        flash = time;
        SetFlash(true);
    }

    void SetFlash(bool on)
    {
        if (rends == null) return;
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (on)
            {
                mpb.Clear();
                mpb.SetColor("_BaseColor", Color.white);
                mpb.SetColor("_Color", Color.white);
                r.SetPropertyBlock(mpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }

    /// Aplica dano. Retorna true se morreu.
    public bool TakeDamage(float dmg)
    {
        if (dead) return false;
        hp -= dmg;
        if (hp <= 0f) return true;
        flash = 0.07f;
        SetFlash(true);
        return false;
    }
}
