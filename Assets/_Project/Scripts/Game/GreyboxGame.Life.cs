using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Survival and walking: hunger / thirst / bladder bars, getting up from the table (F),
    /// walking (WASD, Shift to sprint), using the toilet, water and food (E), and dying when a
    /// bar runs out. Every table keeps its own countdown while you're away from it.
    /// </summary>
    public sealed partial class GreyboxGame
    {
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
            OnPhone(HumanSeat) || phonePanelOpen || InMiniGame || (popupText != null && Time.time <= popupUntil);

        /// <summary>Does the human (rather than a bot) play this seat right now?</summary>
        private bool HumanControls(int seat) => seat == HumanSeat && humanAlive;

        private void SetUpLife()
        {
            room = new RoomView(transform, HumanSeat, tables.Select(t => t.Origin).ToList());
            walker = new Walker(room);
            SetUpHome();
        }

        private void ResetLife()
        {
            needs = new SurvivalNeeds();
            humanAlive = true;
            if (standing)
                SitDown(seatedTable);
            using_ = null;
            ResetHome();
            ResetPhones();
            ResetCooking();
        }

        /// <summary>Runs before the table logic each frame: needs, walking, interactions, phones.</summary>
        private void UpdateLife(float now, float dt)
        {
            UpdateHome(now, dt);
            UpdateHumanPhone(now);
            UpdateDrums(dt);
            UpdateMiniGame(dt);
            if (!standing && humanAlive && Input.GetKeyDown(KeyCode.T))
                TryHumanTrickshot();

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
                if (Input.GetKeyDown(KeyCode.E) && !using_.HasValue && !OnPhone(HumanSeat) && !InMiniGame)
                {
                    if (lookingAtInteractable != null)
                        Use(lookingAtInteractable, now);
                    else if (lookingAtCustomer != null && carryingSoup)
                        Serve(lookingAtCustomer);
                }
                int chair = NearestChair();
                if (Input.GetKeyDown(KeyCode.F) && chair >= 0 && !using_.HasValue && !InMiniGame)
                    SitDown(chair);
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
            walker.Place(RoomView.StandUpPoint(Seated.Origin), 180f); // facing away from the table
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            hoveredTile = null;
            AddLog("You get up from the table.");
        }

        /// <summary>Sit in your chair at <paramref name="tableIndex"/> and play there.</summary>
        private void SitDown(int tableIndex)
        {
            standing = false;
            using_ = null;
            atHome = false;
            seatedTable = tableIndex;
            ctx = Seated;
            hoveredTile = null;
            headLook.SetSeat(avatars[HumanSeat].EyePosition, ctx.Origin);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AddLog(tables.Count > 1 ? $"You sit down at table {tableIndex + 1}." : "You sit back down.");
        }

        /// <summary>The table whose chair you're standing behind, or -1.</summary>
        private int NearestChair()
        {
            Vector3 p = walker.FeetPosition;
            for (int i = 0; i < tables.Count; i++)
            {
                Vector3 chair = RoomView.StandUpPoint(tables[i].Origin);
                if (new Vector2(p.x - chair.x, p.z - chair.z).magnitude < 1.1f)
                    return i;
            }
            return -1;
        }

        private void UpdateInteractables(float now)
        {
            lookingAtInteractable = null;
            lookingAtCustomer = null;
            if (Physics.Raycast(new Ray(cam.transform.position, cam.transform.forward), out RaycastHit hit, ServeRange))
            {
                if (hit.distance <= InteractRange)
                    lookingAtInteractable = hit.collider.GetComponent<Interactable>();
                lookingAtCustomer = hit.collider.GetComponent<AvatarView>();
            }

            if (using_.HasValue && now >= usingUntil)
            {
                switch (using_.Value)
                {
                    case InteractKind.Toilet: needs.UseToilet(); AddLog("Ahh. Much better."); break;
                    case InteractKind.Water: needs.Drink(); AddLog("You drink some water."); break;
                    case InteractKind.Food: needs.Eat(); AddLog("You eat a bun."); break;
                    default:
                        if (!FinishHomeAction(using_.Value))
                            FinishSlicing(using_.Value);
                        break;
                }
                using_ = null;
            }
        }

        private void Use(Interactable thing, float now)
        {
            InteractKind kind = thing.Kind;
            if (kind == InteractKind.Seat)
            {
                SitDown(thing.TableIndex);
                return;
            }
            if (TryHomeUse(thing, now) || TryCookingUse(thing, now))
                return;
            double seconds = kind == InteractKind.Toilet ? needs.Rules.ToiletSeconds
                : kind == InteractKind.Water ? needs.Rules.DrinkSeconds
                : needs.Rules.EatSeconds;
            StartTimedUse(kind, now, seconds);
        }

        /// <summary>Busy for a few seconds (toilet, drinking, eating, slicing) — you can't move meanwhile.</summary>
        private void StartTimedUse(InteractKind kind, float now, double seconds)
        {
            using_ = kind;
            usingStarted = now;
            usingUntil = now + (float)seconds;
        }

        private const float DeathScreenSeconds = 5f;

        private bool stageStopped;      // you died: every table is frozen until the restart
        private float restartAt;
        private string deathMessage;

        /// <summary>You died: stop every table, show the death screen, then restart the whole stage.</summary>
        private void Die(Need cause)
        {
            humanAlive = false;
            CancelMiniGame();
            deathMessage = SurvivalNeeds.DeathMessage(cause);
            if (standing)
                SitDown(seatedTable);
            foreach (TableContext t in tables)
            {
                ctx = t;
                CancelCheats();
            }
            ctx = Seated;
            if (OnPhone(HumanSeat))
                PutPhoneAway(HumanSeat);
            ClosePhone();
            popupText = null;
            stageStopped = true;
            restartAt = Time.time + DeathScreenSeconds;
            AddLog("YOU DIED. " + deathMessage + " The stage restarts…");
        }

        private void RestartStage()
        {
            stageStopped = false;
            SitDown(0);        // back to the middle table
            NewMatchAll();     // resets you, your bars, and every table
            AddLog("— The stage restarts. Good luck. —");
        }

        /// <summary>Full-screen "you died" card with a countdown to the restart.</summary>
        private void DrawDeathScreen()
        {
            var full = new Rect(0, 0, Screen.width, Screen.height);
            uiRects.Add(full);
            GUI.Box(full, GUIContent.none, boxStyle);
            GUI.Box(full, GUIContent.none, boxStyle); // twice for a darker veil

            float cy = Screen.height * 0.38f;
            var big = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Unit * 3.5f) };
            GUI.Label(new Rect(0, cy, Screen.width, Unit * 5), "<color=#ff4040>YOU DIED</color>", big);
            GUI.Label(new Rect(0, cy + Unit * 5, Screen.width, LineHeight * 1.5f), deathMessage, new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
            int secs = Mathf.Max(0, Mathf.CeilToInt(restartAt - Time.time));
            GUI.Label(new Rect(0, cy + Unit * 7.5f, Screen.width, LineHeight), $"All tables stopped. The stage restarts in {secs}s.", centerStyle);

            float bw = Unit * 10;
            if (GUI.Button(new Rect((Screen.width - bw) / 2f, cy + Unit * 10, bw, Unit * 2.4f), "<b>Restart now</b>", buttonStyle))
            {
                RestartStage();
                GUIUtility.ExitGUI();
            }
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
            if (InMiniGame)
            {
                // the mini-game panel says what to do
            }
            else if (using_.HasValue)
            {
                float t = Mathf.Clamp01((Time.time - usingStarted) / Mathf.Max(0.01f, usingUntil - usingStarted));
                string what = HomeUsingText(using_.Value) ?? UsingText(using_.Value)
                    ?? (using_ == InteractKind.Toilet ? "Using the toilet" : using_ == InteractKind.Water ? "Drinking" : "Eating");
                prompt = $"{what}…  {Mathf.RoundToInt(t * 100)}%";
            }
            else if (lookingAtInteractable != null)
            {
                switch (lookingAtInteractable.Kind)
                {
                    case InteractKind.Toilet: prompt = "E — use the toilet"; break;
                    case InteractKind.Water: prompt = "E — drink water"; break;
                    case InteractKind.Food: prompt = "E — eat a bun"; break;
                    case InteractKind.Seat: prompt = $"E — sit down at table {lookingAtInteractable.TableIndex + 1}"; break;
                    default: prompt = HomePrompt(lookingAtInteractable.Kind) ?? CookingPrompt(lookingAtInteractable.Kind); break;
                }
            }
            else if (lookingAtCustomer != null && CustomerOrder(lookingAtCustomer) != null)
            {
                prompt = carryingSoup
                    ? $"E — serve chicken soup to {BotName(lookingAtCustomer.Seat, lookingAtCustomer.TableIndex)}"
                    : "They're waiting for chicken soup — go cook it!";
            }
            else if (NearestChair() >= 0)
            {
                prompt = tables.Count > 1 ? $"F — sit down at table {NearestChair() + 1}" : "F — sit back down";
            }

            if (prompt != null)
            {
                float w = TextWidth(titleStyle, prompt) + Pad * 2;
                var r = new Rect(cx - w / 2f, cy + 30, w, LineHeight * 1.4f);
                GUI.Box(r, GUIContent.none, boxStyle);
                GUI.Label(r, prompt, new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
            }

            string help = atHome
                ? "You're home. Your turn clocks keep running at the club.   WASD move · Shift sprint · E use · door: back to the club"
                : "One table's turn clock runs at a time — the one that's waited longest.   WASD move · Shift sprint · E use · F sit (behind your chair)";
            float hw = TextWidth(logStyle, help) + Pad * 2;
            var hr = new Rect((Screen.width - hw) / 2f, Screen.height - LineHeight - Pad * 2, hw, LineHeight + Pad);
            GUI.Box(hr, GUIContent.none, boxStyle);
            GUI.Label(hr, help, new GUIStyle(logStyle) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
