#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Configura o projeto para publicar EZEQUIEL na Steam (Windows) e na Google Play (Android):
/// nome, empresa, identificador do app, versão, ícones (incluindo os ícones adaptativos do Android)
/// e atalhos de build no menu "Ezequiel".
///
/// Roda sozinho uma vez quando o projeto abre. Para aplicar de novo: menu Ezequiel → Aplicar nome e ícones.
/// </summary>
[InitializeOnLoad]
public static class EzequielBuildSetup
{
    // ------------------------------------------------------------------ ajuste aqui antes de publicar
    public const string GameName = "Ezequiel";
    public const string Company = "Kevin Lima Pena";
    // ATENÇÃO: o identificador do Android não pode mudar depois do primeiro envio à Play Store.
    public const string AppId = "com.kevinlimapena.ezequiel";
    public const string FirstVersion = "1.0.0";

    const string IconDir = "Assets/Ezequiel/Icones/";
    const string IconMain = IconDir + "ezequiel_icon_1024.png";
    const string IconBg = IconDir + "android_adaptive_background.png";
    const string IconFg = IconDir + "android_adaptive_foreground.png";
    const string SetupKey = "EzequielSetup_v2_";
    public const int RequiredTargetApi = 36;

    /// O Unity 6000.0 só consegue gerar para API 36 a partir do 6000.0.46f1.
    public static bool UnitySupportsApi36()
    {
        // ex.: "6000.0.41f1" → 6000, 0, 41
        var v = Application.unityVersion.Split('.');
        if (v.Length < 3) return true;
        int major, minor, patch;
        int.TryParse(v[0], out major);
        int.TryParse(v[1], out minor);
        var digits = new string(v[2].TakeWhile(char.IsDigit).ToArray());
        int.TryParse(digits, out patch);
        if (major > 6000 || minor > 0) return true;
        return patch >= 46;
    }

    static EzequielBuildSetup()
    {
        EditorApplication.delayCall += AutoApply;
    }

    static void AutoApply()
    {
        string key = SetupKey + Application.dataPath;
        if (EditorPrefs.GetBool(key, false) && PlayerSettings.productName == GameName) return;
        if (Apply(false)) EditorPrefs.SetBool(key, true);
    }

    [MenuItem("Ezequiel/Aplicar nome e ícones", priority = 0)]
    static void ApplyMenu()
    {
        if (Apply(true))
            EditorUtility.DisplayDialog("Ezequiel", "Nome, identificador, versão e ícones aplicados para Windows e Android.", "OK");
    }

    /// Retorna false se os ícones ainda não foram importados (tenta de novo depois).
    static bool Apply(bool verbose)
    {
        // ---------------- nome, empresa, identificador, versão
        PlayerSettings.companyName = Company;
        PlayerSettings.productName = GameName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, AppId);
        if (PlayerSettings.bundleVersion == "0.1.0" || string.IsNullOrEmpty(PlayerSettings.bundleVersion))
            PlayerSettings.bundleVersion = FirstVersion;
        if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;

