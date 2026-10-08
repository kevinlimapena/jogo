using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controles de toque para celular.
/// - Deslizar (swipe) para os lados / cima / baixo
/// - Toque rápido
/// - Joystick virtual: arrastar o dedo a partir do ponto onde encostou (avião e carro)
/// - Botões na tela (pausa, habilidade, rerrolar...), registrados pela UI a cada frame
/// Coordenadas de botões são em "GUI" (origem no canto superior esquerdo).
/// </summary>
public static class RunnerTouch
{
    public static bool Active;                 // já houve toque nesta sessão → mostra UI de toque
    public static bool SwipeLeft, SwipeRight, SwipeUp, SwipeDown;
    public static bool Tap;
    public static Vector2 TapPos;              // em coordenadas GUI
    public static Vector2 Stick;               // -1..1
    public static bool Holding;
    public static Vector2 DragDelta;           // quanto o dedo de controle andou neste frame (pixels)

    class Info
    {
        public Vector2 start, swipeOrigin, last;
        public float t0;
        public bool button;
        public bool moved;
    }

    static readonly Dictionary<string, Rect> buttons = new Dictionary<string, Rect>();
    static readonly HashSet<string> pressed = new HashSet<string>();
    static readonly Dictionary<int, Info> touches = new Dictionary<int, Info>();
    static readonly List<int> toRemove = new List<int>();
    static readonly HashSet<int> seen = new HashSet<int>();
    static int controlId = -1;
    static int lastFrame = -1;

    public static bool UseTouchUI => Active || Application.isMobilePlatform;

    public static void ClearButtons() => buttons.Clear();
    public static void SetButton(string name, Rect guiRect) => buttons[name] = guiRect;
    public static bool Pressed(string name) => pressed.Contains(name);

    /// Pode ser chamado várias vezes por frame; só processa uma vez.
    public static void Update()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;

        SwipeLeft = SwipeRight = SwipeUp = SwipeDown = Tap = false;
        DragDelta = Vector2.zero;
        pressed.Clear();

        var ts = Touchscreen.current;
        if (ts == null)
        {
            Stick = Vector2.zero;
            Holding = false;
            return;
        }

        float unit = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height));
        float swipeDist = unit * 0.07f;
        float stickRadius = unit * 0.13f;
        seen.Clear();

        foreach (var tc in ts.touches)
        {
            var phase = tc.phase.ReadValue();
            if (phase == UnityEngine.InputSystem.TouchPhase.None) continue;
            int id = tc.touchId.ReadValue();
            Vector2 pos = tc.position.ReadValue();
            bool ending = phase == UnityEngine.InputSystem.TouchPhase.Ended || phase == UnityEngine.InputSystem.TouchPhase.Canceled;

            if (!touches.TryGetValue(id, out var info))
            {
                if (ending) continue;
                // novo toque
                Active = true;
                info = new Info { start = pos, swipeOrigin = pos, last = pos, t0 = Time.unscaledTime };
                Vector2 gui = new Vector2(pos.x, Screen.height - pos.y);
                foreach (var kv in buttons)
                {
                    if (kv.Value.Contains(gui))
                    {
                        info.button = true;
                        pressed.Add(kv.Key);
                        break;
                    }
                }
                if (!info.button && controlId == -1) controlId = id;
                touches[id] = info;
            }

            seen.Add(id);
            if (id == controlId) DragDelta += pos - info.last;
            info.last = pos;

            if (id == controlId)
            {
                if ((pos - info.start).magnitude > unit * 0.03f) info.moved = true;
                Vector2 d = pos - info.swipeOrigin;
                if (d.magnitude > swipeDist)
                {
                    if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) { if (d.x > 0f) SwipeRight = true; else SwipeLeft = true; }
                    else { if (d.y > 0f) SwipeUp = true; else SwipeDown = true; }
                    info.swipeOrigin = pos;   // permite vários swipes no mesmo arrasto
                }
            }

            if (ending)
            {
                if (id == controlId)
                {
                    if (!info.moved && Time.unscaledTime - info.t0 < 0.3f)
                    {
                        Tap = true;
                        TapPos = new Vector2(pos.x, Screen.height - pos.y);
                    }
                    controlId = -1;
                }
                touches.Remove(id);
                seen.Remove(id);
            }
        }

        // remove toques que sumiram sem "Ended"
        toRemove.Clear();
        foreach (var kv in touches) if (!seen.Contains(kv.Key)) toRemove.Add(kv.Key);
        foreach (var id in toRemove)
        {
            touches.Remove(id);
            if (id == controlId) controlId = -1;
        }

        if (controlId != -1 && touches.TryGetValue(controlId, out var c))
        {
            Stick = Vector2.ClampMagnitude((c.last - c.start) / stickRadius, 1f);
            Holding = true;
        }
        else
        {
            Stick = Vector2.zero;
            Holding = false;
        }
    }

    /// Qualquer tecla/clique/controle desliga a UI de toque (útil no editor e em tablets com teclado).
    public static void NotifyNonTouchInput()
    {
        if (!Application.isMobilePlatform) Active = false;
    }
}
