using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Estábulo (customização do cavalo-anjo por profeta) e animações das cartas.
/// </summary>
public partial class RunnerGame
{
    string horseKey = "";
    float stableSpin;

    // ================================================================== cavalo

    string CurrentHorseKey()
    {
        var p = Meta.Selected;
        return p.id + "|" + Horse.CoatIndex(p.id) + "|" + Horse.AdornIndex(p.id);
    }

    /// Reconstrói o modelo do jogador com a pelagem e o adorno do profeta escolhido.
    void RebuildHorse()
    {
        if (player == null || player.runnerModel == null) return;
        var pid = Meta.Selected.id;
        horseKey = CurrentHorseKey();
        var run = player.runnerModel.transform;
        for (int i = run.childCount - 1; i >= 0; i--)
        {
            var ch = run.GetChild(i);
            if (ch.GetComponent<RunnerCreature>() != null) { ch.SetParent(null); Destroy(ch.gameObject); }
        }
        var pal = Horse.PaletteFor(pid);
        var creature = RunnerCreature.Build(run, 1f, -0.9f, pal, false);
        Horse.Adorn(creature, pid, pal);
        var wm = creature.weaponMount.gameObject.AddComponent<RunnerWeaponModel>();
        wm.followPlayerWeapon = true;
        player.weaponModel = wm;
        wm.currentId = "";
    }

    /// Nos menus, mantém o cavalo igual à escolha atual.
    string menuWeaponFor = "", menuWeaponId = "";

    void SyncHorse()
    {
        if (state != RunnerState.Menu && state != RunnerState.Temple && state != RunnerState.Stable && state != RunnerState.Deck && state != RunnerState.Codex) return;
        if (CurrentHorseKey() != horseKey) RebuildHorse();
        // fora da jornada, o cavalo mostra a arma do profeta escolhido
        var p = Meta.Selected;
        if (menuWeaponFor != p.id) { menuWeaponFor = p.id; menuWeaponId = p.weapon().id; }
        if (stats.weapon == null || stats.weapon.id != menuWeaponId) stats.weapon = p.weapon();
    }

    // ================================================================== estábulo