        // ---------------- Android: requisitos da Google Play (64 bits, API mais nova instalada)
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        // Google Play exige API 36 (Android 16) para apps novos desde 31/08/2026; o Unity 6000.0 só suporta a partir do 6000.0.46f1
        PlayerSettings.Android.targetSdkVersion = UnitySupportsApi36() ? (AndroidSdkVersions)RequiredTargetApi : AndroidSdkVersions.AndroidApiLevelAuto;
        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel23) PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // ---------------- Windows (Steam): tela cheia em janela, pode redimensionar
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;

        // ---------------- ícones
        PrepareIcon(IconMain);
        PrepareIcon(IconBg);
        PrepareIcon(IconFg);
        var main = AssetDatabase.LoadAssetAtPath<Texture2D>(IconMain);
        var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconBg);
        var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(IconFg);
        if (main == null)
        {
            if (verbose) Debug.LogWarning("Ezequiel: ícone não encontrado em " + IconMain);
            return false;
        }

        // ícone padrão (vale para todas as plataformas) e Windows
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { main }, IconKind.Any);
        int n = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any).Length;
        if (n > 0) PlayerSettings.SetIcons(NamedBuildTarget.Standalone, Enumerable.Repeat(main, n).ToArray(), IconKind.Any);

        // Android: adaptativo (fundo + frente), redondo e legado
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons)
            {
                if (icon.maxLayerCount >= 2 && bg != null && fg != null) icon.SetTextures(bg, fg);
                else icon.SetTextures(main);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

        AssetDatabase.SaveAssets();
        if (verbose) Debug.Log("Ezequiel: configurações de publicação aplicadas (" + AppId + ", versão " + PlayerSettings.bundleVersion + ").");
        return true;
    }

    /// Ícones precisam ser importados sem compressão e sem mipmaps para ficarem nítidos.
    static void PrepareIcon(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool changed = imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.textureCompression != TextureImporterCompression.Uncompressed
                       || imp.npotScale != TextureImporterNPOTScale.None || !imp.alphaIsTransparency || imp.maxTextureSize < 1024;
        if (!changed) return;
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }

    // ================================================================== builds

    static string[] Scenes()
    {
        var list = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        return list.Length > 0 ? list : new[] { "Assets/Scenes/SampleScene.unity" };
    }

    static void Build(BuildTarget target, BuildTargetGroup group, string path, bool aab)
    {
        Apply(false);
        if (EditorUserBuildSettings.activeBuildTarget != target)
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        if (group == BuildTargetGroup.Android) EditorUserBuildSettings.buildAppBundle = aab;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var opts = new BuildPlayerOptions { scenes = Scenes(), locationPathName = path, target = target, targetGroup = group, options = BuildOptions.None };
        var report = BuildPipeline.BuildPlayer(opts);
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        EditorUtility.DisplayDialog("Ezequiel", ok ? "Build pronto:\n" + Path.GetFullPath(path) : "O build falhou. Veja o Console para os detalhes.", "OK");
        if (ok) EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Ezequiel/Gerar/Windows (Steam)", priority = 20)]
    static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, "Builds/Windows/" + GameName + ".exe", false);

    [MenuItem("Ezequiel/Gerar/Android APK (teste no celular)", priority = 21)]
    static void BuildApk() => Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/" + GameName + ".apk", false);

    [MenuItem("Ezequiel/Gerar/Android AAB (Google Play)", priority = 22)]
    static void BuildAab()
    {
        if (!UnitySupportsApi36())
        {
            if (!EditorUtility.DisplayDialog("Ezequiel — versão do Unity",
                "A Google Play exige API 36 (Android 16) para apps novos.\n\nEste Unity (" + Application.unityVersion + ") só gera até a API 35. " +
                "Instale o Unity 6000.0.46f1 ou mais novo pelo Unity Hub e abra o projeto nele.\n\nGerar mesmo assim (a Play vai recusar)?",
                "Gerar mesmo assim", "Cancelar")) return;
        }
        if (!PlayerSettings.Android.useCustomKeystore)
        {
            bool go = EditorUtility.DisplayDialog("Ezequiel — assinatura",
                "A Google Play só aceita o AAB assinado com a SUA chave (keystore).\n\n" +
                "Crie ou escolha a chave em: Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager.\n\n" +
                "Guarde o arquivo e a senha em lugar seguro: sem eles você não consegue atualizar o jogo.\n\nGerar mesmo assim (só para teste)?",
                "Gerar mesmo assim", "Cancelar");
            if (!go) return;
        }
        Build(BuildTarget.Android, BuildTargetGroup.Android, "Builds/Android/" + GameName + ".aab", true);
    }

    [MenuItem("Ezequiel/Verificar se está pronto para a Play Store", priority = 30)]
    static void CheckReady()
    {
        Apply(false);
        var ok = new System.Text.StringBuilder();
        var todo = new System.Text.StringBuilder();
        System.Action<bool, string, string> Check = (cond, good, bad) =>
        {
            if (cond) ok.Append("✔ ").Append(good).Append('\n');
            else todo.Append("✘ ").Append(bad).Append('\n');
        };

        string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        Check(!id.Contains("Unity") && !id.Contains("DefaultCompany") && id.Split('.').Length >= 3, "Identificador: " + id, "Identificador inválido: " + id);
        Check(PlayerSettings.productName == GameName, "Nome: " + GameName, "Nome do jogo ainda não é " + GameName);
        Check(UnitySupportsApi36(), "Unity " + Application.unityVersion + " gera para API 36",
              "Atualize o Unity para 6000.0.46f1 ou mais novo (Unity Hub) — a Play exige API 36");
        Check(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) == ScriptingImplementation.IL2CPP
              && (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0, "IL2CPP + ARM64 (64 bits)", "Ative IL2CPP e ARM64");
        Check(PlayerSettings.Android.useCustomKeystore && !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName),
              "Chave de assinatura configurada", "Crie a chave (keystore): Project Settings → Player → Android → Publishing Settings → Keystore Manager");
        var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
        Check(icons != null && icons.Length > 0 && icons[0] != null, "Ícone definido", "Ícone não encontrado em " + IconMain);
        Check(PlayerSettings.Android.bundleVersionCode >= 1, "Versão " + PlayerSettings.bundleVersion + " (código " + PlayerSettings.Android.bundleVersionCode + ")", "Código de versão inválido");
        Check(!EditorUserBuildSettings.development, "Build de lançamento (sem atalhos de desenvolvedor)", "Desmarque 'Development Build' em File → Build Profiles");

        string msg = (todo.Length == 0 ? "Tudo pronto para gerar o AAB!\n\n" : "Falta resolver:\n" + todo + "\n") + ok;
        EditorUtility.DisplayDialog("Ezequiel — Google Play", msg, "OK");
    }

    [MenuItem("Ezequiel/Nova versão (aumentar número)", priority = 40)]
    static void BumpVersion()
    {
        var parts = PlayerSettings.bundleVersion.Split('.');
        int patch = parts.Length >= 3 && int.TryParse(parts[2], out var p) ? p + 1 : 1;
        string major = parts.Length > 0 ? parts[0] : "1", minor = parts.Length > 1 ? parts[1] : "0";
        PlayerSettings.bundleVersion = major + "." + minor + "." + patch;
        PlayerSettings.Android.bundleVersionCode += 1;
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Ezequiel", "Versão " + PlayerSettings.bundleVersion + " (código Android " + PlayerSettings.Android.bundleVersionCode + ").\n" +
            "A Google Play exige um código maior a cada envio.", "OK");
    }
}
#endif
