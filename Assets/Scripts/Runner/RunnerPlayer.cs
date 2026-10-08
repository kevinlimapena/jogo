using UnityEngine;
using UnityEngine.InputSystem;

public class RunnerPlayer : MonoBehaviour
{
    public float jumpVelocity = 9.5f;
    public float gravity = 28f;
    public float laneChangeSharpness = 14f;

    [Header("Voo livre")]
    public float flySpeed = 12f;
    public float flyMinX = -7f, flyMaxX = 7f;
    public float flyMinY = 1f, flyMaxY = 10f;

    [HideInInspector] public int lane = 1;
    [HideInInspector] public float feetY;
    [HideInInspector] public float invuln;
    [HideInInspector] public float inputLock;
    [HideInInspector] public Transform shieldVisual;
    [HideInInspector] public GameObject runnerModel;
    [HideInInspector] public RunnerWeaponModel weaponModel;
    [HideInInspector] public GameObject planeModel;
    [HideInInspector] public bool flying;
    [HideInInspector] public GameObject carModel;
    [HideInInspector] public bool driving;
    [HideInInspector] public float carSpeed;
    [HideInInspector] public float carVx;
    [HideInInspector] public bool offRoad;
    [HideInInspector] public float roadOffset;   // distância lateral do centro da estrada

    [Header("Carro")]
    public float carCruise = 32f;
    public float carMax = 40f;
    public float carBrake = 20f;
    public float carBoostSpeed = 50f;
    public float carSteer = 16f;
    [Tooltip("Quanto a curva empurra o carro para fora (0 = nada).")]
    public float curveDrift = 0.35f;

    static readonly Vector3 RunHalf = new Vector3(0.42f, 0.8f, 0.5f);
    static readonly Vector3 FlyHalf = new Vector3(0.95f, 0.35f, 0.8f);
    static readonly Vector3 CarHalf = new Vector3(0.85f, 0.5f, 1.5f);
    public Vector3 Half => flying ? FlyHalf : (driving ? CarHalf : RunHalf);
    float boostTimer;

    float vy;
    float fireCd;
    float x;
    int airJumps;
    Vector2 flyVel;
    float flyY;
    Renderer[] rends;

    [HideInInspector] public float groundY;   // altura do chão (carroça) embaixo do jogador
    bool Grounded => feetY <= groundY + 0.001f;

    /// Velocidade vertical (negativa = caindo). Usada para saber se o jogador pisou em cima de algo.
    public float VerticalSpeed => vy;

    /// Quica para cima depois de pisar num inimigo (e recupera o pulo duplo).
    public void Bounce(float v, bool refreshAirJump)
    {
        vy = v;
        if (refreshAirJump) airJumps = Mathf.Max(airJumps, 1);
    }

    public void ResetPlayer()
    {
        lane = 1;
        x = 0f;
        feetY = 0f;
        vy = 0f;
        invuln = 0f;
        fireCd = 0f;
        airJumps = 0;
        flying = false;
        driving = false;
        angelOfDeath = false;
        offRoad = false;
        boostTimer = 0f;
        flyVel = Vector2.zero;
        SetModel(false);
        transform.position = new Vector3(0f, 0.9f, 0f);
        transform.rotation = Quaternion.identity;
        rends = null;
        SetVisible(true);
    }

    void SetModel(bool plane, bool car = false, bool angel = false)
    {
        if (runnerModel != null) runnerModel.SetActive(!plane && !car && !angel);
        if (planeModel != null) planeModel.SetActive(plane);
        if (carModel != null) carModel.SetActive(car);
        if (deathModel != null) deathModel.SetActive(angel);
    }

    // ------------------------------------------------------------------ anjo destruidor (Noite da Páscoa)

    [HideInInspector] public GameObject deathModel;
    [HideInInspector] public bool angelOfDeath;

    public void StartAngel()
    {
        angelOfDeath = true;
        SetModel(false, false, true);
        SetVisible(true);
    }

    public void EndAngel()
    {
        angelOfDeath = false;
        SetModel(false);
        SetVisible(true);
    }

    // ------------------------------------------------------------------ carro

    public void StartCar(float startSpeed)
    {
        driving = true;
        carSpeed = startSpeed;
        carVx = 0f;
        roadOffset = x;   // no início da corrida a estrada está centrada em 0
        feetY = 0f;
        vy = 0f;
        SetModel(false, true);
        SetVisible(true);
    }

