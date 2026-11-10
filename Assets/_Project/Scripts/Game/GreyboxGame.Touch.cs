using System;
using System.Collections.Generic;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    public enum TouchControlsMode
    {
        Auto,    // on phones and tablets
        Always,  // also in the editor: the mouse acts as one finger (for testing)
        Never,
    }

    /// <summary>
    /// Touchscreen controls (phones / tablets). Taps already act as mouse clicks, so tapping
    /// buttons, the phone, and the drag-based mini-games just work; on top of that this adds:
    /// <list type="bullet">
    /// <item>On-screen buttons for the keyboard keys (Stand/Sit, Use, Phone, Table view, Look down,
    /// Call out, Trickshot, Menu) — several fingers at once.</item>
    /// <item>Seated: drag anywhere to look around (pinch to zoom); tap a tile to pick it, tap it
    /// again to discard (so a stray drag never throws a tile away).</item>
    /// <item>Standing: a joystick on the left (push it all the way to run), drag on the right to look.</item>
    /// <item>Drum Rush: tap the left half of the screen for red, the right half for blue.</item>
    /// <item>Bigger text, landscape only, 60 fps.</item>
    /// </list>
    /// </summary>
    public sealed partial class GreyboxGame
    {
        [Header("Touch")]
        [Tooltip("Auto: on-screen touch controls on phones and tablets. Always: also in the editor (the mouse acts as one finger) — handy for testing. Never: off.")]
        [SerializeField] private TouchControlsMode touchControls = TouchControlsMode.Auto;

        private bool TouchUI => touchControls == TouchControlsMode.Always ||
                                (touchControls == TouchControlsMode.Auto && Application.isMobilePlatform);

        /// <summary>Text and panels are bigger on a phone.</summary>
        private float UiDivisor => TouchUI ? 40f : 55f;

        // Keys "pressed" by on-screen buttons this frame (read through KeyDown).
        private readonly HashSet<KeyCode> touchKeys = new HashSet<KeyCode>();
        private bool KeyDown(KeyCode key) => Input.GetKeyDown(key) || touchKeys.Contains(key);

        private enum FingerRole { Pending, Button, Joystick, Look }

        private sealed class Finger
        {
            public FingerRole Role;
            public Vector2 Start, Last;
            public int Button = -1;
        }

        private struct TouchButton
        {
            public string Label;
            public Rect Rect;                // GUI coordinates (origin top-left)
            public KeyCode? Key;
            public Action Action;
            public bool Lit;
        }

        private readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        private readonly List<TouchButton> touchButtons = new List<TouchButton>();
        private readonly List<Vector2> touchTaps = new List<Vector2>();   // new touches this frame (not on a button)
        private float touchYaw, touchPitch;          // seated: where you've dragged your head to
        private bool touchLookDown, touchTableView;
        private Vector2 joystick;                    // -1..1, standing only
        private TileView touchSelectedTile;          // tap once to pick, again to discard
        private float pinchDistance = -1f;
        private bool mouseFingerDown;
        private Vector2 mouseFingerLast;

        private const float DragThreshold = 16f;     // pixels before a touch counts as a drag

        private void SetUpTouch()
        {
            if (!TouchUI)
                return;
            Input.multiTouchEnabled = true;
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        /// <summary>The usable part of the screen (inside the notch / home bar), in GUI coordinates.</summary>
        private Rect SafeGui
        {
            get
            {
                Rect s = Screen.safeArea;
                return new Rect(s.x, Screen.height - s.yMax, s.width, s.height);
            }
        }

        private float ButtonSize => Unit * 4.6f;
        private Vector2 JoystickCentre => new Vector2(SafeGui.x + Unit * 8.5f, SafeGui.yMax - Unit * 8.5f);
        private float JoystickRadius => Unit * 5.5f;

        /// <summary>Runs first each frame: lays out the buttons, reads every finger, presses virtual keys.</summary>
        private void UpdateTouch()
        {
            touchKeys.Clear();
            touchTaps.Clear();
            joystick = Vector2.zero;
            touchButtons.Clear();
            if (!TouchUI)
                return;
            if (walker != null)
                walker.UseMouse = false;
            if (menu != MenuMode.None || tables.Count == 0 || Seated.Match == null)
            {
                fingers.Clear();
                return;
            }
            ctx = Seated;
            BuildTouchButtons();

            foreach ((int id, Vector2 gui, TouchPhase phase) in ReadFingers())
            {
                if (phase == TouchPhase.Began)
                {
                    fingers[id] = StartFinger(gui);
                    continue;
                }
                if (!fingers.TryGetValue(id, out Finger f))
                    continue;
                if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
                {
                    fingers.Remove(id);
                    continue;
                }
                Vector2 delta = gui - f.Last;
                f.Last = gui;
                switch (f.Role)
                {
                    case FingerRole.Joystick:
                        Vector2 off = (gui - JoystickCentre) / JoystickRadius;
                        joystick = Vector2.ClampMagnitude(new Vector2(off.x, -off.y), 1f);
                        break;
                    case FingerRole.Pending:
                        if ((gui - f.Start).magnitude > DragThreshold && CanDragLook(f.Start))
                            f.Role = FingerRole.Look;
                        break;
                    case FingerRole.Look:
                        if (CountLookFingers() < 2)
                            DragLook(delta);
                        break;
                }
            }
            UpdatePinch();
            if (walker != null)
                walker.TouchMove = standing ? joystick : Vector2.zero;
        }

        private Finger StartFinger(Vector2 gui)
        {
            var f = new Finger { Start = gui, Last = gui };
            for (int i = 0; i < touchButtons.Count; i++)
            {
                if (!touchButtons[i].Rect.Contains(gui))
                    continue;
                f.Role = FingerRole.Button;
                f.Button = i;
                TouchButton b = touchButtons[i];
                if (b.Key.HasValue) touchKeys.Add(b.Key.Value);
                b.Action?.Invoke();
                return f;
            }
            touchTaps.Add(gui);
            if (standing && (gui - JoystickCentre).magnitude < JoystickRadius * 1.6f)
            {
                f.Role = FingerRole.Joystick;
                Vector2 off = (gui - JoystickCentre) / JoystickRadius;
                joystick = Vector2.ClampMagnitude(new Vector2(off.x, -off.y), 1f);
            }
            else if (standing && !CursorNeeded)
            {
                f.Role = FingerRole.Look;           // standing: any other drag turns your head
            }
            return f;
        }

        /// <summary>Seated drags look around — unless they're for something else (shuffling, a mini-game, a panel).</summary>
        private bool CanDragLook(Vector2 start)
        {
            if (standing)
                return !CursorNeeded;
            if (Shuffling || InMiniGame || PickerOpen || phonePanelOpen || OnPhone(HumanSeat) || headLook.InTableView)
                return false;
            foreach (Rect r in uiRects)
                if (r.Contains(start))
                    return false;
            return true;
        }

        private void DragLook(Vector2 delta)
        {
            if (standing)
            {
                if (walker != null)
                    walker.TouchLook += new Vector2(delta.x, -delta.y) * (180f / Screen.height);
                return;
            }
            touchYaw = Mathf.Clamp(touchYaw + delta.x * (140f / Screen.width), -HeadLook.MaxYaw, HeadLook.MaxYaw);
            touchPitch = Mathf.Clamp(touchPitch + delta.y * (80f / Screen.height), -HeadLook.MaxUp, HeadLook.MaxDown);
            touchLookDown = false;
        }

        private int CountLookFingers()
        {
            int n = 0;
            foreach (Finger f in fingers.Values)
                if (f.Role == FingerRole.Look) n++;
            return n;
        }

        /// <summary>Two fingers: pinch to zoom (seated).</summary>
        private void UpdatePinch()
        {
            Finger a = null, b = null;
            foreach (Finger f in fingers.Values)
            {
                if (f.Role != FingerRole.Look && f.Role != FingerRole.Pending) continue;
                if (a == null) a = f; else if (b == null) b = f;
            }
            if (standing || a == null || b == null)
            {
                pinchDistance = -1f;
                return;
            }
            float d = (a.Last - b.Last).magnitude;
            if (pinchDistance > 0f)
                headLook.AddZoom((d - pinchDistance) / Screen.height * 1.5f);
            pinchDistance = d;
            a.Role = b.Role = FingerRole.Look;
        }

        /// <summary>Every finger this frame — or, with TouchControls = Always and no touchscreen, the mouse as one finger.</summary>
        private IEnumerable<(int, Vector2, TouchPhase)> ReadFingers()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch t = Input.GetTouch(i);
                    yield return (t.fingerId, new Vector2(t.position.x, Screen.height - t.position.y), t.phase);
                }
                yield break;
            }
            Vector3 m = Input.mousePosition;
            var gui = new Vector2(m.x, Screen.height - m.y);
            if (Input.GetMouseButtonDown(0))
            {
                mouseFingerDown = true;
                mouseFingerLast = gui;
                yield return (-1, gui, TouchPhase.Began);
            }
            else if (mouseFingerDown && Input.GetMouseButton(0))
            {
                TouchPhase phase = gui == mouseFingerLast ? TouchPhase.Stationary : TouchPhase.Moved;
                mouseFingerLast = gui;
                yield return (-1, gui, phase);
            }
            else if (mouseFingerDown)
            {
                mouseFingerDown = false;
                yield return (-1, gui, TouchPhase.Ended);
            }
        }

        // ------------------------------------------------------------------ the buttons

        private void AddTouchButton(string label, KeyCode? key = null, Action action = null, bool lit = false)
        {
            // A column up the right-hand side, bottom first.
            Rect safe = SafeGui;
            float s = ButtonSize, gap = Unit * 0.6f;
            int n = touchButtons.Count;
            int perColumn = Mathf.Max(1, Mathf.FloorToInt((safe.height * 0.62f) / (s + gap)));
            int col = n / perColumn, row = n % perColumn;
            var r = new Rect(safe.xMax - (s + gap) * (col + 1), safe.yMax - (s + gap) * (row + 1), s, s);
            touchButtons.Add(new TouchButton { Label = label, Rect = r, Key = key, Action = action, Lit = lit });
        }

        private void BuildTouchButtons()
        {
            if (!humanAlive || stageStopped)
                return;
            if (Drumming || InMiniGame)
            {
                if (Drumming)
                    AddTouchButton("Close\nphone", KeyCode.P);
                return;
            }
            if (standing)
            {
                AddTouchButton("Use", KeyCode.E);
                if (NearestChair() >= 0)
                    AddTouchButton("Sit", KeyCode.F);
                AddTouchButton(phonePanelOpen ? "Close\nphone" : "Phone", KeyCode.P);
                return;
            }

            if (!PureMahjong)
            {
                AddTouchButton("Stand\nup", KeyCode.F);
                AddTouchButton(phonePanelOpen ? "Close\nphone" : "Phone", KeyCode.P);
            }
            AddTouchButton("Table\nview", action: () => touchTableView = !touchTableView, lit: touchTableView);
            if (!PureMahjong && HandLive)
            {
                AddTouchButton("Look\ndown", action: () => { touchLookDown = !touchLookDown; touchTableView = false; }, lit: touchLookDown);
                AddTouchButton("Call\nout!", action: () => HumanCallOut(LookTarget()));
                if (ctx.Ready[HumanSeat] && !ctx.TrickTried[HumanSeat] && match.TrickshotMultiplier(HumanSeat) == 1)
                    AddTouchButton("Trick\nshot", KeyCode.T);
            }
        }

        /// <summary>The touch buttons, the Menu button and (standing) the joystick.</summary>
        private void DrawTouchControls()
        {
            if (!TouchUI || menu != MenuMode.None)
                return;
            var style = new GUIStyle(centerStyle) { wordWrap = true, fontStyle = FontStyle.Bold };
            foreach (TouchButton b in touchButtons)
            {
                uiRects.Add(b.Rect);
                Color keep = GUI.color;
                GUI.color = b.Lit ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(1f, 1f, 1f, 0.85f);
                GUI.Box(b.Rect, GUIContent.none, boxStyle);
                GUI.Box(b.Rect, GUIContent.none, boxStyle);
                GUI.color = keep;
                GUI.Label(b.Rect, b.Label, style);
            }

            // Menu (pause) — a small IMGUI button at the top middle.
            Rect safe = SafeGui;
            var menuRect = new Rect(safe.center.x - Unit * 3f, safe.y + Pad, Unit * 6f, Unit * 2.6f);
            uiRects.Add(menuRect);
            if (GUI.Button(menuRect, "<b>II  Menu</b>", buttonStyle))
            {
                menu = MenuMode.Pause;
                Time.timeScale = 0f;
                GUIUtility.ExitGUI();
            }

            if (standing && humanAlive && !stageStopped)
            {
                var canvas = new MiniCanvas { Area = new Rect(0, 0, Screen.width, Screen.height) };
                float h = Screen.height;
                Vector2 c = JoystickCentre;
                canvas.Circle(c.x / Screen.width, c.y / h, JoystickRadius / h, new Color(0f, 0f, 0f, 0.3f));
                Vector2 knob = c + new Vector2(joystick.x, -joystick.y) * JoystickRadius;
                canvas.Circle(knob.x / Screen.width, knob.y / h, JoystickRadius * 0.45f / h,
                    joystick.magnitude > 0.92f ? new Color(1f, 0.8f, 0.3f, 0.8f) : new Color(1f, 1f, 1f, 0.6f));
            }
        }
    }
}
