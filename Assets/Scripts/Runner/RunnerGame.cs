using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum RunnerState { Menu, Playing, Paused, LevelUp, GameOver, Temple, Choice, Cutscene, Stable, Deck, Codex }

/// <summary>
/// Gerenciador principal do runner de tiro roguelike.
/// Ele se cria sozinho ao dar Play em qualquer cena (veja Boot()).
/// Para desativar a criação automática, mude AutoStart para false.
/// </summary>
public partial class RunnerGame : MonoBehaviour
{
    public static RunnerGame I;
    public static bool AutoStart = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!AutoStart) return;
        if (FindAnyObjectByType<RunnerGame>() != null) return;
        new GameObject("RunnerGame").AddComponent<RunnerGame>();
    }

    public const float LaneWidth = 3f;

    [Header("Velocidade")]
    public float startSpeed = 11f;
    public float maxSpeed = 30f;
    public float accel = 0.16f;

    [Header("Vidas")]
    public int startLives = 3;
    public int hardMaxLives = 10;

    [Header("Cartas")]
    public int firstCardScore = 260;
    public int cardStepBase = 300;
    public int cardStepPerLevel = 190;
    public int cardStepQuad = 8;      // cresce com o quadrado do nível: os níveis altos pedem bem mais

    /// Pontos necessários para a próxima carta, a partir do nível atual.
    int CardStep() => Mathf.RoundToInt((cardStepBase + level * cardStepPerLevel + level * level * cardStepQuad) * stats.cardStepMul);

    [Header("Chefes")]
    public int bossEveryLevels = 5;
    public float bossWarningTime = 3.5f;

    [Header("Voo livre")]
    [Tooltip("Segundos de corrida até a primeira caixa de avião poder aparecer. Negativo desliga.")]
    public float firstPlaneBoxTime = 30f;
    [Tooltip("Segundos entre uma caixa de avião e a próxima (também conta depois de pousar).")]
    public float planeBoxInterval = 35f;
    public float flightDuration = 22f;

    [Header("Desenvolvedor")]
    [Tooltip("Mostra os botões de atalho (pular para o 3º chefe, Roma, Sheol) no menu de pausa. Desligue antes de lançar o jogo.")]
    public bool devTools = true;

    // meta-progressão
    [HideInInspector] public Prophet prophet;
    int lastTalentsEarned;
    readonly Dictionary<string, System.Action> uiActions = new Dictionary<string, System.Action>();
    readonly List<Rect> uiRects = new List<Rect>();   // áreas de botões (GUI absoluta) para o clique não "vazar"
    string templeMsg = "";
    float templeMsgTime;

    [Header("Jornada")]
    [Tooltip("Depois de quantos chefes derrotados a jornada vai para Roma.")]
    public int egyptAfterBoss = 2;
    public int romeAfterBoss = 4;
    [Tooltip("Depois de quantos chefes derrotados a jornada desce ao Sheol.")]
    public int sheolAfterBoss = 6;
    [Tooltip("Número do chefe final (Satanás). Ex.: 9 = depois de 2 chefes no Sheol.")]
    public int finalBossNumber = 9;
    bool finalBeaten;
    Transform trackRoot;
    string bannerText = "", bannerSub = "";
    float bannerTime;

    [Header("Estilo PS1")]
    [Tooltip("Visual de PlayStation 1: baixa resolução, poucas cores com pontilhado e vértices tremendo. Tecla V liga/desliga.")]
    public bool ps1Style = false;
    [Tooltip("Linhas verticais da imagem (PS1 usava ~240).")]
    public int ps1Resolution = 360;
    [Tooltip("Níveis de cor por canal (PS1 = 32).")]
    public int ps1ColorLevels = 32;
    [Range(0f, 1f)] public float ps1Dither = 0.45f;
    [Range(0f, 1f)] public float ps1Wobble = 0.3f;
    [HideInInspector] public RunnerPS1 ps1;
    bool usingPS1Shader;

    [Header("Celular")]
    [Tooltip("No celular o tiro é automático.")]
    public bool touchAutoFire = true;
    [Tooltip("Permite jogar com o celular em pé. Recomendado: desligado (paisagem).")]
    public bool allowPortrait = false;

    [Header("Nave (bullet hell)")]
    public bool enableShipBox = true;
    public float hellTimeLimit = 60f;
    [HideInInspector] public bool bulletHell;
    RunnerBulletHell hell;
    int hellsCleared;

    [Header("Torre de Babel")]
    public bool enableBabelBox = true;
    [HideInInspector] public bool babel;
    RunnerBabel babelGame;
    int babelsCleared;

    [Header("Noite da Páscoa (anjo)")]
    public bool enableAngelBox = true;
    public float passoverDuration = 30f;
    [HideInInspector] public bool passover;
    float passoverTime;
    int judged, spared, missed, bloodHits, passCombo;

    [Header("Corrida de carro")]
    public bool enableCarBox = true;
    public float raceLength = 1100f;
    public int rivalCount = 5;

    [HideInInspector] public RunnerState state = RunnerState.Menu;
    [HideInInspector] public float speed;
    [HideInInspector] public int lives;
    [HideInInspector] public int maxLives;
    [HideInInspector] public float runTime;
    [HideInInspector] public RunnerPlayer player;
    [HideInInspector] public RunnerStats stats = new RunnerStats();
    public readonly List<RunnerObstacle> obstacles = new List<RunnerObstacle>();

    public float Difficulty => Mathf.Clamp01(runTime / 210f + level * 0.03f);
    /// Ameaça extra dos níveis altos (0 no começo, cresce ~0.1 por nível até 2.5). Continua subindo depois que Difficulty chega a 1.
    public float LevelThreat => Mathf.Clamp((level - 1) * 0.14f + Mathf.Max(0f, runTime - 90f) / 300f + ExtraThreat, 0f, 6f);
    /// Ameaça com teto, para cadência de tiro e mini-jogos (depois disso só vida e quantidade de inimigos crescem).
    public float ThreatSoft => Mathf.Min(LevelThreat, 4f);
    public float HpMul => (1f + runTime / 60f) * (1f + LevelThreat * 0.45f);
    public int Score => Mathf.FloorToInt(distanceScore) + killScore;
    public bool BulletTimeActive => btActive > 0f && state == RunnerState.Playing;

    float distanceScore;
    int killScore;
    int kills;
    int highScore;
    float gameOverTime;
    float nextSpawnZ;
    float shake;
    string popup = "";
    float popupTime;
    int vampCounter;

    // cartas
    int level;
    int nextCardScore;
    int prevCardScore;
    readonly Dictionary<string, int> cardStacks = new Dictionary<string, int>();
    readonly List<RunnerCard> history = new List<RunnerCard>();
    List<RunnerCard> offer = new List<RunnerCard>();
    int rerollsLeft;
    bool offerIsBoss;
    int selected;
    float levelUpOpenTime;
    Vector2 lastMouse;

    // chefes
    [HideInInspector] public RunnerBoss boss;
    bool bossPending;
    float bossWarn;
    int bossesDefeated;
    public bool BossFight => boss != null || bossPending;

    // voo livre
    bool flightPending;
    float flightWarn;
    float planeBoxTimer;

    // corrida
    [HideInInspector] public bool racing;
    float raceStartZ;
    float finishZ;
    int racePosition;
    public const float RoadHalfWidth = 6.3f;
    const int RoadSegs = 70;
    const float SegLen = 4f;
    Transform roadRoot;
    readonly List<Transform> roadSegs = new List<Transform>();
    float nextSegZ;
    Transform grass;
    GameObject finishBanner;
    string offerTitle = "", offerSub = "";

    // UI adaptável
    Vector2 guiOffset;
    readonly List<Rect> cardRects = new List<Rect>();
    Rect rerollRect;
    bool IsMobile => Application.isMobilePlatform;

    public float RoadSlope(float z) => (RoadX(z + 1f) - RoadX(z - 1f)) * 0.5f;
    public float RoadCurvature(float z) => (RoadX(z + 2f) - 2f * RoadX(z) + RoadX(z - 2f)) * 0.25f;

    /// Centro da estrada (só curva durante a corrida).
    public float RoadX(float z)
    {
        if (!racing) return 0f;
        float u = z - raceStartZ;
        float ramp = Mathf.Clamp01(u / 80f) * Mathf.Clamp01((raceLength + 60f - u) / 80f);
        return ramp * (10f * Mathf.Sin(u * 2f * Mathf.PI / 300f) + 4f * Mathf.Sin(u * 2f * Mathf.PI / 140f + 1.3f));
    }
    float flightTime;
    public bool FlightEvent => flightPending || (player != null && player.flying) || racing || bulletHell || passover || babel || jericho || goliath;

    // habilidades
    bool shieldReady;
    float shieldTimer;
    float btActive;
    float btCooldownLeft;
    readonly List<RunnerDrone> drones = new List<RunnerDrone>();

    // textos flutuantes
    class FloatText { public Vector3 pos; public string text; public Color color; public float t; public bool big; }
    readonly List<FloatText> floats = new List<FloatText>();

    Camera cam;
    Material baseMat;
    Material glowBaseMat;
    readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    readonly Dictionary<Color, Material> glowMats = new Dictionary<Color, Material>();

    const float TileLen = 20f;
    const int TileCount = 9;
    readonly List<Transform> tiles = new List<Transform>();
    float nextTileZ;

    AudioSource sfx;
    AudioClip sShoot, sBoom, sHurt, sPickup, sEnemyShot, sClank, sLevel, sShield;
    float lastBoomSfx, lastClankSfx, lastShootSfx;

    GUIStyle bigStyle, midStyle, smallStyle, tinyStyle, cardTitle, cardDesc, cardSmall, floatStyle;

    // ---------------- bioma atual (cores vêm do tema)
    [HideInInspector] public BiomeTheme Theme = Biomes.Jerusalem;
    BiomeTheme T => Theme;
    Color Sky => T.sky;
    Color[] StoneColors => T.stones;
    Color BabylonBlue => T.accent;
    Color Gold => T.metal;
    Color DarkOpening => T.dark;
    Color Clay => T.idol;

    // ================================================================== setup

    void Awake()
    {
        // atalhos de desenvolvedor: só no editor do Unity e em builds de desenvolvimento (nunca na versão da loja)
        devTools = devTools && (Application.isEditor || Debug.isDebugBuild);
        I = this;
        LoadMaterials();

        highScore = PlayerPrefs.GetInt("runner_highscore", 0);
        SetupMobile();

        SetupCameraAndLight();
        SetupPerformance();
        hell = gameObject.AddComponent<RunnerBulletHell>();
        babelGame = gameObject.AddComponent<RunnerBabel>();
        ps1 = gameObject.AddComponent<RunnerPS1>();
        ps1Style = PlayerPrefs.GetInt("runner_ps1_v2", ps1Style ? 1 : 0) == 1;   // lembra a escolha do jogador (padrão: desligado)
        ps1.styleOn = ps1Style;
        ps1.resolution = ps1Resolution;
        ps1.colorLevels = ps1ColorLevels;
        ps1.dither = ps1Dither;
        ps1.wobble = ps1Wobble;
        ps1.Init(cam);
        SetupAudio();
        BuildTrack();
        BuildRaceRoad();
        CreatePlayer();
        ResetRun();
        state = RunnerState.Menu;
        if (!TrailerSeen) StartTrailer();   // primeira vez: a abertura
    }

    void TogglePS1()
    {
        if (ps1 == null) return;
        ps1.Toggle();
        ps1Style = ps1.styleOn;
        PlayerPrefs.SetInt("runner_ps1_v2", ps1Style ? 1 : 0);
        PlayerPrefs.Save();
        ShowPopup(ps1.styleOn ? "FILTRO PS1: LIGADO" : "FILTRO PS1: DESLIGADO");
    }

    /// Posição na tela (pixels, origem embaixo) — funciona mesmo com a câmera desenhando em baixa resolução.
    Vector3 WorldToScreen(Vector3 world)
    {
        Vector3 v = cam.WorldToViewportPoint(world);
        return new Vector3(v.x * Screen.width, v.y * Screen.height, v.z);
    }

    /// Céu, neblina e sol de acordo com o bioma.
    void ApplyAtmosphere()
    {
        if (cam != null) cam.backgroundColor = T.sky;
        RenderSettings.fogColor = T.fogColor;
        RenderSettings.fogStartDistance = T.fogStart;
        RenderSettings.fogEndDistance = T.fogEnd;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.color = T.sunColor;
            l.intensity = T.sunIntensity;
        }
    }

    /// Troca de cenário: reconstrói a rua, as construções e a pista de corrida.
    void SetBiome(BiomeTheme nb, bool announce)
    {
        bool changed = nb != Theme;
        Theme = nb;
        if (changed)
        {
            ClearStructPoolExcept(nb.id);
            if (roadRoot != null) Destroy(roadRoot.gameObject);
            roadSegs.Clear();
            BuildRaceRoad();
            if (trackRoot != null) Destroy(trackRoot.gameObject);
            tiles.Clear();
            BuildTrack();
            SetRaceScenery(false);
        }
        ApplyAtmosphere();
        if (announce && changed)
        {
            bannerText = T.arrivalText;
            bannerSub = T.id == 1 ? "a jornada segue para o coração do império" : (T.id == 2 ? "a descida ao mundo dos mortos" : (T.id == 3 ? "\"Do Egito chamei o meu filho\" (Os 11:1)" : ""));
            bannerTime = 4f;
            shake = 0.6f;
        }
    }

    void SetupMobile()
    {
        if (!IsMobile) return;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = allowPortrait;
        Screen.autorotateToPortraitUpsideDown = false;
        if (!allowPortrait && Screen.height > Screen.width) Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    void Vibrate()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    void SetupCameraAndLight()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Sky;
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 220f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = T.fogColor;
        RenderSettings.fogStartDistance = 40f;
        RenderSettings.fogEndDistance = 150f;

        bool hasSun = false;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) hasSun = true;
        if (!hasSun)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    // ================================================================== helpers

    /// Carrega os materiais de Assets/Resources (incluídos no build).
    /// Sem isso, no celular/build o material padrão das primitivas não existe e tudo fica roxo.
    void LoadMaterials()
    {
        // shader estilo PS1 (iluminação por vértice + tremido); se não der, usa o URP Lit normal
        var ps1Shader = Resources.Load<Shader>("RunnerPS1Lit");
        if (ps1Shader != null && ps1Shader.isSupported)
        {
            baseMat = new Material(ps1Shader);
            glowBaseMat = baseMat;
            usingPS1Shader = true;
            return;
        }

        baseMat = Resources.Load<Material>("RunnerLit");
        glowBaseMat = Resources.Load<Material>("RunnerLitGlow");

        if (baseMat == null || baseMat.shader == null || !baseMat.shader.isSupported)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null || !sh.isSupported) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (sh == null || !sh.isSupported) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null) baseMat = new Material(sh);
            else
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                baseMat = tmp.GetComponent<Renderer>().sharedMaterial;
                Destroy(tmp);
            }
            Debug.LogWarning("[Runner] Material 'Resources/RunnerLit' não encontrado; usando fallback " + (baseMat != null ? baseMat.shader.name : "nenhum"));
        }
        if (glowBaseMat == null) glowBaseMat = baseMat;
    }

    public Material Mat(Color c)
    {
        if (!mats.TryGetValue(c, out var m))
        {
            m = new Material(baseMat) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            mats[c] = m;
        }
        return m;
    }

    public Material Glow(Color c)
    {
        if (!glowMats.TryGetValue(c, out var m))
        {
            m = new Material(glowBaseMat) { color = c };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * (usingPS1Shader ? 0.75f : 2.5f));
            glowMats[c] = m;
        }
        return m;
    }

    /// Cria uma primitiva sem collider (a colisão do jogo é feita manualmente).
    public GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool glow = false)
    {
        MeshRenderer r;
        var go = NewPrim(type, out r);   // sem colisor: mais leve de criar
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        r.sharedMaterial = glow ? Glow(color) : Mat(color);
        if (glow) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }   // brilhos não fazem sombra
        return go;
    }

    static float LaneX(int lane) => (lane - 1) * LaneWidth;

    int Stacks(RunnerCard c) => cardStacks.TryGetValue(c.id, out var n) ? n : 0;
    public int CardStacks(string id) => cardStacks.TryGetValue(id, out var n) ? n : 0;

    // ================================================================== track

    void BuildTrack()
    {
        var root = new GameObject("Track").transform;
        trackRoot = root;
        for (int i = 0; i < TileCount; i++)
        {
            var tile = new GameObject("Tile" + i).transform;
            tile.SetParent(root, false);
            // rua de pedra
            Color ground = i % 2 == 0 ? T.groundA : T.groundB;
            Prim(PrimitiveType.Cube, tile, new Vector3(0, -0.1f, TileLen / 2), new Vector3(9.5f, 0.2f, TileLen), ground);
            // juntas das lajes
            for (int d = 0; d < 4; d++)
                Prim(PrimitiveType.Cube, tile, new Vector3(0f, 0.005f, d * TileLen / 4f), new Vector3(9.5f, 0.02f, 0.12f), T.joint, T.glowingJoints);
            // divisórias das faixas: pedrinhas escuras
            for (int d = 0; d < 2; d++)
            {
                float z = TileLen * 0.25f + d * TileLen * 0.5f;
                Prim(PrimitiveType.Cube, tile, new Vector3(-1.5f, 0.01f, z), new Vector3(0.16f, 0.04f, 4f), T.laneMark);
                Prim(PrimitiveType.Cube, tile, new Vector3(1.5f, 0.01f, z), new Vector3(0.16f, 0.04f, 4f), T.laneMark);
            }
            // calçadas de pedra com meio-fio azul (Babilônia)
            for (int side = -1; side <= 1; side += 2)
            {
                Prim(PrimitiveType.Cube, tile, new Vector3(side * 6.25f, 0.15f, TileLen / 2), new Vector3(3f, 0.5f, TileLen), T.sidewalk);
                Prim(PrimitiveType.Cube, tile, new Vector3(side * 4.82f, 0.2f, TileLen / 2), new Vector3(0.16f, 0.42f, TileLen), T.curb, T.glowingJoints);
            }
            for (int b = 0; b < 4; b++)
            {
                var slot = new GameObject("Building").transform;
                slot.SetParent(tile, false);
            }
            tiles.Add(tile);
        }
    }

    void RandomizeBuildings(Transform tile)
    {
        int idx = 0;
        foreach (Transform c in tile)
        {
            if (c.name != "Building") continue;
            float side = idx < 2 ? -1f : 1f;
            float z = (idx % 2) * TileLen * 0.5f + TileLen * 0.25f;
            c.localRotation = Quaternion.identity;
            c.localScale = Vector3.one;
            c.localPosition = new Vector3(c.localPosition.x, 0f, z);
            SwapStructure(c, side);   // reaproveita prédios já construídos
            idx++;
        }
    }

    /// Constrói uma estrutura de Jerusalém/Babilônia. Retorna a meia-largura (eixo X).
    /// "side" = -1 lado esquerdo, +1 lado direito (a fachada fica virada para a rua).
    float BuildStructure(Transform t, float side)
    {
        if (T.id == 1) return Biomes.BuildRome(this, t, side);
        if (T.id == 2) return Biomes.BuildSheol(this, t, side);
        if (T.id == 3) return Biomes.BuildEgypt(this, t, side);
        float r = Random.value;
        Color stone = StoneColors[Random.Range(0, StoneColors.Length)];
        float face = -side; // direção da rua no eixo X

        if (r < 0.34f)
        {
            // casa de pedra com telhado plano e ameias
            float w = Random.Range(4f, 6.5f), h = Random.Range(3f, 7f), d = Random.Range(5f, 8.5f);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), stone);
            int n = Mathf.Max(2, Mathf.FloorToInt(d / 1.3f));
            for (int i = 0; i < n; i++)
                Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f - 0.25f), h + 0.3f, -d / 2f + 0.65f + i * (d - 1.3f) / Mathf.Max(1, n - 1)), new Vector3(0.5f, 0.6f, 0.55f), stone);
            Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.01f), 1f, Random.Range(-d / 4f, d / 4f)), new Vector3(0.08f, 2f, 1.2f), DarkOpening);
            if (h > 4f)
                for (int i = -1; i <= 1; i += 2)
                    Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.01f), h * 0.7f, i * d * 0.25f), new Vector3(0.08f, 0.7f, 0.5f), DarkOpening);
            if (Random.value < 0.45f)
                Prim(PrimitiveType.Cube, t, new Vector3(-face * w * 0.15f, h + 1.1f, d * 0.15f), new Vector3(w * 0.45f, 2.2f, d * 0.4f), stone * 0.95f);
            return w / 2f;
        }
        if (r < 0.52f)
        {
            // torre com cúpula (dourada ou azul)
            float w = Random.Range(2.8f, 3.6f), h = Random.Range(7f, 13f);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, w), stone);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, h * 0.75f, 0f), new Vector3(w + 0.06f, 0.4f, w + 0.06f), BabylonBlue);
            Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.01f), h * 0.55f, 0f), new Vector3(0.08f, 1.2f, 0.6f), DarkOpening);
            Prim(PrimitiveType.Cylinder, t, new Vector3(0f, h + 0.15f, 0f), new Vector3(w * 0.95f, 0.15f, w * 0.95f), stone * 0.9f);
            Prim(PrimitiveType.Sphere, t, new Vector3(0f, h + 0.3f, 0f), new Vector3(w * 0.9f, w * 0.75f, w * 0.9f), Random.value < 0.5f ? Gold : BabylonBlue);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, h + w * 0.42f + 0.4f, 0f), new Vector3(0.12f, 0.6f, 0.12f), Gold);
            return w / 2f;
        }
        if (r < 0.66f)
        {
            // zigurate (torre de degraus da Babilônia)
            int tiers = Random.Range(3, 5);
            float w = 7f, y = 0f;
            var brick = new Color(0.72f, 0.52f, 0.36f);
            for (int i = 0; i < tiers; i++)
            {
                float tw = w - i * 1.6f;
                Prim(PrimitiveType.Cube, t, new Vector3(0f, y + 1.1f, 0f), new Vector3(tw, 2.2f, tw), i % 2 == 0 ? brick : brick * 0.9f);
                y += 2.2f;
            }
            Prim(PrimitiveType.Cube, t, new Vector3(face * (w / 2f + 0.3f), 1.6f, 0f), new Vector3(1.4f, 0.3f, 1.6f), brick * 0.85f); // escada
            Prim(PrimitiveType.Cube, t, new Vector3(0f, y + 0.8f, 0f), new Vector3(1.6f, 1.6f, 1.6f), BabylonBlue);                  // santuário
            Prim(PrimitiveType.Cube, t, new Vector3(0f, y + 1.7f, 0f), new Vector3(1.7f, 0.2f, 1.7f), Gold);
            return w / 2f;
        }
        if (r < 0.82f)
        {
            // trecho da Porta de Ishtar: tijolos azuis com animais dourados
            float h = Random.Range(6f, 8f), d = TileLen * 0.45f;
            Prim(PrimitiveType.Cube, t, new Vector3(0f, h / 2f, 0f), new Vector3(2.2f, h, d), BabylonBlue);
            for (int row = 0; row < 2; row++)
            for (int i = 0; i < 3; i++)
            {
                float zz = -d / 3f + i * d / 3f;
                float yy = h * (0.35f + row * 0.3f);
                Prim(PrimitiveType.Cube, t, new Vector3(face * 1.11f, yy, zz), new Vector3(0.06f, 0.55f, 1.3f), Gold);                       // corpo do animal
                Prim(PrimitiveType.Cube, t, new Vector3(face * 1.11f, yy + 0.35f, zz + 0.65f), new Vector3(0.06f, 0.35f, 0.35f), Gold);       // cabeça
            }
            Prim(PrimitiveType.Cube, t, new Vector3(face * 1.11f, h * 0.15f, 0f), new Vector3(0.06f, 0.25f, d), new Color(0.95f, 0.9f, 0.8f));
            int n = 5;
            for (int i = 0; i < n; i++)
                Prim(PrimitiveType.Cube, t, new Vector3(0f, h + 0.35f, -d / 2f + 0.6f + i * (d - 1.2f) / (n - 1)), new Vector3(2.2f, 0.7f, 0.7f), BabylonBlue);
            return 1.1f;
        }

        // palmeiras
        int palms = Random.Range(1, 3);
        for (int p = 0; p < palms; p++)
        {
            float px = Random.Range(-1f, 1f), pz = Random.Range(-3.5f, 3.5f);
            float ph = Random.Range(4f, 6.5f);
            var trunk = new Color(0.5f, 0.36f, 0.22f);
            var t1 = Prim(PrimitiveType.Cylinder, t, new Vector3(px, ph * 0.25f, pz), new Vector3(0.35f, ph * 0.25f, 0.35f), trunk);
            var t2 = Prim(PrimitiveType.Cylinder, t, new Vector3(px + 0.2f, ph * 0.72f, pz), new Vector3(0.3f, ph * 0.25f, 0.3f), trunk * 0.9f);
            t2.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            for (int k = 0; k < 6; k++)
            {
                var leaf = Prim(PrimitiveType.Cube, t, new Vector3(px + 0.3f, ph, pz), new Vector3(0.45f, 0.06f, 2.2f), new Color(0.25f, 0.5f, 0.2f));
                leaf.transform.localRotation = Quaternion.Euler(25f, k * 60f, 0f);
                leaf.transform.localPosition += leaf.transform.forward * 0.9f;
            }
            Prim(PrimitiveType.Sphere, t, new Vector3(px + 0.3f, ph - 0.2f, pz), Vector3.one * 0.5f, new Color(0.45f, 0.28f, 0.12f)); // tâmaras
        }
        return 1.5f;
    }

    void UpdateTrack()
    {
        float pz = player.transform.position.z;
        foreach (var t in tiles)
        {
            if (t.position.z + TileLen < pz - 15f)
            {
                t.position = new Vector3(0, 0, nextTileZ);
                nextTileZ += TileLen;
                RandomizeBuildings(t);
            }
        }
    }

    // ================================================================== player

    void CreatePlayer()
    {
        var go = new GameObject("Player");
        player = go.AddComponent<RunnerPlayer>();
        var t = go.transform;

        // modelo corredor
        var run = new GameObject("RunnerModel").transform;
        run.SetParent(t, false);
        // criatura de 4 "patas" (esferas com olhos) — centro do jogador fica 0.9 acima do chão
        var creature = RunnerCreature.Build(run, 1f, -0.9f, RunnerCreature.PlayerPalette, false);
        var wm = creature.weaponMount.gameObject.AddComponent<RunnerWeaponModel>();
        wm.followPlayerWeapon = true;
        player.weaponModel = wm;
        player.runnerModel = run.gameObject;

        // carro de fogo de Elias (modo voo)
        var plane = new GameObject("PlaneModel").transform;
        plane.SetParent(t, false);
        RunnerChariot.Build(plane, new Color(1f, 0.5f, 0.08f), true, true, -0.45f);
        player.planeModel = plane.gameObject;
        plane.gameObject.SetActive(false);

        // modelo carro
        var car = new GameObject("CarModel").transform;
        car.SetParent(t, false);
        BuildCarVisual(car, new Color(0.15f, 0.45f, 0.95f), true);
        player.carModel = car.gameObject;
        car.gameObject.SetActive(false);

        // anjo destruidor (Noite da Páscoa)
        var death = new GameObject("DeathAngelModel").transform;
        death.SetParent(t, false);
        BuildDeathAngel(death);
        player.deathModel = death.gameObject;
        death.gameObject.SetActive(false);

        var halo = Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.25f, 0f), new Vector3(0.8f, 0.02f, 0.8f), new Color(0.3f, 0.9f, 1f), true);
        halo.name = "ShieldHalo";
        player.shieldVisual = halo.transform;
        halo.SetActive(false);
    }

    public void AddDrone()
    {
        var go = new GameObject("Drone");
        Prim(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.35f, 0.15f, 0.35f), new Color(0.2f, 0.2f, 0.25f));
        Prim(PrimitiveType.Sphere, go.transform, new Vector3(0, 0.05f, 0), new Vector3(0.2f, 0.2f, 0.2f), new Color(0.4f, 1f, 0.6f), true);
        var d = go.AddComponent<RunnerDrone>();
        d.index = drones.Count;
        go.transform.position = player.transform.position + Vector3.up * 1.3f;
        drones.Add(d);
    }

    public void Heal(int n)
    {
        if (lives < maxLives && n > 0) { Sfx("cura", 0.7f, 0.02f, 0.2f); if (player != null && state == RunnerState.Playing) CardFx.Hearts(player.transform, 3 + n * 2); }
        lives = Mathf.Min(maxLives, lives + n);
    }

    public void RechargeShieldNow()
    {
        shieldReady = true;
        shieldTimer = 0f;
    }

    public void TryBulletTime()
    {
        if (!stats.bulletTime || btActive > 0f || btCooldownLeft > 0f) return;
        btActive = stats.bulletTimeDuration;
        btCooldownLeft = stats.bulletTimeCooldown;
        Play(sLevel, 0.6f);
    }

    // ================================================================== run lifecycle

    void ResetRun()
    {
        ResetRogue();
        ResetRunDeck();
        ResetRules();
        ResetPacing();
        ResetSkyTransit();
        for (int i = obstacles.Count - 1; i >= 0; i--)
            if (obstacles[i] != null) Destroy(obstacles[i].gameObject);
        obstacles.Clear();
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach (var d in FindObjectsByType<RunnerDebris>(FindObjectsSortMode.None)) Destroy(d.gameObject);
        foreach (var d in drones) if (d != null) Destroy(d.gameObject);
        drones.Clear();
        floats.Clear();

        stats = new RunnerStats();
        cardStacks.Clear();
        history.Clear();
        level = 0;
        prevCardScore = 0;
        nextCardScore = firstCardScore;
        shieldReady = false;
        shieldTimer = 0f;
        btActive = 0f;
        btCooldownLeft = 0f;
        vampCounter = 0;
        boss = null;
        bossPending = false;
        bossWarn = 0f;
        bossesDefeated = 0;
        offerIsBoss = false;
        flightPending = false;
        flightWarn = 0f;
        flightTime = 0f;
        planeBoxTimer = firstPlaneBoxTime;
        if (racing || roadRoot.gameObject.activeSelf) SetRaceScenery(false);
        if (hell != null && hell.Active) hell.End();
        bulletHell = false;
        hellsCleared = 0;
        if (babelGame != null && babelGame.Active) babelGame.End();
        babel = false;
        babelsCleared = 0;
        racing = false;
        if (finishBanner != null) Destroy(finishBanner);

        passover = false;
        SetBiome(Biomes.Jerusalem, false);
        bannerTime = 0f;
        finalBeaten = false;
        manaCounter = 0;
        manaPortions = 0;
        aaronCounter = 0;
        trumpetTimer = 12f;
        skyFireTimer = 6f;
        if (orbitRoot != null) Destroy(orbitRoot.gameObject);
        orbitRoot = null;
        player.ResetPlayer();
        speed = startSpeed;
        maxLives = startLives;
        lives = startLives;
        runTime = 0f;
        distanceScore = 0f;
        killScore = 0;
        kills = 0;
        popup = "";
        nextSpawnZ = 45f;
        Time.timeScale = 1f;

        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].position = new Vector3(0, 0, (i - 2) * TileLen);
            RandomizeBuildings(tiles[i]);
        }
        nextTileZ = (tiles.Count - 2) * TileLen;

        var p = player.transform.position;
        cam.transform.position = new Vector3(0, 4.2f, p.z - 8f);
        cam.transform.LookAt(new Vector3(0, 1.2f, p.z + 10f));
    }

    void StartRun()
    {
        if (Meta.DailyMode) Random.InitState(Meta.DailySeed);   // desafio do dia: mesmo baralho para todos
        ResetRun();
        ApplyProphetAndTemple();
        state = RunnerState.Playing;
        player.inputLock = 0.15f;
    }

    /// Dá uma carta "de graça" (sem subir de nível): usada pelos profetas e pelas Primícias.
    void GrantCard(RunnerCard c)
    {
        if (c == null) return;
        cardStacks[c.id] = Stacks(c) + 1;
        history.Add(c);
        c.apply(this);
        OnCardGained(c);
    }

    RunnerCard FindCard(string id)
    {
        foreach (var c in CardDB.All) if (c.id == id) return c;
        return null;
    }

    void ApplyProphetAndTemple()
    {
        prophet = Meta.Selected;
        RebuildHorse();   // cavalo com a pelagem e o adorno do profeta
        stats.weapon = prophet.weapon();
        bool exile = Meta.OathOn("exilio");   // Exílio: as bênçãos do Templo não valem
        System.Func<string, int> T = id => exile ? 0 : Meta.Level(id);
        maxLives += prophet.extraLives + T("vida");
        if (Meta.OathOn("nazireu")) maxLives -= 1;
        stats.damageMul += prophet.damageBonus + 0.1f * T("uncao");
        stats.critChance += prophet.critBonus;
        stats.rerolls += prophet.extraRerolls + T("sabedoria");
        stats.scoreMul += 0.15f * T("heranca");
        foreach (var id in prophet.startCards) GrantCard(FindCard(id));
        // Primícias: as primeiras cartas comuns/raras do seu baralho
        offerIsBoss = false;
        for (int i = 0; i < T("primicias"); i++)
        {
            for (int tries = 0; tries < 10; tries++)
            {
                var roll = DrawCards(1);
                ReturnHand();
                if (roll.Count == 0) break;
                var c = roll[0];
                if (c.curse || c.rarity > Rarity.Raro) continue;
                GrantCard(c);
                break;
            }
        }
        if (Meta.OathOn("pragas")) AddPlague("praga_ras", 2);
        if (Meta.DailyMode)
        {
            var rule = FindCard(Meta.DailyRule);
            GrantCard(rule);
            Banner("DESAFIO DO DIA", prophet.name + "  •  " + (rule != null ? rule.name : "") + "  •  juramento: " + Meta.DailyOath.name);
            bannerTime = 6f;
        }
        maxLives = Mathf.Clamp(maxLives, 1, hardMaxLives);
        lives = maxLives;
        if (player.weaponModel != null) player.weaponModel.currentId = "";
    }

    void GameOver()
    {
        state = RunnerState.GameOver;
        gameOverTime = Time.unscaledTime;
        Explode(player.transform.position, new Color(0.15f, 0.45f, 0.95f), 30);
        player.SetVisible(false);
        player.shieldVisual.gameObject.SetActive(false);
        Play(sBoom, 1f);
        Sfx("lamento", 0.9f, 0f, 1f);
        shake = 0.8f;
        if (Score > highScore)
        {
            highScore = Score;
            PlayerPrefs.SetInt("runner_highscore", highScore);
            PlayerPrefs.Save();
        }
        RecordDaily();
        // Talentos (Mt 25): moeda permanente para o Templo
        lastTalentsEarned = Meta.TalentsForRun(Score, bossesDefeated, level, finalBeaten);
        Meta.Talents += lastTalentsEarned;
    }

    // ================================================================== loop

    static bool ConfirmPressed()
    {
        var kb = Keyboard.current;
        var ms = Mouse.current;
        var gp = Gamepad.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)) return true;
        if (ms != null && ms.leftButton.wasPressedThisFrame) return true;
        if (gp != null && (gp.startButton.wasPressedThisFrame || gp.buttonSouth.wasPressedThisFrame)) return true;
        if (RunnerTouch.Tap) return true;
        return false;
    }

    static bool PausePressed()
    {
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame)) return true;
        if (gp != null && gp.startButton.wasPressedThisFrame) return true;
        if (RunnerTouch.Pressed("pause")) return true;
        return false;
    }

    void Update()
    {
        RunnerTouch.Update();
        // botões de toque registrados pela UI (menu, templo, fim de jogo)
        foreach (var kv in new List<KeyValuePair<string, System.Action>>(uiActions))
            if (RunnerTouch.Pressed(kv.Key)) { kv.Value(); break; }
        var kbAny = Keyboard.current;
        if (kbAny != null && kbAny.anyKey.wasPressedThisFrame) RunnerTouch.NotifyNonTouchInput();
        if (kbAny != null && (kbAny.vKey.wasPressedThisFrame || kbAny.f1Key.wasPressedThisFrame)) TogglePS1();
        if (RunnerTouch.Pressed("ps1")) TogglePS1();
        if (kbAny != null && kbAny.mKey.wasPressedThisFrame) ToggleMusic();
        UpdateMusic();
        SyncHorse();
        CheckScreenshotKey();
        float dt = Time.deltaTime;
        float udt = Time.unscaledDeltaTime;
        if (popupTime > 0) popupTime -= udt;
        if (bannerTime > 0f && state == RunnerState.Playing) bannerTime -= udt;
        UpdateFloats(udt);
        UpdatePerf(udt);
        UpdateSkyFlash(udt);
        if (trailerActive && UpdateTrailer(udt)) return;   // abertura: o clique que pula não inicia o jogo

        switch (state)
        {
            case RunnerState.Menu:
            {
                Meta.DailyMode = false;
                if (menuPanel == 0 && MenuBackPressed()) break;
                string mg = MenuMinigamePressed();
                if (mg != null) { menuPanel = 0; StartMinigame(mg); }
                else if (menuPanel != 0)
                {
                    var kbx = Keyboard.current;
                    if ((kbx != null && kbx.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)) menuPanel = 0;
                }
                else if (!MouseOverUI() && ConfirmPressed()) { StartRun(); StartIntro(); }
                var kbm = Keyboard.current;
                if (kbm != null && (kbm.leftArrowKey.wasPressedThisFrame || kbm.aKey.wasPressedThisFrame)) Meta.Cycle(-1);
                if (kbm != null && (kbm.rightArrowKey.wasPressedThisFrame || kbm.dKey.wasPressedThisFrame)) Meta.Cycle(1);
                if (kbm != null && kbm.tKey.wasPressedThisFrame) state = RunnerState.Temple;
                break;
            }

            case RunnerState.Temple:
            {
                var kbt = Keyboard.current;
                if ((kbt != null && kbt.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
                    state = RunnerState.Menu;
                if (templeMsgTime > 0f) templeMsgTime -= udt;
                break;
            }

            case RunnerState.Paused:
            {
                var kbp = Keyboard.current;
                if (kbp != null && kbp.hKey.wasPressedThisFrame) ToggleHints();
                if (kbp != null && kbp.qKey.wasPressedThisFrame) { RequestQuitToMenu(); break; }
            }
                if (devTools)
                {
                    if (RunnerTouch.Pressed("dev_boss3")) { DevSkipToBoss(3); break; }
                    if (RunnerTouch.Pressed("dev_egypt")) { DevSkipToBiome(Biomes.Egypt); break; }
                    if (RunnerTouch.Pressed("dev_rome")) { DevSkipToBiome(Biomes.Rome); break; }
                    if (RunnerTouch.Pressed("dev_sheol")) { DevSkipToBiome(Biomes.Sheol); break; }
                    if (RunnerTouch.Pressed("dev_satan")) { DevSkipToBoss(finalBossNumber); break; }
                }
                if (PausePressed() || RunnerTouch.Tap)
                {
                    state = RunnerState.Playing;
                    player.inputLock = 0.1f;
                }
                break;

            case RunnerState.LevelUp:
                if (chooseIdx >= 0)
                {
                    chooseT += udt;
                    if (chooseT >= ChooseAnimTime) { int ci = chooseIdx; chooseIdx = -1; ApplyCardChoice(ci); }
                    break;
                }
                HandleCardInput();
                break;

            case RunnerState.Choice:
                HandleChoiceInput();
                break;

            case RunnerState.Cutscene:
                UpdateCinema(udt);
                break;

            case RunnerState.Stable:
                UpdateStable(udt);
                break;

            case RunnerState.Deck:
                UpdateDeckEditor(udt);
                break;

            case RunnerState.Codex:
                UpdateCodex();
                break;

            case RunnerState.GameOver:
                if (Time.unscaledTime - gameOverTime > 0.8f && !MouseOverUI() && ConfirmPressed()) StartRun();
                break;

            case RunnerState.Playing:
                if (PausePressed())
                {
                    state = RunnerState.Paused;
                    break;
                }
                if (bulletHell)
                {
                    if (btActive > 0f) btActive -= udt;
                    else if (btCooldownLeft > 0f) btCooldownLeft -= udt;
                    hell.Tick(dt);
                    if (bulletHell) UpdateTrack();   // a cidade continua passando lá embaixo
                    break;
                }
                if (babel)
                {
                    if (btActive > 0f) btActive -= udt;
                    else if (btCooldownLeft > 0f) btCooldownLeft -= udt;
                    babelGame.Tick(dt);
                    break;
                }
                if (goliath)
                {
                    goliathGame.Tick(dt);
                    break;
                }
                runTime += dt;
                speed = Mathf.Min(maxSpeed, startSpeed + accel * runTime) * stats.runSpeedMul * (player.flying ? 1.1f * SkySpeedMul : 1f);
                if (racing) speed = player.carSpeed;
                if (passover) speed *= 0.85f;
                if (jericho) speed = jerichoSpeed;
                distanceScore += speed * dt * 0.5f * stats.scoreMul;

                // escudo
                if (stats.shieldLevel > 0 && !shieldReady)
                {
                    shieldTimer -= dt;
                    if (shieldTimer <= 0f)
                    {
                        shieldReady = true;
                        Play(sShield, 0.6f);
                    }
                }
                // tempo bala (conta em tempo real)
                if (btActive > 0f) btActive -= udt;
                else if (btCooldownLeft > 0f) btCooldownLeft -= udt;

                UpdateTrack();
                UpdateBlessings(dt);
                UpdateRules(dt);
                UpdatePacing(dt);
                UpdateChampion(dt);
                UpdateRelics(dt);
                UpdateDarkness(dt);
                UpdateHazards();
                if (bossPending)
                {
                    bossWarn -= dt;
                    if (bossWarn <= 0f) SpawnBoss();
                }
                else if (flightPending)
                {
                    flightWarn -= dt;
                    if (flightWarn <= 0f) StartFlight();
                }
                else if (player.flying && skyTransit)
                {
                    UpdateSkyTransit(dt);   // Travessia dos Céus entre regiões
                }
                else if (player.flying)
                {
                    flightTime -= dt;
                    if (flightTime > 4f) SpawnAhead(true);   // últimos segundos: céu limpo para pousar
                    if (flightTime <= 0f) EndFlight();
                }
                else if (racing)
                {
                    UpdateRace();
                }
                else if (passover)
                {
                    UpdatePassover(dt);
                }
                else if (jericho)
                {
                    UpdateJericho(dt);
                }
                else if (boss == null)
                {
                    if (planeBoxTimer > 0f) planeBoxTimer -= dt;
                    SpawnAhead(false);
                }
                CheckPlayerCollisions();
                Cleanup();

                if (state == RunnerState.Playing && !BossFight && !FlightEvent && Score >= nextCardScore) OpenCardChoice(false);
                break;
        }

        player.shieldVisual.gameObject.SetActive(state != RunnerState.GameOver && stats.shieldLevel > 0 && shieldReady);
        if (player.shieldVisual.gameObject.activeSelf) player.shieldVisual.Rotate(0f, 180f * udt, 0f);

        float ts = 1f;
        if (state == RunnerState.Paused || state == RunnerState.LevelUp || state == RunnerState.Choice) ts = 0f;
        else if (BulletTimeActive) ts = 0.4f;
        Time.timeScale = ts;
    }

    void LateUpdate()
    {
        if (player == null) return;
        if (state == RunnerState.Cutscene) return;   // a câmera é da cena animada
        if (state == RunnerState.Stable) { StableCamera(); return; }
        if (bulletHell && hell != null && hell.Active) { hell.UpdateCamera(); return; }
        if (babel && babelGame != null && babelGame.Active) { babelGame.UpdateCamera(); return; }
        if (goliath && goliathGame != null && goliathGame.Active) { goliathGame.UpdateCamera(); return; }
        float dt = Time.unscaledDeltaTime;
        var p = player.transform.position;
        var cp = cam.transform.position;
        float k = 1f - Mathf.Exp(-8f * dt);
        if (racing && player.driving)
        {
            // câmera atrás do carro, alinhada com a estrada
            float kc = 1f - Mathf.Exp(-7f * dt);
            float camZ = p.z - 7.5f;
            float camX = RoadX(camZ) + player.roadOffset * 0.85f;
            cp.x = Mathf.Lerp(cp.x, camX, kc);
            cp.y = Mathf.Lerp(cp.y, 2.8f, kc);
            cp.z = camZ;
            cam.transform.position = cp;
            float az = p.z + 18f;
            cam.transform.LookAt(new Vector3(RoadX(az) + player.roadOffset * 0.6f, 0.9f, az));
        }
        else if (player.flying)
        {
            float kf = 1f - Mathf.Exp(-5f * dt);
            cp.x = Mathf.Lerp(cp.x, p.x * 0.75f, kf);
            cp.y = Mathf.Lerp(cp.y, 3.2f + p.y * 0.75f, kf);
            cp.z = p.z - 10f;
            cam.transform.position = cp;
            cam.transform.LookAt(new Vector3(p.x * 0.6f, p.y * 0.85f + 0.6f, p.z + 12f));
        }
        else
        {
            cp.x = Mathf.Lerp(cp.x, p.x * 0.6f, k);
            cp.y = Mathf.Lerp(cp.y, 4.2f + player.feetY * 0.4f, k);
            cp.z = p.z - 8f;
            cam.transform.position = cp;
            cam.transform.LookAt(new Vector3(p.x * 0.4f, 1.2f, p.z + 10f));
        }

        float fov = 62f + (speed - startSpeed) * 0.5f + (player.flying ? 6f : 0f) + (racing ? 4f : 0f);
        if (BulletTimeActive) fov -= 8f;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, AspectFov(fov), k);

        // telas estreitas (celular em pé, tablets 4:3): afasta a câmera para caber as 3 faixas
        float aspect = cam.aspect;
        if (aspect < 1.5f) cam.transform.position -= cam.transform.forward * (1.5f - aspect) * 6f;

        if (shake > 0f && state != RunnerState.Paused && state != RunnerState.LevelUp)
        {
            cam.transform.position += Random.insideUnitSphere * shake * 0.5f;
            shake = Mathf.Max(0f, shake - dt * 2.5f);
        }
    }

    /// Converte um FOV vertical pensado para 16:9 de forma que a largura visível
    /// seja parecida em qualquer proporção de tela (21:9, 16:9, 4:3, em pé...).
    float AspectFov(float vFov169)
    {
        float aspect = Mathf.Max(0.3f, cam.aspect);
        float h = 2f * Mathf.Atan(Mathf.Tan(vFov169 * 0.5f * Mathf.Deg2Rad) * (16f / 9f));
        float v = 2f * Mathf.Atan(Mathf.Tan(h * 0.5f) / aspect) * Mathf.Rad2Deg;
        return Mathf.Clamp(v, vFov169 * 0.85f, 100f);
    }

    // ================================================================== cards

    /// Sorteia as cartas e, se alguma evolução estiver pronta, ela aparece garantida na primeira posição.
    List<RunnerCard> RollOfferWithEvolution(int n)
    {
        // nível normal: saca do baralho; recompensa (chefe, loja, altar...): coleção + uma carta NOVA
        var o = offerIsBoss ? RollGoodOffer(n) : DrawOffer(n);
        foreach (var evo in Evolutions.All)
        {
            if (!evo.Ready(this)) continue;
            if (o.Count >= n && o.Count > 0) RemoveFromOffer(o, o.Count - 1);
            o.Insert(0, evo.Card);
            break;
        }
        return o;
    }

    void OpenCardChoice(bool bossReward)
    {
        offerIsBoss = bossReward;
        offer = RollOfferWithEvolution(ChoiceCount());
        if (offer.Count == 0)
        {
            if (bossReward) { prevCardScore = Score; nextCardScore = Score + CardStep(); RunAfterCard(); return; }
            prevCardScore = nextCardScore;
            nextCardScore += CardStep();
            return;
        }
        state = RunnerState.LevelUp;
        chooseIdx = -1;
        rerollsLeft = stats.rerolls;
        selected = 0;
        levelUpOpenTime = Time.unscaledTime;
        Play(sLevel, 0.8f);
    }

    bool CardInputReady => Time.unscaledTime - levelUpOpenTime > 0.45f;

    void HandleCardInput()
    {
        if (!CardInputReady) return;
        if (RunnerTouch.Pressed("reroll")) { Reroll(); return; }
        if (RunnerTouch.Tap)
        {
            Vector2 tp = RunnerTouch.TapPos - guiOffset;
            for (int i = 0; i < cardRects.Count && i < offer.Count; i++)
            {
                if (cardRects[i].Contains(tp))
                {
                    if (selected == i) { ChooseCard(i); return; }   // 2º toque confirma
                    selected = i;
                    if (RunnerTouch.UseTouchUI) { ChooseCard(i); return; }
                }
            }
        }
        var kb = Keyboard.current;
        var gp = Gamepad.current;

        if (kb != null)
        {
            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key };
            var pads = new[] { kb.numpad1Key, kb.numpad2Key, kb.numpad3Key, kb.numpad4Key, kb.numpad5Key };
            for (int i = 0; i < offer.Count && i < digits.Length; i++)
            {
                if (digits[i].wasPressedThisFrame || pads[i].wasPressedThisFrame)
                {
                    ChooseCard(i);
                    return;
                }
            }
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) selected = Mathf.Max(0, selected - 1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) selected = Mathf.Min(offer.Count - 1, selected + 1);
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) { ChooseCard(selected); return; }
            if (kb.rKey.wasPressedThisFrame) Reroll();
        }
        if (gp != null)
        {
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame) selected = Mathf.Max(0, selected - 1);
            if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame) selected = Mathf.Min(offer.Count - 1, selected + 1);
            if (gp.buttonSouth.wasPressedThisFrame) { ChooseCard(selected); return; }
            if (gp.buttonWest.wasPressedThisFrame) Reroll();
        }
    }

    void Reroll()
    {
        if (rerollsLeft <= 0) return;
        rerollsLeft--;
        offer = RollOfferWithEvolution(ChoiceCount());
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, offer.Count - 1));
        Sfx("embaralhar", 0.8f);
    }

    int chooseIdx = -1;
    float chooseT;
    const float ChooseAnimTime = 0.55f;

    /// Escolher uma carta: primeiro a animação (a carta voa para o centro), depois o efeito.
    void ChooseCard(int i)
    {
        if (state != RunnerState.LevelUp || chooseIdx >= 0 || i < 0 || i >= offer.Count) return;
        if (offer[i].plague && offer.Exists(c => !c.plague))
        {
            ShowPopup("PRAGA: NÃO PODE SER ESCOLHIDA");
            Sfx("clique", 0.5f, 0.1f, 0.05f);
            return;
        }
        chooseIdx = i;
        chooseT = 0f;
        selected = i;
        Sfx("laminas", 0.6f, 0.05f, 0.1f);
    }

    void ApplyCardChoice(int i)
    {
        if (state != RunnerState.LevelUp || i < 0 || i >= offer.Count) return;
        var card = offer[i];
        if (!card.plague)
        {
            cardStacks[card.id] = Stacks(card) + 1;
            history.Add(card);
            card.apply(this);
            ApplyExtraCopies(card);   // Gideão / carta ungida
        }
        ResolveDeckChoice(card, offerIsBoss);
        offerHasNova = false;
        OnCardGained(card);
        if (!offerIsBoss) TickPlenty();   // descarte, carta nova liberada, recompensa entra no baralho
        if (stats.dailyBread > 0) Heal(stats.dailyBread);   // Pão Diário
        if (card.isEvolution)
        {
            bannerText = "EVOLUÇÃO!";
            bannerSub = card.name;
            bannerTime = 3.5f;
            shake = 0.5f;
            FxSphere(player.transform.position, 3f, CardDB.EvolutionColor);
            Sfx("evolucao", 1f, 0f, 1f);
        }
        else Sfx("carta", 0.8f, 0.03f);
        maxLives = Mathf.Clamp(maxLives, 1, hardMaxLives);
        lives = Mathf.Min(lives, maxLives);

        level++;
        if (offerIsBoss)
        {
            prevCardScore = Score;
            nextCardScore = Score + CardStep();
        }
        else
        {
            prevCardScore = nextCardScore;
            nextCardScore += CardStep();
            if (bossEveryLevels > 0 && level % bossEveryLevels == 0)
            {
                bossPending = true;
                bossWarn = bossWarningTime;
                Play(sHurt, 0.7f);
            }
        }
        offerIsBoss = false;

        state = RunnerState.Playing;
        player.inputLock = 0.3f;
        player.invuln = Mathf.Max(player.invuln, 0.8f);
        ShowPopup(card.name.ToUpper());
        PlayCardFx(card);
        RunAfterCard();   // relíquia, caminho, voltar à loja...
    }

    // ================================================================== spawning

    void SpawnAhead(bool flight)
    {
        float pz = player.transform.position.z;
        while (nextSpawnZ < pz + 120f)
        {
            // limite de objetos na pista ao mesmo tempo (mantém o FPS estável)
            if (obstacles.Count >= MaxObstacles) { nextSpawnZ += 6f; continue; }
            if (flight) { if (skyTransit) SpawnSkyRow(nextSpawnZ); else SpawnFlightRow(nextSpawnZ); }
            else SpawnRow(nextSpawnZ);
            nextSpawnZ += Mathf.Max(8.5f, speed * Random.Range(0.85f, 1.3f) * Mathf.Lerp(0.95f, 0.66f, Difficulty) / (1f + Mathf.Min(LevelThreat, 4.5f) * 0.24f)) * (flight ? 1f : SectionSpacing);
        }
    }

    int[] ShuffledLanes()
    {
        int[] l = { 0, 1, 2 };
        for (int i = 2; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = l[i]; l[i] = l[j]; l[j] = tmp;
        }
        return l;
    }

    void SpawnRow(float z)
    {
        SpawnRowBase(z);
        // níveis altos: às vezes vem uma segunda leva logo atrás
        if ((LevelThreat > 0.5f && Random.value < Mathf.Min(0.45f, LevelThreat * 0.18f))
            || (PacingActive && section == Section.Emboscada && Random.value < 0.2f))   // emboscada: segunda leva
        {
            int lane = Random.Range(0, 3);
            float zz = z + Random.Range(5f, 8f);
            float r = Random.value;
            if (r < 0.5f) SpawnTarget(lane, zz);
            else if (r < 0.75f) SpawnTurret(lane, zz);
            else SpawnMover(zz);
        }
    }

    void SpawnRowBase(float z)
    {
        float d = Difficulty;
        var lanes = ShuffledLanes();

        // caixa de avião: aparece de vez em quando, o jogador escolhe se pega
        if (firstPlaneBoxTime >= 0f && planeBoxTimer <= 0f && Random.value < 0.2f)
        {
            float pick = Random.value;
            if (enableAngelBox && pick < 0.14f) SpawnAngelBox(lanes[0], z);
            else if (enableShipBox && pick < 0.28f) SpawnShipBox(lanes[0], z);
            else if (enableBabelBox && pick < 0.42f) SpawnBabelBox(lanes[0], z);
            else if (enableJerichoBox && pick < 0.56f) SpawnJerichoBox(lanes[0], z);
            else if (enableGoliathBox && pick < 0.7f) SpawnGoliathBox(lanes[0], z);
            else if (enableCarBox && pick < 0.85f) SpawnCarBox(lanes[0], z);
            else SpawnPlaneBox(lanes[0], z);
            if (d > 0.2f && Random.value < 0.6f) SpawnTarget(lanes[1], z);
            planeBoxTimer = planeBoxInterval;
            return;
        }

        // trecho de descanso: siclos e caravanas
        if (SpawnCalmRow(z, lanes)) return;

        if (lives < maxLives && !Meta.OathOn("jejum") && Random.value < 0.03f)
        {
            SpawnHealth(lanes[0], z);
            return;
        }

        // caravana: carroças compridas para correr lá em cima
        if (TrySpawnPlatforms(z, lanes)) return;

        // obstáculos novos (pisão, indestrutíveis, fundibulários, saltadores)
        if (Random.value < HazardChance)
        {
            SpawnHazardRow(z, lanes);
            return;
        }

        float r = Random.value;
        if (r < 0.17f)
        {
            int count = (d > 0.3f && Random.value < 0.5f) ? 2 : 1;
            for (int i = 0; i < count; i++) SpawnWall(lanes[i], z);
            if (count == 1 && d > 0.15f && Random.value < 0.6f) SpawnTarget(lanes[1], z);
        }
        else if (r < 0.28f)
        {
            for (int l = 0; l < 3; l++) SpawnBarrierLane(l, z);
            if (d > 0.35f && Random.value < 0.5f) SpawnTarget(lanes[0], z + 4f);
        }
        else if (r < 0.52f)
        {
            int n = Random.Range(1, d > 0.25f ? 4 : 3);
            for (int i = 0; i < n; i++) SpawnTarget(lanes[i], z);
        }
        else if (r < 0.64f)
        {
            SpawnMover(z);
            if (d > 0.5f && Random.value < 0.5f) SpawnMover(z + 3f);
        }
        else if (r < 0.76f && d > 0.08f)
        {
            SpawnTurret(lanes[0], z);
            if (d > 0.4f && Random.value < 0.5f) SpawnWall(lanes[1], z);
        }
        else if (r < 0.86f && d > 0.15f)
        {
            SpawnTank(lanes[0], z);
            if (d > 0.45f && Random.value < 0.5f) SpawnTarget(lanes[1], z);
        }
        else
        {
            SpawnWall(lanes[0], z);
            SpawnBarrierLane(lanes[1], z);
            SpawnTarget(lanes[2], z);
        }
    }

    RunnerObstacle MakeObstacle(string name, ObType type, Vector3 pos, Vector3 half, float hp, int points, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var o = go.AddComponent<RunnerObstacle>();
        o.type = type;
        o.half = half;
        o.hp = hp;
        o.points = points;
        o.mainColor = color;
        obstacles.Add(o);
        return o;
    }

    public void SpawnWall(int lane, float z)
    {
        // muralha da Babilônia (azul vitrificado com leão dourado) — desvie
        var c = BabylonBlue;
        var o = MakeObstacle("Wall", ObType.Wall, new Vector3(LaneX(lane), 1.5f, z), new Vector3(1.3f, 1.5f, 0.5f), 9999f, 0, c);
        Prim(PrimitiveType.Cube, o.transform, Vector3.zero, new Vector3(2.6f, 3f, 1f), c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, -1.2f, 0f), new Vector3(2.64f, 0.6f, 1.04f), StoneColors[2]);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.75f, -0.51f), new Vector3(2.6f, 0.12f, 0.04f), Gold);
        // leão dourado na frente
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.05f, -0.52f), new Vector3(1.2f, 0.45f, 0.04f), Gold);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0.65f, 0.3f, -0.52f), new Vector3(0.35f, 0.35f, 0.04f), Gold);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(-0.4f, -0.3f, -0.52f), new Vector3(0.12f, 0.3f, 0.04f), Gold);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0.4f, -0.3f, -0.52f), new Vector3(0.12f, 0.3f, 0.04f), Gold);
        // ameias
        for (int i = -1; i <= 1; i++)
            Prim(PrimitiveType.Cube, o.transform, new Vector3(i * 0.9f, 1.72f, 0f), new Vector3(0.55f, 0.45f, 1f), c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0, 1.48f, -0.02f), new Vector3(2.65f, 0.08f, 1.05f), Gold, true);
        o.Init();
    }

    public void SpawnBarrierLane(int lane, float z)
    {
        // coluna de templo caída — pule por cima
        var c = T.barrierMain;
        var o = MakeObstacle("Barrier", ObType.Barrier, new Vector3(LaneX(lane), 0.45f, z), new Vector3(1.4f, 0.45f, 0.25f), 9999f, 0, c);
        var col = Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, -0.02f, 0f), new Vector3(0.82f, 1.25f, 0.82f), c);
        col.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int s2 = -1; s2 <= 1; s2 += 2)
            Prim(PrimitiveType.Cube, o.transform, new Vector3(s2 * 1.25f, -0.02f, 0f), new Vector3(0.3f, 0.92f, 0.92f), StoneColors[1]);
        for (int k = -2; k <= 2; k++)
            Prim(PrimitiveType.Cube, o.transform, new Vector3(k * 0.4f, 0.4f, 0f), new Vector3(0.05f, 0.04f, 0.5f), new Color(0.6f, 0.55f, 0.48f));
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.43f, -0.1f), new Vector3(2.2f, 0.04f, 0.1f), new Color(1f, 0.55f, 0.15f), true);
        o.Init();
    }

    /// Ídolo de barro (ou de ouro) sobre um pedestal, virado para o jogador (-Z).
    void BuildTargetVisual(Transform t, Color outer, float scale)
    {
        float s = scale;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -1.2f * s, 0f), new Vector3(1f, 0.35f, 0.7f) * s, T.pedestal);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.55f * s, 0f), new Vector3(0.75f, 0.95f, 0.45f) * s, outer);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.45f * s, 0f), new Vector3(0.78f, 0.12f, 0.48f) * s, outer * 0.6f);
        for (int side = -1; side <= 1; side += 2)
        {
            var arm = Prim(PrimitiveType.Cube, t, new Vector3(side * 0.48f * s, -0.5f * s, -0.05f * s), new Vector3(0.18f, 0.6f, 0.2f) * s, outer);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 10f);
        }
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.18f * s, 0f), new Vector3(0.5f, 0.45f, 0.42f) * s, outer);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.0f, -0.22f * s), new Vector3(0.4f, 0.3f, 0.1f) * s, T.idolBeard);   // barba
        if (T.id == 3)
        {
            // orelhas de chacal (Anúbis) e colar largo de ouro e turquesa
            for (int hs = -1; hs <= 1; hs += 2)
            {
                var ear = Prim(PrimitiveType.Cube, t, new Vector3(hs * 0.16f * s, 0.62f * s, 0.02f * s), new Vector3(0.1f, 0.42f, 0.08f) * s, T.idol);
                ear.transform.localRotation = Quaternion.Euler(0f, 0f, hs * -12f);
            }
            Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.12f * s, -0.24f * s), new Vector3(0.62f, 0.14f, 0.06f) * s, T.metal, true);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.24f * s, -0.24f * s), new Vector3(0.56f, 0.08f, 0.06f) * s, T.accent);
        }
        if (T.id == 2)
        {
            // chifres de demônio no Sheol
            for (int hs = -1; hs <= 1; hs += 2)
            {
                var horn = Prim(PrimitiveType.Cube, t, new Vector3(hs * 0.22f * s, 0.55f * s, 0f), new Vector3(0.08f, 0.35f, 0.08f) * s, Gold, true);
                horn.transform.localRotation = Quaternion.Euler(0f, 0f, hs * -25f);
            }
        }
        Prim(PrimitiveType.Cube, t, new Vector3(-0.12f * s, 0.25f * s, -0.22f * s), new Vector3(0.1f, 0.06f, 0.05f) * s, new Color(1f, 0.2f, 0.1f), true);
        Prim(PrimitiveType.Cube, t, new Vector3(0.12f * s, 0.25f * s, -0.22f * s), new Vector3(0.1f, 0.06f, 0.05f) * s, new Color(1f, 0.2f, 0.1f), true);
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 0.55f * s, 0f), new Vector3(0.42f, 0.18f, 0.42f) * s, Gold);              // coroa
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.8f * s, 0f), new Vector3(0.1f, 0.2f, 0.1f) * s, Gold);
    }

    bool RollElite() => Random.value < Mathf.Min(0.35f, 0.02f + Difficulty * 0.15f + LevelThreat * 0.06f);

    void SpawnTarget(int lane, float z)
    {
        bool elite = RollElite();
        float s = elite ? 1.3f : 1f;
        var c = elite ? new Color(1f, 0.8f, 0.1f) : Clay;
        var o = MakeObstacle(elite ? "EliteTarget" : "Target", ObType.Target, new Vector3(LaneX(lane), 1.4f, z),
            new Vector3(0.85f, 0.85f, 0.3f) * s, (elite ? 3f : 1f) * HpMul, elite ? 150 : 50, c);
        o.elite = elite;
        BuildTargetVisual(o.transform, c, s);
        if (elite) Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0, 0, 0.05f), new Vector3(2.4f, 0.04f, 2.4f), new Color(1f, 0.9f, 0.3f), true).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        o.Init();
    }

    void SpawnMover(float z)
    {
        // ídolo num carrinho de madeira
        var c = T.moverIdol;
        var o = MakeObstacle("MovingTarget", ObType.Mover, new Vector3(0, 1.4f, z), new Vector3(0.85f, 0.85f, 0.3f), 2f * HpMul, 100, c);
        BuildTargetVisual(o.transform, c, 1f);
        var wood = new Color(0.4f, 0.26f, 0.14f);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, -1.3f, 0f), new Vector3(1.2f, 0.12f, 0.8f), wood);
        for (int sx = -1; sx <= 1; sx += 2)
            Prim(PrimitiveType.Cylinder, o.transform, new Vector3(sx * 0.62f, -1.2f, 0f), new Vector3(0.42f, 0.05f, 0.42f), wood).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        o.moveFreq = Mathf.Lerp(1.6f, 3f, Difficulty);
        o.Init();
    }

    void SpawnTurret(int lane, float z)
    {
        // altar de fogo de Baal: cospe bolas de fogo
        var c = new Color(1f, 0.45f, 0.1f);
        var o = MakeObstacle("Turret", ObType.Turret, new Vector3(LaneX(lane), 1f, z), new Vector3(0.75f, 1f, 0.75f), 3f * HpMul, 150, c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0, -0.5f, 0), new Vector3(1.4f, 1f, 1.4f), StoneColors[3]);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0, -0.3f, 0), new Vector3(1.45f, 0.18f, 1.45f), BabylonBlue);
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            Prim(PrimitiveType.Cube, o.transform, new Vector3(sx * 0.6f, 0.08f, sz * 0.6f), new Vector3(0.22f, 0.25f, 0.22f), Gold); // chifres do altar
        var head = Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0, 0.12f, 0), new Vector3(1.0f, 0.18f, 1.0f), new Color(0.65f, 0.4f, 0.18f));
        Prim(PrimitiveType.Sphere, head.transform, new Vector3(0, 2.6f, 0), new Vector3(0.75f, 4.5f, 0.75f), c, true);
        Prim(PrimitiveType.Cube, head.transform, new Vector3(0, 0.5f, -0.6f), new Vector3(0.25f, 1.4f, 0.55f), new Color(0.65f, 0.4f, 0.18f));
        o.head = head.transform;
        o.fireTimer = Random.Range(0.2f, 0.8f);
        o.Init();
    }

    void SpawnTank(int lane, float z)
    {
        // lamassu: touro alado com cabeça humana, guardião da Babilônia (avança devagar)
        var c = T.tankBody;
        var o = MakeObstacle("Tank", ObType.Tank, new Vector3(LaneX(lane), 1.2f, z), new Vector3(1.2f, 1.2f, 0.8f), 9f * HpMul, 300, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.05f, 0.1f), new Vector3(1.6f, 1.1f, 1.6f), c);
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sz = -1; sz <= 1; sz += 2)
            Prim(PrimitiveType.Cube, t, new Vector3(sx * 0.55f, -0.85f, sz * 0.55f), new Vector3(0.35f, 0.7f, 0.35f), c * 0.9f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var wing = Prim(PrimitiveType.Cube, t, new Vector3(sx * 0.88f, 0.4f, 0.35f), new Vector3(0.14f, 1.2f, 1.4f), c * 0.95f);
            wing.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, wing.transform, new Vector3(sx * 0.55f, 0.3f - k * 0.3f, 0f), new Vector3(0.4f, 0.07f, 0.9f), Gold);
        }
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.7f, -0.7f), new Vector3(0.8f, 0.75f, 0.7f), c);                                // cabeça
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.35f, -1.05f), new Vector3(0.7f, 0.6f, 0.2f), T.tankBeard);  // barba
        Prim(PrimitiveType.Cylinder, t, new Vector3(0f, 1.2f, -0.7f), new Vector3(0.8f, 0.3f, 0.8f), Gold);                          // coroa
        Prim(PrimitiveType.Cube, t, new Vector3(-0.18f, 0.82f, -1.06f), new Vector3(0.14f, 0.08f, 0.05f), new Color(1f, 0.2f, 0.1f), true);
        Prim(PrimitiveType.Cube, t, new Vector3(0.18f, 0.82f, -1.06f), new Vector3(0.14f, 0.08f, 0.05f), new Color(1f, 0.2f, 0.1f), true);
        o.Init();
    }

    public void SpawnEnemyShot(Vector3 pos)
    {
        SpawnEnemyShot(pos, new Vector3(0f, 0f, -(12f + Mathf.Min(LevelThreat, 4.5f) * 3f)), 0f);
    }

    public RunnerObstacle SpawnEnemyShot(Vector3 pos, Vector3 velocity, float homing)
    {
        var c = homing > 0f ? new Color(1f, 0.3f, 0.8f) : new Color(1f, 0.45f, 0.1f);
        var o = MakeObstacle("EnemyShot", ObType.EnemyShot, pos, new Vector3(0.35f, 0.35f, 0.35f), 0.5f, 10, c);
        Prim(PrimitiveType.Sphere, o.transform, Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), c, true);
        o.velocity = velocity;
        o.homingShot = homing;
        o.Init();
        Play(sEnemyShot, 0.3f);
        return o;
    }

    public RunnerObstacle SpawnRocket(Vector3 pos, Vector3 velocity)
    {
        var c = new Color(0.9f, 0.1f, 0.1f);
        var o = MakeObstacle("Rocket", ObType.EnemyShot, pos, new Vector3(1.0f, 0.55f, 0.7f), 2.5f * HpMul, 40, c);
        var body = Prim(PrimitiveType.Cylinder, o.transform, Vector3.zero, new Vector3(0.6f, 0.7f, 0.6f), new Color(0.25f, 0.25f, 0.28f));
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Prim(PrimitiveType.Sphere, o.transform, new Vector3(0, 0, -0.7f), new Vector3(0.6f, 0.6f, 0.6f), c, true);
        Prim(PrimitiveType.Sphere, o.transform, new Vector3(0, 0, 0.75f), new Vector3(0.45f, 0.45f, 0.45f), new Color(1f, 0.7f, 0.2f), true);
        o.velocity = velocity;
        o.Init();
        Play(sEnemyShot, 0.6f);
        return o;
    }

    public RunnerObstacle SpawnBossDrone(Vector3 pos)
    {
        var c = new Color(0.8f, 0.15f, 0.25f);
        var o = MakeObstacle("BossDrone", ObType.BossDrone, pos, new Vector3(0.45f, 0.35f, 0.45f), 4f * HpMul, 80, c);
        Prim(PrimitiveType.Cube, o.transform, Vector3.zero, new Vector3(0.7f, 0.3f, 0.7f), new Color(0.15f, 0.12f, 0.15f));
        Prim(PrimitiveType.Sphere, o.transform, new Vector3(0, 0.05f, -0.2f), new Vector3(0.35f, 0.35f, 0.35f), c, true);
        o.Init();
        return o;
    }

    // ================================================================== voo livre

    void StartFlight()
    {
        flightPending = false;
        flightTime = flightDuration;
        player.StartFlight();
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        // a decolagem limpa a pista à frente
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Explode(o.transform.position, o.mainColor, 4);
            Destroy(o.gameObject);
        }
        nextSpawnZ = player.transform.position.z + 45f;
        Explode(player.transform.position, new Color(0.6f, 0.9f, 1f), 24);
        FxSphere(player.transform.position, 3f, new Color(0.6f, 0.9f, 1f));
        ShowPopup("CARRO DE FOGO!");
        Play(sLevel, 1f);
        shake = 0.4f;
    }

    void EndFlight()
    {
        player.EndFlight();
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        // remove o que sobrou da fase aérea
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o != null && o.airborne && !o.dead)
            {
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
        Explode(player.transform.position, new Color(0.6f, 0.9f, 1f), 16);
        ShowPopup("POUSO!");
        Play(sShield, 0.8f);
    }

    static Vector3 RandomSkyPos(float z) => new Vector3(Random.Range(-6.5f, 6.5f), Random.Range(1.6f, 9f), z);

    void SpawnFlightRow(float z)
    {
        float d = Difficulty;
        float r = Random.value;

        if (lives < maxLives && !Meta.OathOn("jejum") && Random.value < 0.05f)
        {
            var h = RandomSkyPos(z);
            SpawnHealth(1, z);
            var last = obstacles[obstacles.Count - 1];
            last.transform.position = h;
            last.baseY = h.y;
            last.airborne = true;
            return;
        }

        if (r < 0.2f)
        {
            // trilha de anéis
            var p = RandomSkyPos(z);
            int n = Random.Range(2, 5);
            for (int i = 0; i < n; i++)
            {
                SpawnRing(p + new Vector3(0f, 0f, i * 6f));
                p.x = Mathf.Clamp(p.x + Random.Range(-1.5f, 1.5f), -6.5f, 6.5f);
                p.y = Mathf.Clamp(p.y + Random.Range(-1f, 1f), 1.6f, 9f);
            }
        }
        else if (r < 0.45f)
        {
            int n = Random.Range(2, d > 0.3f ? 6 : 4);
            for (int i = 0; i < n; i++) SpawnMine(RandomSkyPos(z + Random.Range(-3f, 3f)));
        }
        else if (r < 0.65f)
        {
            int n = Random.Range(1, d > 0.4f ? 4 : 3);
            for (int i = 0; i < n; i++) SpawnFlyer(RandomSkyPos(z + i * 4f));
        }
        else if (r < 0.8f)
        {
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++) SpawnPillar(Random.Range(-6.5f, 6.5f), Random.Range(3f, 9.5f), z + i * 5f);
        }
        else
        {
            SpawnGate(z);
        }
    }

    RunnerObstacle SpawnBlock(Vector3 center, Vector3 half, Color c)
    {
        var o = MakeObstacle("SkyBlock", ObType.Wall, center, half, 9999f, 0, c);
        Prim(PrimitiveType.Cube, o.transform, Vector3.zero, half * 2f, c);
        o.airborne = true;
        o.Init();
        return o;
    }

    void SpawnPillar(float x, float h, float z)
    {
        var c = new Color(0.5f, 0.5f, 0.58f);
        var o = SpawnBlock(new Vector3(x, h / 2f, z), new Vector3(0.9f, h / 2f, 0.9f), c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, h / 2f, 0f), new Vector3(1.9f, 0.25f, 1.9f), new Color(1f, 0.3f, 0.25f), true);
    }

    void SpawnGate(float z)
    {
        // parede que cobre o céu inteiro, com um buraco para atravessar
        const float minX = -8f, maxX = 8f, minY = 0f, maxY = 11f;
        float gw = Mathf.Lerp(4.2f, 3.2f, Difficulty), gh = Mathf.Lerp(3.6f, 2.8f, Difficulty);
        float gx = Random.Range(minX + gw / 2f + 1f, maxX - gw / 2f - 1f);
        float gy = Random.Range(1.2f + gh / 2f, maxY - gh / 2f - 1f);
        var c = new Color(0.4f, 0.3f, 0.55f);
        const float hz = 0.4f;

        float lx0 = minX, lx1 = gx - gw / 2f;
        SpawnBlock(new Vector3((lx0 + lx1) / 2f, (minY + maxY) / 2f, z), new Vector3((lx1 - lx0) / 2f, (maxY - minY) / 2f, hz), c);
        float rx0 = gx + gw / 2f, rx1 = maxX;
        SpawnBlock(new Vector3((rx0 + rx1) / 2f, (minY + maxY) / 2f, z), new Vector3((rx1 - rx0) / 2f, (maxY - minY) / 2f, hz), c);
        float by1 = gy - gh / 2f;
        if (by1 > minY + 0.05f)
            SpawnBlock(new Vector3(gx, (minY + by1) / 2f, z), new Vector3(gw / 2f, (by1 - minY) / 2f, hz), c);
        float ty0 = gy + gh / 2f;
        SpawnBlock(new Vector3(gx, (ty0 + maxY) / 2f, z), new Vector3(gw / 2f, (maxY - ty0) / 2f, hz), c);

        SpawnRing(new Vector3(gx, gy, z));
    }

    void SpawnRing(Vector3 pos)
    {
        var c = new Color(1f, 0.85f, 0.2f);
        var o = MakeObstacle("Ring", ObType.Ring, pos, new Vector3(1.3f, 1.3f, 0.4f), 1f, 0, c);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            Prim(PrimitiveType.Cube, o.transform, new Vector3(Mathf.Cos(a) * 1.4f, Mathf.Sin(a) * 1.4f, 0f), new Vector3(0.35f, 0.35f, 0.2f), c, true);
        }
        o.airborne = true;
        o.Init();
    }

    void SpawnMine(Vector3 pos)
    {
        var c = new Color(0.75f, 0.1f, 0.12f);
        var o = MakeObstacle("Mine", ObType.Mine, pos, new Vector3(0.65f, 0.65f, 0.65f), 1.5f * HpMul, 60, c);
        Prim(PrimitiveType.Sphere, o.transform, Vector3.zero, Vector3.one * 1.1f, new Color(0.2f, 0.18f, 0.2f));
        Prim(PrimitiveType.Sphere, o.transform, Vector3.zero, Vector3.one * 0.5f, c, true);
        Vector3[] dirs = { Vector3.up, -Vector3.up, Vector3.right, -Vector3.right, Vector3.forward, -Vector3.forward };
        foreach (var dir in dirs)
            Prim(PrimitiveType.Cube, o.transform, dir * 0.65f, new Vector3(0.18f, 0.18f, 0.18f) + new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z)) * 0.25f, c, true);
        o.baseY = pos.y;
        o.airborne = true;
        o.Init();
    }

    void SpawnFlyer(Vector3 pos)
    {
        var c = new Color(0.9f, 0.2f, 0.15f);
        var o = MakeObstacle("Flyer", ObType.Flyer, pos, new Vector3(1.0f, 0.4f, 0.7f), 2f * HpMul, 120, c);
        Prim(PrimitiveType.Cube, o.transform, Vector3.zero, new Vector3(0.5f, 0.45f, 1.6f), new Color(0.25f, 0.22f, 0.25f));
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0f, 0.1f), new Vector3(2.4f, 0.08f, 0.6f), c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.3f, 0.6f), new Vector3(0.08f, 0.45f, 0.4f), c);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.05f, -0.85f), new Vector3(0.3f, 0.2f, 0.15f), new Color(1f, 0.3f, 0.2f), true);
        o.baseX = pos.x;
        o.baseY = pos.y;
        o.moveFreq = Random.Range(1.2f, 2.2f);
        o.fireTimer = Random.Range(0.5f, 1.5f);
        o.airborne = true;
        o.Init();
    }

    // ================================================================== corrida de carro

    // ================================================================== Noite da Páscoa (Êxodo 12)

    void SpawnAngelBox(int lane, float z)
    {
        var c = new Color(0.8f, 0.08f, 0.08f);
        var o = MakeObstacle("AngelBox", ObType.AngelBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.08f, 0.07f, 0.09f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), c);
        // porta com sangue na frente
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.1f, -0.62f), new Vector3(0.45f, 0.75f, 0.04f), new Color(0.25f, 0.15f, 0.08f));
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.32f, -0.63f), new Vector3(0.6f, 0.08f, 0.04f), c, true);
        for (int sx = -1; sx <= 1; sx += 2)
            Prim(PrimitiveType.Cube, t, new Vector3(sx * 0.27f, -0.1f, -0.63f), new Vector3(0.06f, 0.75f, 0.04f), c, true);
        o.Init();
    }

    /// Anjo destruidor: manto negro, capuz com olhos vermelhos, grandes asas de sombra e uma espada.
    void BuildDeathAngel(Transform root)
    {
        var cloth = new Color(0.05f, 0.05f, 0.07f);
        var body = new GameObject("Body").transform;
        body.SetParent(root, false);
        body.localPosition = new Vector3(0f, 0.3f, 0f);
        Prim(PrimitiveType.Cube, body, new Vector3(0f, -0.1f, 0f), new Vector3(0.7f, 1.3f, 0.55f), cloth);
        var hem = Prim(PrimitiveType.Cube, body, new Vector3(0f, -0.8f, -0.1f), new Vector3(0.8f, 0.3f, 0.75f), cloth);
        hem.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.75f, 0.02f), new Vector3(0.55f, 0.55f, 0.55f), cloth);
        Prim(PrimitiveType.Cube, body, new Vector3(0f, 0.72f, 0.28f), new Vector3(0.38f, 0.36f, 0.04f), Color.black);
        for (int sx = -1; sx <= 1; sx += 2)
            Prim(PrimitiveType.Cube, body, new Vector3(sx * 0.09f, 0.76f, 0.3f), new Vector3(0.08f, 0.04f, 0.03f), new Color(1f, 0.1f, 0.05f), true);
        // espada
        Prim(PrimitiveType.Cube, body, new Vector3(0.45f, 0.1f, 0.5f), new Vector3(0.06f, 0.05f, 1.2f), new Color(0.75f, 0.78f, 0.85f));
        Prim(PrimitiveType.Cube, body, new Vector3(0.45f, 0.1f, -0.1f), new Vector3(0.3f, 0.06f, 0.06f), new Color(0.4f, 0.4f, 0.45f));
        // asas
        var wings = new Transform[2];
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var w = new GameObject("Wing").transform;
            w.SetParent(body, false);
            w.localPosition = new Vector3(sx * 0.3f, 0.45f, -0.25f);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, w, new Vector3(sx * (0.45f + k * 0.35f), -k * 0.08f, -k * 0.12f), new Vector3(0.45f, 0.05f, 1.0f - k * 0.15f), new Color(0.1f, 0.1f, 0.13f) * (1f - k * 0.1f));
            wings[(sx + 1) / 2] = w;
        }
        var flap = root.gameObject.AddComponent<RunnerFlap>();
        flap.left = wings[0];
        flap.right = wings[1];
        flap.body = body;
    }

    void StartPassover()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Destroy(o.gameObject);
        }
        passover = true;
        passoverTime = passoverDuration;
        judged = spared = missed = bloodHits = passCombo = 0;
        player.StartAngel();
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        nextSpawnZ = player.transform.position.z + 35f;

        // noite no Egito
        var night = new Color(0.04f, 0.05f, 0.13f);
        RenderSettings.fogColor = night;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 110f;
        if (cam != null) cam.backgroundColor = night;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { l.color = new Color(0.55f, 0.6f, 1f); l.intensity = 0.45f; }

        bannerText = "A NOITE DA PÁSCOA";
        bannerSub = "Julgue as casas sem sangue; poupe as marcadas pelo cordeiro (Êx 12:23)";
        bannerTime = 4f;
        Play(sHurt, 0.8f);
    }

    void UpdatePassover(float dt)
    {
        passoverTime -= dt;
        float pz = player.transform.position.z;
        if (passoverTime > 3f)
        {
            while (nextSpawnZ < pz + 110f)
            {
                SpawnHouseRow(nextSpawnZ);
                nextSpawnZ += Mathf.Max(12f, speed * Random.Range(0.95f, 1.25f));
            }
        }
        // casas que ficaram para trás
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || o.flag) continue;
            if (o.transform.position.z > pz - 2f) continue;
            if (o.type == ObType.HouseOpen) { o.flag = true; missed++; passCombo = 0; }
            else if (o.type == ObType.HouseBlood) { o.flag = true; spared++; }
        }
        // rastro de sombra do anjo
        if (Random.value < (IsMobile ? 0.25f : 0.5f))
        {
            MeshRenderer goR;
            var go = RunnerGame.NewPrim(PrimitiveType.Cube, out goR, false);
            goR.sharedMaterial = Mat(new Color(0.03f, 0.03f, 0.05f));
            go.transform.position = player.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.6f, 0.6f), -0.6f);
            go.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);
            var d = go.AddComponent<RunnerDebris>();
            d.gravity = false;
            d.velocity = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0.2f, 1f), -1f);
            d.life = 0.5f;
        }
        if (passoverTime <= 0f) EndPassover(true);
    }

    void SpawnHouseRow(float z)
    {
        // pelo menos uma casa com sangue e uma sem, na maioria das fileiras
        bool[] blood = new bool[3];
        int nBlood = Random.value < 0.15f ? 0 : (Random.value < 0.6f ? 1 : 2);
        var lanes = ShuffledLanes();
        for (int i = 0; i < nBlood; i++) blood[lanes[i]] = true;
        for (int l = 0; l < 3; l++)
        {
            if (Random.value < 0.15f && nBlood > 0 && !blood[l]) continue;   // às vezes uma rua vazia
            SpawnHouse(l, z, blood[l]);
        }
    }

    void SpawnHouse(int lane, float z, bool hasBlood)
    {
        var mud = new Color(0.62f, 0.52f, 0.38f) * Random.Range(0.9f, 1.05f);
        var o = MakeObstacle(hasBlood ? "CasaComSangue" : "CasaSemSangue", hasBlood ? ObType.HouseBlood : ObType.HouseOpen,
            new Vector3(LaneX(lane), 1.2f, z), new Vector3(1.0f, 1.2f, 1.0f), 1f, 0, mud);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(2.4f, 2.4f, 2.2f), mud);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.25f, 0f), new Vector3(2.5f, 0.12f, 2.3f), mud * 0.85f);   // telhado plano
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.45f, -1.11f), new Vector3(0.8f, 1.5f, 0.04f), new Color(0.22f, 0.13f, 0.07f));   // porta
        if (hasBlood)
        {
            // sangue do cordeiro na verga e nos umbrais (Êx 12:7)
            var blood = new Color(0.85f, 0.03f, 0.03f);
            Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.38f, -1.13f), new Vector3(1.1f, 0.16f, 0.04f), blood, true);
            for (int sx = -1; sx <= 1; sx += 2)
                Prim(PrimitiveType.Cube, t, new Vector3(sx * 0.48f, -0.45f, -1.13f), new Vector3(0.12f, 1.5f, 0.04f), blood, true);
            // luz de lamparina na janela: a família está protegida lá dentro
            Prim(PrimitiveType.Cube, t, new Vector3(0.75f, 0.35f, -1.11f), new Vector3(0.35f, 0.35f, 0.04f), new Color(1f, 0.75f, 0.3f), true);
        }
        else
        {
            Prim(PrimitiveType.Cube, t, new Vector3(0.75f, 0.35f, -1.11f), new Vector3(0.35f, 0.35f, 0.04f), new Color(0.05f, 0.04f, 0.04f));
        }
        o.airborne = true;
        o.Init();
    }

    void JudgeHouse(RunnerObstacle o)
    {
        o.flag = true;
        judged++;
        passCombo++;
        int pts = Mathf.RoundToInt(80 * Mathf.Min(passCombo, 8) * stats.scoreMul);
        killScore += pts;
        Vector3 p = o.transform.position;
        if (TextMode == 2 || passCombo % 5 == 0) AddFloat(p + Vector3.up * 2.4f, "JULGADA x" + passCombo, new Color(0.75f, 0.5f, 1f), false);
        // a casa escurece e solta uma sombra
        foreach (var r in o.GetComponentsInChildren<Renderer>()) r.sharedMaterial = Mat(new Color(0.12f, 0.1f, 0.12f));
        Explode(p + Vector3.up * 0.5f, new Color(0.08f, 0.05f, 0.1f), 10);
        Sfx("julgamento", 0.7f, 0.08f, 0.1f);
    }

    void EndPassover(bool finished)
    {
        passover = false;
        player.EndAngel();
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o != null && (o.type == ObType.HouseOpen || o.type == ObType.HouseBlood))
            {
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        ApplyAtmosphere();
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
        if (!finished) return;

        int total = judged + missed;
        bool perfect = bloodHits == 0 && total > 0 && judged >= total * 0.7f;
        int bonus = Mathf.RoundToInt((judged * 60 + spared * 40) * stats.scoreMul);
        killScore += bonus;
        AddFloat(player.transform.position + Vector3.up * 2.5f, "+" + bonus, new Color(0.6f, 1f, 0.6f), true);
        if (perfect)
        {
            Heal(1);
            offerTitle = "JULGAMENTO PERFEITO!";
            offerSub = "Nenhuma casa marcada foi tocada. Escolha uma carta rara, épica ou lendária (+1 vida)";
            OpenCardChoice(true);
        }
        else
        {
            ShowPopup("AMANHECEU  —  julgadas " + judged + "/" + total);
        }
    }

    // ================================================================== nave (bullet hell)

    void SpawnShipBox(int lane, float z)
    {
        var c = new Color(0.8f, 0.45f, 1f);
        var o = MakeObstacle("ShipBox", ObType.ShipBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.3f, 0.22f, 0.35f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), Gold);
        // navezinha de luz
        var d = Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.65f, 0f), new Vector3(0.35f, 0.1f, 0.35f), Gold, true);
        d.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.66f, -0.05f), new Vector3(0.9f, 0.06f, 0.2f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.62f), new Vector3(0.7f, 0.15f, 0.05f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.12f, -0.62f), new Vector3(0.2f, 0.4f, 0.05f), c, true);
        o.Init();
    }

    void StartBulletHell()
    {
        // limpa a pista (a arena fica lá no céu)
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Destroy(o.gameObject);
        }
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        bulletHell = true;
        player.SetVisible(false);
        hell.timeLimit = hellTimeLimit;
        hell.Begin(this, hellsCleared);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        ShowPopup("EXPULSE O ESPÍRITO MALIGNO!");
        Play(sLevel, 1f);
    }

    void EndBulletHell()
    {
        hell.End();
        bulletHell = false;
        player.SetVisible(true);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
    }

    public void HellWon(Vector3 at)
    {
        Explode(at, new Color(0.8f, 0.4f, 1f), 40);
        PlayBoom();
        EndBulletHell();
        hellsCleared++;
        int pts = Mathf.RoundToInt(1500 * (1 + hellsCleared * 0.5f) * stats.scoreMul);
        killScore += pts;
        kills++;
        Heal(1);
        shake = 0.8f;
        ShowPopup("ESPÍRITO EXPULSO! +" + pts);
        offerTitle = "ESPÍRITO EXPULSO!";
        offerSub = "Recompensa: escolha uma carta rara, épica ou lendária (+1 vida)";
        OpenCardChoice(true);
    }

    public void HellFailed()
    {
        EndBulletHell();
        ShowPopup("O ESPÍRITO FUGIU...");
        Play(sHurt, 0.6f);
    }

    // ================================================================== Torre de Babel (Gn 11)

    void SpawnBabelBox(int lane, float z)
    {
        var c = new Color(1f, 0.75f, 0.35f);
        var o = MakeObstacle("BabelBox", ObType.BabelBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.45f, 0.33f, 0.2f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), c);
        // zigurate em miniatura em cima
        for (int k = 0; k < 4; k++)
            Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.68f + k * 0.16f, 0f), new Vector3(0.9f - k * 0.2f, 0.16f, 0.9f - k * 0.2f), k % 2 == 0 ? new Color(0.78f, 0.6f, 0.4f) : new Color(0.65f, 0.48f, 0.3f));
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 1.38f, 0f), new Vector3(0.12f, 0.2f, 0.12f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.62f), new Vector3(0.7f, 0.12f, 0.05f), c, true);
        o.Init();
    }

    void StartBabel()
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Destroy(o.gameObject);
        }
        foreach (var b in FindObjectsByType<RunnerBullet>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        babel = true;
        player.SetVisible(false);
        babelGame.Begin(this, babelsCleared);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        // céu do entardecer na planície de Sinar
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = 160f;
        bannerText = "TORRE DE BABEL  —  " + RunnerBabel.StyleNames[babelGame.Style];
        bannerSub = "Suba até o topo e derrube o ídolo!  " + RunnerBabel.StyleVerses[babelGame.Style];
        bannerTime = 4f;
        Play(sLevel, 1f);
    }

    void EndBabel()
    {
        babelGame.End();
        babel = false;
        player.SetVisible(true);
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        ApplyAtmosphere();
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;
    }

    public void BabelWon(Vector3 at, int style)
    {
        Explode(at, new Color(1f, 0.82f, 0.3f), 40);
        PlayBoom();
        EndBabel();
        babelsCleared++;
        int pts = Mathf.RoundToInt(1500 * (1 + babelsCleared * 0.5f + style * 0.25f) * stats.scoreMul);
        killScore += pts;
        kills++;
        Heal(1);
        shake = 0.8f;
        ShowPopup("O ÍDOLO CAIU! +" + pts);
        if (style == 2)
        {
            offerTitle = "PENTECOSTES!";
            offerSub = "As línguas voltam a se entender (At 2:6). Escolha uma carta rara, épica ou lendária (+1 vida)";
        }
        else
        {
            offerTitle = "O ÍDOLO DE BABEL CAIU!";
            offerSub = "Recompensa: escolha uma carta rara, épica ou lendária (+1 vida)";
        }
        OpenCardChoice(true);
    }

    public void BabelFailed()
    {
        EndBabel();
        ShowPopup("O TEMPO ACABOU... A TORRE FICOU DE PÉ");
        Play(sHurt, 0.6f);
    }

    public void ShowFloat(Vector3 pos, string text, Color color, bool big) => AddFloat(pos, text, color, big);

    public void AddBonus(int points, Vector3 at)
    {
        int pts = Mathf.RoundToInt(points * stats.scoreMul);
        killScore += pts;
        kills++;
        AddFloat(at, "+" + pts, new Color(0.6f, 1f, 0.6f), false);
    }

    void SpawnCarBox(int lane, float z)
    {
        var c = new Color(1f, 0.55f, 0.15f);
        var o = MakeObstacle("CarBox", ObType.CarBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.55f, 0.38f, 0.2f));
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), new Color(0.3f, 0.2f, 0.1f));
        // carrinho brilhante
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.65f, 0f), new Vector3(0.5f, 0.12f, 0.9f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.75f, -0.05f), new Vector3(0.36f, 0.1f, 0.4f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, -0.05f, -0.62f), new Vector3(0.8f, 0.3f, 0.05f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.18f, -0.62f), new Vector3(0.5f, 0.2f, 0.05f), c, true);
        o.Init();
    }

    /// Monta um carro (usado pelo jogador e pelos rivais). Centro no meio da carroceria.
    /// Carruagem antiga puxada por cavalos (corrida). Centro no meio da carroceria.
    void BuildCarVisual(Transform root, Color team, bool isPlayer)
    {
        RunnerChariot.Build(root, team, false, isPlayer, -0.55f);
    }

    void BuildRaceRoad()
    {
        roadRoot = new GameObject("RaceRoad").transform;
        var asphalt = T.raceRoad;   // terra batida / circo / cinzas
        for (int i = 0; i < RoadSegs; i++)
        {
            var seg = new GameObject("Seg" + i).transform;
            seg.SetParent(roadRoot, false);
            Prim(PrimitiveType.Cube, seg, new Vector3(0f, 0.03f, 0f), new Vector3(RoadHalfWidth * 2f, 0.1f, SegLen + 0.4f), asphalt);
            Color curb = i % 2 == 0 ? T.raceCurbA : T.raceCurbB;
            Prim(PrimitiveType.Cube, seg, new Vector3(-RoadHalfWidth - 0.3f, 0.06f, 0f), new Vector3(0.6f, 0.14f, SegLen + 0.4f), curb);
            Prim(PrimitiveType.Cube, seg, new Vector3(RoadHalfWidth + 0.3f, 0.06f, 0f), new Vector3(0.6f, 0.14f, SegLen + 0.4f), curb);
            if (i % 2 == 0)
                Prim(PrimitiveType.Cube, seg, new Vector3(0f, 0.085f, 0f), new Vector3(0.6f, 0.02f, SegLen * 0.9f), T.raceRut); // marcas de rodas
            roadSegs.Add(seg);
        }
        grass = Prim(PrimitiveType.Cube, roadRoot, new Vector3(0f, -0.06f, 0f), new Vector3(500f, 0.1f, 500f), T.raceGround).transform;
        roadRoot.gameObject.SetActive(false);
    }

    void PlaceSeg(Transform seg, float z)
    {
        float x0 = RoadX(z), x1 = RoadX(z + SegLen);
        seg.position = new Vector3((x0 + x1) * 0.5f, 0f, z + SegLen * 0.5f);
        seg.rotation = Quaternion.Euler(0f, Mathf.Atan2(x1 - x0, SegLen) * Mathf.Rad2Deg, 0f);
    }

    void SetRaceScenery(bool on)
    {
        roadRoot.gameObject.SetActive(on);
        foreach (var t in tiles) t.gameObject.SetActive(!on);
        if (!on)
        {
            // recoloca a pista normal em volta do jogador
            float pz = player.transform.position.z;
            float baseZ = Mathf.Floor(pz / TileLen) * TileLen;
            for (int i = 0; i < tiles.Count; i++)
            {
                tiles[i].position = new Vector3(0, 0, baseZ + (i - 2) * TileLen);
                RandomizeBuildings(tiles[i]);
            }
            nextTileZ = baseZ + (tiles.Count - 2) * TileLen;
        }
    }

    void UpdateRoad()
    {
        float pz = player.transform.position.z;
        grass.position = new Vector3(player.transform.position.x, -0.06f, pz);
        foreach (var seg in roadSegs)
        {
            if (seg.position.z + SegLen < pz - 12f)
            {
                PlaceSeg(seg, nextSegZ);
                nextSegZ += SegLen;
            }
        }
    }

    void StartRace()
    {
        float pz = player.transform.position.z;
        racing = true;
        raceStartZ = pz;
        finishZ = pz + raceLength;

        // limpa a pista
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone) continue;
            o.dead = true;
            obstacles.RemoveAt(i);
            Explode(o.transform.position, o.mainColor, 4);
            Destroy(o.gameObject);
        }

        SetRaceScenery(true);
        float z0 = Mathf.Floor((pz - 12f) / SegLen) * SegLen;
        for (int i = 0; i < roadSegs.Count; i++) PlaceSeg(roadSegs[i], z0 + i * SegLen);
        nextSegZ = z0 + roadSegs.Count * SegLen;

        player.StartCar(Mathf.Max(speed, 22f));
        player.invuln = Mathf.Max(player.invuln, 1.5f);

        Color[] colors =
        {
            new Color(0.9f, 0.15f, 0.15f), new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.8f, 0.3f),
            new Color(0.7f, 0.3f, 0.9f), new Color(1f, 0.5f, 0.1f), new Color(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.2f, 0.25f)
        };
        for (int i = 0; i < rivalCount; i++)
        {
            float rz = pz + 14f + i * 14f + Random.Range(-3f, 3f);
            float off = (i % 2 == 0 ? -1f : 1f) * Random.Range(1.5f, 4f);
            var c = colors[i % colors.Length];
            var o = MakeObstacle("Rival", ObType.Rival, new Vector3(RoadX(rz) + off, 0.55f, rz), new Vector3(0.85f, 0.5f, 1.5f), 9999f, 0, c);
            BuildCarVisual(o.transform, c, false);
            o.rivalSpeed = player.carCruise * Random.Range(0.93f, 1.07f) + i * 0.4f;
            o.rivalOffset = off;
            o.rivalTimer = Random.Range(0.5f, 2f);
            o.airborne = true;
            o.Init();
        }

        // linha de chegada
        finishBanner = new GameObject("FinishLine");
        float fx = RoadX(finishZ);
        finishBanner.transform.position = new Vector3(fx, 0f, finishZ);
        var archStone = new Color(0.88f, 0.8f, 0.64f);
        Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(-RoadHalfWidth - 1f, 3f, 0f), new Vector3(1.0f, 6f, 1.0f), archStone);
        Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(RoadHalfWidth + 1f, 3f, 0f), new Vector3(1.0f, 6f, 1.0f), archStone);
        Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(0f, 6.6f, 0f), new Vector3(RoadHalfWidth * 2f + 3.4f, 0.9f, 1.1f), archStone);
        Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(0f, 6.6f, -0.56f), new Vector3(RoadHalfWidth * 2f + 3.4f, 0.3f, 0.04f), BabylonBlue);
        for (int sx = -1; sx <= 1; sx += 2)
            Prim(PrimitiveType.Sphere, finishBanner.transform, new Vector3(sx * (RoadHalfWidth + 1f), 6.3f + 0.9f, 0f), Vector3.one * 0.7f, Gold);
        int cells = 16;
        float cw = (RoadHalfWidth * 2f + 2f) / cells;
        for (int i = 0; i < cells; i++)
        for (int r = 0; r < 2; r++)
        {
            Color cc = (i + r) % 2 == 0 ? new Color(0.45f, 0.15f, 0.5f) : Gold;   // púrpura e ouro
            Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(-RoadHalfWidth - 1f + cw * (i + 0.5f), 5.6f + r * cw, 0f), new Vector3(cw, cw, 0.2f), cc);
            Prim(PrimitiveType.Cube, finishBanner.transform, new Vector3(-RoadHalfWidth - 1f + cw * (i + 0.5f), 0.1f, (r - 0.5f) * cw), new Vector3(cw, 0.02f, cw), cc);
        }

        nextSpawnZ = pz + 60f;
        racePosition = rivalCount + 1;
        ShowPopup("CORRIDA! CHEGUE EM 1º");
        Play(sLevel, 1f);
        shake = 0.4f;
    }

    void UpdateRace()
    {
        UpdateRoad();
        float pz = player.transform.position.z;

        while (nextSpawnZ < Mathf.Min(pz + 150f, finishZ - 40f))
        {
            SpawnRaceRow(nextSpawnZ);
            nextSpawnZ += Random.Range(22f, 34f);
        }
        // posição na corrida
        int pos = 1;
        foreach (var o in obstacles)
            if (o != null && !o.dead && o.type == ObType.Rival && o.transform.position.z > pz) pos++;
        racePosition = pos;

        if (pz >= finishZ) EndRace();
    }

    void SpawnRaceRow(float z)
    {
        float rx = RoadX(z);
        float d = Difficulty;

        // árvores fora da pista (cenário e perigo)
        for (int side = -1; side <= 1; side += 2)
        {
            if (Random.value < 0.75f) SpawnTree(rx + side * Random.Range(8.5f, 15f), z + Random.Range(-6f, 6f));
        }

        float r = Random.value;
        if (r < 0.35f)
        {
            int n = Random.Range(2, d > 0.3f ? 5 : 4);
            for (int i = 0; i < n; i++) SpawnCone(rx + Random.Range(-5.5f, 5.5f), z + Random.Range(-4f, 4f));
        }
        else if (r < 0.6f)
        {
            SpawnBoostPad(rx + Random.Range(-4f, 4f), z);
        }
        else if (r < 0.75f)
        {
            // fileira de cones com uma brecha
            float gap = Random.Range(-4f, 4f);
            for (float cx = -5.5f; cx <= 5.5f; cx += 1.4f)
                if (Mathf.Abs(cx - gap) > 1.8f) SpawnCone(rx + cx, z);
        }
    }

    void SpawnTree(float x, float z)
    {
        // palmeira ou obelisco à beira da pista (bater custa vida)
        if (T.id != 0)
        {
            var side = MakeObstacle("RaceSide", ObType.Wall, new Vector3(x, 2f, z), new Vector3(0.6f, 2f, 0.6f), 9999f, 0, StoneColors[0]);
            Biomes.BuildRaceSide(this, side.transform, T);
            side.airborne = true;
            side.Init();
            return;
        }
        if (Random.value < 0.3f)
        {
            var sc = new Color(0.86f, 0.76f, 0.58f);
            var ob = MakeObstacle("Obelisk", ObType.Wall, new Vector3(x, 2f, z), new Vector3(0.6f, 2f, 0.6f), 9999f, 0, sc);
            Prim(PrimitiveType.Cube, ob.transform, new Vector3(0f, -1.7f, 0f), new Vector3(1.4f, 0.6f, 1.4f), sc * 0.85f);
            Prim(PrimitiveType.Cube, ob.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.8f, 3.4f, 0.8f), sc);
            var tip = Prim(PrimitiveType.Cube, ob.transform, new Vector3(0f, 2.05f, 0f), new Vector3(0.55f, 0.55f, 0.55f), Gold);
            tip.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, ob.transform, new Vector3(0f, 1f - k * 0.7f, -0.41f), new Vector3(0.3f, 0.3f, 0.02f), BabylonBlue);
            ob.airborne = true;
            ob.Init();
            return;
        }
        var o = MakeObstacle("Tree", ObType.Wall, new Vector3(x, 2f, z), new Vector3(0.6f, 2f, 0.6f), 9999f, 0, new Color(0.25f, 0.5f, 0.2f));
        var trunk = new Color(0.5f, 0.36f, 0.22f);
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, -0.9f, 0f), new Vector3(0.4f, 1.1f, 0.4f), trunk);
        var t2 = Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0.15f, 1.0f, 0f), new Vector3(0.34f, 0.9f, 0.34f), trunk * 0.9f);
        t2.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
        for (int k = 0; k < 6; k++)
        {
            var leaf = Prim(PrimitiveType.Cube, o.transform, new Vector3(0.3f, 1.9f, 0f), new Vector3(0.45f, 0.06f, 2.2f), new Color(0.25f, 0.5f + Random.value * 0.1f, 0.2f));
            leaf.transform.localRotation = Quaternion.Euler(25f, k * 60f, 0f);
            leaf.transform.localPosition += leaf.transform.localRotation * Vector3.forward * 0.9f;
        }
        o.airborne = true;
        o.Init();
    }

    void SpawnCone(float x, float z)
    {
        // ânfora de barro (quebra ao bater e te atrasa um pouco)
        var c = Clay;
        var o = MakeObstacle("Cone", ObType.Cone, new Vector3(x, 0.45f, z), new Vector3(0.35f, 0.45f, 0.35f), 1f, 0, c);
        Prim(PrimitiveType.Sphere, o.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.6f, 0.7f, 0.6f), c);
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, 0.32f, 0f), new Vector3(0.22f, 0.12f, 0.22f), c * 0.9f);
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, 0.44f, 0f), new Vector3(0.32f, 0.03f, 0.32f), c * 0.8f);
        Prim(PrimitiveType.Cylinder, o.transform, new Vector3(0f, 0.0f, 0f), new Vector3(0.62f, 0.04f, 0.62f), new Color(0.15f, 0.1f, 0.08f));
        for (int s2 = -1; s2 <= 1; s2 += 2)
            Prim(PrimitiveType.Cube, o.transform, new Vector3(s2 * 0.2f, 0.25f, 0f), new Vector3(0.06f, 0.22f, 0.06f), c * 0.85f);
        o.airborne = true;
        o.Init();
    }

    void SpawnBoostPad(float x, float z)
    {
        var c = Gold;   // pedras douradas que dão turbo
        float yaw = Mathf.Atan2(RoadX(z + 2f) - RoadX(z - 2f), 4f) * Mathf.Rad2Deg;
        var o = MakeObstacle("Boost", ObType.Boost, new Vector3(x, 0.3f, z), new Vector3(1.2f, 0.6f, 1.6f), 1f, 0, c);
        o.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, -0.2f, 0f), new Vector3(2.2f, 0.04f, 3f), BabylonBlue);
        for (int i = 0; i < 3; i++)
        {
            var a = Prim(PrimitiveType.Cube, o.transform, new Vector3(-0.35f, -0.17f, -0.9f + i * 0.9f), new Vector3(0.9f, 0.03f, 0.18f), c, true);
            a.transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
            var b = Prim(PrimitiveType.Cube, o.transform, new Vector3(0.35f, -0.17f, -0.9f + i * 0.9f), new Vector3(0.9f, 0.03f, 0.18f), c, true);
            b.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
        }
        o.airborne = true;
        o.Init();
    }

    public void HitRival(RunnerObstacle o, Vector3 at)
    {
        o.rivalStun = Mathf.Max(o.rivalStun, 0.7f);
        Explode(at, o.mainColor, 3);
        PlayClank();
        if (TextMode == 2 && Random.value < 0.25f) AddFloat(at + Vector3.up, "RODOU!", new Color(1f, 0.8f, 0.3f), false);
    }

    void EndRace()
    {
        int pos = racePosition;
        racing = false;

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o != null && o.airborne && !o.dead)
            {
                o.dead = true;
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
        if (finishBanner != null) Destroy(finishBanner);
        SetRaceScenery(false);
        player.EndCar();
        player.invuln = Mathf.Max(player.invuln, 1.5f);
        nextSpawnZ = player.transform.position.z + 40f;
        planeBoxTimer = planeBoxInterval;

        int[] prize = { 2000, 1200, 800, 500, 300, 150, 100, 50 };
        int pts = Mathf.RoundToInt(prize[Mathf.Clamp(pos - 1, 0, prize.Length - 1)] * stats.scoreMul);
        killScore += pts;
        AddFloat(player.transform.position + Vector3.up * 2f, "+" + pts, new Color(0.6f, 1f, 0.6f), true);
        Explode(player.transform.position, new Color(1f, 0.85f, 0.2f), 20);

        if (pos == 1)
        {
            Heal(1);
            ShowPopup("VITÓRIA!");
            offerTitle = "1º LUGAR!";
            offerSub = "Recompensa da corrida: escolha uma carta rara, épica ou lendária (+1 vida)";
            OpenCardChoice(true);
        }
        else
        {
            ShowPopup(pos + "º LUGAR");
            Play(sPickup, 0.8f);
        }
    }

    void SpawnBoss()
    {
        bossPending = false;
        int tier = bossesDefeated;
        bool final = bossesDefeated + 1 == finalBossNumber;
        float hp = Mathf.Max(36f, stats.EstimatedDps * 0.75f * (25f + 7f * tier));
        if (final) hp *= 2.2f;
        hp *= BossHpMul();
        var c = new Color(0.3f, 0.05f, 0.09f);
        var pz = player.transform.position.z;
        var o = MakeObstacle("Boss", ObType.Boss, new Vector3(0f, 1.8f, pz + 95f),
            final ? new Vector3(1.4f, 1.8f, 1.0f) : new Vector3(1.1f, 1.8f, 0.9f), hp, final ? 25000 : 1000 * (tier + 1), c);
        var t = o.transform;
        // criatura sombria gigante, virada para o jogador (centro do chefe fica 1.8 acima do chão)
        var bossCreature = RunnerCreature.Build(t, final ? 2.3f : 1.8f, -1.8f, final ? SatanPalette : T.bossPalette, true);
        if (final) BuildSatanExtras(t, bossCreature);
        var bossGun = bossCreature.weaponMount.gameObject.AddComponent<RunnerWeaponModel>();
        bossGun.followPlayerWeapon = false;
        var sh = Prim(PrimitiveType.Cylinder, t, new Vector3(0f, -0.2f, 0f), new Vector3(3f, 0.03f, 3f), new Color(0.3f, 0.9f, 1f), true);
        sh.name = "BossShield";

        o.Init();
        var b = o.gameObject.AddComponent<RunnerBoss>();
        b.body = o;
        b.shieldVisual = sh.transform;
        b.Setup(tier, final);
        // a arma do chefe combina com a build dele
        string gunId = b.Has(BossAbility.Foguetes) ? "bazuca" : b.Has(BossAbility.Rajada) ? "metralhadora"
                     : b.Has(BossAbility.TiroMultiplo) ? "escopeta" : "railgun";
        bossGun.Build(gunId, new Color(1f, 0.2f, 0.15f));
        b.gun = bossGun;
        boss = b;
        if (final)
        {
            bannerText = "SATANÁS";
            bannerSub = RunnerBoss.Rtl(RunnerBoss.HebrewName) + "  —  o Acusador";
            bannerTime = 4f;
            RenderSettings.fogColor = Color.Lerp(T.fogColor, Color.black, 0.5f);
            if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
        }
        ShowPopup(b.bossName.ToUpper());
        Sfx("rugido", 1f, 0.05f, 1f, b.isFinal ? 0.75f : 1f);
        Sfx("shofar", 0.7f, 0f, 1f);
        shake = 0.4f;
    }

    void OnBossDefeated(Vector3 pos)
    {
        bool wasFinal = boss != null && boss.isFinal;
        if (boss != null && !trailerActive) Meta.MarkSeen("boss", boss.bossName);
        RestartPacingAfterBoss();
        if (boss != null) boss.CleanupAll(true);
        boss = null;
        bossesDefeated++;
        Heal(1);
        EndPath();
        AddSiclos(25, pos + Vector3.up * 3f);
        var nextBiome = Biomes.ForBosses(bossesDefeated, egyptAfterBoss, romeAfterBoss, sheolAfterBoss);
        bool travel = nextBiome != Theme && !wasFinal;   // mudou de região: Travessia dos Céus
        if (!travel) SetBiome(nextBiome, true);
        FxSphere(pos, 9f, new Color(1f, 0.8f, 0.3f));
        Explode(pos, new Color(1f, 0.8f, 0.3f), 40);
        shake = 1f;
        Sfx("vitoria", 1f, 0f, 1f);
        ShowPopup("CHEFE DERROTADO!");
        nextSpawnZ = player.transform.position.z + 45f;
        offerTitle = "CHEFE DERROTADO!";
        offerSub = "Recompensa: escolha uma carta rara, épica ou lendária (+1 vida)";
        if (wasFinal)
        {
            finalBeaten = true;
            PlayerPrefs.SetInt("runner_final_win", 1);
            PlayerPrefs.Save();
            ApplyAtmosphere();
            bannerText = "VITÓRIA!";
            bannerSub = "O Acusador foi lançado fora (Ap 12:10)  —  a jornada continua no modo infinito";
            bannerTime = 6f;
            offerTitle = "SATANÁS FOI DERROTADO!";
            offerSub = "Você venceu a jornada! Escolha sua recompensa e continue no modo infinito.";
            ShowPopup("VITÓRIA!");
            bannerTime = 0f;
            StartVictory(pos);   // cena da vitória → Modo Infinito → recompensas
            return;
        }
        offerHasNova = Random.value < 0.4f;   // às vezes o chefe deixa uma carta nova
        if (travel)
        {
            // primeiro a viagem pelos céus; as recompensas vêm quando pousar na nova região
            StartSkyTransit(nextBiome);
            return;
        }
        AfterBossRewards();
        OpenCardChoice(true);
    }

    // ================================================================== chefe final

    static RunnerCreature.Palette SatanPalette => new RunnerCreature.Palette
    {
        body = new Color(0.04f, 0.02f, 0.03f),
        accent = new Color(0.75f, 0.02f, 0.05f),
        orb = new Color(0.9f, 0.05f, 0.05f),
        eye = new Color(1f, 0.9f, 0.3f),
        halo = new Color(1f, 0.1f, 0.05f),
        boss = true
    };

    /// Dez chifres, asas enormes de dragão e o nome em hebraico flutuando em fogo (Ap 12:3).
    void BuildSatanExtras(Transform t, RunnerCreature creature)
    {
        var horn = new Color(0.15f, 0.05f, 0.05f);
        var crown = new GameObject("DezChifres").transform;
        crown.SetParent(creature.body, false);
        crown.localPosition = new Vector3(0f, 0.75f, 0.72f);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2f / 10f;
            var h = Prim(PrimitiveType.Cube, crown, new Vector3(Mathf.Cos(a) * 0.32f, 0.12f, Mathf.Sin(a) * 0.32f), new Vector3(0.06f, 0.35f, 0.06f), horn);
            h.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 25f, 0f, -Mathf.Cos(a) * 25f);
            Prim(PrimitiveType.Cube, h.transform, new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.25f, 1.2f), new Color(1f, 0.25f, 0.05f), true);
        }
        // asas de dragão
        for (int sd = -1; sd <= 1; sd += 2)
        {
            var w = new GameObject("AsaDragao").transform;
            w.SetParent(creature.body, false);
            w.localPosition = new Vector3(sd * 0.3f, 0.2f, -0.1f);
            w.localRotation = Quaternion.Euler(0f, 0f, sd * 35f);
            for (int k = 0; k < 4; k++)
            {
                var bone = Prim(PrimitiveType.Cube, w, new Vector3(sd * (0.5f + k * 0.35f), 0.2f + k * 0.12f, -0.1f * k), new Vector3(0.08f, 0.08f, 1.0f - k * 0.15f), horn);
                bone.transform.localRotation = Quaternion.Euler(0f, sd * (10f + k * 12f), 0f);
            }
            Prim(PrimitiveType.Cube, w, new Vector3(sd * 0.95f, 0.25f, -0.15f), new Vector3(1.5f, 0.03f, 0.9f), new Color(0.3f, 0.02f, 0.04f));
        }
        // nome em hebraico flutuando acima (TextMesh)
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            var go = new GameObject("NomeHebraico");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0f, 4.6f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = RunnerBoss.Rtl(RunnerBoss.HebrewName);
            tm.fontSize = 80;
            tm.characterSize = 0.13f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.3f, 0.05f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }

    public void FinalBossTrueForm(RunnerBoss b)
    {
        bannerText = "FORMA VERDADEIRA";
        bannerSub = "o antigo dragão revela seu poder (Ap 12:9)";
        bannerTime = 3f;
        shake = 1f;
        Play(sHurt, 1f);
        FxSphere(b.transform.position, 6f, new Color(1f, 0.1f, 0.05f));
        RenderSettings.fogColor = Color.Lerp(T.fogColor, new Color(0.3f, 0f, 0f), 0.7f);
        if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
    }

    public void BossEnraged(RunnerBoss b)
    {
        ShowPopup("FASE 2 — FÚRIA!");
        shake = 0.6f;
        Play(sHurt, 0.8f);
        FxSphere(b.transform.position, 4f, new Color(1f, 0.1f, 0.1f));
    }

    public void BossMessage(Vector3 pos, string text, Color color) => AddFloat(pos, text, color, true);

    public void BossShake(float amount) => shake = Mathf.Max(shake, amount);

    public void TryHurtPlayer()
    {
        if (state != RunnerState.Playing || player.invuln > 0f) return;
        HurtPlayer(null);
    }

    void SpawnHealth(int lane, float z)
    {
        // rolo do livro (Ezequiel 3: "come este rolo") — dá +1 vida
        var c = new Color(0.2f, 1f, 0.4f);
        var o = MakeObstacle("Health", ObType.Health, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.6f, 0.6f, 0.6f), 1f, 0, c);
        var paper = Prim(PrimitiveType.Cylinder, o.transform, Vector3.zero, new Vector3(0.42f, 0.42f, 0.42f), new Color(0.95f, 0.88f, 0.68f));
        paper.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            var h = Prim(PrimitiveType.Cylinder, o.transform, new Vector3(sx * 0.5f, 0f, 0f), new Vector3(0.16f, 0.12f, 0.16f), new Color(0.45f, 0.28f, 0.14f));
            h.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.06f, 0.5f), c, true);
        Prim(PrimitiveType.Cube, o.transform, new Vector3(0f, 0.25f, -0.12f), new Vector3(0.5f, 0.04f, 0.04f), new Color(0.3f, 0.2f, 0.1f));
        o.Init();
    }

    void SpawnPlaneBox(int lane, float z)
    {
        var c = new Color(0.4f, 0.85f, 1f);
        var o = MakeObstacle("PlaneBox", ObType.PlaneBox, new Vector3(LaneX(lane), 1.2f, z), new Vector3(0.75f, 0.75f, 0.75f), 1f, 0, c);
        var t = o.transform;
        Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(1.2f, 1.2f, 1.2f), new Color(0.55f, 0.38f, 0.2f));
        // bordas
        foreach (var e in new[] { new Vector3(1, 1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(-1, -1, 0) })
            Prim(PrimitiveType.Cube, t, new Vector3(e.x * 0.58f, e.y * 0.58f, 0f), new Vector3(0.12f, 0.12f, 1.25f), new Color(0.3f, 0.2f, 0.1f));
        // aviãozinho brilhante em cima e na frente
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.65f, 0f), new Vector3(0.18f, 0.08f, 0.9f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.65f, 0.1f), new Vector3(0.9f, 0.08f, 0.2f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.65f, -0.35f), new Vector3(0.4f, 0.08f, 0.12f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0f, -0.62f), new Vector3(0.9f, 0.2f, 0.05f), c, true);
        Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.05f, -0.62f), new Vector3(0.18f, 0.7f, 0.05f), c, true);
        o.Init();
    }

    // ================================================================== combat

    public RunnerBullet FireBullet(Vector3 pos, float vx, float vz, float dmg, float size, float life, int pierce, float explode, float homing, Color color)
    {
        // tiros reaproveitados (sem criar e destruir objetos a cada disparo)
        var b = RunnerBullet.Rent();
        GameObject go;
        if (b == null)
        {
            MeshRenderer r;
            go = NewPrim(PrimitiveType.Sphere, out r, false);
            go.name = "Bullet";
            b = go.AddComponent<RunnerBullet>();
            b.rend = r;
        }
        else
        {
            go = b.gameObject;
            go.SetActive(true);
        }
        b.ResetShot();
        b.rend.sharedMaterial = Glow(color);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.18f, 0.18f, 0.6f) * size;
        go.transform.rotation = Quaternion.LookRotation(new Vector3(vx, 0f, vz));
        b.vx = vx;
        b.vz = vz;
        b.damage = dmg;
        b.life = life;
        b.pierceLeft = pierce;
        b.explode = explode;
        b.homing = homing;
        b.radius = Mathf.Max(0.15f, 0.2f * size);
        return b;
    }

    /// Inimigo atirável mais próximo à frente de p (entre minDz e maxDz no eixo Z).
    public RunnerObstacle NearestShootable(Vector3 p, float minDz, float maxDz, HashSet<RunnerObstacle> exclude)
    {
        RunnerObstacle best = null;
        float bestScore = float.MaxValue;
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || !o.Shootable) continue;
            if (exclude != null && exclude.Contains(o)) continue;
            var c = o.transform.position;
            float dz = c.z - p.z;
            if (dz < minDz || dz > maxDz) continue;
            float score = dz + Mathf.Abs(c.x - p.x) * 3f;
            if (score < bestScore) { bestScore = score; best = o; }
        }
        return best;
    }

    RunnerObstacle NearestAround(Vector3 p, float radius, HashSet<RunnerObstacle> exclude)
    {
        RunnerObstacle best = null;
        float bestD = radius * radius;
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || !o.Shootable || exclude.Contains(o)) continue;
            float d = (o.transform.position - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = o; }
        }
        return best;
    }

    public void DamageEnemy(RunnerObstacle o, float dmg, bool canCrit, Vector3 at)
    {
        if (o == null || o.dead) return;
        bool crit = canCrit && Random.value < stats.critChance;
        if (crit)
        {
            dmg *= stats.critMul;
            Sfx("crit", 0.4f, 0.1f, 0.09f);
            if (stats.Melee || stats.Cooldown >= 0.3f) shake = Mathf.Max(shake, 0.12f);   // armas pesadas: tranco na câmera
        }
        if (o.champion) dmg = ChampionDamageMod(o, dmg >= 9999f ? o.maxHp * 0.15f : dmg, crit);

        if (o.type == ObType.Boss)
        {
            if (dmg >= 9999f) dmg = o.maxHp * 0.08f;   // efeitos de "morte instantânea" só arranham o chefe
            var b = o.GetComponent<RunnerBoss>();
            if (b != null)
            {
                float before = dmg;
                dmg = b.Absorb(dmg);
                if (dmg <= 0f)
                {
                    if (TextMode == 2) AddFloat(at + Vector3.up * 0.6f, before < 10f ? before.ToString("0.#") : Mathf.RoundToInt(before).ToString(), new Color(0.4f, 0.9f, 1f), false);
                    PlayClank();
                    return;
                }
            }
        }

        // bênçãos que mexem no dano
        dmg = DamageMods(o, dmg);
        if (stats.slingLevel > 0 && (o.elite || o.type == ObType.Tank || o.type == ObType.Boss || o.type == ObType.BossDrone))
            dmg *= 1f + 0.35f * stats.slingLevel;
        if (stats.wrathLevel > 0) dmg *= 1f + 0.12f * stats.wrathLevel * Mathf.Max(0, maxLives - lives);
        if (stats.mosesStaff && o.type != ObType.Boss && !o.champion && o.type != ObType.EnemyShot && Random.value < stats.mosesChance)
        {
            dmg = 99999f;
            AddFloat(at + Vector3.up, "PÓ!", new Color(0.9f, 0.85f, 0.6f), false);
            Sfx("po", 0.7f, 0.1f, 0.1f);
        }

        if (dmg < 1000f && (canCrit || crit || dmg >= stats.Damage * 2.5f))   // só acertos diretos e golpes grandes (menos números na tela)
        {
            AddDamageNumber(at + Vector3.up * 0.6f, dmg, crit);
        }

        dmg = StatusDamageMods(o, dmg);
        if (o.TakeDamage(dmg)) DestroyObstacle(o, true);
        else
        {
            PlayClank();
            if (RunnerDebris.Live < DebrisBudget / 3) Explode(at, o.mainColor, 2);
            OnEnemyHit(o, canCrit);   // fogo, água, raio, confusão
        }
    }

    public void DestroyObstacle(RunnerObstacle o, bool byPlayer)
    {
        if (o == null || o.dead) return;
        o.dead = true;
        obstacles.Remove(o);
        Vector3 pos = o.transform.position;

        if (byPlayer && o.points > 0)
        {
            kills++;
            int pts = Mathf.RoundToInt(o.points * stats.scoreMul);
            killScore += pts;
            if (o.elite || o.type == ObType.Tank) AddFloat(pos + Vector3.up * 1.4f, "+" + pts, new Color(0.6f, 1f, 0.6f), false);
            OnKillReward(o, pos);

            if (stats.mana && o.type != ObType.EnemyShot)
            {
                manaCounter++;
                if (manaCounter >= (stats.manaEternal ? 10 : 15))
                {
                    manaCounter = 0;
                    float bonus = stats.manaEternal ? 0.07f : 0.05f;
                    stats.damageMul += bonus;
                    AddFloat(player.transform.position + Vector3.up * 1.8f, "MANÁ: +" + Mathf.RoundToInt(bonus * 100) + "% DANO", new Color(1f, 0.95f, 0.7f), true);
                    Sfx("mana", 0.7f);
                    CardFx.Flakes(player.transform, new Color(1f, 0.97f, 0.85f), 14);
                    if (stats.manaEternal && ++manaPortions % 3 == 0 && lives < maxLives)
                    {
                        Heal(1);
                        AddFloat(player.transform.position + Vector3.up * 2.4f, "PÃO DA VIDA: +1 VIDA", new Color(1f, 0.4f, 0.4f), true);
                    }
                }
            }
            if (stats.aaronRod && o.type != ObType.EnemyShot && ++aaronCounter >= 25)
            {
                aaronCounter = 0;
                PartTheSea();
            }

            if (stats.vampKills > 0 && o.type != ObType.EnemyShot)
            {
                vampCounter++;
                if (vampCounter >= stats.vampKills)
                {
                    vampCounter = 0;
                    if (lives < maxLives)
                    {
                        Heal(1);
                        AddFloat(player.transform.position + Vector3.up * 1.5f, "+1 VIDA", new Color(1f, 0.3f, 0.4f), true);
                    }
                }
            }
        }

        if (byPlayer) OnEnemyKilled(o, pos);
        if (byPlayer && o.champion) OnChampionKilled(o, pos);
        else if (byPlayer && (o.elite || o.type == ObType.Tank)) { shake = Mathf.Max(shake, 0.3f); Sfx("impacto", 0.7f, 0.08f, 0.1f); }
        Explode(pos, o.mainColor, o.type == ObType.EnemyShot ? 4 : (o.type == ObType.Tank ? 16 : 9));
        PlayBoom();
        Destroy(o.gameObject);

        if (o.type == ObType.Boss)
        {
            OnBossDefeated(pos);
            return;
        }

        if (byPlayer && stats.killExplodeChance > 0f && o.type != ObType.EnemyShot && Random.value < stats.killExplodeChance)
            Blast(pos, 4f, 2f * stats.damageMul, null);
    }

    public void Blast(Vector3 center, float radius, float dmg, RunnerObstacle exclude)
    {
        FxSphere(center, radius, new Color(1f, 0.55f, 0.15f));
        PlayBoom();
        shake = Mathf.Max(shake, Mathf.Min(0.35f, radius * 0.06f));
        var list = SnapshotObstacles();
        foreach (var o in list)
        {
            if (o == null || o.dead || o == exclude || !o.Shootable) continue;
            Vector3 c = o.transform.position;
            Vector3 closest = new Vector3(
                Mathf.Clamp(center.x, c.x - o.half.x, c.x + o.half.x),
                Mathf.Clamp(center.y, c.y - o.half.y, c.y + o.half.y),
                Mathf.Clamp(center.z, c.z - o.half.z, c.z + o.half.z));
            if ((closest - center).sqrMagnitude <= radius * radius)
                DamageEnemy(o, dmg, false, c);
        }
        ReleaseList(list);
    }

    public void ChainLightning(RunnerObstacle from, float dmg)
    {
        if (stats.chain <= 0 || from == null) return;
        var visited = new HashSet<RunnerObstacle> { from };
        Vector3 cur = from.transform.position;
        for (int i = 0; i < stats.chain; i++)
        {
            var next = NearestAround(cur, 12f, visited);
            if (next == null) break;
            Vector3 np = next.transform.position;
            Zap(cur, np);
            if (i == 0) Sfx("zap", 0.55f, 0.12f, 0.12f);
            visited.Add(next);
            hitKind = HitShock;
            DamageEnemy(next, dmg * stats.chainMul, false, np);
            hitKind = 0;
            cur = np;
        }
    }

    void Nova(Vector3 center, float radius)
    {
        FxSphere(center, radius * 0.5f, new Color(0.4f, 0.9f, 1f));
        shake = 0.6f;
        Sfx("nova", 0.9f, 0.04f, 0.2f);
        var list = SnapshotObstacles();
        foreach (var o in list)
        {
            if (o == null || o.dead || !o.Shootable) continue;
            if ((o.transform.position - center).sqrMagnitude <= radius * radius)
                DamageEnemy(o, 99999f, false, o.transform.position);
        }
        ReleaseList(list);
    }

    void HurtPlayer(RunnerObstacle o)
    {
        if (trailerActive)   // demo da abertura: ninguém se machuca, o obstáculo só some
        {
            if (o != null && !o.dead && o.type != ObType.Boss) DestroyObstacle(o, false);
            return;
        }
        if (stats.retaliation) Nova(player.transform.position, 20f);
        if (o != null && !o.dead && o.type != ObType.Boss) DestroyObstacle(o, false);

        if (RelicBlocksHit()) return;
        if (FaithMiracle()) return;   // Fé 5
        if (stats.shieldLevel > 0 && shieldReady)
        {
            shieldReady = false;
            shieldTimer = stats.ShieldRecharge;
            player.invuln = 0.7f;
            shake = 0.25f;
            AddFloat(player.transform.position + Vector3.up * 1.5f, "ESCUDO!", new Color(0.4f, 0.9f, 1f), true);
            Sfx("escudo_quebra", 0.8f);
            if (stats.pillarOfFire && !bulletHell)
            {
                Nova(player.transform.position, 18f);
                player.invuln = 1.5f;
            }
            return;
        }

        lives--;
        Vibrate();
        if (stats.elijahMantle && !bulletHell) for (int k = 0; k < 3; k++) SkyFireStrike(player.transform.position);
        if (boss != null) boss.OnPlayerHurt();
        player.invuln = stats.invulnTime;
        shake = 0.5f;
        if (stats.redSea && !bulletHell) PartTheSea();
        Play(sHurt, 0.9f);
        if (lives <= 0 && RelicSavesLife()) return;
        if (lives <= 0)
        {
            if (stats.resurrection && !stats.resurrectionUsed)
            {
                stats.resurrectionUsed = true;
                lives = maxLives;
                player.invuln = 3f;
                if (!bulletHell) Nova(player.transform.position, 25f);
                Sfx("ressurreicao", 1f, 0f, 1f);
                bannerText = "RESSURREIÇÃO!";
                bannerSub = "\"Onde está, ó morte, a tua vitória?\" (1Co 15:55)";
                bannerTime = 3.5f;
                Play(sLevel, 1f);
            }
            else GameOver();
        }
    }

    void CheckPlayerCollisions()
    {
        Vector3 pc = player.transform.position;
        var list = SnapshotObstacles();
        try
        {
        foreach (var o in list)
        {
            if (o == null || o.dead || o.type == ObType.Platform) continue;
            if (!o.Overlaps(pc, player.Half)) continue;
            if (o.type == ObType.Coin) { PickCoin(o); continue; }

            if (o.type == ObType.PlaneBox || o.type == ObType.CarBox || o.type == ObType.ShipBox || o.type == ObType.AngelBox || o.type == ObType.BabelBox || o.type == ObType.JerichoBox || o.type == ObType.GoliathBox)
            {
                DestroyObstacle(o, false);
                if (BossFight || FlightEvent || trailerActive) continue;   // chefe a caminho (ou demo): a caixa some
                Sfx("reliquia", 0.7f, 0f, 0.5f);
                if (o.type == ObType.PlaneBox) StartFlight();
                else if (o.type == ObType.CarBox) StartRace();
                else if (o.type == ObType.ShipBox) StartBulletHell();
                else if (o.type == ObType.BabelBox) StartBabel();
                else if (o.type == ObType.JerichoBox) StartJericho();
                else if (o.type == ObType.GoliathBox) StartGoliath();
                else StartPassover();
                return;
            }

            if (o.type == ObType.HouseOpen)
            {
                if (!o.flag) JudgeHouse(o);
                continue;
            }
            if (o.type == ObType.Note)
            {
                if (!o.flag) JerichoHit(o);
                continue;
            }
            if (o.type == ObType.NoteBad)
            {
                if (!o.flag) JerichoIdol(o);
                if (state == RunnerState.GameOver) return;
                continue;
            }
            if (o.type == ObType.HouseBlood)
            {
                if (!o.flag)
                {
                    o.flag = true;
                    bloodHits++;
                    passCombo = 0;
                    AddFloat(o.transform.position + Vector3.up * 2.8f, "PASSE ADIANTE! (Êx 12:13)", new Color(1f, 0.25f, 0.2f), true);
                    if (player.invuln <= 0f) HurtPlayer(null);
                    if (state == RunnerState.GameOver) return;
                }
                continue;
            }

            if (o.type == ObType.Rival)
            {
                if (o.bumpCd > 0f) continue;
                o.bumpCd = 0.5f;
                float dir = Mathf.Sign(pc.x - o.transform.position.x);
                if (dir == 0f) dir = 1f;
                player.Bump(dir * 12f, 0.88f);
                o.rivalOffset -= dir * 2f;
                o.rivalStun = Mathf.Max(o.rivalStun, 0.25f);
                shake = Mathf.Max(shake, 0.25f);
                PlayClank();
                Explode((pc + o.transform.position) * 0.5f, new Color(1f, 0.8f, 0.4f), 5);
                continue;
            }

            if (o.type == ObType.Cone)
            {
                player.carSpeed *= 0.93f;
                Explode(o.transform.position, o.mainColor, 8);
                PlayClank();
                DestroyObstacle(o, false);
                continue;
            }

            if (o.type == ObType.Boost)
            {
                if (o.bumpCd > 0f) continue;
                o.bumpCd = 99f;
                player.Boost();
                AddFloat(pc + Vector3.up * 1.5f, "TURBO!", new Color(0.4f, 0.9f, 1f), true);
                Play(sShield, 0.7f);
                shake = Mathf.Max(shake, 0.2f);
                continue;
            }

            if (o.type == ObType.Ring)
            {
                int pts = Mathf.RoundToInt(150 * stats.scoreMul);
                killScore += pts;
                AddFloat(o.transform.position + Vector3.up * 1.5f, "+" + pts + " ANEL", new Color(1f, 0.85f, 0.2f), true);
                Play(sPickup, 0.7f);
                DestroyObstacle(o, false);
                continue;
            }

            if (o.type == ObType.Health)
            {
                if (lives < maxLives)
                {
                    Heal(1);
                    AddFloat(pc + Vector3.up * 1.5f, "+1 VIDA", new Color(1f, 0.3f, 0.4f), true);
                }
                else
                {
                    int pts = Mathf.RoundToInt(100 * stats.scoreMul);
                    killScore += pts;
                    AddFloat(pc + Vector3.up * 1.5f, "+" + pts, new Color(0.6f, 1f, 0.6f), true);
                }
                Play(sPickup, 0.8f);
                DestroyObstacle(o, false);
                continue;
            }

            // fornalha / espinhos só machucam quando ativos
            if ((o.type == ObType.FireJet || o.type == ObType.Spikes) && !o.hazardOn) continue;
            // inimigos que só morrem com um pisão
            if (o.stompable && TryStomp(o)) continue;
            // Escada de Jacó: pisa em quase tudo que não é indestrutível
            if (stats.jacobLadder && !o.Indestructible && o.type != ObType.Boss && o.type != ObType.EnemyShot && o.type != ObType.Shockwave
                && (o.Shootable || o.type == ObType.Wall || o.type == ObType.Barrier) && TryStomp(o)) continue;

            if (stats.ram && o.Shootable && o.type != ObType.Boss)
            {
                DamageEnemy(o, 99999f, false, o.transform.position);
                shake = Mathf.Max(shake, 0.2f);
                continue;
            }

            if (player.invuln > 0f) continue;

            HurtPlayer(o);
            if (state == RunnerState.GameOver) return;
        }
        }
        finally { ReleaseList(list); }
    }

    void Cleanup()
    {
        float limit = player.transform.position.z - 12f;
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var o = obstacles[i];
            if (o == null) { obstacles.RemoveAt(i); continue; }
            if (o.transform.position.z < limit && o.type != ObType.Boss && o.type != ObType.BossDrone && o.type != ObType.Rival)
            {
                obstacles.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
    }

    // ================================================================== fx

    void ShowPopup(string text)
    {
        popup = text;
        popupTime = 1.0f;
    }

    void AddFloat(Vector3 pos, string text, Color color, bool big)
    {
        int mode = TextMode;   // 0 poucos, 1 normal, 2 todos
        if (mode < 2)
        {
            // pontos soltos ("+150") já aparecem no placar: não precisam voar pela tela
            if (IsScorePopup(text)) return;
            // quanto mais rápido/difícil, menos textos pequenos
            if (!big)
            {
                if (mode == 0) return;
                int cap = LevelThreat > 2f || Difficulty > 0.8f ? 2 : 4;
                if (smallFloats >= cap || Time.time - lastSmallFloat < 0.12f) return;
            }
        }
        else if (!big && smallFloats >= 12) return;

        // o mesmo texto que já está na tela só renova (não empilha)
        foreach (var f in floats)
            if (f.text == text && f.t > 0.2f) { f.t = f.big ? 1.1f : 0.6f; return; }

        if (big)
        {
            int bigCap = mode == 2 ? 3 : 2, bigs = 0, oldest = -1;
            for (int i = 0; i < floats.Count; i++) if (floats[i].big) { bigs++; if (oldest < 0) oldest = i; }
            if (bigs >= bigCap && oldest >= 0) floats.RemoveAt(oldest);
            // mensagens grandes perto do jogador não se sobrepõem: sobem um degrau
            if (player != null && (pos - player.transform.position).sqrMagnitude < 9f)
                pos += Vector3.up * 0.7f * Mathf.Min(bigs, bigCap - 1);
        }
        else lastSmallFloat = Time.time;
        if (floats.Count > 24) floats.RemoveAt(0);
        floats.Add(new FloatText { pos = pos, text = text, color = color, t = big ? 1.1f : 0.6f, big = big });
    }

    float lastSmallFloat, lastDamageNumber;
    int smallFloats { get { int n = 0; foreach (var f in floats) if (!f.big) n++; return n; } }

    static bool IsScorePopup(string t)
    {
        if (t.Length < 2 || t[0] != '+') return false;
        for (int i = 1; i < t.Length; i++) if (!char.IsDigit(t[i])) return false;
        return true;
    }

    /// Números de dano: POUCOS = nenhum; NORMAL = só críticos, no máximo ~4 por segundo; TODOS = como antes.
    void AddDamageNumber(Vector3 at, float dmg, bool crit)
    {
        int mode = TextMode;
        if (mode == 0) return;
        if (mode == 1)
        {
            if (!crit || Time.time - lastDamageNumber < 0.25f) return;
            lastDamageNumber = Time.time;
        }
        string txt = dmg < 10f ? dmg.ToString("0.#") : Mathf.RoundToInt(dmg).ToString();
        AddFloat(at, crit ? txt + "!" : txt, crit ? new Color(1f, 0.85f, 0.1f) : Color.white, false);
    }

    void UpdateFloats(float udt)
    {
        for (int i = floats.Count - 1; i >= 0; i--)
        {
            var f = floats[i];
            f.t -= udt;
            f.pos += new Vector3(0f, 2f * udt, speed * Time.deltaTime);
            if (f.t <= 0f) floats.RemoveAt(i);
        }
    }


    public void Explode(Vector3 pos, Color color, int count = 12)
    {
        if (IsMobile) count = Mathf.Max(2, count / 2);
        // limite de pedacinhos na tela ao mesmo tempo
        int room = DebrisBudget - RunnerDebris.Live;
        if (room <= 0) return;
        if (RunnerDebris.Live > DebrisBudget / 2) count = Mathf.Max(1, count / 2);
        count = Mathf.Min(count, room);
        var mat = Mat(color);
        for (int i = 0; i < count; i++)
        {
            // pedacinhos reaproveitados (sem criar e destruir objetos a cada explosão)
            var d = RunnerDebris.Rent();
            GameObject go;
            if (d == null)
            {
                MeshRenderer goR;
                go = NewPrim(PrimitiveType.Cube, out goR, false);
                d = go.AddComponent<RunnerDebris>();
                d.rend = goR;
                d.pooled = true;
            }
            else
            {
                go = d.gameObject;
                go.SetActive(true);
            }
            d.rend.sharedMaterial = mat;
            go.transform.position = pos + Random.insideUnitSphere * 0.4f;
            go.transform.rotation = Quaternion.identity;
            float s = Random.Range(0.12f, 0.3f);
            go.transform.localScale = Vector3.one * s;
            d.velocity = Random.insideUnitSphere * 7f + new Vector3(0, 5f, speed * 0.6f);
            d.life = 0.8f;
            d.gravity = true;
            d.spin = true;
            d.grow = false;
            d.Begin();
        }
    }

    void FxSphere(Vector3 pos, float radius, Color color)
    {
        if (radius < 6f && RunnerDebris.LiveSpheres >= 5) return;   // muitas ondas juntas: pula as pequenas
        MeshRenderer goR;
        var go = RunnerGame.NewPrim(PrimitiveType.Sphere, out goR, false);
        goR.sharedMaterial = Glow(color);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * radius * 1.6f;
        var d = go.AddComponent<RunnerDebris>();
        d.velocity = new Vector3(0f, 0f, speed);
        d.gravity = false;
        d.spin = false;
        d.grow = true;
        d.life = 0.22f;
        d.countsAsSphere = true;
    }

    void Zap(Vector3 a, Vector3 b)
    {
        MeshRenderer goR;
        var go = RunnerGame.NewPrim(PrimitiveType.Cube, out goR, false);
        goR.sharedMaterial = Glow(new Color(0.6f, 0.85f, 1f));
        Vector3 dir = b - a;
        go.transform.position = (a + b) * 0.5f;
        if (dir.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.LookRotation(dir);
        go.transform.localScale = new Vector3(0.12f, 0.12f, dir.magnitude);
        var d = go.AddComponent<RunnerDebris>();
        d.gravity = false;
        d.spin = false;
        d.life = 0.15f;
    }

    // ================================================================== audio

    void SetupAudio()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sShoot = MakeClip("shoot", 0.09f, 1400f, 500f, 0.15f);
        sBoom = MakeClip("boom", 0.35f, 180f, 40f, 0.8f);
        sHurt = MakeClip("hurt", 0.3f, 220f, 80f, 0.35f);
        sPickup = MakeClip("pickup", 0.25f, 600f, 1500f, 0f);
        sEnemyShot = MakeClip("eshot", 0.15f, 500f, 250f, 0.2f);
        sClank = MakeClip("clank", 0.06f, 2500f, 1800f, 0.4f);
        sLevel = MakeClip("level", 0.45f, 400f, 1800f, 0.05f);
        sShield = MakeClip("shield", 0.2f, 900f, 1300f, 0.1f);

        // sons sintetizados com timbres do Oriente (RunnerAudio.cs / RunnerSynth.cs)
        fx = gameObject.AddComponent<RunnerSfx>();
        fx.Build();
        sLevel = fx.Get("abrir_carta") ?? sLevel;
        sShield = fx.Get("escudo") ?? sShield;
        sPickup = fx.Get("caixa") ?? sPickup;
        music = gameObject.AddComponent<RunnerMusic>();
        music.Prewarm("menu", "jerusalem", "chefe", "egito", "vitoria", "roma", "carruagem", "sheol", "satanas", "noite", "jerico", "golias", "babel");
    }

    RunnerSfx fx;
    RunnerMusic music;
    string lastMusic = "menu";

    /// Toca um efeito sonoro do banco (com variação de tom e limite de repetição).
    public void Sfx(string id, float vol = 1f, float pitchVar = 0.05f, float minGap = 0.04f, float pitch = 1f)
    {
        if (fx != null) fx.Play(id, vol, pitchVar, minGap, pitch);
    }

    /// Escolhe a música pelo momento do jogo e pela região.
    string MusicId()
    {
        if (trailerActive) return TrailerMusic();
        if (state == RunnerState.Menu || state == RunnerState.Temple || state == RunnerState.Stable || state == RunnerState.Deck || state == RunnerState.Codex) return "menu";
        if (state == RunnerState.Cutscene) return cineKind == 0 ? "jerusalem" : "vitoria";
        if (boss != null && boss.isFinal) return "satanas";
        if (boss != null || bossPending) return (bossesDefeated + 1 == finalBossNumber) ? "satanas" : "chefe";
        if (passover) return "noite";
        if (jericho) return "jerico";
        if (goliath) return "golias";
        if (babel) return "babel";
        if (bulletHell) return "chefe";
        if (racing || flightPending || (player != null && player.flying)) return "carruagem";
        if (Theme == Biomes.Sheol) return "sheol";
        if (Theme == Biomes.Rome) return "roma";
        if (Theme == Biomes.Egypt) return "egito";
        return "jerusalem";
    }

    void UpdateMusic()
    {
        if (music == null) return;
        if (state != RunnerState.GameOver) lastMusic = MusicId();
        float duck = state == RunnerState.Paused ? 0.4f : (state == RunnerState.GameOver ? 0.25f : (state == RunnerState.LevelUp || state == RunnerState.Choice ? 0.7f : 1f));
        music.Want(lastMusic, duck);
    }

    void ToggleMusic()
    {
        RunnerMusic.Enabled = !RunnerMusic.Enabled;
        ShowPopup(RunnerMusic.Enabled ? "MÚSICA LIGADA" : "MÚSICA DESLIGADA");
    }

    static AudioClip MakeClip(string name, float dur, float f0, float f1, float noise)
    {
        const int sr = 44100;
        int n = Mathf.Max(1, (int)(sr * dur));
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float f = Mathf.Lerp(f0, f1, t);
            phase += 2f * Mathf.PI * f / sr;
            float tone = Mathf.Sin(phase) > 0 ? 0.5f : -0.5f;
            float s = tone * (1f - noise) + (Random.value * 2f - 1f) * noise;
            float env = (1f - t) * (1f - t);
            data[i] = s * env * 0.5f;
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    public void Play(AudioClip clip, float vol)
    {
        if (clip != null && sfx != null) sfx.PlayOneShot(clip, vol);
    }

    public void PlayShoot()
    {
        if (Time.unscaledTime - lastShootSfx < 0.05f) return;
        lastShootSfx = Time.unscaledTime;
        Play(sShoot, 0.3f);
    }

    public void PlayBoom()
    {
        if (Time.unscaledTime - lastBoomSfx < 0.05f) return;
        lastBoomSfx = Time.unscaledTime;
        Play(sBoom, 0.55f);
    }

    // ================================================================== bênçãos

    int manaCounter, manaPortions, aaronCounter;
    float trumpetTimer = 12f, skyFireTimer = 6f, orbitTimer;
    Transform orbitRoot;

    void UpdateBlessings(float dt)
    {
        var pp = player.transform.position;

        if (stats.trumpets)
        {
            trumpetTimer -= dt;
            if (trumpetTimer <= 0f)
            {
                int n = 0;
                var list = new List<RunnerObstacle>(obstacles);
                foreach (var o in list)
                {
                    if (o == null || o.dead || o.airborne) continue;
                    if (o.type != ObType.Wall && o.type != ObType.Barrier) continue;
                    float dz = o.transform.position.z - pp.z;
                    if (dz < 2f || dz > 60f) continue;
                    DestroyObstacle(o, false);
                    n++;
                }
                if (stats.judgmentTrumpet)
                {
                    // Trombeta do Juízo: apaga o fogo inimigo e chama fogo do céu em 3 inimigos
                    foreach (var o in new List<RunnerObstacle>(obstacles))
                        if (o != null && !o.dead && o.type == ObType.EnemyShot && o.transform.position.z - pp.z < 60f)
                            DestroyObstacle(o, false);
                    int strikes = 0;
                    for (int k = 0; k < 3; k++) if (SkyFireStrike(pp)) strikes++;
                    n += strikes;
                }
                if (n > 0)
                {
                    trumpetTimer = stats.judgmentTrumpet ? 8f : 12f;
                    AddFloat(pp + new Vector3(0f, 2.5f, 6f), "JERICÓ!", new Color(1f, 0.85f, 0.3f), true);
                    Sfx(stats.judgmentTrumpet ? "shofar_longo" : "shofar", 0.9f, 0.03f, 0.5f);
                    for (int k = 0; k < 3; k++) CardFx.Ring(player.transform, new Color(1f, 0.85f, 0.35f), 10f, 0.8f, k * 0.18f, 0.8f);
                    shake = Mathf.Max(shake, 0.5f);
                }
                else trumpetTimer = 1f;   // espera aparecer uma muralha
            }
        }

        if (stats.skyFire > 0)
        {
            skyFireTimer -= dt;
            if (skyFireTimer <= 0f)
            {
                if (SkyFireStrike(pp)) skyFireTimer = 6f / (1f + 0.35f * (stats.skyFire - 1));
                else skyFireTimer = 0.5f;
            }
        }

        if (stats.orbitBlades > 0)
        {
            if (orbitRoot == null) RefreshOrbitBlades();
            orbitTimer -= dt;
            if (orbitTimer <= 0f)
            {
                orbitTimer = 0.25f;
                float dmg = stats.Damage * 0.35f * stats.orbitBlades;
                var list = SnapshotObstacles();
                foreach (var o in list)
                {
                    if (o == null || o.dead || !o.Shootable || o.type == ObType.Boss) continue;
                    if (!InMeleeBox(o, pp, 2.3f, 2.3f)) continue;
                    DamageEnemy(o, dmg, false, o.transform.position);
                    Sfx("laminas", 0.4f, 0.15f, 0.2f);
                }
                ReleaseList(list);
            }
        }
    }

    /// Fogo do céu sobre um inimigo aleatório à frente. Retorna false se não havia alvo.
    bool SkyFireStrike(Vector3 pp)
    {
        RunnerObstacle best = null;
        int seen = 0;
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || !o.Shootable || o.type == ObType.EnemyShot) continue;
            float dz = o.transform.position.z - pp.z;
            if (dz < 8f || dz > 60f) continue;
            seen++;
            if (Random.Range(0, seen) == 0) best = o;   // escolhe um aleatório
        }
        if (best == null) return false;
        Vector3 c = best.transform.position;
        MeshRenderer goR;
        var go = RunnerGame.NewPrim(PrimitiveType.Cube, out goR, false);
        goR.sharedMaterial = Glow(new Color(1f, 0.55f, 0.1f));
        go.transform.position = c + new Vector3(0f, 7f, 0f);
        go.transform.localScale = new Vector3(0.9f, 14f, 0.9f);
        var d = go.AddComponent<RunnerDebris>();
        d.gravity = false; d.spin = false; d.life = 0.3f;
        Sfx("fogo", 0.7f, 0.1f, 0.1f);
        hitKind = HitFire;
        DamageEnemy(best, stats.Damage * 4f, true, c);
        hitKind = 0;
        Blast(c, 2.5f, stats.Damage * 1.5f, best);
        return true;
    }

    /// (Re)cria as espadas flamejantes que giram em volta do jogador.
    public void RefreshOrbitBlades()
    {
        if (orbitRoot != null) Destroy(orbitRoot.gameObject);
        orbitRoot = null;
        if (stats.orbitBlades <= 0 || player == null) return;
        orbitRoot = new GameObject("OrbitBlades").transform;
        orbitRoot.SetParent(player.transform, false);
        orbitRoot.gameObject.AddComponent<RunnerSpin>().speed = 280f;
        int n = stats.orbitBlades * 2;
        for (int i = 0; i < n; i++)
        {
            float a = i * 360f / n;
            var arm = new GameObject("Blade").transform;
            arm.SetParent(orbitRoot, false);
            arm.localRotation = Quaternion.Euler(0f, a, 0f);
            Prim(PrimitiveType.Cube, arm, new Vector3(0f, 0.1f, 1.7f), new Vector3(0.1f, 0.06f, 0.9f), new Color(1f, 0.55f, 0.15f), true);
            Prim(PrimitiveType.Cube, arm, new Vector3(0f, 0.1f, 1.2f), new Vector3(0.3f, 0.06f, 0.06f), Gold);
        }
    }

    /// Pisão do Querubim: chamado pelo jogador ao aterrissar de um pulo.
    public void OnPlayerLanded(Vector3 pos)
    {
        if (stats.stompLevel <= 0) return;
        Blast(pos + new Vector3(0f, 0f, 1f), 3f + stats.stompLevel, stats.Damage * (1f + 0.5f * stats.stompLevel), null);
        shake = Mathf.Max(shake, 0.25f);
        Sfx("pisao", 0.8f, 0.08f, 0.15f);
    }

    /// Abrir o Mar: varre tudo nos próximos 45 m.
    void PartTheSea()
    {
        var pp = player.transform.position;
        Sfx("mar", 0.9f, 0.03f, 0.5f);
        CardFx.WaterWalls(player.transform);
        var list = new List<RunnerObstacle>(obstacles);
        foreach (var o in list)
        {
            if (o == null || o.dead || o.type == ObType.Boss || o.type == ObType.BossDrone || o.type == ObType.Rival
                || o.type == ObType.HouseOpen || o.type == ObType.HouseBlood || o.Indestructible || o.type == ObType.Platform || o.type == ObType.Coin) continue;
            float dz = o.transform.position.z - pp.z;
            if (dz < -2f || dz > 45f) continue;
            DestroyObstacle(o, o.Shootable && o.type != ObType.EnemyShot);
        }
        player.invuln += 0.5f;
        AddFloat(pp + new Vector3(0f, 2.5f, 5f), "O MAR SE ABRIU!", new Color(0.4f, 0.8f, 1f), true);
    }

    // ================================================================== corpo a corpo

    bool InMeleeBox(RunnerObstacle o, Vector3 origin, float reach, float halfWidth)
    {
        Vector3 c = o.transform.position;
        float dz = c.z - origin.z;
        if (dz < -1.2f - o.half.z || dz > reach + o.half.z) return false;
        if (Mathf.Abs(c.x - origin.x) > halfWidth + o.half.x) return false;
        if (Mathf.Abs(c.y - origin.y) > 2.2f + o.half.y) return false;
        return true;
    }

    public bool EnemyInMelee(Vector3 origin, float reach, float halfWidth)
    {
        foreach (var o in obstacles)
            if (o != null && !o.dead && o.Shootable && InMeleeBox(o, origin, reach, halfWidth)) return true;
        return false;
    }

    /// Golpe corpo a corpo: acerta tudo que estiver na área à frente (e destrói bolas de fogo).
    public void MeleeAttack(Vector3 origin, float reach, float halfWidth, float dmg, RunnerWeapon w)
    {
        var list = SnapshotObstacles();
        int hits = 0;
        foreach (var o in list)
        {
            if (o == null || o.dead || !InMeleeBox(o, origin, reach, halfWidth)) continue;
            if (!o.Shootable)
            {
                // a espada ricocheteia em escudos e no que é indestrutível
                if (o.stompable || o.Indestructible) { ImmuneHint(o); PlayClank(); }
                continue;
            }
            Vector3 c = o.transform.position;
            DamageEnemy(o, dmg, true, c);
            ChainLightning(o, dmg * 0.5f);
            if (stats.Explode > 0f) Blast(c, stats.Explode, dmg * 0.6f, o);
            hits++;
        }
        ReleaseList(list);
        SpawnSlash(origin, Mathf.Min(reach, 5f), halfWidth, w);
        if (hits > 0) shake = Mathf.Max(shake, 0.12f + hits * 0.03f);
        if (hits > 0) Sfx("espada_hit", 0.55f, 0.12f, 0.06f, w.id == "martelo" ? 0.6f : 1f);
    }

    /// Rastro visual do golpe (arco brilhante que some rápido).
    void SpawnSlash(Vector3 origin, float reach, float halfWidth, RunnerWeapon w)
    {
        int segs = w.id == "lanca" ? 3 : 7;
        for (int i = 0; i < segs; i++)
        {
            float k = segs == 1 ? 0.5f : i / (float)(segs - 1);
            Vector3 p;
            Quaternion rot;
            if (w.id == "lanca")
            {
                p = origin + new Vector3(0f, 0.2f, 1.2f + k * (reach - 1f));
                rot = Quaternion.identity;
            }
            else
            {
                float a = Mathf.Lerp(-60f, 60f, k) * Mathf.Deg2Rad;
                float rx = Mathf.Min(halfWidth, 3.8f);
                p = origin + new Vector3(Mathf.Sin(a) * rx, w.id == "martelo" ? -0.5f : 0.3f, 1.2f + Mathf.Cos(a) * Mathf.Min(reach, 2.5f));
                rot = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
            }
            MeshRenderer goR;
            var go = RunnerGame.NewPrim(PrimitiveType.Cube, out goR, false);
            goR.sharedMaterial = Glow(w.color);
            go.transform.position = p;
            go.transform.rotation = rot;
            go.transform.localScale = w.id == "lanca" ? new Vector3(0.12f, 0.12f, 1.4f) : new Vector3(0.12f, 0.06f, 0.9f);
            var d = go.AddComponent<RunnerDebris>();
            d.gravity = false;
            d.spin = false;
            d.velocity = new Vector3(0f, 0f, speed);
            d.life = 0.14f;
        }
    }

    /// Transforma um projétil numa "onda de luz" achatada e larga.
    public void MakeWave(RunnerBullet b, RunnerWeapon w, float size)
    {
        if (b == null) return;
        float width = w.id == "martelo" ? 3.2f : (w.id == "lanca" ? 0.5f : 1.6f);
        b.radius = Mathf.Max(b.radius, width * 0.5f * size);
        b.transform.localScale = new Vector3(width * size, 0.1f, 0.35f);
    }

    public void PlaySwing()
    {
        if (Time.unscaledTime - lastShootSfx < 0.05f) return;
        lastShootSfx = Time.unscaledTime;
        Sfx("espada", 0.6f, 0.15f, 0.05f);
    }

    public void PlayClank()
    {
        if (Time.unscaledTime - lastClankSfx < 0.04f) return;
        lastClankSfx = Time.unscaledTime;
        Play(sClank, 0.35f);
    }

    // ================================================================== UI

    void EnsureStyles()
    {
        if (bigStyle != null) return;
        bigStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        midStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        smallStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
        tinyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
        cardTitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        cardDesc = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
        cardSmall = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        floatStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
    }

    void ShadowLabel(Rect r, string text, GUIStyle style, Color color)
    {
        var old = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, 0.7f * color.a);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        style.normal.textColor = color;
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }

    static void Box(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    string BuildSummary(int maxLines)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, int>();
        var names = new Dictionary<string, string>();
        foreach (var c in history)
        {
            if (!counts.ContainsKey(c.id)) { order.Add(c.id); counts[c.id] = 0; names[c.id] = c.name; }
            counts[c.id]++;
        }
        var sb = new System.Text.StringBuilder();
        int lines = 0;
        foreach (var id in order)
        {
            if (lines >= maxLines) { sb.Append("..."); break; }
            sb.Append(names[id]);
            if (counts[id] > 1) sb.Append("  x").Append(counts[id]);
            sb.Append('\n');
            lines++;
        }
        return sb.ToString();
    }

    int lastGuiFrame = -1;
    bool guiNewFrame;

    void OnGUI()
    {
        EnsureStyles();
        guiNewFrame = Time.frameCount != lastGuiFrame;   // sem GUILayout: limpa no 1º evento de cada quadro
        lastGuiFrame = Time.frameCount;
        if (guiNewFrame)
        {
            RunnerTouch.ClearButtons();
            uiActions.Clear();
            uiRects.Clear();
        }
        if (Event.current.type == EventType.Repaint && ps1 != null) ps1.DrawToScreen();

        // área segura (notch, barras arredondadas) e escala pela menor dimensão
        float sw = Screen.width, sh = Screen.height;
        Rect sa = Screen.safeArea;
        var safe = new Rect(sa.x, sh - sa.yMax, sa.width, sa.height);
        if (safe.width < 10f || safe.height < 10f) safe = new Rect(0, 0, sw, sh);
        float s = Mathf.Min(safe.height / 1080f, safe.width / 1280f);
        guiOffset = safe.position;

        if (!trailerActive && state != RunnerState.Menu && state != RunnerState.Cutscene && state != RunnerState.Stable && state != RunnerState.Deck && state != RunnerState.Codex) DrawWorldUI(s, sh);

        GUI.BeginGroup(safe);
        DrawScreens(s, safe.width, safe.height);
        GUI.EndGroup();
        DrawSkyFlash(sw, sh);   // clarão da subida/descida da travessia
    }

    /// Botão de toque: desenha e registra a área para RunnerTouch.
    bool TouchButton(string id, Rect r, string label, Color color)
    {
        Box(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.35f));
        Box(r, color);
        var st = Sty(cardSmall, al: TextAnchor.MiddleCenter, ww: 1);
        ShadowLabel(r, label, st, Color.white);
        RunnerTouch.SetButton(id, new Rect(r.x + guiOffset.x, r.y + guiOffset.y, r.width, r.height));
        return RunnerTouch.Pressed(id);
    }

    void DrawScreens(float s, float W, float H)
    {
        bigStyle.fontSize = Mathf.RoundToInt(96 * s);
        midStyle.fontSize = Mathf.RoundToInt(36 * s);
        smallStyle.fontSize = Mathf.RoundToInt(32 * s);
        tinyStyle.fontSize = Mathf.RoundToInt(22 * s);
        cardTitle.fontSize = Mathf.RoundToInt(34 * s);
        cardDesc.fontSize = Mathf.RoundToInt(26 * s);
        cardSmall.fontSize = Mathf.RoundToInt(22 * s);

        if (state == RunnerState.Menu)
        {
            DrawMenu(s, W, H);
            return;
        }
        if (state == RunnerState.Temple)
        {
            DrawTemple(s, W, H);
            return;
        }
        if (state == RunnerState.Cutscene)
        {
            DrawCinema(s, W, H);
            return;
        }
        if (state == RunnerState.Stable)
        {
            DrawStable(s, W, H);
            return;
        }
        if (state == RunnerState.Deck)
        {
            DrawDeckEditor(s, W, H);
            return;
        }
        if (state == RunnerState.Codex)
        {
            DrawCodex(s, W, H);
            return;
        }

        if (trailerActive) { DrawTrailer(s, W, H); return; }
        DrawHUD(s, W, H);
        if (RunnerTouch.UseTouchUI) DrawTouchControls(s, W, H);

        if (state == RunnerState.Paused)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            ShadowLabel(new Rect(0, H * 0.12f, W, 120 * s), "PAUSADO", bigStyle, Color.white);
            ShadowLabel(new Rect(0, H * 0.12f + 110 * s, W, 60 * s), RunnerTouch.UseTouchUI ? "Toque para continuar" : "Esc / P para continuar", midStyle, Color.white);

            // opção: filtro PS1
            bool on = ps1 != null && ps1.styleOn;
            float bw = 480 * s, bh = 74 * s;
            var btn = new Rect(W / 2 - bw / 2, H * 0.12f + 178 * s, bw, bh);
            string lbl = "Filtro PS1:  " + (on ? "LIGADO" : "DESLIGADO") + (RunnerTouch.UseTouchUI ? "" : "   [V]");
            Color bc = on ? new Color(0.2f, 0.55f, 0.3f, 0.85f) : new Color(0.45f, 0.2f, 0.2f, 0.85f);
            // opção: música
            bool mOn = RunnerMusic.Enabled;
            ActionButton("music_p", new Rect(btn.xMax + 14 * s, btn.y, 300 * s, bh), "Música: " + (mOn ? "LIGADA" : "DESLIGADA") + (RunnerTouch.UseTouchUI ? "" : "  [M]"),
                mOn ? new Color(0.2f, 0.45f, 0.55f, 0.85f) : new Color(0.45f, 0.2f, 0.2f, 0.85f), s, ToggleMusic);
            DrawPauseOptions(s, W, H, btn);
            DrawPerfButton(s, W);
            if (RunnerTouch.UseTouchUI)
            {
                TouchButton("ps1", btn, lbl, bc);
            }
            else
            {
                var e = Event.current;
                bool hover = btn.Contains(e.mousePosition);
                Box(new Rect(btn.x + 3, btn.y + 3, btn.width, btn.height), new Color(0, 0, 0, 0.35f));
                Box(btn, hover ? Color.Lerp(bc, Color.white, 0.2f) : bc);
                ShadowLabel(btn, lbl, Sty(cardSmall, al: TextAnchor.MiddleCenter, fs: Mathf.RoundToInt(28 * s)), Color.white);
                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    TogglePS1();
                    e.Use();
                }
            }

            float panelBottom = devTools ? H - 120 * s : H * 0.86f;
            DrawBuildPanel(s, W * 0.5f - 300 * s, H * 0.12f + 356 * s, 600 * s, panelBottom - (H * 0.12f + 356 * s));

            if (devTools)
            {
                // atalhos de desenvolvedor
                float dw = 260 * s, dh = 70 * s, gap = 16 * s;
                dw = Mathf.Min(dw, (W - 40 * s - gap * 4) / 5f);
                float total = dw * 5 + gap * 4;
                float x0 = W / 2 - total / 2, y0 = H - dh - 30 * s;
                ShadowLabel(new Rect(0, y0 - 34 * s, W, 30 * s), "MODO DESENVOLVEDOR", Sty(cardSmall), new Color(1f, 0.6f, 0.3f));
                var devColor = new Color(0.55f, 0.3f, 0.05f, 0.85f);
                if (DevButton("dev_boss3", new Rect(x0, y0, dw, dh), "PULAR P/ 3º CHEFE", devColor, s)) DevSkipToBoss(3);
                else if (DevButton("dev_egypt", new Rect(x0 + dw + gap, y0, dw, dh), "IR PARA O EGITO", devColor, s)) DevSkipToBiome(Biomes.Egypt);
                else if (DevButton("dev_rome", new Rect(x0 + (dw + gap) * 2, y0, dw, dh), "IR PARA ROMA", devColor, s)) DevSkipToBiome(Biomes.Rome);
                else if (DevButton("dev_sheol", new Rect(x0 + (dw + gap) * 3, y0, dw, dh), "IR PARA O SHEOL", devColor, s)) DevSkipToBiome(Biomes.Sheol);
                else if (DevButton("dev_satan", new Rect(x0 + (dw + gap) * 4, y0, dw, dh), "PULAR P/ SATANÁS", new Color(0.5f, 0.02f, 0.05f, 0.9f), s)) DevSkipToBoss(finalBossNumber);
            }
        }

        if (state == RunnerState.LevelUp) DrawCardChoice(s, W, H);
        if (state == RunnerState.Choice) DrawChoice(s, W, H);

        if (state == RunnerState.GameOver)
        {
            Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
            ShadowLabel(new Rect(0, H * 0.1f, W, 120 * s), "FIM DE JOGO", bigStyle, new Color(1f, 0.35f, 0.3f));
            ShadowLabel(new Rect(0, H * 0.1f + 120 * s, W, 60 * s), "Pontos: " + Score + "   •   Abates: " + kills + "   •   Nível " + level, midStyle, Color.white);
            bool newRecord = Score >= highScore && Score > 0;
            ShadowLabel(new Rect(0, H * 0.1f + 175 * s, W, 60 * s), newRecord ? "NOVO RECORDE!" : "Recorde: " + highScore, midStyle, new Color(1f, 0.85f, 0.3f));
            ShadowLabel(new Rect(0, H * 0.1f + 225 * s, W, 50 * s), "+" + lastTalentsEarned + " Talentos" + (Meta.ActiveOaths > 0 ? " (juramentos x" + Meta.OathTalentMul.ToString("0.00") + ")" : "") + "  (total: " + Meta.Talents + ")  —  gaste no Templo, no menu", midStyle, new Color(0.7f, 1f, 0.75f));
            DrawBuildPanel(s, W * 0.5f - 300 * s, H * 0.38f, 600 * s, H * 0.42f);
            if (Time.unscaledTime - gameOverTime > 0.8f)
                ActionButton("go_menu", new Rect(W / 2 - 170 * s, H * 0.81f, 340 * s, 60 * s), "MENU / TEMPLO", new Color(0.3f, 0.25f, 0.1f, 0.85f), s, () => { ResetRun(); state = RunnerState.Menu; });
            if (Time.unscaledTime - gameOverTime > 0.8f && Mathf.Repeat(Time.unscaledTime, 1f) < 0.7f)
                ShadowLabel(new Rect(0, H * 0.9f, W, 60 * s), RunnerTouch.UseTouchUI ? "Toque para jogar de novo" : "ENTER / R / clique para jogar de novo", midStyle, new Color(0.5f, 1f, 0.6f));
        }
    }

    /// Botão que funciona com mouse (retorna true no clique) e com toque (tratado no Update).
    bool DevButton(string id, Rect r, string label, Color c, float s)
    {
        if (RunnerTouch.UseTouchUI)
        {
            TouchButton(id, r, label, c);
            return false;
        }
        var e = Event.current;
        bool hover = r.Contains(e.mousePosition);
        Box(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.35f));
        Box(r, hover ? Color.Lerp(c, Color.white, 0.2f) : c);
        ShadowLabel(r, label, Sty(cardSmall, al: TextAnchor.MiddleCenter, fs: Mathf.RoundToInt(24 * s)), Color.white);
        if (e.type == EventType.MouseDown && e.button == 0 && hover)
        {
            e.Use();
            return true;
        }
        return false;
    }

    // ================================================================== atalhos de desenvolvedor

    /// Sai de qualquer modo especial e limpa a pista (para os atalhos).
    void DevResetToRunning()
    {
        if (bulletHell) EndBulletHell();
        if (babel) EndBabel();
        if (goliath) EndGoliath();
        if (jericho) { jericho = false; CleanupJericho(); ApplyAtmosphere(); }
        if (racing)
        {
            racing = false;
            if (finishBanner != null) Destroy(finishBanner);
            SetRaceScenery(false);
            player.EndCar();
        }
        if (player.flying) EndFlight();
        if (passover) EndPassover(false);
        flightPending = false;
        if (boss != null)
        {
            boss.CleanupAll(false);
            if (boss.body != null) { boss.body.dead = true; obstacles.Remove(boss.body); Destroy(boss.gameObject); }
            boss = null;
        }
        bossPending = false;
        for (int i = obstacles.Count - 1; i >= 0; i--)
            if (obstacles[i] != null) Destroy(obstacles[i].gameObject);
        obstacles.Clear();
        nextSpawnZ = player.transform.position.z + 40f;
    }

    /// Dá cartas aleatórias (sem maldições) até chegar no nível desejado, para a build ficar parecida com uma jogatina real.
    void DevGrantCardsUntil(int targetLevel)
    {
        offerIsBoss = false;
        int guard = 0;
        while (level < targetLevel && guard++ < 200)
        {
            var roll = DrawOffer(1);
            ReturnHand();
            if (roll.Count == 0) break;
            var c = roll[0];
            if (c.curse) continue;
            cardStacks[c.id] = Stacks(c) + 1;
            history.Add(c);
            c.apply(this);
            level++;
        }
        maxLives = Mathf.Clamp(maxLives, 1, hardMaxLives);
        lives = maxLives;
        runTime = Mathf.Max(runTime, 40f + level * 12f);
        prevCardScore = Score;
        nextCardScore = Score + CardStep();
    }

    void DevResume(string msg)
    {
        state = RunnerState.Playing;
        player.inputLock = 0.3f;
        player.invuln = Mathf.Max(player.invuln, 2f);
        ShowPopup(msg);
    }

    /// Pula direto para o chefe N (ex.: 3º chefe — derrotá-lo leva a Roma).
    public void DevSkipToBoss(int bossNumber)
    {
        DevResetToRunning();
        int every = Mathf.Max(1, bossEveryLevels);
        bossesDefeated = Mathf.Max(bossesDefeated, bossNumber - 1);
        SetBiome(Biomes.ForBosses(bossesDefeated, egyptAfterBoss, romeAfterBoss, sheolAfterBoss), false);
        DevGrantCardsUntil(bossNumber * every);
        bossPending = true;
        bossWarn = 1.5f;
        DevResume("DEV: " + bossNumber + "º CHEFE");
    }

    /// Vai direto para um bioma (Roma ou Sheol), com uma build compatível.
    public void DevSkipToBiome(BiomeTheme target)
    {
        DevResetToRunning();
        int every = Mathf.Max(1, bossEveryLevels);
        int bosses = target.id == 2 ? sheolAfterBoss : (target.id == 1 ? romeAfterBoss : (target.id == 3 ? egyptAfterBoss : 0));
        bossesDefeated = Mathf.Max(bossesDefeated, bosses);
        DevGrantCardsUntil(bosses * every + 1);
        SetBiome(target, true);
        DevResume("DEV: " + target.arrivalText);
    }

    void DrawTouchControls(float s, float W, float H)
    {
        if (state != RunnerState.Playing && state != RunnerState.Paused) return;
        float b = 92 * s;
        TouchButton("pause", new Rect(W - b - 16 * s, 16 * s, b, b), state == RunnerState.Paused ? "▶" : "II", new Color(0f, 0f, 0f, 0.45f));

        if (state == RunnerState.Playing && stats.bulletTime)
        {
            float a = 170 * s;
            bool ready = btActive <= 0f && btCooldownLeft <= 0f;
            string lbl = btActive > 0f ? "TEMPO\nBALA!" : (ready ? "TEMPO\nBALA" : Mathf.CeilToInt(btCooldownLeft) + "s");
            TouchButton("ability", new Rect(W - a - 30 * s, H - a - 30 * s, a, a), lbl,
                ready ? new Color(1f, 0.7f, 0.1f, 0.6f) : new Color(0.3f, 0.3f, 0.3f, 0.45f));
        }

        // joystick visual no avião / carro
        if (state == RunnerState.Playing && (player.flying || player.driving) && RunnerTouch.Holding)
        {
            Vector2 st = RunnerTouch.Stick;
            float r = 120 * s;
            var c = new Vector2(W * 0.5f, H * 0.78f);
            Box(new Rect(c.x - r, c.y - 3 * s, r * 2, 6 * s), new Color(1, 1, 1, 0.25f));
            Box(new Rect(c.x - 3 * s, c.y - r, 6 * s, r * 2), new Color(1, 1, 1, 0.25f));
            Box(new Rect(c.x + st.x * r - 22 * s, c.y - st.y * r - 22 * s, 44 * s, 44 * s), new Color(1, 1, 1, 0.6f));
        }
    }

    // ================================================================== mini-jogos no menu

    static readonly string[] MenuGames = { "voo", "corrida", "nave", "anjo", "babel", "jerico", "golias" };
    static readonly string[] MenuGameNames = { "CARRO DE FOGO", "CARRUAGEM", "NAVE", "ANJO (PÁSCOA)", "TORRE DE BABEL", "JERICÓ", "DAVI E GOLIAS" };
    readonly Dictionary<string, Rect> menuButtons = new Dictionary<string, Rect>();   // coordenadas GUI absolutas

    /// Retorna o mini-jogo escolhido no menu neste frame (clique, toque ou teclas 1-4), ou null.
    string MenuMinigamePressed()
    {
        foreach (var id in MenuGames)
            if (RunnerTouch.Pressed("menu_" + id)) return id;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return MenuGames[0];
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return MenuGames[1];
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return MenuGames[2];
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return MenuGames[3];
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return MenuGames[4];
            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return MenuGames[5];
            if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return MenuGames[6];
        }

        var ms = Mouse.current;
        if (ms != null && ms.leftButton.wasPressedThisFrame && !RunnerTouch.UseTouchUI)
        {
            Vector2 mp = ms.position.ReadValue();
            var gui = new Vector2(mp.x, Screen.height - mp.y);
            foreach (var kv in menuButtons)
                if (kv.Value.Contains(gui)) return kv.Key;
        }
        return null;
    }

    /// Começa uma jogatina já dentro do mini-jogo (quando ele termina, a corrida normal continua).
    void StartMinigame(string id)
    {
        StartRun();
        switch (id)
        {
            case "voo": StartFlight(); break;
            case "corrida": StartRace(); break;
            case "nave": StartBulletHell(); break;
            case "anjo": StartPassover(); break;
            case "babel": StartBabel(); break;
            case "jerico": StartJericho(); break;
            case "golias": StartGoliath(); break;
        }
        player.inputLock = 0.3f;
    }

    void DrawMenuMinigames(float s, float W, float H)
    {
        if (guiNewFrame) menuButtons.Clear();
        int nGames = MenuGames.Length;
        float bw = 250 * s, bh = 66 * s, gap = 12 * s;
        float total = bw * nGames + gap * (nGames - 1);
        if (total > W - 20 * s)
        {
            float k = (W - 20 * s) / total;
            bw *= k; gap *= k; total = W - 20 * s;
        }
        float x0 = W / 2 - total / 2, y0 = menuGamesY;
        Color[] cols =
        {
            new Color(0.75f, 0.3f, 0.05f, 0.85f), new Color(0.55f, 0.35f, 0.15f, 0.85f),
            new Color(0.4f, 0.2f, 0.6f, 0.85f), new Color(0.5f, 0.05f, 0.05f, 0.85f),
            new Color(0.6f, 0.45f, 0.2f, 0.85f), new Color(0.7f, 0.55f, 0.25f, 0.85f),
            new Color(0.3f, 0.45f, 0.2f, 0.85f)
        };
        var mouse = Event.current.mousePosition;
        for (int i = 0; i < nGames; i++)
        {
            var r = new Rect(x0 + i * (bw + gap), y0, bw, bh);
            string id = MenuGames[i];
            string label = (RunnerTouch.UseTouchUI ? "" : (i + 1) + ". ") + MenuGameNames[i];
            if (id == "babel") label += "\n(" + RunnerBabel.StyleNames[RunnerBabel.NextStyle].ToLower() + ")";
            if (id == "jerico") label += "\n(" + JerichoStyles[JerichoNextStyle].ToLower() + ")";
            if (id == "golias") label += "\n(" + RunnerGoliath.StyleNames[RunnerGoliath.NextStyle].ToLower() + ")";
            if (RunnerTouch.UseTouchUI)
            {
                TouchButton("menu_" + id, r, label, cols[i]);
            }
            else
            {
                bool hover = r.Contains(mouse);
                Box(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.35f));
                Box(r, hover ? Color.Lerp(cols[i], Color.white, 0.2f) : cols[i]);
                ShadowLabel(r, label, Sty(cardSmall, al: TextAnchor.MiddleCenter, fs: Mathf.RoundToInt(24 * s)), Color.white);
            }
            menuButtons[id] = new Rect(r.x + guiOffset.x, r.y + guiOffset.y, r.width, r.height);
        }
    }

    /// Botão genérico: clique do mouse executa na hora; toque é executado no Update.
    void ActionButton(string id, Rect r, string label, Color c, float s, System.Action action, bool enabled = true)
    {
        var abs = new Rect(r.x + guiOffset.x, r.y + guiOffset.y, r.width, r.height);
        uiRects.Add(abs);
        if (!enabled) c = new Color(0.25f, 0.25f, 0.25f, 0.7f);
        if (Time.unscaledTime < uiBlockUntil) enabled = false;   // o toque que pulou a abertura não aperta botão do menu
        if (RunnerTouch.UseTouchUI)
        {
            TouchButton(id, r, label, c);
            if (enabled) uiActions[id] = () => { Sfx("clique", 0.5f, 0.05f, 0.03f); action(); };
            return;
        }
        var e = Event.current;
        bool hover = r.Contains(e.mousePosition);
        Box(new Rect(r.x + 3, r.y + 3, r.width, r.height), new Color(0, 0, 0, 0.35f));
        Box(r, hover && enabled ? Color.Lerp(c, Color.white, 0.2f) : c);
        ShadowLabel(r, label, Sty(cardSmall, al: TextAnchor.MiddleCenter, ww: 1, fs: Mathf.RoundToInt(22 * s)), enabled ? Color.white : new Color(0.7f, 0.7f, 0.7f));
        if (enabled && e.type == EventType.MouseDown && e.button == 0 && hover)
        {
            e.Use();
            Sfx("clique", 0.5f, 0.05f, 0.03f);
            action();
        }
    }

    /// O mouse está em cima de algum botão? (evita que o clique também comece a jogatina)
    bool MouseOverUI()
    {
        var ms = Mouse.current;
        if (ms == null || RunnerTouch.UseTouchUI) return false;
        Vector2 mp = ms.position.ReadValue();
        var gui = new Vector2(mp.x, Screen.height - mp.y);
        foreach (var r in uiRects) if (r.Contains(gui)) return true;
        foreach (var kv in menuButtons) if (kv.Value.Contains(gui)) return true;
        return false;
    }

    bool templeOaths;

    void DrawTemple(float s, float W, float H)
    {
        Box(new Rect(0, 0, W, H), new Color(0.05f, 0.04f, 0.02f, 0.88f));
        ShadowLabel(new Rect(0, H * 0.04f, W, 90 * s), "TEMPLO", bigStyle, new Color(1f, 0.85f, 0.3f));
        ShadowLabel(new Rect(0, H * 0.04f + 88 * s, W, 44 * s), "Talentos: " + Meta.Talents + "   —   \"Foste fiel no pouco, sobre o muito te colocarei\" (Mt 25:21)", midStyle, new Color(0.7f, 1f, 0.75f));

        float colW = Mathf.Min(620 * s, W * 0.46f), rowH = 70 * s, gap = 10 * s;
        float leftX = W * 0.5f - colW - 20 * s, rightX = W * 0.5f + 20 * s, top = H * 0.2f;

        // melhorias permanentes
        ShadowLabel(new Rect(leftX, top - 40 * s, colW, 36 * s), "BÊNÇÃOS PERMANENTES", Sty(cardSmall), new Color(1f, 0.85f, 0.4f));
        for (int i = 0; i < Meta.Upgrades.Count; i++)
        {
            var u = Meta.Upgrades[i];
            int lvl = Meta.Level(u);
            bool max = lvl >= u.MaxLevel;
            int cost = max ? 0 : u.costs[lvl];
            var r = new Rect(leftX, top + i * (rowH + gap), colW, rowH);
            string label = u.name + "  (" + lvl + "/" + u.MaxLevel + ")\n" + u.desc + (max ? "  —  MÁXIMO" : "  —  " + cost + " talentos");
            var uu = u;
            ActionButton("tpl_" + u.id, r, label, new Color(0.35f, 0.25f, 0.08f, 0.85f), s, () =>
            {
                templeMsg = Meta.TryBuy(uu) ? "Comprado: " + uu.name : "Talentos insuficientes";
                templeMsgTime = 2f;
                Play(sPickup, 0.6f);
            }, !max && Meta.Talents >= cost);
        }

        // abas: profetas / juramentos
        float tabW = colW / 2f - 5 * s;
        ActionButton("tab_pro", new Rect(rightX, top - 52 * s, tabW, 44 * s), "PROFETAS", templeOaths ? new Color(0.2f, 0.2f, 0.25f, 0.85f) : new Color(0.45f, 0.35f, 0.1f, 0.95f), s, () => templeOaths = false);
        ActionButton("tab_oath", new Rect(rightX + tabW + 10 * s, top - 52 * s, tabW, 44 * s), "JURAMENTOS (" + Meta.ActiveOaths + ")", templeOaths ? new Color(0.5f, 0.15f, 0.1f, 0.95f) : new Color(0.2f, 0.2f, 0.25f, 0.85f), s, () => templeOaths = true);
        if (templeOaths)
        {
            for (int i = 0; i < Meta.Oaths.Count; i++)
            {
                var o = Meta.Oaths[i];
                bool on = Meta.OathOn(o.id);
                var r = new Rect(rightX, top + i * (rowH + gap), colW, rowH);
                string label = (on ? "[ATIVO] " : "") + o.name + "  (" + o.verse + ")  +" + Mathf.RoundToInt(o.bonus * 100) + "% talentos\n" + o.desc;
                var oo = o;
                ActionButton("oath_" + o.id, r, label, on ? new Color(0.6f, 0.12f, 0.08f, 0.92f) : new Color(0.22f, 0.15f, 0.12f, 0.85f), s, () =>
                {
                    Meta.ToggleOath(oo);
                    templeMsg = (Meta.OathOn(oo.id) ? "Juramento feito: " : "Juramento desfeito: ") + oo.name + "  —  talentos x" + Meta.OathTalentMul.ToString("0.00");
                    templeMsgTime = 2.5f;
                    Play(sClank, 0.6f);
                });
            }
        }
        else
        for (int i = 0; i < Meta.Prophets.Count; i++)
        {
            var p = Meta.Prophets[i];
            bool un = Meta.IsUnlocked(p);
            bool sel = Meta.Selected == p;
            var r = new Rect(rightX, top + i * (rowH * 0.8f + gap), colW, rowH * 0.8f);
            string label = p.name + " — " + p.title + (un ? (sel ? "   [ESCOLHIDO]" : "   (liberado: toque para escolher)") : "   [BLOQUEADO] " + p.cost + " talentos");
            var pp = p;
            Color c = un ? (sel ? new Color(0.15f, 0.45f, 0.2f, 0.9f) : new Color(0.15f, 0.25f, 0.4f, 0.85f)) : new Color(0.3f, 0.15f, 0.1f, 0.85f);
            ActionButton("pro_" + p.id, r, label, c, s, () =>
            {
                if (Meta.IsUnlocked(pp)) { Meta.Selected = pp; templeMsg = pp.name + " escolhido"; }
                else if (Meta.TryUnlock(pp)) { Meta.Selected = pp; templeMsg = pp.name + " liberado!"; Play(sLevel, 0.7f); }
                else templeMsg = "Talentos insuficientes";
                templeMsgTime = 2f;
            }, un || Meta.Talents >= p.cost);
        }

        var cur = Meta.Selected;
        ShadowLabel(new Rect(W * 0.08f, H * 0.8f, W * 0.84f, 60 * s), cur.name + ": " + cur.desc, Sty(cardDesc, al: TextAnchor.MiddleCenter), cur.color);
        if (templeMsgTime > 0f)
            ShadowLabel(new Rect(0, H * 0.75f, W, 40 * s), templeMsg, midStyle, new Color(1f, 1f, 0.6f));
        ActionButton("tpl_back", new Rect(W / 2 - 150 * s, H - 85 * s, 300 * s, 62 * s), "VOLTAR  (Esc)", new Color(0.3f, 0.3f, 0.35f, 0.85f), s, () => state = RunnerState.Menu);
    }

    int menuPanel;          // 0 nada, 1 mini-jogos, 2 como jogar, 3 opções
    float menuGamesY = 0.5f;

    void DrawMenu(float s, float W, float H)
    {
        Box(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.35f));
        ShadowLabel(new Rect(0, H * 0.06f, W, 120 * s), "EZEQUIEL", Sty(bigStyle, fs: Mathf.RoundToInt(120 * s)), new Color(1f, 0.85f, 0.2f));
        ShadowLabel(new Rect(0, H * 0.06f + 118 * s, W, 40 * s), "de Jerusalém ao Sheol", Sty(midStyle, fs: Mathf.RoundToInt(28 * s), fst: FontStyle.Italic), new Color(1f, 0.95f, 0.85f));

        // seletor de profeta (nome grande, descrição curta)
        var p = Meta.Selected;
        float pw = Mathf.Min(860 * s, W - 40 * s), ph = 112 * s;
        var pr = new Rect(W / 2 - pw / 2, H * 0.34f, pw, ph);
        Box(pr, new Color(0f, 0f, 0f, 0.4f));
        ShadowLabel(new Rect(pr.x, pr.y + 6 * s, pr.width, 46 * s), p.name.ToUpper(), Sty(midStyle, fs: Mathf.RoundToInt(40 * s)), p.color);
        ShadowLabel(new Rect(pr.x + 90 * s, pr.y + 54 * s, pr.width - 180 * s, 52 * s), p.desc, Sty(cardDesc, al: TextAnchor.MiddleCenter, fs: Mathf.RoundToInt(20 * s)), new Color(1f, 1f, 1f, 0.85f));
        ActionButton("pro_prev", new Rect(pr.x + 10 * s, pr.y + 26 * s, 70 * s, 60 * s), "◀", new Color(0.2f, 0.2f, 0.3f, 0.85f), s, () => Meta.Cycle(-1));
        ActionButton("pro_next", new Rect(pr.xMax - 80 * s, pr.y + 26 * s, 70 * s, 60 * s), "▶", new Color(0.2f, 0.2f, 0.3f, 0.85f), s, () => Meta.Cycle(1));

        // botão principal
        var play = new Rect(W / 2 - 200 * s, pr.yMax + 26 * s, 400 * s, 84 * s);
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
        ActionButton("play", play, "JOGAR", new Color(0.75f * pulse, 0.55f * pulse, 0.1f, 0.95f), s, () => { menuPanel = 0; StartRun(); StartIntro(); });
        float sideW = Mathf.Min(250 * s, (W - play.width) / 2f - 26 * s);
        int best = Meta.DailyBest;
        ActionButton("open_codex", new Rect(play.x - sideW - 16 * s, play.y + 6 * s, sideW, play.height - 12 * s), "LIVRO DA VIDA", new Color(0.32f, 0.24f, 0.12f, 0.92f), s, OpenCodex);
        ActionButton("daily", new Rect(play.xMax + 16 * s, play.y + 6 * s, sideW, play.height - 12 * s),
            "DESAFIO DO DIA\n" + Meta.DailyProphet.name + (best > 0 ? "  •  " + best : ""), new Color(0.15f, 0.32f, 0.42f, 0.92f), s, StartDaily);

        // linha de botões secundários
        string[] labels = { "TEMPLO (" + Meta.Talents + ")", "BARALHO", "ESTÁBULO", "MINI-JOGOS", "COMO JOGAR", "OPÇÕES" };
        int nb = labels.Length;
        float bw = 200 * s, bh = 58 * s, gap = 10 * s;
        float total = bw * nb + gap * (nb - 1);
        if (total > W - 30 * s) { float k = (W - 30 * s) / total; bw *= k; gap *= k; total = W - 30 * s; }
        float bx = W / 2 - total / 2, by = play.yMax + 26 * s;
        var col = new Color(0.22f, 0.2f, 0.28f, 0.88f);
        var on = new Color(0.45f, 0.35f, 0.15f, 0.95f);
        System.Func<int, Rect> slot = i => new Rect(bx + i * (bw + gap), by, bw, bh);
        ActionButton("open_temple", slot(0), labels[0], new Color(0.4f, 0.3f, 0.08f, 0.9f), s, () => { menuPanel = 0; state = RunnerState.Temple; });
        ActionButton("open_deck", slot(1), labels[1], new Color(0.3f, 0.18f, 0.38f, 0.9f), s, OpenDeckEditor);
        ActionButton("open_stable", slot(2), labels[2], new Color(0.3f, 0.22f, 0.12f, 0.9f), s, () => { menuPanel = 0; stableSpin = 0f; state = RunnerState.Stable; });
        ActionButton("panel_games", slot(3), labels[3], menuPanel == 1 ? on : col, s, () => menuPanel = menuPanel == 1 ? 0 : 1);
        ActionButton("panel_help", slot(4), labels[4], menuPanel == 2 ? on : col, s, () => menuPanel = menuPanel == 2 ? 0 : 2);
        ActionButton("panel_opts", slot(5), labels[5], menuPanel == 3 ? on : col, s, () => menuPanel = menuPanel == 3 ? 0 : 3);

        if (menuNoteTime > 0f)
        {
            menuNoteTime -= Time.unscaledDeltaTime;
            ShadowLabel(new Rect(0, H * 0.27f, W, 40 * s), menuNote, Sty(midStyle, fs: Mathf.RoundToInt(26 * s)), new Color(0.7f, 1f, 0.75f));
        }
        bool everWon = PlayerPrefs.GetInt("runner_final_win", 0) == 1;
        ShadowLabel(new Rect(0, H - 52 * s, W, 40 * s), "Recorde  " + highScore + (everWon ? "   ★" : ""), Sty(cardSmall, fs: Mathf.RoundToInt(24 * s)), new Color(1f, 0.9f, 0.6f));

        if (menuPanel != 0) DrawMenuPanel(s, W, H, by + bh + 16 * s);
        DrawExitConfirm(s, W, H);
    }

    void DrawMenuPanel(float s, float W, float H, float top)
    {
        float pw = Mathf.Min(1100 * s, W - 30 * s), ph = Mathf.Min(H - top - 60 * s, 300 * s);
        var r = new Rect(W / 2 - pw / 2, top, pw, ph);
        uiRects.Add(new Rect(r.x + guiOffset.x, r.y + guiOffset.y, r.width, r.height));   // o painel não deixa o clique começar o jogo
        Box(r, new Color(0.05f, 0.04f, 0.06f, 0.92f));
        var txt = Sty(cardDesc, al: TextAnchor.UpperCenter, fs: Mathf.RoundToInt(22 * s), ww: 1);
        if (menuPanel == 1)
        {
            menuGamesY = r.y + r.height * 0.5f - 33 * s;
            ShadowLabel(new Rect(r.x, r.y + 14 * s, r.width, 30 * s), RunnerTouch.UseTouchUI ? "Toque num mini-jogo para jogar direto" : "Clique ou aperte 1-7", Sty(cardSmall), new Color(1f, 0.85f, 0.4f));
            DrawMenuMinigames(s, W, H);
        }
        else if (menuPanel == 2)
        {
            string controls = RunnerTouch.UseTouchUI
                ? "Deslize ← → para trocar de faixa  •  deslize ↑ ou toque para pular  •  ↓ cai rápido  •  o tiro é automático"
                : "A/D ou ← →  faixas   •   W / Espaço  pular   •   S  cair rápido   •   Q / Shift  tempo bala   •   Esc  pausar";
            string tips = "Suba de nível e escolha cartas sacadas do seu BARALHO — combine as certas para EVOLUÇÕES.\n" +
                          "Escudeiros e muralhas de barro: PULE EM CIMA.  Pedras, colossos e carros de guerra: DESVIE.\n" +
                          "A cada " + bossEveryLevels + " níveis vem um CHEFE. Depois dele: relíquia e escolha de caminho.\n" +
                          "Junte 3 ou 5 cartas da mesma FAMÍLIA (Fogo, Água, Guerra, Fé, Sinais) para bônus. Molhado + raio = choque!\n" +
                          "Cartas NOVA! nas recompensas entram na sua coleção. Ganhe Talentos e gaste no TEMPLO e no BARALHO.";
            ShadowLabel(new Rect(r.x + 20 * s, r.y + 18 * s, r.width - 40 * s, 60 * s), controls, new GUIStyle(txt) { fontStyle = FontStyle.Bold }, new Color(0.9f, 0.95f, 1f));
            ShadowLabel(new Rect(r.x + 20 * s, r.y + 84 * s, r.width - 40 * s, r.height - 100 * s), tips, txt, new Color(1f, 1f, 1f, 0.85f));
        }
        else
        {
            float bw = Mathf.Min(320 * s, (r.width - 60 * s) / 3f), bh = 70 * s, gap = 16 * s;
            bh = Mathf.Min(bh, (r.height - 30 * s - gap * 2f) / 3f);
            float bx = r.center.x - (bw * 3 + gap * 2) / 2f, by = r.y + (r.height - (bh * 3f + gap * 2f)) / 2f;
            // rever a abertura (trailer)
            ActionButton("opt_trailer", new Rect(bx + bw + gap, by + (bh + gap) * 2f, bw, bh), "▶  VER ABERTURA",
                new Color(0.45f, 0.32f, 0.08f, 0.92f), s, StartTrailer);
            // gráficos (automático / alta / média / baixa) + FPS
            ActionButton("opt_quality", new Rect(bx, by + bh + gap, bw * 2 + gap, bh), QualityLabel + "   •   " + Mathf.RoundToInt(fpsShown) + " FPS",
                new Color(0.2f, 0.3f, 0.45f, 0.9f), s, CycleQualityMode);
            ActionButton("opt_texts", new Rect(bx + (bw + gap) * 2, by + bh + gap, bw, bh), "Textos: " + TextModeNames[TextMode],
                new Color(0.3f, 0.25f, 0.45f, 0.9f), s, CycleTextMode);
            bool ps1On = ps1 != null && ps1.styleOn;
            ActionButton("opt_ps1", new Rect(bx, by, bw, bh), "Filtro PS1: " + (ps1On ? "LIGADO" : "DESLIGADO") + (RunnerTouch.UseTouchUI ? "" : "  [V]"),
                ps1On ? new Color(0.2f, 0.5f, 0.3f, 0.9f) : new Color(0.35f, 0.2f, 0.2f, 0.9f), s, TogglePS1);
            ActionButton("opt_music", new Rect(bx + bw + gap, by, bw, bh), "Música: " + (RunnerMusic.Enabled ? "LIGADA" : "DESLIGADA") + (RunnerTouch.UseTouchUI ? "" : "  [M]"),
                RunnerMusic.Enabled ? new Color(0.2f, 0.45f, 0.55f, 0.9f) : new Color(0.35f, 0.2f, 0.2f, 0.9f), s, ToggleMusic);
            ActionButton("opt_hints", new Rect(bx + (bw + gap) * 2, by, bw, bh), "Dicas: " + (HintsOn ? "LIGADAS" : "DESLIGADAS"),
                HintsOn ? new Color(0.2f, 0.5f, 0.3f, 0.9f) : new Color(0.35f, 0.2f, 0.2f, 0.9f), s, ToggleHints);
        }
    }

    void DrawWorldUI(float s, float H)
    {
        if (cam == null) return;

        // barras de vida dos inimigos
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || !o.Shootable || o.champion || o.maxHp <= 1.01f || o.hp >= o.maxHp) continue;
            if (TextMode < 2 && !o.elite && o.type != ObType.Tank && o.type != ObType.Turret && o.type != ObType.BossDrone && o.maxHp < stats.Damage * 4f) continue;
            Vector3 sp = WorldToScreen(o.transform.position + Vector3.up * (o.half.y + 0.35f));
            if (sp.z <= 0f) continue;
            float w = 70 * s, h = 8 * s;
            var r = new Rect(sp.x - w / 2, H - sp.y - h / 2, w, h);
            Box(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), new Color(0, 0, 0, 0.7f));
            Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(o.hp / o.maxHp), r.height), new Color(1f, 0.25f, 0.25f));
        }

        DrawChampionLabel(s, H);

        // rótulo das caixas de avião
        foreach (var o in obstacles)
        {
            if (o == null || o.dead || (o.type != ObType.PlaneBox && o.type != ObType.CarBox && o.type != ObType.ShipBox && o.type != ObType.AngelBox && o.type != ObType.BabelBox && o.type != ObType.JerichoBox && o.type != ObType.GoliathBox)) continue;
            Vector3 sp = WorldToScreen(o.transform.position + Vector3.up * 1.4f);
            if (sp.z <= 0f || sp.z > 70f) continue;
            floatStyle.fontSize = Mathf.RoundToInt(24 * s);
            bool isCar = o.type == ObType.CarBox;
            bool isShip = o.type == ObType.ShipBox;
            bool isAngel = o.type == ObType.AngelBox;
            bool isBabel = o.type == ObType.BabelBox;
            bool isJer = o.type == ObType.JerichoBox, isGol = o.type == ObType.GoliathBox;
            string boxName = isJer ? "JERICÓ" : isGol ? "DAVI E GOLIAS" : isBabel ? "TORRE DE BABEL" : isAngel ? "ANJO" : (isShip ? "NAVE" : (isCar ? "CARRUAGEM" : "CARRO DE FOGO"));
            Color boxColor = isJer ? new Color(1f, 0.8f, 0.4f) : isGol ? new Color(0.7f, 0.95f, 0.5f) : isBabel ? new Color(1f, 0.8f, 0.45f) : isAngel ? new Color(1f, 0.3f, 0.3f) : (isShip ? new Color(0.85f, 0.5f, 1f) : (isCar ? new Color(1f, 0.6f, 0.3f) : new Color(0.5f, 0.9f, 1f)));
            ShadowLabel(new Rect(sp.x - 150 * s, H - sp.y - 25 * s, 300 * s, 50 * s), boxName, floatStyle, boxColor);
        }

        // números de dano
        foreach (var f in floats)
        {
            Vector3 sp = WorldToScreen(f.pos);
            if (sp.z <= 0f) continue;
            floatStyle.fontSize = Mathf.RoundToInt((f.big ? 36 : 22) * s);
            var c = f.color;
            c.a = Mathf.Clamp01(f.t / 0.3f);
            ShadowLabel(new Rect(sp.x - 150 * s, H - sp.y - 25 * s, 300 * s, 50 * s), f.text, floatStyle, c);
        }
    }

    void DrawHUD(float s, float W, float H)
    {
        // pontuação em destaque (∞ depois de vencer Satanás); o recorde só aparece quando é batido
        var scoreSt = Sty(bigStyle, fs: Mathf.RoundToInt(52 * s), al: TextAnchor.UpperLeft);
        ShadowLabel(new Rect(24 * s, 8 * s, 700 * s, 66 * s), (finalBeaten ? "∞  " : "") + Score, scoreSt, Color.white);
        if (highScore > 0 && Score > highScore)
            ShadowLabel(new Rect(26 * s, 64 * s, 500 * s, 30 * s), "NOVO RECORDE!", Sty(cardSmall, al: TextAnchor.UpperLeft), new Color(1f, 0.85f, 0.3f));
        DrawRogueHUD(s, W, H);

        // vidas
        float inset = RunnerTouch.UseTouchUI ? 120 * s : 0f;
        for (int i = 0; i < maxLives; i++)
        {
            var r = new Rect(W - inset - (i + 1) * 44 * s - 20 * s, 24 * s, 34 * s, 34 * s);
            Box(new Rect(r.x + 2, r.y + 2, r.width, r.height), new Color(0, 0, 0, 0.5f));
            Box(r, i < lives ? new Color(1f, 0.25f, 0.3f) : new Color(1f, 1f, 1f, 0.25f));
        }

        // habilidades como "pílulas" compactas embaixo das vidas
        float pillX = W - inset - 20 * s, pillY = 68 * s;
        if (stats.shieldLevel > 0)
        {
            float k = shieldReady ? 1f : 1f - Mathf.Clamp01(shieldTimer / Mathf.Max(0.1f, stats.ShieldRecharge));
            pillX = Pill(s, pillX, pillY, "ESCUDO", k, shieldReady, new Color(0.4f, 0.9f, 1f));
        }
        if (stats.bulletTime)
        {
            bool ready = btActive <= 0f && btCooldownLeft <= 0f;
            float k = btActive > 0f ? 1f : (ready ? 1f : 1f - Mathf.Clamp01(btCooldownLeft / Mathf.Max(0.1f, stats.bulletTimeCooldown)));
            pillX = Pill(s, pillX, pillY, RunnerTouch.UseTouchUI ? "TEMPO" : "TEMPO [Q]", k, ready || btActive > 0f, new Color(1f, 0.85f, 0.3f));
        }

        if (bulletHell && hell != null && hell.Active)
        {
            DrawHellBar(s, W, H);
        }
        else if (babel && babelGame != null && babelGame.Active)
        {
            DrawBabelBar(s, W, H);
        }
        else if (goliath && goliathGame != null && goliathGame.Active)
        {
            DrawGoliathHUD(s, W, H);
        }
        else if (jericho)
        {
            DrawJerichoBar(s, W, H);
        }
        else if (boss != null && boss.body != null && !boss.body.dead)
        {
            DrawBossBar(s, W);
        }
        else if (bossPending)
        {
            if (Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.33f)
                ShadowLabel(new Rect(0, H * 0.16f, W, 90 * s), (bossesDefeated + 1 == finalBossNumber ? "!! O ACUSADOR SE APROXIMA !!" : "!! CHEFE SE APROXIMANDO !!"), Sty(bigStyle, fs: Mathf.RoundToInt(62 * s)), new Color(1f, 0.25f, 0.2f));
            Box(new Rect(0, 0, W, H), new Color(1f, 0f, 0f, 0.06f + 0.06f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f))));
        }
        else if (flightPending)
        {
            if (Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.33f)
                ShadowLabel(new Rect(0, H * 0.16f, W, 90 * s), "PREPARAR PARA DECOLAR!", Sty(bigStyle, fs: Mathf.RoundToInt(62 * s)), new Color(0.5f, 0.9f, 1f));
        }
        else if (passover)
        {
            float bw = 620 * s, bh = 16 * s;
            var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
            Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.55f));
            Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(passoverTime / passoverDuration), bar.height), new Color(0.75f, 0.1f, 0.1f));
            string ptxt = "NOITE DA PÁSCOA  —  julgadas " + judged + "  •  poupadas " + spared + (passCombo > 1 ? "  •  combo x" + passCombo : "");
            ShadowLabel(new Rect(0, bar.y + bh + 2 * s, W, 40 * s), ptxt, Sty(cardSmall), Color.white);
            ShadowLabel(new Rect(0, bar.y + bh + 34 * s, W, 40 * s), "Passe pelas casas SEM sangue  •  evite as portas marcadas de vermelho", Sty(cardSmall), new Color(1f, 0.6f, 0.55f));
        }
        else if (racing)
        {
            float bw = 620 * s, bh = 16 * s;
            var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
            float prog = Mathf.Clamp01((player.transform.position.z - raceStartZ) / raceLength);
            Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.55f));
            Box(new Rect(bar.x, bar.y, bar.width * prog, bar.height), new Color(1f, 0.6f, 0.2f));
            foreach (var o in obstacles)
            {
                if (o == null || o.dead || o.type != ObType.Rival) continue;
                float rp = Mathf.Clamp01((o.transform.position.z - raceStartZ) / raceLength);
                Box(new Rect(bar.x + bar.width * rp - 3 * s, bar.y - 4 * s, 6 * s, bh + 8 * s), o.mainColor);
            }
            Box(new Rect(bar.x + bar.width * prog - 4 * s, bar.y - 6 * s, 8 * s, bh + 12 * s), Color.white);
            string raceHint = RunnerTouch.UseTouchUI
                ? "Arraste o dedo: ←/→ vira   •   ↑ acelera   •   ↓ freia   •   nas curvas, vire para dentro"
                : "A/D: esquerda/direita   •   W: acelerar   •   S: frear   •   nas curvas, vire para dentro";
            ShadowLabel(new Rect(0, bar.y + bh + 2 * s, W, 40 * s), raceHint, Sty(cardSmall), Color.white);

            // aviso de curva à frente
            float ahead = player.transform.position.z + 45f;
            float cv = RoadCurvature(ahead);
            if (Mathf.Abs(cv) > 0.0035f)
            {
                bool strong = Mathf.Abs(cv) > 0.0075f;
                string txt = cv > 0f ? "CURVA À DIREITA  >>>" : "<<<  CURVA À ESQUERDA";
                if (strong) txt = cv > 0f ? "CURVA FECHADA  >>>" : "<<<  CURVA FECHADA";
                ShadowLabel(new Rect(0, H * 0.24f, W, 60 * s), txt, midStyle, strong ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.85f, 0.3f));
            }

            Color pc = racePosition == 1 ? new Color(1f, 0.85f, 0.2f) : (racePosition <= 3 ? Color.white : new Color(1f, 0.5f, 0.4f));
            ShadowLabel(new Rect(0, H * 0.72f, W, 110 * s), racePosition + "º / " + (rivalCount + 1), Sty(bigStyle, fs: Mathf.RoundToInt(84 * s)), pc);
            if (player.offRoad && Mathf.Repeat(Time.unscaledTime, 0.4f) < 0.25f)
                ShadowLabel(new Rect(0, H * 0.62f, W, 60 * s), "FORA DA PISTA!", midStyle, new Color(1f, 0.4f, 0.3f));
        }
        else if (player.flying && skyTransit)
        {
            DrawSkyHUD(s, W);
        }
        else if (player.flying)
        {
            float bw = 520 * s, bh = 16 * s;
            var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
            Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.55f));
            Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(flightTime / flightDuration), bar.height), new Color(0.5f, 0.9f, 1f));
            string ft = flightTime > 4f
                ? "CARRO DE FOGO  —  " + Mathf.CeilToInt(flightTime) + "s   (" + (RunnerTouch.UseTouchUI ? "arraste o dedo para voar" : "WASD / setas / analógico para voar") + ")"
                : "POUSANDO EM " + Mathf.CeilToInt(Mathf.Max(0f, flightTime)) + "...";
            ShadowLabel(new Rect(0, bar.y + bh + 2 * s, W, 40 * s), ft, Sty(cardSmall), flightTime > 4f ? Color.white : new Color(1f, 0.85f, 0.3f));
        }
        else
        {
            // barra de progresso até a próxima carta
            float bw = 520 * s, bh = 16 * s;
            var bar = new Rect(W / 2 - bw / 2, 22 * s, bw, bh);
            float prog = Mathf.Clamp01((Score - prevCardScore) / (float)Mathf.Max(1, nextCardScore - prevCardScore));
            Box(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0, 0, 0, 0.55f));
            Box(new Rect(bar.x, bar.y, bar.width * prog, bar.height), new Color(0.5f, 0.85f, 1f));
            // nível à esquerda da barra; à direita, um marcador por nível até o chefe
            var lvSt = Sty(cardSmall, al: TextAnchor.MiddleRight);
            ShadowLabel(new Rect(bar.x - 130 * s, bar.y - 8 * s, 120 * s, 32 * s), "NÍVEL " + level, lvSt, Color.white);
            int toBoss = bossEveryLevels > 0 ? bossEveryLevels - (level % bossEveryLevels) : 0;
            if (boss == null && !bossPending)
                for (int k = 0; k < toBoss; k++)
                {
                    bool bossPip = k == toBoss - 1;
                    var pr = new Rect(bar.xMax + 12 * s + k * 20 * s, bar.y - (bossPip ? 4 * s : 0), bossPip ? 22 * s : 14 * s, bossPip ? 22 * s : 14 * s);
                    Box(pr, bossPip ? new Color(1f, 0.25f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.45f));
                }
        }

        if (bannerTime > 0f && state == RunnerState.Playing)
        {
            float a = Mathf.Clamp01(bannerTime / 0.8f) * Mathf.Clamp01((4f - bannerTime) / 0.4f);
            Color bc = T.id == 2 ? new Color(1f, 0.35f, 0.1f, a) : (T.id == 1 ? new Color(0.9f, 0.15f, 0.15f, a) : (T.id == 3 ? new Color(0.3f, 0.85f, 0.85f, a) : new Color(1f, 0.85f, 0.3f, a)));
            Box(new Rect(0, H * 0.3f, W, 170 * s), new Color(0f, 0f, 0f, 0.45f * a));
            ShadowLabel(new Rect(0, H * 0.3f + 10 * s, W, 110 * s), bannerText, Sty(bigStyle, fs: Mathf.RoundToInt(100 * s)), bc);
            ShadowLabel(new Rect(0, H * 0.3f + 110 * s, W, 50 * s), bannerSub, midStyle, new Color(1f, 1f, 1f, a));
        }

        if (popupTime > 0f && state == RunnerState.Playing)
        {
            float a = Mathf.Clamp01(popupTime / 0.5f);
            ShadowLabel(new Rect(0, H * 0.2f - (1 - a) * 40 * s, W, 60 * s), popup, midStyle, new Color(1f, 1f, 0.4f, a));
        }

        if (state == RunnerState.Playing)
        {
            Box(new Rect(W / 2 - 2 * s, H * 0.42f - 10 * s, 4 * s, 20 * s), new Color(1, 1, 1, 0.6f));
            Box(new Rect(W / 2 - 10 * s, H * 0.42f - 2 * s, 20 * s, 4 * s), new Color(1, 1, 1, 0.6f));
            if (BulletTimeActive) Box(new Rect(0, 0, W, H), new Color(0.2f, 0.4f, 1f, 0.12f));
        }
    }

    /// Pílula de habilidade (desenhada da direita para a esquerda). Retorna o x livre à esquerda.
    float Pill(float s, float right, float y, string label, float fill, bool ready, Color c)
    {
        float w = 150 * s, h = 30 * s;
        var r = new Rect(right - w, y, w, h);
        Box(r, new Color(0f, 0f, 0f, 0.5f));
        Box(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), ready ? new Color(c.r, c.g, c.b, 0.75f) : new Color(c.r, c.g, c.b, 0.3f));
        ShadowLabel(r, label, Sty(cardSmall, al: TextAnchor.MiddleCenter, fs: Mathf.RoundToInt(18 * s)), ready ? Color.white : new Color(1f, 1f, 1f, 0.6f));
        return r.x - 10 * s;
    }

    void DrawHellBar(float s, float W, float H)
    {
        float bw = 760 * s, bh = 26 * s;
        var bar = new Rect(W / 2 - bw / 2, 52 * s, bw, bh);
        ShadowLabel(new Rect(0, 8 * s, W, 44 * s), "ESPÍRITO MALIGNO  —  FASE " + hell.Phase, Sty(midStyle, fs: Mathf.RoundToInt(32 * s)), new Color(0.85f, 0.4f, 1f));
        Box(new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), new Color(0, 0, 0, 0.7f));
        Box(bar, new Color(0.15f, 0.03f, 0.18f));
        Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(hell.SpiritHp / hell.SpiritMaxHp), bar.height), new Color(0.7f, 0.2f, 0.95f));
        for (int k = 1; k <= 2; k++) Box(new Rect(bar.x + bar.width * k / 3f - 1, bar.y, 2, bar.height), new Color(1f, 1f, 1f, 0.5f));
        ShadowLabel(bar, Mathf.CeilToInt(Mathf.Max(0f, hell.SpiritHp)) + " / " + Mathf.CeilToInt(hell.SpiritMaxHp), Sty(cardSmall), Color.white);
        string hint = RunnerTouch.UseTouchUI ? "Arraste o dedo para mover a nave • tiro automático" : "WASD / setas: mover   •   Shift: modo preciso   •   tiro automático";
        ShadowLabel(new Rect(0, bar.yMax + 6 * s, W, 34 * s), "Tempo: " + Mathf.CeilToInt(Mathf.Max(0f, hell.TimeLeft)) + "s   •   " + hint, Sty(cardSmall), hell.TimeLeft < 10f ? new Color(1f, 0.5f, 0.4f) : Color.white);
    }

    void DrawBabelBar(float s, float W, float H)
    {
        var bg = babelGame;
        bool conf = bg.Style == 2;
        float tm = Time.unscaledTime;
        System.Func<string, string> T = x => conf && (bg.Inverted || bg.InvertWarning) ? RunnerBabel.Garble(x, tm) : x;

        float bw = 760 * s, bh = 22 * s;
        var bar = new Rect(W / 2 - bw / 2, 52 * s, bw, bh);
        ShadowLabel(new Rect(0, 8 * s, W, 44 * s), T("TORRE DE BABEL  —  " + RunnerBabel.StyleNames[bg.Style]), Sty(midStyle, fs: Mathf.RoundToInt(32 * s)), new Color(1f, 0.8f, 0.45f));
        Box(new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), new Color(0, 0, 0, 0.7f));
        Box(bar, new Color(0.25f, 0.18f, 0.1f));
        Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(bg.Height / bg.Goal), bar.height), new Color(0.85f, 0.65f, 0.35f));
        ShadowLabel(bar, Mathf.RoundToInt(Mathf.Max(0f, bg.Height)) + " / " + Mathf.RoundToInt(bg.Goal) + " côvados", Sty(cardSmall), Color.white);
        string hint = RunnerTouch.UseTouchUI ? "Segure e arraste para os lados" : "A/D ou setas: mover";
        hint += "   •   pise nos construtores   •   tiro automático";
        ShadowLabel(new Rect(0, bar.yMax + 6 * s, W, 34 * s), T("Tempo: " + Mathf.CeilToInt(Mathf.Max(0f, bg.TimeLeft)) + "s   •   " + hint), Sty(cardSmall), bg.TimeLeft < 10f ? new Color(1f, 0.5f, 0.4f) : Color.white);

        var big = Sty(bigStyle, fs: Mathf.RoundToInt(52 * s));
        if (bg.Style == 1 && Mathf.Abs(bg.Wind) > 1f)
        {
            string arrows = bg.Wind > 0f ? "VENTO  >>>" : "<<<  VENTO";
            ShadowLabel(new Rect(0, H * 0.2f, W, 70 * s), arrows, big, new Color(0.95f, 0.85f, 0.6f, Mathf.Clamp01(Mathf.Abs(bg.Wind) / 4f)));
        }
        if (conf)
        {
            if (bg.InvertWarning && Mathf.Repeat(tm, 0.4f) < 0.25f)
                ShadowLabel(new Rect(0, H * 0.2f, W, 70 * s), "AS LÍNGUAS SE CONFUNDEM...", big, new Color(1f, 0.6f, 0.3f));
            else if (bg.Inverted)
            {
                ShadowLabel(new Rect(0, H * 0.2f, W, 70 * s), RunnerBabel.Garble("CONTROLES INVERTIDOS", tm), big, new Color(1f, 0.35f, 0.3f));
                Box(new Rect(0, 0, W, H), new Color(0.6f, 0.2f, 0f, 0.06f + 0.04f * Mathf.Sin(tm * 8f)));
            }
        }
    }

    void DrawBossBar(float s, float W)
    {
        var b = boss;
        var o = b.body;
        float bw = 760 * s, bh = 26 * s;
        var bar = new Rect(W / 2 - bw / 2, 52 * s, bw, bh);
        string bossTitle = b.isFinal
            ? "SATANÁS  " + RunnerBoss.Rtl(RunnerBoss.HebrewName) + "  —  CHEFE FINAL" + (b.trueForm ? "  (FORMA VERDADEIRA)" : (b.enraged ? "  (FÚRIA)" : ""))
            : b.bossName.ToUpper() + "  —  CHEFE " + (bossesDefeated + 1) + (b.enraged ? "  (FÚRIA)" : "");
        ShadowLabel(new Rect(0, 8 * s, W, 44 * s), bossTitle,
            Sty(midStyle, fs: Mathf.RoundToInt(32 * s)), b.enraged ? new Color(1f, 0.3f, 0.25f) : new Color(1f, 0.85f, 0.3f));
        Box(new Rect(bar.x - 3, bar.y - 3, bar.width + 6, bar.height + 6), new Color(0, 0, 0, 0.7f));
        Box(bar, new Color(0.25f, 0.05f, 0.05f));
        Box(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(o.hp / o.maxHp), bar.height), b.enraged ? new Color(1f, 0.15f, 0.1f) : new Color(0.85f, 0.15f, 0.2f));
        Box(new Rect(bar.x + bar.width * 0.5f - 1, bar.y, 2, bar.height), new Color(1f, 1f, 1f, 0.5f));
        if (b.shieldMax > 0f && b.shield > 0f)
            Box(new Rect(bar.x, bar.y + bar.height - 7 * s, bar.width * Mathf.Clamp01(b.shield / o.maxHp) , 7 * s), new Color(0.4f, 0.9f, 1f));
        ShadowLabel(bar, Mathf.CeilToInt(o.hp) + " / " + Mathf.CeilToInt(o.maxHp), Sty(cardSmall), Color.white);

        var names = new List<string>();
        foreach (var a in b.abilities) names.Add(RunnerBoss.AbilityName(a));
        ShadowLabel(new Rect(0, bar.yMax + 6 * s, W, 34 * s), string.Join("  •  ", names), Sty(cardSmall), new Color(1f, 0.75f, 0.7f));
    }

    /// Lista as evoluções que estão perto de acontecer (pelo menos 1 requisito cumprido).
    string EvolutionHints()
    {
        var sb = new System.Text.StringBuilder();
        int shown = 0;
        foreach (var evo in Evolutions.All)
        {
            if (CardStacks(evo.id) > 0)
            {
                if (shown < 4) { sb.Append("\n★ ").Append(evo.name).Append(" (evoluída)"); shown++; }
                continue;
            }
            int total;
            int ok = evo.Progress(this, out total);
            if (ok == 0 || shown >= 4) continue;
            sb.Append("\n◆ ").Append(evo.name).Append(ok == total ? " — PRONTA na próxima carta!" : "  (" + ok + "/" + total + "): " + evo.ReqText);
            shown++;
        }
        return shown > 0 ? "\n\nEVOLUÇÕES:" + sb : "";
    }

    void DrawBuildPanel(float s, float x, float y, float w, float h)
    {
        Box(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.5f));
        float pad = 14 * s, cy = y + pad;
        var head = Sty(midStyle, al: TextAnchor.MiddleLeft, fs: Mathf.RoundToInt(30 * s));
        ShadowLabel(new Rect(x + pad, cy, w - pad * 2, 40 * s), stats.weapon.name.ToUpper(), head, stats.weapon.color);
        ShadowLabel(new Rect(x + pad, cy, w - pad * 2, 40 * s), "SUA BUILD", Sty(cardSmall, al: TextAnchor.MiddleRight), new Color(1f, 0.85f, 0.3f));
        cy += 46 * s;

        // ---- atributos em "fichas"
        string[] lbl = { "DANO", "TIROS/S", "CRÍTICO", "PROJÉTEIS", "ALCANCE" };
        string[] val =
        {
            stats.Damage.ToString("0.0"), (1f / stats.Cooldown).ToString("0.0"), Mathf.RoundToInt(stats.critChance * 100) + "%",
            stats.Pellets.ToString(), stats.Melee ? stats.Reach.ToString("0.0") + "m" : Mathf.RoundToInt(stats.Range) + "m"
        };
        float cw = (w - pad * 2 - 4 * 8 * s) / 5f;
        for (int i = 0; i < 5; i++)
        {
            var r = new Rect(x + pad + i * (cw + 8 * s), cy, cw, 58 * s);
            Box(r, new Color(1f, 1f, 1f, 0.08f));
            ShadowLabel(new Rect(r.x, r.y + 2 * s, r.width, 20 * s), lbl[i], Sty(cardSmall, fs: Mathf.RoundToInt(15 * s)), new Color(0.75f, 0.8f, 0.9f));
            ShadowLabel(new Rect(r.x, r.y + 22 * s, r.width, 34 * s), val[i], Sty(cardTitle, fs: Mathf.RoundToInt(26 * s)), Color.white);
        }
        cy += 70 * s;

        // ---- famílias (3 e 5 cartas diferentes ligam bônus)
        {
            float fw = (w - pad * 2 - 4 * 6 * s) / 5f;
            for (int i = 0; i < Families.All.Length; i++)
            {
                var f = Families.All[i];
                int n = FamilyCount(f);
                Color fc = Families.Tint(f);
                var r = new Rect(x + pad + i * (fw + 6 * s), cy, fw, 26 * s);
                Box(r, new Color(fc.r * 0.22f, fc.g * 0.22f, fc.b * 0.22f, 0.9f));
                Box(new Rect(r.x, r.yMax - 4 * s, r.width * Mathf.Clamp01(n / 5f), 4 * s), fc);
                string stars = Fam(f, 5) ? " ★★" : (Fam(f, 3) ? " ★" : "");
                ShadowLabel(r, Families.Name(f) + " " + n + stars, Sty(cardSmall, fs: Mathf.RoundToInt(14 * s)), n >= 3 ? Color.Lerp(fc, Color.white, 0.4f) : fc);
            }
            cy += 34 * s;
        }

        // ---- cartas como blocos coloridos pela raridade
        var order = new List<RunnerCard>();
        var counts = new Dictionary<string, int>();
        foreach (var c in history)
        {
            if (!counts.ContainsKey(c.id)) { order.Add(c); counts[c.id] = 0; }
            counts[c.id]++;
        }
        int cols = 3;
        float tw = (w - pad * 2 - (cols - 1) * 6 * s) / cols, th = 30 * s;
        int evoLines = 0;
        foreach (var evo in Evolutions.All) { int tot; if (CardStacks(evo.id) == 0 && evo.Progress(this, out tot) > 0) evoLines++; }
        float reserve = Mathf.Min(evoLines, 3) * 26 * s + (relicList.Count > 0 ? 40 * s : 0f);
        int maxRows = Mathf.Max(1, Mathf.FloorToInt((y + h - pad - cy - reserve) / (th + 6 * s)));
        var tile = Sty(cardSmall, fs: Mathf.RoundToInt(16 * s), al: TextAnchor.MiddleLeft, cl: TextClipping.Clip);
        if (order.Count == 0)
            ShadowLabel(new Rect(x, cy, w, th), "nenhuma carta ainda", Sty(cardSmall), new Color(1f, 1f, 1f, 0.5f));
        for (int i = 0; i < order.Count; i++)
        {
            int row = i / cols, col = i % cols;
            if (row >= maxRows)
            {
                ShadowLabel(new Rect(x, cy + maxRows * (th + 6 * s) - 4 * s, w - pad, 20 * s), "+" + (order.Count - i) + " cartas", Sty(cardSmall, al: TextAnchor.MiddleRight, fs: Mathf.RoundToInt(15 * s)), new Color(1f, 1f, 1f, 0.6f));
                break;
            }
            var c = order[i];
            var r = new Rect(x + pad + col * (tw + 6 * s), cy + row * (th + 6 * s), tw, th);
            Color rc = c.isEvolution ? CardDB.EvolutionColor : CardDB.RarityColor(c.rarity);
            Box(r, new Color(rc.r * 0.25f, rc.g * 0.25f, rc.b * 0.25f, 0.9f));
            Box(new Rect(r.x, r.y, 5 * s, r.height), rc);
            float ic = r.height - 4 * s;
            CardIcons.Draw(new Rect(r.x + 8 * s, r.y + 2 * s, ic, ic), c);
            string n = c.name + (IsAnointed(c) ? "+" : "") + (counts[c.id] > 1 ? "  x" + counts[c.id] : "");
            ShadowLabel(new Rect(r.x + 12 * s + ic, r.y, r.width - 14 * s - ic, r.height), n, tile, Color.white);
        }
        cy += Mathf.Max(1, Mathf.Min(maxRows, (order.Count + cols - 1) / cols)) * (th + 6 * s) + 8 * s;

        // ---- relíquias
        if (relicList.Count > 0)
        {
            float rx = x + pad;
            float rw = Mathf.Min(220 * s, (w - pad * 2) / relicList.Count - 6 * s);
            foreach (var r in relicList)
            {
                var rr = new Rect(rx, cy, rw, 30 * s);
                Box(rr, new Color(0.25f, 0.2f, 0.05f, 0.9f));
                Box(new Rect(rr.x, rr.y, 5 * s, rr.height), r.color);
                ShadowLabel(new Rect(rr.x + 9 * s, rr.y, rr.width - 10 * s, rr.height), r.name, tile, new Color(1f, 0.9f, 0.6f));
                rx += rw + 6 * s;
            }
            cy += 40 * s;
        }

        // ---- evoluções em andamento (barra de progresso)
        int shown = 0;
        foreach (var evo in Evolutions.All)
        {
            if (shown >= 3 || CardStacks(evo.id) > 0) continue;
            int total;
            int ok = evo.Progress(this, out total);
            if (ok == 0) continue;
            var r = new Rect(x + pad, cy, w - pad * 2, 22 * s);
            Box(r, new Color(1f, 1f, 1f, 0.06f));
            Box(new Rect(r.x, r.y, r.width * ok / Mathf.Max(1, total), r.height), new Color(CardDB.EvolutionColor.r, CardDB.EvolutionColor.g, CardDB.EvolutionColor.b, 0.45f));
            ShadowLabel(r, "★ " + evo.name + (ok == total ? "  —  PRONTA!" : "  (" + ok + "/" + total + ")"), Sty(cardSmall, fs: Mathf.RoundToInt(16 * s)), Color.white);
            cy += 26 * s;
            shown++;
        }
    }

    void DrawCardChoice(float s, float W, float H)
    {
        Box(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.06f, 0.7f));
        string head = offerIsBoss ? offerTitle : "SUBIU PARA O NÍVEL " + (level + 1) + "!";
        string sub = offerIsBoss ? offerSub : (PlentyText() != "" ? "Escolha uma carta  —  " + PlentyText() : "Escolha uma carta");
        ShadowLabel(new Rect(0, H * 0.08f, W, 80 * s), head, Sty(bigStyle, fs: Mathf.RoundToInt(64 * s)), offerIsBoss ? new Color(1f, 0.6f, 0.15f) : new Color(1f, 0.85f, 0.2f));
        ShadowLabel(new Rect(0, H * 0.08f + 75 * s, W, 50 * s), sub, midStyle, Color.white);
        if (!offerIsBoss)
            ShadowLabel(new Rect(0, H * 0.08f + 120 * s, W, 34 * s), "baralho " + drawPile.Count + "   •   descarte " + discardPile.Count + JosephPreview(),
                Sty(cardSmall, fs: Mathf.RoundToInt(20 * s)), new Color(0.75f, 0.7f, 0.95f));

        int n = offer.Count;
        float cw = 330 * s, ch = 540 * s, gap = 34 * s;
        float totalW = n * cw + (n - 1) * gap;
        if (totalW > W - 40 * s)
        {
            float k = (W - 40 * s) / totalW;
            cw *= k; ch *= k; gap *= k; totalW = W - 40 * s;
        }
        // se não couber (tela estreita), encolhe tudo dentro da carta junto
        float cs = s * (cw / (330 * s));
        // limita a altura para caber na tela
        float maxH = H * 0.58f;
        if (ch > maxH) { float k2 = maxH / ch; cw *= k2; ch *= k2; gap *= k2; cs *= k2; totalW = n * cw + (n - 1) * gap; }
        cardTitle.fontSize = Mathf.RoundToInt(34 * cs);
        cardDesc.fontSize = Mathf.RoundToInt(26 * cs);
        float x0 = W / 2 - totalW / 2;
        float y0 = H * 0.27f;
        cardRects.Clear();

        Event e = Event.current;
        bool mouseMoved = (e.mousePosition - lastMouse).sqrMagnitude > 1f;
        if (e.type == EventType.Repaint) lastMouse = e.mousePosition;

        for (int i = 0; i < n; i++)
        {
            var card = offer[i];
            var rect = new Rect(x0 + i * (cw + gap), y0, cw, ch);
            cardRects.Add(rect);

            if (mouseMoved && e.type == EventType.Repaint && rect.Contains(e.mousePosition)) selected = i;
            if (CardInputReady && e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                ChooseCard(i);
                e.Use();
                return;
            }

            bool sel = i == selected;
            if (sel) rect.y -= 16 * cs;
            Color rc = card.plague ? new Color(0.85f, 0.25f, 0.22f) : (card.isEvolution ? CardDB.EvolutionColor : CardDB.RarityColor(card.rarity));

            // entrada: as cartas sobem uma depois da outra, com um leve "quique"
            float deal = Mathf.Clamp01((Time.unscaledTime - levelUpOpenTime - i * 0.08f) / 0.38f);
            float back = 1f + 2.2f * Mathf.Pow(deal - 1f, 3f) + 1.2f * Mathf.Pow(deal - 1f, 2f);
            rect.y += (1f - back) * H * 0.7f;
            // escolha: a carta escolhida vai para o centro e cresce; as outras caem
            if (chooseIdx >= 0)
            {
                float k = Mathf.SmoothStep(0f, 1f, chooseT / ChooseAnimTime);
                if (i == chooseIdx)
                {
                    float sc = 1f + 0.2f * k;
                    var target = new Rect(W / 2 - cw * sc / 2, H * 0.22f, cw * sc, ch * sc);
                    rect = new Rect(Mathf.Lerp(rect.x, target.x, k), Mathf.Lerp(rect.y, target.y, k), Mathf.Lerp(rect.width, target.width, k), Mathf.Lerp(rect.height, target.height, k));
                    Box(new Rect(rect.x - 14 * k * cs, rect.y - 14 * k * cs, rect.width + 28 * k * cs, rect.height + 28 * k * cs), new Color(rc.r, rc.g, rc.b, 0.35f * k));
                }
                else rect.y += k * k * H * 0.8f;
            }
            // brilho pulsante nas épicas, lendárias e evoluções
            if (card.isEvolution || card.rarity >= Rarity.Epico)
            {
                float gl = (6f + 5f * Mathf.Sin(Time.unscaledTime * 4f + i)) * cs;
                Box(new Rect(rect.x - gl, rect.y - gl, rect.width + gl * 2, rect.height + gl * 2), new Color(rc.r, rc.g, rc.b, 0.22f));
            }
            float border = sel ? 7 * cs : 4 * cs;

            Box(new Rect(rect.x + 6 * cs, rect.y + 8 * cs, rect.width, rect.height), new Color(0, 0, 0, 0.5f));
            Box(rect, sel ? Color.Lerp(rc, Color.white, 0.35f) : rc);
            var inner = new Rect(rect.x + border, rect.y + border, rect.width - border * 2, rect.height - border * 2);
            Box(inner, card.curse ? new Color(0.16f, 0.05f, 0.07f) : (card.isEvolution ? new Color(0.2f, 0.15f, 0.06f) : new Color(0.09f, 0.1f, 0.14f)));

            // faixa da raridade
            var band = new Rect(inner.x, inner.y, inner.width, 44 * cs);
            Box(band, new Color(rc.r * 0.45f, rc.g * 0.45f, rc.b * 0.45f));
            bool isNew = Deck.IsCollectible(card) && !Deck.Owned(card.id);
            string tag = card.plague ? "PRAGA" : card.isEvolution ? "★ EVOLUÇÃO ★" : (isNew ? "NOVA! • " : "") + CardDB.RarityName(card.rarity) + (card.isWeapon ? " • ARMA" : "") + (card.curse ? " • MALDIÇÃO" : "");
            if (isNew)
            {
                float np = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                Box(new Rect(band.x, band.y, band.width, band.height), new Color(1f, 0.9f, 0.3f, 0.25f + 0.3f * np));
            }
            ShadowLabel(band, tag, cardSmall, card.curse ? new Color(1f, 0.5f, 0.5f) : Color.white);
            if (card.isEvolution || card.rarity == Rarity.Lendario)
            {
                // reflexo que atravessa a carta
                float sweep = Mathf.Repeat(Time.unscaledTime * 0.6f + i * 0.3f, 1.6f) - 0.3f;
                if (sweep > 0f && sweep < 1f)
                    Box(new Rect(inner.x + inner.width * sweep - 10 * cs, inner.y, 20 * cs, inner.height), new Color(1f, 1f, 1f, 0.12f));
            }

            // ícone (Assets/Resources/CardIcons/<id>.png)
            float isz = 88 * cs;
            CardIcons.Draw(new Rect(inner.center.x - isz / 2, inner.y + 52 * cs, isz, isz), card);
            ShadowLabel(new Rect(inner.x + 10 * cs, inner.y + 146 * cs, inner.width - 20 * cs, 74 * cs), card.name + (IsAnointed(card) ? "+" : ""), cardTitle, rc);
            Box(new Rect(inner.x + 30 * cs, inner.y + 224 * cs, inner.width - 60 * cs, 2 * cs), new Color(rc.r, rc.g, rc.b, 0.5f));
            ShadowLabel(new Rect(inner.x + 18 * cs, inner.y + 234 * cs, inner.width - 36 * cs, inner.height - 234 * cs - 122 * cs), card.desc, cardDesc, new Color(0.92f, 0.92f, 0.95f));
            if (card.isEvolution)
                ShadowLabel(new Rect(inner.x + 10 * cs, inner.yMax - 118 * cs, inner.width - 20 * cs, 36 * cs), card.reqText, Sty(cardSmall, ww: 1), new Color(1f, 0.85f, 0.4f));

            int have = Stacks(card);
            string stackTxt = card.maxStacks >= 99 ? (have > 0 ? "já pego " + have + "x" : "") :
                              (card.maxStacks > 1 ? "nível " + have + " → " + (have + 1) + "  (máx " + card.maxStacks + ")" : "única");
            if (offerIsBoss && Deck.IsCollectible(card)) stackTxt = isNew ? "fica na sua coleção" : "+1 no baralho";
            if (CardTimes(card) > 1) stackTxt = (IsAnointed(card) ? "UNGIDA" : "GIDEÃO") + ": vale " + CardTimes(card) + "x";
            if (card.plague) stackTxt = "queime no altar";
            // família: progresso para o bônus
            var fam = Families.Of(card);
            if (fam != Family.Nenhuma)
            {
                int fn = FamilyCount(fam), after = fn + (have == 0 ? 1 : 0);
                Color fc = Families.Tint(fam);
                var fr = new Rect(inner.x + 14 * cs, inner.yMax - 116 * cs, inner.width - 28 * cs, 30 * cs);
                Box(fr, new Color(fc.r * 0.3f, fc.g * 0.3f, fc.b * 0.3f, 0.9f));
                Box(new Rect(fr.x, fr.y, 6 * cs, fr.height), fc);
                bool bonus = after != fn && (after == 3 || after == 5);
                string ft = Families.Name(fam) + "  " + (after != fn ? fn + " → " + after : fn.ToString()) + (bonus ? "   BÔNUS!" : (after < 3 ? "/3" : (after < 5 ? "/5" : "")));
                ShadowLabel(fr, ft, Sty(cardSmall, fs: Mathf.RoundToInt(18 * cs)), bonus ? Color.Lerp(fc, Color.white, 0.5f) : fc);
            }
            ShadowLabel(new Rect(inner.x, inner.yMax - 80 * cs, inner.width, 34 * cs), stackTxt, cardSmall, new Color(0.7f, 0.75f, 0.85f));
            ShadowLabel(new Rect(inner.x, inner.yMax - 44 * cs, inner.width, 34 * cs), "[" + (i + 1) + "]", cardSmall, sel ? Color.white : new Color(0.6f, 0.6f, 0.7f));
        }

        if (RunnerTouch.UseTouchUI)
        {
            ShadowLabel(new Rect(0, y0 + ch + 20 * s, W, 50 * s), "Toque numa carta para escolher", midStyle, CardInputReady ? Color.white : new Color(1, 1, 1, 0.4f));
            if (rerollsLeft > 0)
            {
                float bw = 360 * s, bh = 84 * s;
                TouchButton("reroll", new Rect(W / 2 - bw / 2, Mathf.Min(H - bh - 10 * s, y0 + ch + 80 * s), bw, bh),
                    "REROLAR (" + rerollsLeft + ")", new Color(0.3f, 0.4f, 0.7f, 0.8f));
            }
        }
        else
        {
            string hint = "Clique, aperte 1-" + n + ", ou use ← → e Enter";
            hint += rerollsLeft > 0 ? "     •     R: rerrolar (" + rerollsLeft + ")" : "     •     sem rerrolagens";
            ShadowLabel(new Rect(0, y0 + ch + 30 * s, W, 50 * s), hint, midStyle, CardInputReady ? Color.white : new Color(1, 1, 1, 0.4f));
        }
    }
}