    public void EndCar()
    {
        driving = false;
        offRoad = false;
        SetModel(false);
        SetVisible(true);
        lane = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(x, -3f, 3f) / RunnerGame.LaneWidth) + 1, 0, 2);
        transform.rotation = Quaternion.identity;
    }

    public void Boost() => boostTimer = 1.6f;

    public void Bump(float pushVx, float speedLoss)
    {
        carVx += pushVx;
        carSpeed *= speedLoss;
    }

    public void SetVisible(bool v)
    {
        // sempre busca de novo: o modelo da arma é reconstruído quando muda de arma
        rends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends)
        {
            if (r == null) continue;
            if (shieldVisual != null && r.transform == shieldVisual) continue;
            r.enabled = v;
        }
    }

    // ------------------------------------------------------------------ voo

    /// keepHorse = true: voa com o próprio cavalo (Travessia dos Céus) em vez do carro de fogo.
    public void StartFlight(bool keepHorse = false)
    {
        flying = true;
        flyY = transform.position.y;
        flyVel = new Vector2(0f, 6f);
        vy = 0f;
        SetModel(!keepHorse);
        SetVisible(true);
    }

    /// Empurra o voo para cima (subida da travessia).
    public void LiftUp(float v)
    {
        if (flyVel.y < v) flyVel.y = v;
    }

    public void EndFlight()
    {
        flying = false;
        SetModel(false);
        SetVisible(true);
        lane = Mathf.Clamp(Mathf.RoundToInt(x / RunnerGame.LaneWidth) + 1, 0, 2);
        feetY = Mathf.Max(0f, flyY - 0.9f);
        vy = 0f;
        transform.rotation = Quaternion.identity;
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var g = RunnerGame.I;
        if (g == null || g.state != RunnerState.Playing) return;
        if (g.bulletHell || g.babel || g.goliath) return;   // no mini-jogo da nave quem controla é o RunnerBulletHell
        var st = g.stats;
        float dt = Time.deltaTime;
        // durante o Tempo Bala o jogador se move em "tempo real"
        float pdt = g.BulletTimeActive ? Time.unscaledDeltaTime : dt;

        var kb = Keyboard.current;
        var ms = Mouse.current;
        var gp = Gamepad.current;
        RunnerTouch.Update();

        bool left = false, right = false, jump = false, down = false, fire = false, ability = false;
        Vector2 axis = Vector2.zero;
        if (inputLock > 0f)
        {
            inputLock -= Time.unscaledDeltaTime;
        }
        else
        {
            if (kb != null)
            {
                left |= kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame;
                right |= kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame;
                jump |= kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                down |= kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame;
                fire |= kb.jKey.isPressed || kb.kKey.isPressed || kb.leftCtrlKey.isPressed;
                ability |= kb.leftShiftKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame;

                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis.x += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.spaceKey.isPressed) axis.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) axis.y -= 1f;
            }
            if (ms != null)
            {
                fire |= ms.leftButton.isPressed;
                ability |= ms.rightButton.wasPressedThisFrame;
            }
            if (gp != null)
            {
                left |= gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame;
                right |= gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame;
                jump |= gp.buttonSouth.wasPressedThisFrame;
                down |= gp.dpad.down.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame;
                fire |= gp.rightTrigger.isPressed || gp.buttonWest.isPressed;
                ability |= gp.leftShoulder.wasPressedThisFrame || gp.leftTrigger.wasPressedThisFrame;
                axis += gp.leftStick.ReadValue();
                axis += gp.dpad.ReadValue();
            }

            // toque: deslizar troca de faixa / pula, toque rápido pula, arrastar = joystick
            left |= RunnerTouch.SwipeLeft;
            right |= RunnerTouch.SwipeRight;
            jump |= RunnerTouch.SwipeUp || RunnerTouch.Tap;
            down |= RunnerTouch.SwipeDown;
            ability |= RunnerTouch.Pressed("ability");
            if (RunnerTouch.UseTouchUI && g.touchAutoFire) fire = true;
            if (flying || driving) axis += RunnerTouch.Stick;
        }
        axis = Vector2.ClampMagnitude(axis, 1f);

        // abertura: a demo se joga sozinha
        if (g.TrailerAuto && !flying && !driving)
        {
            g.AutoPilot(this, out left, out right, out jump);
            down = false;
            ability = false;
            fire = true;
        }

        if (ability) g.TryBulletTime();

        if (flying) FlyStep(g, st, axis, dt, pdt);
        else if (driving) DriveStep(g, st, axis, dt, pdt);
        else RunStep(g, st, left, right, jump, down, dt, pdt);

        // tiro / golpe
        fireCd -= pdt;
        // armas corpo a corpo golpeiam sozinhas quando algo chega perto
        if (st.Melee && !flying && !driving && !fire && fireCd <= 0f
            && g.EnemyInMelee(transform.position, MeleeReach(g, st), st.MeleeHalfWidth)) fire = true;
        if (angelOfDeath) fire = false;   // o anjo não atira: ele julga ao passar
        if (fire && fireCd <= 0f)
        {
            fireCd = st.Cooldown;
            Shoot(g, st);
        }

        // piscar quando invencível
        if (invuln > 0f)
        {
            invuln -= dt;
            SetVisible(invuln <= 0f || Mathf.Repeat(Time.time * 14f, 1f) < 0.5f);
        }
    }

    void RunStep(RunnerGame g, RunnerStats st, bool left, bool right, bool jump, bool down, float dt, float pdt)
    {
        groundY = g.GroundAt(x, transform.position.z, feetY);   // em cima de uma carroça?
        int laneBefore = lane;
        if (left && lane > 0) lane--;
        if (right && lane < 2) lane++;
        if (lane != laneBefore) g.OnLaneChanged(laneBefore, lane);   // Mulher de Ló

        if (jump)
        {
            if (Grounded)
            {
                vy = jumpVelocity * Mathf.Sqrt(st.jumpMul);
                airJumps = st.doubleJump ? 1 : 0;
            }
            else if (airJumps > 0)
            {
                airJumps--;
                vy = jumpVelocity * 0.9f * Mathf.Sqrt(st.jumpMul);
                g.Explode(transform.position - new Vector3(0, 0.9f, 0), new Color(0.6f, 0.9f, 1f), 5);
            }
        }
        if (down && !Grounded) vy = -22f;

        bool wasAirborne = feetY > groundY + 0.05f;
        vy -= gravity * st.gravityMul * pdt;   // Dilúvio: você flutua
        feetY += vy * pdt;
        if (feetY <= groundY)
        {
            if (wasAirborne) g.OnPlayerLanded(transform.position - new Vector3(0f, 0.9f, 0f));   // Pisão do Querubim
            feetY = groundY;
            if (vy < 0f) vy = 0f;
        }

        float targetX = (lane - 1) * RunnerGame.LaneWidth;
        float prevX = x;
        x = Mathf.Lerp(x, targetX, 1f - Mathf.Exp(-laneChangeSharpness * st.laneMul * pdt));

        var p = transform.position;
        p.x = x;
        p.y = feetY + 0.9f;
        p.z += g.speed * dt;
        transform.position = p;

        // inclinação ao trocar de faixa
        float lean = Mathf.Clamp((x - prevX) / Mathf.Max(pdt, 0.0001f) * -2.5f, -25f, 25f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, 0f, lean), 1f - Mathf.Exp(-12f * pdt));
    }

    void FlyStep(RunnerGame g, RunnerStats st, Vector2 axis, float dt, float pdt)
    {
        float maxSpd = flySpeed * Mathf.Sqrt(st.laneMul);
        Vector2 target = axis * maxSpd;
        flyVel = Vector2.Lerp(flyVel, target, 1f - Mathf.Exp(-7f * pdt));

        x += flyVel.x * pdt;
        flyY += flyVel.y * pdt;
        if (x < flyMinX) { x = flyMinX; flyVel.x = 0f; }
        if (x > flyMaxX) { x = flyMaxX; flyVel.x = 0f; }
        if (flyY < flyMinY) { flyY = flyMinY; flyVel.y = 0f; }
        if (flyY > flyMaxY) { flyY = flyMaxY; flyVel.y = 0f; }
        feetY = flyY - 0.9f;

        var p = transform.position;
        p.x = x;
        p.y = flyY;
        p.z += g.speed * dt;
        transform.position = p;

        // inclina o avião conforme a direção
        var rot = Quaternion.Euler(-flyVel.y * 2.5f, flyVel.x * 0.8f, -flyVel.x * 3.5f);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-10f * pdt));
    }

    void DriveStep(RunnerGame g, RunnerStats st, Vector2 axis, float dt, float pdt)
    {
        float z = transform.position.z;
        offRoad = Mathf.Abs(roadOffset) > RunnerGame.RoadHalfWidth;

        float target = carCruise;
        if (axis.y > 0.3f) target = carMax;
        else if (axis.y < -0.3f) target = carBrake;
        if (boostTimer > 0f)
        {
            boostTimer -= pdt;
            target = Mathf.Max(target, carBoostSpeed);
        }
        if (offRoad) target = Mathf.Min(target, 16f);
        carSpeed = Mathf.MoveTowards(carSpeed, target, (target > carSpeed ? 16f : 40f) * pdt);

        // Direção relativa à estrada: o carro segue a pista sozinho,
        // A/D sempre move para a esquerda/direita DA PISTA.
        float maxLat = carSteer * Mathf.Sqrt(st.laneMul);
        // força centrífuga: nas curvas o carro é empurrado para fora (mais forte quanto mais rápido)
        float drift = -g.RoadCurvature(z) * carSpeed * carSpeed * curveDrift;
        float targetVx = axis.x * maxLat + drift;
        float steerAccel = Mathf.Abs(axis.x) > 0.1f ? 60f : 35f;
        carVx = Mathf.MoveTowards(carVx, targetVx, steerAccel * pdt);
        roadOffset = Mathf.Clamp(roadOffset + carVx * pdt, -15f, 15f);
        feetY = 0f;

        var p = transform.position;
        p.z += g.speed * dt;
        x = g.RoadX(p.z) + roadOffset;
        p.x = x;
        p.y = 0.55f + (offRoad ? Mathf.Sin(Time.time * 60f) * 0.04f : 0f);
        transform.position = p;

        // o carro aponta na direção da estrada + um pouco para onde está virando
        float roadYaw = Mathf.Atan(g.RoadSlope(p.z)) * Mathf.Rad2Deg;
        float steerYaw = Mathf.Clamp(carVx * 1.4f, -22f, 22f);
        var rot = Quaternion.Euler(0f, roadYaw + steerYaw, -carVx * 0.6f);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-12f * pdt));
    }

    /// Alcance efetivo do golpe: cresce com a velocidade (o personagem "avança" no golpe).
    float MeleeReach(RunnerGame g, RunnerStats st) => st.Reach + g.speed * 0.25f;

    void Shoot(RunnerGame g, RunnerStats st)
    {
        var w = st.weapon;
        int n = Mathf.Max(1, st.Pellets);
        float rel = st.BulletSpeed;
        float total = w.spread > 0f ? w.spread + st.extraProjectiles * 2.5f : (n - 1) * 3.5f;

        if (w.melee)
        {
            if (!flying && !driving)
                g.MeleeAttack(transform.position, MeleeReach(g, st), st.MeleeHalfWidth, st.Damage, w);
            if (weaponModel != null && !flying && !driving) weaponModel.OnFire();
            // onda de luz curta (60% do dano) — alcança o chefe e funciona no carro/voo
            Vector3 o = transform.position + new Vector3(0f, flying ? 0f : 0.2f - (driving ? 0f : groundY * 0.85f), 1f);
            float wtotal = (n - 1) * 6f;
            for (int i = 0; i < n; i++)
            {
                float ang = n == 1 ? 0f : Mathf.Lerp(-wtotal / 2f, wtotal / 2f, i / (float)(n - 1));
                float vx = Mathf.Tan(ang * Mathf.Deg2Rad) * rel;
                var b = g.FireBullet(o, vx, g.speed + rel, st.Damage * 0.6f, st.Size, st.BulletLife, st.Pierce, st.Explode, st.homing, w.color);
                g.MakeWave(b, w, st.Size);
            }
            g.PlaySwing();
            return;
        }
        Vector3 origin = flying
            ? transform.position + new Vector3(0f, -0.05f, 1.3f)
            : driving ? transform.position + new Vector3(0f, 0.35f, 1.8f)
            : (weaponModel != null ? weaponModel.MuzzlePosition : transform.position + new Vector3(0.3f, 0.1f, 0.8f));
        if (!flying && !driving && groundY > 0.5f) origin.y -= groundY * 0.85f;   // em cima da carroça: atira para baixo, nos inimigos do chão
        if (!flying && !driving && weaponModel != null) weaponModel.OnFire();

        for (int i = 0; i < n; i++)
        {
            float ang = n == 1 ? 0f : Mathf.Lerp(-total / 2f, total / 2f, i / (float)(n - 1));
            if (w.jitter > 0f) ang += Random.Range(-w.jitter, w.jitter);
            float vx = Mathf.Tan(ang * Mathf.Deg2Rad) * rel;
            g.FireBullet(origin, vx, g.speed + rel, st.Damage, st.Size, st.BulletLife, st.Pierce, st.Explode, st.homing, w.color);
        }
        g.PlayShoot();
    }
}
