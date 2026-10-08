using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Desempenho: qualidade gráfica automática (Alta / Média / Baixa), estilos de texto em cache,
/// primitivas sem colisor, listas reaproveitadas e prédios reaproveitados.
/// </summary>
public partial class RunnerGame
{
    // ================================================================== qualidade

    /// 0 = baixa, 1 = média, 2 = alta.
    public static int QualityTier = 2;
    int qualityMode;                 // 0 automático, 1 alta, 2 média, 3 baixa (PlayerPrefs "runner_quality")
    UniversalRenderPipelineAsset rpClone;
    Volume sceneVolume;
    float fpsTimer, fpsShown = 60f;
    int fpsFrames, slowSeconds;
    static readonly string[] TierNames = { "BAIXA", "MÉDIA", "ALTA" };

    void SetupPerformance()
    {
        useGUILayout = false;   // a interface não usa GUILayout: pula o passo de Layout do OnGUI
        qualityMode = PlayerPrefs.GetInt("runner_quality", 0);
        if (!Application.isEditor)
        {
            QualitySettings.vSyncCount = IsMobile ? 0 : 1;          // PC: sincroniza com o monitor (sem tremedeira)
            Application.targetFrameRate = IsMobile ? 60 : -1;
            // cópia do pipeline: dá para mudar escala de render e sombras sem mexer no arquivo do projeto
            var src = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (src != null)
            {
                rpClone = Instantiate(src);
                QualitySettings.renderPipeline = rpClone;
            }
        }
        sceneVolume = FindAnyObjectByType<Volume>();
        ApplyQuality(StartTier());
    }

    int StartTier()
    {
        if (qualityMode == 1) return 2;
        if (qualityMode == 2) return 1;
        if (qualityMode == 3) return 0;
        if (IsMobile) return SystemInfo.systemMemorySize < 3500 || SystemInfo.processorCount < 6 ? 0 : 1;
        return SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 1500 ? 1 : 2;
    }

    void ApplyQuality(int tier)
    {
        QualityTier = Mathf.Clamp(tier, 0, 2);
        bool high = QualityTier == 2, low = QualityTier == 0;

        // sombras do sol: só na alta (e média no PC), sempre duras (as suaves custam caro)
        bool shadows = high || (QualityTier == 1 && !IsMobile);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.shadows = shadows ? LightShadows.Hard : LightShadows.None;

        // pós-processamento (brilho/bloom): só onde aguenta; desfoque de movimento sempre desligado
        if (cam != null)
        {
            var cd = cam.GetUniversalAdditionalCameraData();
            if (cd != null)
            {
                cd.renderPostProcessing = high || (QualityTier == 1 && !IsMobile);
                cd.antialiasing = AntialiasingMode.None;
                cd.renderShadows = shadows;
            }
            cam.farClipPlane = low ? 175f : 220f;
        }
        if (sceneVolume != null && sceneVolume.profile != null)
        {
            MotionBlur mb;
            if (sceneVolume.profile.TryGet(out mb)) mb.active = false;
            Vignette vg;
            if (sceneVolume.profile.TryGet(out vg)) vg.active = !low;
        }

        if (rpClone != null)
        {
            rpClone.renderScale = high ? 1f : (QualityTier == 1 ? (IsMobile ? 0.8f : 0.9f) : (IsMobile ? 0.65f : 0.75f));
            rpClone.shadowDistance = high ? 45f : 30f;
            rpClone.shadowCascadeCount = high && !IsMobile ? 2 : 1;
            rpClone.msaaSampleCount = 1;
            rpClone.supportsHDR = !IsMobile || high;   // HDR pesa no celular
        }
    }

