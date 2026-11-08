using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Phones: every so often your dog texts you ("when r u coming home??") and you're forced to
    /// look at your phone and reply. You can also call another player (P, or 1-3) to make them
    /// stare at their phone for a few seconds — a window to cheat — then wait out a cooldown.
    /// Bots get texts too, and will call you when you're watching them too closely.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private const float HumanCallCooldown = 45f;
        private const float BotCallCooldown = 60f;
        private const float DogTextMinRead = 2.5f;

        private sealed class PhoneState
        {
            public string From;
            public string Message;
            public bool IsDogText;
            public bool IsLatte;       // from your own dog (feed them to make it stop)
            public float MinUntil;     // can't put it down before this
            public float Until;        // calls and bot texts end by themselves
            public bool Replied;
        }

        // Your phone is global (you only have one); bots' phones belong to their table (ctx.Phones).
        private PhoneState humanPhone;
        private float humanNextDogText;
        private float humanCallReadyAt;
        private bool phonePanelOpen;
        private SeededRandom phoneRng = new SeededRandom(7);

        private static readonly string[] DogTexts =
        {
            "woof. when r u coming home??",
            "i ate the couch. when home",
            "bark bark. dinner??? DINNER???",
            "theres a sound outside. come home pls",
            "u said 1 more hand 3 hands ago",
            "i love u. also where r u",
        };

        private static readonly string[] DogReplies = { "Soon!", "After this hand", "Go to sleep, buddy" };

        private static readonly string[] CallLines =
        {
            "\"Hey… did you leave the stove on?\"",
            "\"Quick question about your car's extended warranty…\"",
            "\"It's your mum. Are you eating properly?\"",
            "\"Wrong number. Or is it?\"",
        };

        private PhoneState GetPhone(int seat) => seat == HumanSeat ? humanPhone : ctx.Phones[seat];

        private bool OnPhone(int seat) => GetPhone(seat) != null;

        /// <summary>The avatar holding this seat's phone (yours sits at whichever table you're at).</summary>
        private PlayerAvatar PhoneHolder(int seat) => seat == HumanSeat ? Seated.Avatars[HumanSeat] : avatars[seat];

        private void ResetPhones()
        {
            phoneRng = new SeededRandom((ulong)System.DateTime.Now.Ticks);
            float now = Time.time;
            TableContext keep = ctx;
            foreach (TableContext t in tables)
            {
                ctx = t;
                for (int s = 0; s < HandState.SeatCount; s++)
                {
                    t.Phones[s] = null;
                    t.Avatars[s].SetPhone(false);
                    t.NextDogText[s] = now + Range(40f, 90f);
                    t.CallCooldownUntil[s] = now + 15f;
                }
            }
            ctx = keep;
            humanPhone = null;
            humanNextDogText = now + Range(20f, 40f);
            humanCallReadyAt = now + 15f;
            phonePanelOpen = false;
        }

        private float Range(float min, float max) => min + (float)phoneRng.NextDouble() * (max - min);

        /// <summary>Your phone: dog texts, replying (1-3), and calling (P / 1-3).</summary>
        private void UpdateHumanPhone(float now)
        {
            if (humanPhone != null)
            {
                bool done = humanPhone.IsDogText ? humanPhone.Replied && now >= humanPhone.MinUntil : now >= humanPhone.Until;
                if (done || !humanAlive)
                    PutPhoneAway(HumanSeat);
            }
            else if (humanAlive)
            {
                // How often Latte messages you depends on how hungry they are (see GreyboxGame.Home).
                ScheduleDogMessages(now);
                if (now >= humanNextDogText && dog.MessageGap != null && !atHome)
                    StartLatteMessage(now);
            }

            // Reply to the dog with 1-3 (no mouse needed).
            PhoneState mine = humanPhone;
            if (mine != null && mine.IsDogText && !mine.Replied && now >= mine.MinUntil && humanAlive)
            {
                for (int i = 0; i < DogReplies.Length; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        mine.Replied = true;
                        AddLog($"You text your dog: \"{DogReplies[i]}\"");
                    }
                }
                return; // number keys answer the dog instead of calling
            }

            // Calling: P opens the phone, 1-3 call a player at the table you're sitting at.
            if (humanAlive)
            {
                if (Input.GetKeyDown(KeyCode.P))
                    phonePanelOpen = !phonePanelOpen;
                for (int s = 1; s < HandState.SeatCount; s++)
                    if (Input.GetKeyDown(KeyCode.Alpha0 + s))
                        TryHumanCall(s);
            }
        }

        /// <summary>Bots' phones at the current table: their dog texts, and calls ending.</summary>
        private void UpdateBotPhones(float now)
        {
            bool playing = match.Phase != MatchPhase.MatchOver;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                if (s == HumanSeat)
                    continue;
                PhoneState p = ctx.Phones[s];
                if (p != null)
                {
                    if (now >= p.Until)
                        PutPhoneAway(s);
                    continue;
                }
                if (playing && now >= ctx.NextDogText[s])
                {
                    ctx.NextDogText[s] = now + Range(45f, 100f);
                    StartDogText(s, now);
                }
            }
        }

        private void SetPhone(int seat, PhoneState state)
        {
            if (seat == HumanSeat)
                humanPhone = state;
            else
                ctx.Phones[seat] = state;
        }

        private void StartDogText(int seat, float now)
        {
            SetPhone(seat, new PhoneState
            {
                From = "Dog (home)",
                Message = DogTexts[phoneRng.NextInt(DogTexts.Length)],
                IsDogText = true,
                MinUntil = now + DogTextMinRead,
                Until = now + Range(3f, 5f),
            });
            OnPhoneStarted(seat);
            if (seat != HumanSeat)
                AddLog($"{SeatName(seat)} checks their phone.");
        }

        private void StartCall(int caller, int target, float now)
        {
            if (OnPhone(target))
                return;
            string from = SeatName(caller) + (tables.Count > 1 && caller != HumanSeat ? $" (table {ctx.Index + 1})" : "");
            SetPhone(target, new PhoneState
            {
                From = from,
                Message = CallLines[phoneRng.NextInt(CallLines.Length)],
                IsDogText = false,
                MinUntil = now,
                Until = now + Range(4f, 5.5f),
            });
            if (caller == HumanSeat)
                humanCallReadyAt = now + HumanCallCooldown;
            else
                ctx.CallCooldownUntil[caller] = now + BotCallCooldown;
            OnPhoneStarted(target);
            AddLog(target == HumanSeat ? $"{from} is calling you!" : $"{SeatName(caller)} calls {SeatName(target)}.");
        }

        private void OnPhoneStarted(int seat)
        {
            PhoneHolder(seat).SetPhone(true);
            if (seat != HumanSeat)
            {
                avatars[seat].LookAt(avatars[seat].PhonePoint);
                if (brains[seat] != null)
                    brains[seat].CallOutTarget = -1; // distracted: forget what they were about to say
            }
        }

        private void PutPhoneAway(int seat)
        {
            PhoneHolder(seat).SetPhone(false);
            SetPhone(seat, null);
            if (seat != HumanSeat)
                brains[seat]?.ReleaseGaze(Time.time);
        }

        private void TryHumanCall(int target)
        {
            float now = Time.time;
            if (!humanAlive || target == HumanSeat || OnPhone(HumanSeat))
                return;
            if (now < humanCallReadyAt)
            {
                ShowPopup("Phone", $"You can call again in {Mathf.CeilToInt(humanCallReadyAt - now)}s.");
                return;
            }
            if (OnPhone(target))
            {
                ShowPopup("Phone", $"{SeatName(target)} is already on their phone.");
                return;
            }
            StartCall(HumanSeat, target, now);
            phonePanelOpen = false;
        }

        /// <summary>A bot that wants to cheat but sees you watching may call you to look away.</summary>
        private bool TryBotDistractHuman(int bot)
        {
            float now = Time.time;
            if (!humanAlive || !AtThisTable || OnPhone(HumanSeat) || now < ctx.CallCooldownUntil[bot])
                return false;
            // Only worth it if you're the one watching (another bot staring is a different problem).
            for (int w = 0; w < HandState.SeatCount; w++)
                if (w != bot && w != HumanSeat && LookingAt(w, avatars[bot].CheatAnchor, 35f))
                    return false;
            if (brains[bot].Roll() > 0.5)
            {
                ctx.CallCooldownUntil[bot] = now + 10f; // thought about it, not this time
                return false;
            }
            StartCall(bot, HumanSeat, now);
            return true;
        }

        // ------------------------------------------------------------------ phone UI

        /// <summary>Your phone, when it's out: the dog's text (reply to put it away) or a call.</summary>
        private void DrawPhoneOverlay()
        {
            PhoneState p = humanPhone;
            if (p == null)
                return;
            float now = Time.time;
            float w = Unit * 18, h = Unit * 22;
            Rect r = Panel(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h));
            Panel(r);
            float x = r.x + Pad * 1.5f, iw = r.width - Pad * 3, y = r.y + Pad * 1.5f;

            GUI.Label(new Rect(x, y, iw, LineHeight), p.IsDogText ? "New message" : "Incoming call", logStyle);
            y += LineHeight;
            GUI.Label(new Rect(x, y, iw, LineHeight * 1.3f), p.From, titleStyle);
            y += LineHeight * 1.6f;
            var wrap = new GUIStyle(textStyle) { wordWrap = true };
            GUI.Label(new Rect(x, y, iw, LineHeight * 3), p.Message, wrap);
            y += LineHeight * 3.2f;
            if (p.IsLatte)
            {
                GUI.Label(new Rect(x, y - LineHeight * 0.9f, iw, LineHeight), "<color=#ffcc40>Feed Latte to make this stop: front door → home.</color>", logStyle);
                y += LineHeight * 0.3f;
            }

            if (p.IsDogText)
            {
                if (now < p.MinUntil)
                {
                    GUI.Label(new Rect(x, y, iw, LineHeight), "typing…", logStyle);
                }
                else
                {
                    for (int i = 0; i < DogReplies.Length; i++)
                    {
                        string reply = DogReplies[i];
                        if (GUI.Button(new Rect(x, y, iw, Unit * 2.2f), $"{i + 1}:  {reply}", buttonStyle))
                        {
                            p.Replied = true;
                            AddLog($"You text your dog: \"{reply}\"");
                            GUIUtility.ExitGUI();
                        }
                        y += Unit * 2.2f + 6;
                    }
                }
            }
            else
            {
                GUI.Label(new Rect(x, y, iw, LineHeight), $"On the call…  {Mathf.CeilToInt(p.Until - now)}s", textStyle);
            }
        }

        /// <summary>P: pick someone to call (they'll stare at their phone for a few seconds).</summary>
        private void DrawCallPanel()
        {
            if (!phonePanelOpen || OnPhone(HumanSeat) || !humanAlive)
                return;
            float now = Time.time;
            float w = Unit * 16, h = Unit * 13;
            Rect r = Panel(new Rect(Screen.width - w - Pad, Screen.height * 0.3f, w, h));
            float x = r.x + Pad, iw = r.width - Pad * 2, y = r.y + Pad;
            GUI.Label(new Rect(x, y, iw, LineHeight), "Call someone (distracts them ~5s)", titleStyle);
            y += LineHeight * 1.5f;
            bool ready = now >= humanCallReadyAt;
            for (int s = 1; s < HandState.SeatCount; s++)
            {
                string label = ready ? $"{s}: Call {SeatName(s)}" : $"Cooldown {Mathf.CeilToInt(humanCallReadyAt - now)}s";
                bool wasEnabled = GUI.enabled;
                GUI.enabled = ready && !OnPhone(s);
                if (GUI.Button(new Rect(x, y, iw, Unit * 2.2f), label, buttonStyle))
                {
                    GUI.enabled = wasEnabled;
                    TryHumanCall(s);
                    GUIUtility.ExitGUI();
                }
                GUI.enabled = wasEnabled;
                y += Unit * 2.2f + 6;
            }
        }

        private string PhoneStatusLine()
        {
            float now = Time.time;
            string call = now >= humanCallReadyAt ? "<color=#8f8>ready</color>" : $"{Mathf.CeilToInt(humanCallReadyAt - now)}s";
            var busy = Enumerable.Range(0, HandState.SeatCount).Where(s => s != HumanSeat && OnPhone(s)).Select(ShortName).ToList();
            return $"Phone (P / 1-3 to call): {call}" + (busy.Count > 0 ? $"   ·   On their phone: {string.Join(", ", busy)}" : "");
        }
    }
}
