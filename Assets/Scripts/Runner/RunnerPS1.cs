using UnityEngine;

/// <summary>
/// Visual estilo PlayStation 1.
/// - A câmera do jogo renderiza numa textura menor (~360 linhas) sem suavização, e ela é ampliada
///   por um fator INTEIRO (2x, 3x...) — os pixels ficam quadrados e nítidos, sem borrão.
/// - Cores reduzidas (15-bit) com pontilhado (dithering).
/// - Vértices tremendo (wobble) e iluminação por vértice via shader Runner/PS1Lit.
/// Liga/desliga em tempo real (tecla V / F1 ou botão no menu).
/// </summary>
public class RunnerPS1 : MonoBehaviour
{
    public bool styleOn = false;
    public int resolution = 360;     // linhas aproximadas (o fator de ampliação é sempre inteiro)
    public int colorLevels = 32;
    public float dither = 1f;
    [Range(0f, 1f)] public float wobble = 0.6f;

    Camera gameCam;
    Camera outputCam;
    RenderTexture rt;
    Material postMat;
    Light sun;
    int lastW, lastH, lastRes;
    int pixelScale = 3;

    static readonly int IdLightDir = Shader.PropertyToID("_PS1LightDir");
    static readonly int IdLightColor = Shader.PropertyToID("_PS1LightColor");
    static readonly int IdAmbient = Shader.PropertyToID("_PS1Ambient");
    static readonly int IdFogColor = Shader.PropertyToID("_PS1FogColor");
    static readonly int IdFogParams = Shader.PropertyToID("_PS1FogParams");
    static readonly int IdSnap = Shader.PropertyToID("_PS1Snap");

    public bool Active => styleOn && postMat != null;

    public void Init(Camera cam)
    {
        gameCam = cam;
        var postShader = Resources.Load<Shader>("RunnerPS1Post");
        if (postShader != null && postShader.isSupported) postMat = new Material(postShader);
        else Debug.LogWarning("[Runner] Shader RunnerPS1Post não encontrado/suportado: filtro PS1 de tela desligado.");

        // câmera que só limpa a tela (a imagem do jogo é desenhada por cima no OnGUI)
        var go = new GameObject("PS1 Output Camera");
        outputCam = go.AddComponent<Camera>();
        outputCam.cullingMask = 0;
        outputCam.clearFlags = CameraClearFlags.SolidColor;
        outputCam.backgroundColor = Color.black;
        outputCam.depth = gameCam.depth + 10;
        outputCam.useOcclusionCulling = false;

        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }

        Apply();
    }

    public void Toggle()
    {
        styleOn = !styleOn;
        Apply();
    }

    void Apply()
    {
        bool on = Active;
        if (on) EnsureRT();
        if (gameCam != null)
        {
            gameCam.targetTexture = on ? rt : null;
            gameCam.allowMSAA = !on;   // anti-serrilhado não faz sentido (e custa caro) na imagem pequena
        }
        if (outputCam != null) outputCam.enabled = on;
        UpdateGlobals();
    }

    void EnsureRT()
    {
        // fator inteiro: cada pixel do "PS1" vira exatamente 2x2, 3x3... pixels da tela
        int target = Mathf.Clamp(resolution, 180, 720);
        pixelScale = Mathf.Max(2, Mathf.RoundToInt(Screen.height / (float)target));
        int w = Mathf.Max(64, Screen.width / pixelScale);
        int h = Mathf.Max(64, Screen.height / pixelScale);
        if (rt != null && rt.width == w && rt.height == h) return;

        if (gameCam != null && gameCam.targetTexture == rt) gameCam.targetTexture = null;
        if (rt != null) { rt.Release(); Destroy(rt); }
        rt = new RenderTexture(w, h, 24)
        {
            name = "PS1 Low Res",
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false
        };
        rt.Create();
        lastW = Screen.width;
        lastH = Screen.height;
        lastRes = resolution;
    }

    void LateUpdate()
    {
        if (Active && (Screen.width != lastW || Screen.height != lastH || resolution != lastRes))
        {
            EnsureRT();
            if (gameCam != null) gameCam.targetTexture = rt;
        }
        UpdateGlobals();
    }

    void UpdateGlobals()
    {
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        Vector3 dir = sun != null ? sun.transform.forward : new Vector3(-0.3f, -0.8f, 0.5f).normalized;
        Color lc = sun != null ? sun.color * sun.intensity : Color.white;
        Shader.SetGlobalVector(IdLightDir, dir);
        Shader.SetGlobalColor(IdLightColor, lc * 0.85f);
        Color sky = RenderSettings.fogColor;
        Shader.SetGlobalColor(IdAmbient, new Color(0.35f, 0.37f, 0.42f) + sky * 0.15f);
        Shader.SetGlobalColor(IdFogColor, RenderSettings.fogColor);
        Shader.SetGlobalVector(IdFogParams, new Vector4(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance, RenderSettings.fog ? 1f : 0f, 0f));

        // grade do "tremido": mais grossa quanto maior o wobble
        bool snap = Active && wobble > 0.01f && rt != null;
        float k = Mathf.Lerp(1f, 0.3f, Mathf.Clamp01(wobble));
        Vector4 grid = snap ? new Vector4(rt.width * 0.5f * k, rt.height * 0.5f * k, 1f, 0f) : Vector4.zero;
        Shader.SetGlobalVector(IdSnap, grid);
    }

    /// Desenha a imagem de baixa resolução na tela. Chamar no OnGUI (evento Repaint) antes do HUD.
    public void DrawToScreen()
    {
        if (!Active || rt == null) return;
        postMat.SetFloat("_Levels", colorLevels);
        postMat.SetFloat("_Dither", dither);
        int w = rt.width * pixelScale, h = rt.height * pixelScale;
        Graphics.DrawTexture(new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h), rt, postMat);
    }

    void OnDestroy()
    {
        if (gameCam != null) gameCam.targetTexture = null;
        if (rt != null) rt.Release();
    }
}