    /// Mede o FPS e, no modo automático, baixa a qualidade se o jogo ficar lento por alguns segundos.
    void UpdatePerf(float udt)
    {
        fpsTimer += udt;
        fpsFrames++;
        if (fpsTimer < 1f) return;
        fpsShown = fpsFrames / fpsTimer;
        fpsTimer = 0f;
        fpsFrames = 0;
        if (qualityMode != 0 || state != RunnerState.Playing) { slowSeconds = 0; return; }
        float target = 60f;
        if (fpsShown < target * 0.82f) slowSeconds++;
        else slowSeconds = Mathf.Max(0, slowSeconds - 1);
        if (slowSeconds >= 4 && QualityTier > 0)
        {
            ApplyQuality(QualityTier - 1);
            slowSeconds = 0;
        }
    }

    void CycleQualityMode()
    {
        qualityMode = (qualityMode + 1) % 4;
        PlayerPrefs.SetInt("runner_quality", qualityMode);
        PlayerPrefs.Save();
        ApplyQuality(StartTier());
    }

    string QualityLabel => "Gráficos: " + (qualityMode == 0 ? "AUTO (" + TierNames[QualityTier] + ")" : TierNames[QualityTier]);

    /// Limite de obstáculos/inimigos na pista ao mesmo tempo, conforme a qualidade.
    int MaxObstacles => QualityTier == 0 ? 45 : (QualityTier == 1 ? 60 : 80);

    /// Limite de pedacinhos de explosão, conforme o aparelho e a qualidade.
    int DebrisBudget => Mathf.RoundToInt((IsMobile ? 60 : 120) * (QualityTier == 0 ? 0.5f : (QualityTier == 1 ? 0.75f : 1f)));

    void DrawPerfButton(float s, float W)
    {
        ActionButton("quality_p", new Rect(W - 24 * s - 400 * s, 24 * s, 400 * s, 78 * s), QualityLabel,
            new Color(0.2f, 0.3f, 0.45f, 0.9f), s, CycleQualityMode);
        ShadowLabel(new Rect(W - 24 * s - 400 * s, 106 * s, 400 * s, 30 * s), Mathf.RoundToInt(fpsShown) + " FPS",
            Sty(cardSmall, fs: Mathf.RoundToInt(20 * s), al: TextAnchor.MiddleRight), new Color(0.8f, 0.9f, 1f, 0.8f));
    }

    // ================================================================== estilos de texto em cache

    readonly Dictionary<long, GUIStyle> styleCache = new Dictionary<long, GUIStyle>();
    readonly Dictionary<GUIStyle, int> styleIds = new Dictionary<GUIStyle, int>();

    /// Igual a "new GUIStyle(base) { ... }", mas sem criar um objeto novo a cada quadro.
    GUIStyle Sty(GUIStyle b, int fs = -1, TextAnchor? al = null, int ww = -1, FontStyle? fst = null, TextClipping? cl = null)
    {
        int id;
        if (!styleIds.TryGetValue(b, out id)) { id = styleIds.Count + 1; styleIds[b] = id; }
        long key = id;
        key = key * 4096 + Mathf.Clamp(fs, -1, 4094) + 1;
        key = key * 4096 + Mathf.Clamp(b.fontSize, 0, 4095);
        key = key * 16 + (al.HasValue ? (int)al.Value + 1 : 0);
        key = key * 4 + (ww + 1);
        key = key * 8 + (fst.HasValue ? (int)fst.Value + 1 : 0);
        key = key * 4 + (cl.HasValue ? (int)cl.Value + 1 : 0);
        GUIStyle st;
        if (styleCache.TryGetValue(key, out st)) return st;
        if (styleCache.Count > 3000) styleCache.Clear();
        st = new GUIStyle(b);
        if (fs >= 0) st.fontSize = fs;
        if (al.HasValue) st.alignment = al.Value;
        if (ww >= 0) st.wordWrap = ww == 1;
        if (fst.HasValue) st.fontStyle = fst.Value;
        if (cl.HasValue) st.clipping = cl.Value;
        styleCache[key] = st;
        return st;
    }

    // ================================================================== primitivas leves

    static readonly Dictionary<PrimitiveType, Mesh> primMeshes = new Dictionary<PrimitiveType, Mesh>();