    void UpdateStable(float udt)
    {
        stableSpin += udt * 35f;
        var kb = Keyboard.current;
        var gp = Gamepad.current;
        var pid = Meta.Selected.id;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame)) LeaveStable();
        if (kb != null)
        {
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) Horse.Cycle(pid, true, -1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) Horse.Cycle(pid, true, 1);
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) Horse.Cycle(pid, false, -1);
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) Horse.Cycle(pid, false, 1);
            if (kb.qKey.wasPressedThisFrame) Meta.Cycle(-1);
            if (kb.eKey.wasPressedThisFrame) Meta.Cycle(1);
        }
    }

    void LeaveStable()
    {
        if (player != null && player.runnerModel != null) player.runnerModel.transform.localRotation = Quaternion.identity;
        state = RunnerState.Menu;
    }

    /// Câmera de vitrine: de frente para o cavalo, que gira devagar.
    void StableCamera()
    {
        var run = player.runnerModel.transform;
        run.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(stableSpin * Mathf.Deg2Rad) * 70f, 0f);
        var p = player.transform.position;
        cam.transform.position = Vector3.Lerp(cam.transform.position, p + new Vector3(1.4f, 1.5f, 3.6f), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        cam.transform.LookAt(p + new Vector3(0f, 0.35f, 0f));
    }

    void DrawStable(float s, float W, float H)
    {
        var p = Meta.Selected;
        var coats = Horse.Coats(p.id);
        var coat = coats[Horse.CoatIndex(p.id)];
        ShadowLabel(new Rect(0, 24 * s, W, 70 * s), "ESTÁBULO", Sty(bigStyle, fs: Mathf.RoundToInt(64 * s)), new Color(1f, 0.85f, 0.3f));
        ShadowLabel(new Rect(0, 92 * s, W, 40 * s), "o cavalo-anjo de " + p.name, Sty(midStyle, fs: Mathf.RoundToInt(28 * s), fst: FontStyle.Italic), p.color);

        float pw = Mathf.Min(760 * s, W - 30 * s), rowH = 70 * s, gap = 12 * s;
        float x = W / 2 - pw / 2, y = H - 30 * s - rowH * 3 - gap * 2 - 90 * s;
        var dark = new Color(0f, 0f, 0f, 0.55f);
        var arrow = new Color(0.25f, 0.22f, 0.3f, 0.9f);
        string[] titles = { "PROFETA", "PELAGEM", "ADORNO" };
        string[] values = { p.name, coat.name, Horse.AdornLabel(p.id) };
        string[] keys = { "Q / E", "A / D", "W / S" };
        for (int i = 0; i < 3; i++)
        {
            var r = new Rect(x, y + i * (rowH + gap), pw, rowH);
            Box(r, dark);
            ShadowLabel(new Rect(r.x + 90 * s, r.y + 4 * s, r.width - 180 * s, 24 * s), titles[i] + (RunnerTouch.UseTouchUI ? "" : "   [" + keys[i] + "]"), Sty(cardSmall, fs: Mathf.RoundToInt(17 * s)), new Color(0.8f, 0.8f, 0.9f));
            Color vc = i == 1 ? coat.body : (i == 0 ? p.color : coat.halo);
            ShadowLabel(new Rect(r.x + 90 * s, r.y + 26 * s, r.width - 180 * s, 40 * s), values[i], Sty(midStyle, fs: Mathf.RoundToInt(28 * s)), vc);
            int ii = i;
            ActionButton("stb_l" + i, new Rect(r.x + 8 * s, r.y + 8 * s, 70 * s, rowH - 16 * s), "◀", arrow, s, () => StableStep(ii, -1));
            ActionButton("stb_r" + i, new Rect(r.xMax - 78 * s, r.y + 8 * s, 70 * s, rowH - 16 * s), "▶", arrow, s, () => StableStep(ii, 1));
            if (i == 1)
            {
                // amostras de cor das pelagens
                for (int k = 0; k < coats.Count; k++)
                {
                    var sw = new Rect(r.xMax - 78 * s - (coats.Count - k) * 22 * s - 8 * s, r.y + 40 * s, 16 * s, 16 * s);
                    Box(new Rect(sw.x - 2, sw.y - 2, sw.width + 4, sw.height + 4), k == Horse.CoatIndex(p.id) ? Color.white : new Color(0, 0, 0, 0.6f));
                    Box(sw, coats[k].body);
                }
            }
        }
        ActionButton("stb_back", new Rect(W / 2 - 160 * s, H - 30 * s - 72 * s, 320 * s, 64 * s), "PRONTO" + (RunnerTouch.UseTouchUI ? "" : "  (Esc)"), new Color(0.45f, 0.33f, 0.08f, 0.95f), s, LeaveStable);
    }

    void StableStep(int row, int dir)
    {
        var pid = Meta.Selected.id;
        if (row == 0) Meta.Cycle(dir);
        else Horse.Cycle(pid, row == 1, dir);
    }

    // ================================================================== animações das cartas

    /// Efeito 3D curto quando a carta é escolhida, com o tema da carta.
    void PlayCardFx(RunnerCard c)
    {
        if (player == null) return;
        var p = player.transform;
        if (c.isEvolution)
        {
            CardFx.Pillar(p, CardDB.EvolutionColor, 2.6f, 1.4f);
            for (int k = 0; k < 3; k++) CardFx.Ring(p, CardDB.EvolutionColor, 6f + k * 2f, 0.9f, k * 0.18f);
            CardFx.Burst(p, CardDB.EvolutionColor, 24, 7f, 0.3f);
            return;
        }
        if (c.isWeapon)
        {
            CardFx.Spinner(p, stats.weapon.color, new Vector3(0.15f, 0.15f, 1.3f));
            CardFx.Burst(p, stats.weapon.color, 14, 5f, 0.2f);
            return;
        }
        switch (c.id)
        {
            case "fogoceu": case "ira":
                CardFx.Pillar(p, new Color(1f, 0.5f, 0.1f), 1.8f, 0.9f);
                CardFx.Burst(p, new Color(1f, 0.6f, 0.15f), 16, 6f, 0.25f);
                break;
            case "escudo": case "fantasma": case "ariete":
                CardFx.Ring(p, new Color(0.4f, 0.9f, 1f), 4f, 0.7f, 0f, 1f);
                FxSphere(p.position, 2.2f, new Color(0.4f, 0.9f, 1f));
                break;
            case "jerico":
                for (int k = 0; k < 3; k++) CardFx.Ring(p, new Color(1f, 0.85f, 0.35f), 9f, 0.8f, k * 0.2f, 0.6f);
                break;
            case "marvermelho":
                CardFx.WaterWalls(p);
                break;
            case "mana": case "pao":
                CardFx.Flakes(p, new Color(1f, 0.97f, 0.85f), 26);
                break;
            case "coracao": case "kit": case "vampiro": case "pacto":
                CardFx.Hearts(p, 8);
                break;
            case "corrente": case "reacao":
                for (int k = 0; k < 5; k++)
                {
                    var a = p.position + Vector3.up * 0.8f;
                    Zap(a, a + new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 2.5f), Random.Range(-1f, 4f)));
                }
                break;
            case "laminas": case "pisao":
                CardFx.Ring(p, new Color(1f, 0.55f, 0.15f), 3f, 0.6f, 0f, 0.3f);
                CardFx.Burst(p, new Color(1f, 0.55f, 0.15f), 10, 4f, 0.25f);
                break;
            case "tempo": case "setimo":
                CardFx.Ring(p, new Color(0.6f, 0.75f, 1f), 7f, 1.1f, 0f, 1f);
                CardFx.Ring(p, new Color(0.6f, 0.75f, 1f), 5f, 1.1f, 0.25f, 1f);
                break;
            case "vara": case "funda":
                CardFx.Spinner(p, c.id == "vara" ? new Color(0.55f, 0.4f, 0.2f) : new Color(0.75f, 0.73f, 0.68f), c.id == "vara" ? new Vector3(0.1f, 1.4f, 0.1f) : Vector3.one * 0.35f);
                CardFx.Burst(p, new Color(1f, 0.85f, 0.4f), 10, 4f, 0.2f);
                break;
            default:
                // genérico, pela raridade
                Color rc = CardDB.RarityColor(c.rarity);
                int n = 8 + (int)c.rarity * 6;
                CardFx.Burst(p, rc, n, 3.5f + (int)c.rarity * 1.5f, 0.18f + (int)c.rarity * 0.04f);
                if (c.rarity >= Rarity.Epico) CardFx.Ring(p, rc, 5f, 0.7f);
                if (c.rarity == Rarity.Lendario) CardFx.Pillar(p, rc, 1.6f, 1f);
                break;
        }
    }
}
