using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Survival and walking: hunger / thirst / bladder bars, getting up from the table (F),
    /// walking (WASD, Shift to sprint), using the toilet, water and food (E), and dying when a
    /// bar runs out. While you're away from the table your turns play themselves after a moment.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private const float AwayAutoPlaySeconds = 3f;
        private const float InteractRange = 2.2f;

        private RoomView room;
        private Walker walker;
        private SurvivalNeeds needs;
        private bool humanAlive = true;
        private bool standing;
        private InteractKind? using_;          // what you're busy using, if anything
        private float usingUntil, usingStarted;
        private Interactable lookingAtInteractable;

        /// <summary>Is there UI on screen that needs a free mouse cursor?</summary>
        private bool CursorNeeded =>
            OnPhone(HumanSeat) || phonePanelOpen || !HandLive || (popupText != null && Time.time <= popupUntil);

        /// <summary>Does the human (rather than a bot) play this seat right now?</summary>
        private bool HumanControls(int seat) => seat == HumanSeat && humanAlive;

        /// <summary>Away from the table: up and walking, or busy at the toilet / water / food.</summary>
        private bool HumanAway => standing;

        private void SetUpLife()
        {
            room = new RoomView(transform, HumanSeat);
            walker = new Walker(room);
        }

        private void ResetLife()
        {
            needs = new SurvivalNeeds();
            humanAlive = true;
            if (standing)
                SitDown();
            using_ = null;
            avatars[HumanSeat].SetVisible(true);
            ResetPhones();
        }

        /// <summary>Runs before the table logic each frame: needs, walking, interactions, phones.</summary>
        private void UpdateLife(float now, float dt)
        {
            UpdatePhones(now);

            // Standing captures the mouse for looking around — but release it whenever there's
            // something on screen to click (phone, call panel, popup, results/shop).
            bool needsCursor = standing && CursorNeeded;
            if (standing)
            {
                Cursor.lockState = needsCursor ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = needsCursor;
            }

            if (humanAlive && match.Phase != MatchPhase.MatchOver)
            {
                bool canMove = standing && !using_.HasValue && !OnPhone(HumanSeat) && !needsCursor;
                Exertion exertion = standing ? walker.Tick(dt, canMove) : Exertion.Resting;
                needs.Tick(dt, exertion);
                if (needs.IsDead)
                {
                    Die(needs.CauseOfDeath.Value);
                    return;
                }
            }

            if (standing)
            {
                walker.ApplyTo(cam);
                UpdateInteractables(now);
                if (Input.GetKeyDown(KeyCode.E) && lookingAtInteractable != null && !using_.HasValue && !OnPhone(HumanSeat))
                    Use(lookingAtInteractable.Kind, now);
                if (Input.GetKeyDown(KeyCode.F) && NearChair() && !using_.HasValue)
                    SitDown();
            }
            else if (humanAlive && Input.GetKeyDown(KeyCode.F) && !OnPhone(HumanSeat)
                     && (cheats[HumanSeat] == null || !cheats[HumanSeat].InProgress))
            {
                StandUp();
            }
        }

        private void StandUp()
        {
            standing = true;
            walker.Place(RoomView.StandUpPoint(HumanSeat), 180f); // facing away from the table
            avatars[HumanSeat].SetVisible(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            hoveredTile = null;
            AddLog("You get up from the table.");
        }

        private void SitDown()
        {
            standing = false;
            using_ = null;
            avatars[HumanSeat].SetVisible(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            headLook.AimNeutral();
            AddLog("You sit back down.");
        }

        private bool NearChair()
        {
            Vector3 p = walker.FeetPosition;
            Vector3 chair = RoomView.StandUpPoint(HumanSeat);
            return (new Vector2(p.x - chair.x, p.z - chair.z)).magnitude < 1.1f;
        }

        private void UpdateInteractables(float now)
        {
            lookingAtInteractable = null;
            if (Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, InteractRange))
                lookingAtInteractable = hit.collider.GetComponent<Interactable>();

            if (using_.HasValue && now >= usingUntil)
            {
                switch (using_.Value)
                {
                    case InteractKind.Toilet: needs.UseToilet(); AddLog("Ahh. Much better."); break;
                    case InteractKind.Water: needs.Drink(); AddLog("You drink some water."); break;
                    case InteractKind.Food: needs.Eat(); AddLog("You eat a bun."); break;
                }
                using_ = null;
            }
        }

        private void Use(InteractKind kind, float now)
        {
            if (kind == InteractKind.Seat)
            {
                SitDown();
                return;
            }
            double seconds = kind == InteractKind.Toilet ? needs.Rules.ToiletSeconds
                : kind == InteractKind.Water ? needs.Rules.DrinkSeconds
                : needs.Rules.EatSeconds;
            using_ = kind;
            usingStarted = now;
            usingUntil = now + (float)seconds;
        }

        private void Die(Need cause)
        {
            humanAlive = false;
            string message = SurvivalNeeds.DeathMessage(cause);
            if (standing)
                SitDown();
            CancelCheats();
            match.Eliminate(HumanSeat, message);
            FlushMatchEvents();
            AddLog("YOU DIED. " + message);
            ShowPopup("YOU DIED", message + "\nA house bot takes over your seat. You can't win this match.");
            nextBotTime = Time.time + 0.5f;
        }

        // ------------------------------------------------------------------ HUD

        private static string Bar(double value)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt((float)value / 10f), 0, 10);
            string color = value < 20 ? "#ff5050" : value < 45 ? "#ffcc40" : "#70e070";
            return $"<color={color}>{new string('|', filled * 2)}</color><color=#444>{new string('|', (10 - filled) * 2)}</color> {Mathf.RoundToInt((float)value),3}";
        }

        private string NeedsLine()
        {
            if (!humanAlive)
                return "<color=#ff5050>You are dead.</color>";
            return $"Food {Bar(needs.Hunger)}   Water {Bar(needs.Thirst)}   Pee {Bar(needs.Bladder)}";
        }

        /// <summary>Crosshair, "E: use" prompt, action progress, and the way back to your chair.</summary>
        private void DrawStandingHud()
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.Label(new Rect(cx - 10, cy - 12, 20, 24), "+", centerStyle);

            string prompt = null;
            if (using_.HasValue)
            {
                float t = Mathf.Clamp01((Time.time - usingStarted) / Mathf.Max(0.01f, usingUntil - usingStarted));
                string what = using_ == InteractKind.Toilet ? "Using the toilet" : using_ == InteractKind.Water ? "Drinking" : "Eating";
                prompt = $"{what}…  {Mathf.RoundToInt(t * 100)}%";
            }
            else if (lookingAtInteractable != null)
            {
                switch (lookingAtInteractable.Kind)
                {
                    case InteractKind.Toilet: prompt = "E — use the toilet"; break;
                    case InteractKind.Water: prompt = "E — drink water"; break;
                    case InteractKind.Food: prompt = "E — eat a bun"; break;
                    case InteractKind.Seat: prompt = "E — sit back down"; break;
                }
            }
            else if (NearChair())
            {
                prompt = "F — sit back down";
            }

            if (prompt != null)
            {
                float w = TextWidth(titleStyle, prompt) + Pad * 2;
                var r = new Rect(cx - w / 2f, cy + 30, w, LineHeight * 1.4f);
                GUI.Box(r, GUIContent.none, boxStyle);
                GUI.Label(r, prompt, new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
            }

            const string help = "You're away from the table — your turns play themselves.   WASD move · Shift sprint · E use · F sit (at your chair)";
            float hw = TextWidth(logStyle, help) + Pad * 2;
            var hr = new Rect((Screen.width - hw) / 2f, Screen.height - LineHeight - Pad * 2, hw, LineHeight + Pad);
            GUI.Box(hr, GUIContent.none, boxStyle);
            GUI.Label(hr, help, new GUIStyle(logStyle) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
