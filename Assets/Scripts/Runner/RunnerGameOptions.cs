using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Opções do jogador: ligar/desligar as dicas e sair para o menu principal pela pausa.
/// </summary>
public partial class RunnerGame
{
    /// Dicas de obstáculos ("PISE EM CIMA!", "INDESTRUTÍVEL!", avisos da 1ª aparição). Salvo entre jogatinas.
    public static bool HintsOn
    {
        get => PlayerPrefs.GetInt("runner_hints", 1) == 1 && !(I != null && I.trailerActive);
        set { PlayerPrefs.SetInt("runner_hints", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// Quantidade de textos na tela: 0 = POUCOS, 1 = NORMAL (padrão), 2 = TODOS.
    public static int TextMode
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("runner_texts", 1), 0, 2);
        set { PlayerPrefs.SetInt("runner_texts", value); PlayerPrefs.Save(); }
    }
    static readonly string[] TextModeNames = { "POUCOS", "NORMAL", "TODOS" };

    void CycleTextMode()
    {
        TextMode = (TextMode + 1) % 3;
        floats.Clear();
        ShowPopup("TEXTOS NA TELA: " + TextModeNames[TextMode]);
    }

    float quitConfirmUntil;
    string menuNote = "";
    float menuNoteTime;

    void ToggleHints()
    {
        HintsOn = !HintsOn;
        ShowPopup(HintsOn ? "DICAS LIGADAS" : "DICAS DESLIGADAS");
    }

    /// Primeiro toque pede confirmação; o segundo (em até 3 s) volta ao menu.
    void RequestQuitToMenu()
    {
        if (Time.unscaledTime > quitConfirmUntil)
        {
            quitConfirmUntil = Time.unscaledTime + 3f;
            Sfx("clique", 0.6f);
            return;
        }
        QuitToMenu();
    }

    /// Encerra a jornada sem morrer: guarda recorde e Talentos (como no fim de jogo) e volta ao menu.
    void QuitToMenu()
    {
        quitConfirmUntil = 0f;
        RecordDaily();
        if (Score > highScore)
        {
            highScore = Score;
            PlayerPrefs.SetInt("runner_highscore", highScore);
            PlayerPrefs.Save();
        }
        lastTalentsEarned = Meta.TalentsForRun(Score, bossesDefeated, level, finalBeaten);
        Meta.Talents += lastTalentsEarned;
        menuNote = lastTalentsEarned > 0 ? "Jornada encerrada  —  +" + lastTalentsEarned + " Talentos" : "Jornada encerrada";
        menuNoteTime = 4f;

        DevResetToRunning();   // encerra mini-jogos, voo, corrida, chefe...
        ResetRun();
        player.SetVisible(true);
        state = RunnerState.Menu;
        Time.timeScale = 1f;
    }

    // ================================================================== celular / loja

    float exitConfirmUntil;

    /// O app foi para o segundo plano (ligação, troca de app): pausa e salva.
    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        if (state == RunnerState.Playing) state = RunnerState.Paused;
        PlayerPrefs.Save();
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus && Application.isMobilePlatform && state == RunnerState.Playing) state = RunnerState.Paused;
    }

    void OnApplicationQuit() { PlayerPrefs.Save(); }

    /// Botão "voltar" do Android (Esc no PC) no menu principal: aperte duas vezes para sair.
    bool MenuBackPressed()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return false;
        if (Time.unscaledTime < exitConfirmUntil)
        {
            PlayerPrefs.Save();
            Application.Quit();
            return true;
        }
        exitConfirmUntil = Time.unscaledTime + 2.5f;
        return true;
    }

    void DrawExitConfirm(float s, float W, float H)
    {
        if (Time.unscaledTime >= exitConfirmUntil) return;
        var r = new Rect(W / 2 - 330 * s, H * 0.5f - 40 * s, 660 * s, 80 * s);
        Box(r, new Color(0f, 0f, 0f, 0.8f));
        ShadowLabel(r, Application.isMobilePlatform ? "Toque em VOLTAR de novo para sair do jogo" : "Aperte Esc de novo para sair do jogo",
            Sty(midStyle, fs: Mathf.RoundToInt(28 * s)), Color.white);
    }

    /// F12: captura de tela em alta resolução (para as imagens da loja). Só no editor e em builds de desenvolvimento.
    void CheckScreenshotKey()
    {
        if (!(Application.isEditor || Debug.isDebugBuild)) return;
        var kb = Keyboard.current;
        if (kb == null || !kb.f12Key.wasPressedThisFrame) return;
        string dir = Application.isEditor ? "Screenshots" : System.IO.Path.Combine(Application.persistentDataPath, "Screenshots");
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, "ezequiel_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        ScreenCapture.CaptureScreenshot(path, 1);
        Debug.Log("Captura salva em " + System.IO.Path.GetFullPath(path));
    }

    /// Botões extras da tela de pausa (dicas e sair para o menu).
    void DrawPauseOptions(float s, float W, float H, Rect ps1Btn)
    {
        bool on = HintsOn;
        ActionButton("hints_p", new Rect(ps1Btn.x - 14 * s - 300 * s, ps1Btn.y, 300 * s, ps1Btn.height),
            "Dicas: " + (on ? "LIGADAS" : "DESLIGADAS") + (RunnerTouch.UseTouchUI ? "" : "  [H]"),
            on ? new Color(0.2f, 0.5f, 0.3f, 0.85f) : new Color(0.45f, 0.2f, 0.2f, 0.85f), s, ToggleHints);
        ActionButton("texts_p", new Rect(ps1Btn.x, ps1Btn.yMax + 14 * s, ps1Btn.width, ps1Btn.height),
            "Textos na tela: " + TextModeNames[TextMode], new Color(0.3f, 0.25f, 0.45f, 0.85f), s, CycleTextMode);

        bool confirming = Time.unscaledTime < quitConfirmUntil;
        string lbl = confirming ? (RunnerTouch.UseTouchUI ? "TOQUE DE NOVO PARA SAIR" : "APERTE DE NOVO PARA SAIR")
                                : "MENU PRINCIPAL" + (RunnerTouch.UseTouchUI ? "" : "  [Q]");
        ActionButton("quit_menu", new Rect(24 * s, 24 * s, 400 * s, 78 * s), lbl,
            confirming ? new Color(0.75f, 0.15f, 0.1f, 0.95f) : new Color(0.35f, 0.25f, 0.1f, 0.9f), s, RequestQuitToMenu);
        if (confirming)
            ShadowLabel(new Rect(24 * s, 106 * s, 600 * s, 40 * s), "A jornada termina e você recebe os Talentos ganhos até aqui",
                Sty(cardSmall, al: TextAnchor.UpperLeft), new Color(1f, 0.85f, 0.6f));
    }
}