    static Mesh PrimMesh(PrimitiveType t)
    {
        Mesh m;
        if (primMeshes.TryGetValue(t, out m) && m != null) return m;
        var tmp = GameObject.CreatePrimitive(t);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        primMeshes[t] = m;
        return m;
    }

    /// Cria uma primitiva sem colisor (bem mais barato que CreatePrimitive + Destroy do colisor).
    public static GameObject NewPrim(PrimitiveType t, out MeshRenderer mr, bool castShadows = true)
    {
        var go = new GameObject(t.ToString());
        go.AddComponent<MeshFilter>().sharedMesh = PrimMesh(t);
        mr = go.AddComponent<MeshRenderer>();
        if (!castShadows)
        {
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        return go;
    }

    // ================================================================== listas reaproveitadas

    readonly Stack<List<RunnerObstacle>> listPool = new Stack<List<RunnerObstacle>>();

    /// Cópia da lista de obstáculos sem gerar lixo (devolva com ReleaseList).
    List<RunnerObstacle> SnapshotObstacles()
    {
        var l = listPool.Count > 0 ? listPool.Pop() : new List<RunnerObstacle>(128);
        l.Clear();
        l.AddRange(obstacles);
        return l;
    }

    void ReleaseList(List<RunnerObstacle> l)
    {
        l.Clear();
        if (listPool.Count < 8) listPool.Push(l);
    }

    // ================================================================== prédios reaproveitados

    readonly Dictionary<int, List<GameObject>> structPool = new Dictionary<int, List<GameObject>>();
    Transform structPoolRoot;

    /// Troca a estrutura de um lote: reaproveita prédios já construídos em vez de destruir e criar de novo.
    void SwapStructure(Transform lot, float side)
    {
        if (structPoolRoot == null)
        {
            structPoolRoot = new GameObject("PoolPredios").transform;
            structPoolRoot.gameObject.SetActive(false);
        }
        // devolve o que estava no lote
        for (int i = lot.childCount - 1; i >= 0; i--)
        {
            var old = lot.GetChild(i);
            var info = old.GetComponent<RunnerStructInfo>();
            if (info == null) { Destroy(old.gameObject); continue; }
            old.SetParent(structPoolRoot, false);
            List<GameObject> back;
            if (!structPool.TryGetValue(info.key, out back)) { back = new List<GameObject>(); structPool[info.key] = back; }
            if (back.Count < 24) back.Add(old.gameObject);
            else Destroy(old.gameObject);
        }

        int key = T.id * 2 + (side > 0f ? 1 : 0);
        List<GameObject> list;
        structPool.TryGetValue(key, out list);
        GameObject st = null;
        // com estoque suficiente, reaproveita (de vez em quando constrói um novo para variar)
        if (list != null && list.Count >= 8 && Random.value < 0.85f)
        {
            int k = Random.Range(0, list.Count);
            st = list[k];
            list.RemoveAt(k);
        }
        float half;
        if (st != null)
        {
            st.transform.SetParent(lot, false);
            half = st.GetComponent<RunnerStructInfo>().halfWidth;
        }
        else
        {
            st = new GameObject("Estrutura");
            st.transform.SetParent(lot, false);
            half = BuildStructure(st.transform, side);
            var info = st.AddComponent<RunnerStructInfo>();
            info.key = key;
            info.halfWidth = half;
        }
        lot.localPosition = new Vector3(side * (8.4f + half), 0f, lot.localPosition.z);
    }

    /// Ao trocar de região, os prédios guardados de outras regiões não servem mais.
    void ClearStructPoolExcept(int biomeId)
    {
        var keys = new List<int>(structPool.Keys);
        foreach (var k in keys)
        {
            if (k / 2 == biomeId) continue;
            foreach (var go in structPool[k]) if (go != null) Destroy(go);
            structPool.Remove(k);
        }
    }
}

/// Guarda de que região/lado é um prédio reaproveitável e a meia-largura dele.
public class RunnerStructInfo : MonoBehaviour
{
    public int key;
    public float halfWidth;
}
