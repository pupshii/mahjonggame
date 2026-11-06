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
            public float MinUntil;     // can't put it down before this
            public float Until;        // calls and bot texts end by themselves
            public bool Replied;
        }

        private readonly PhoneState[] phones = new PhoneState[HandState.SeatCount];
        private readonly float[] nextDogText = new float[HandState.SeatCount];
        private readonly float[] callCooldownUntil = new float[HandState.SeatCount];
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

        private bool OnPhone(int seat) => phones[seat] != null;

        private void ResetPhones()
        {
            phoneRng = new SeededRandom((ulong)System.DateTime.Now.Ticks);
            float now = Time.time;
            for (int s = 0; s < HandState.SeatCount; s++)
            {
                phones[s] = null;
                avatars[s].SetPhone(false);
                nextDogText[s] = now + Range(40f, 90f);
                callCooldownUntil[s] = now + 15f;
            }
            phonePanelOpen = false;
        }

        private float Range(float min, float max) => min + (float)phoneRng.NextDouble() * (max - min);

        private void UpdatePhones(float now)
        {
            bool playing = match.Phase != MatchPhase.MatchOver;

            for (int s = 0; s < HandState.SeatCount; s++)
            {
                PhoneState p = phones[s];
                if (p != null)
                {
                    bool human = HumanControls(s);
                    bool done = human && p.IsDogText ? p.Replied && now >= p.MinUntil : now >= p.Until;
                    if (done)
                        PutPhoneAway(s);
                    continue;
                }

                bool canText = playing && (s != HumanSeat || humanAlive);
                if (canText && now >= nextDogText[s])
                {
                    nextDogText[s] = now + (s == HumanSeat ? Range(55f, 110f) : Range(45f, 100f));
                    StartDogText(s, now);
                }
            }

            // Calling: P opens the phone, 1-3 call a player directly.
            if (humanAlive && playing)
            {
                if (Input.GetKeyDown(KeyCode.P))
                    phonePanelOpen = !phonePanelOpen;
                for (int s = 1; s < HandState.SeatCount; s++)
                    if (Input.GetKeyDown(KeyCode.Alpha0 + s))
                        TryHumanCall(s);
            }
        }

        private void StartDogText(int seat, float now)
        {
            phones[seat] = new PhoneState
            {
                From = "Dog (home)",
                Message = DogTexts[phoneRng.NextInt(DogTexts.Length)],
                IsDogText = true,
                MinUntil = now + DogTextMinRead,
                Until = now + Range(3f, 5f),
            };
            OnPhoneStarted(seat);
            if (seat != HumanSeat)
                AddLog($"{SeatName(seat)} checks their phone.");
        }

        private void StartCall(int caller, int target, float now)
        {
            if (OnPhone(target))
                return;
            phones[target] = new PhoneState
            {
                From = SeatName(caller),
                Message = CallLines[phoneRng.NextInt(CallLines.Length)],
                IsDogText = false,
                MinUntil = now,
                Until = now + Range(4f, 5.5f),
            };
            callCooldownUntil[caller] = now + (caller == HumanSeat ? HumanCallCooldown : BotCallCooldown);
            OnPhoneStarted(target);
            AddLog(target == HumanSeat ? $"{SeatName(caller)} is calling you!" : $"{SeatName(caller)} calls {SeatName(target)}.");
        }

        private void OnPhoneStarted(int seat)
        {
            avatars[seat].SetPhone(true);
            if (seat != HumanSeat)
            {
                avatars[seat].LookAt(avatars[seat].PhonePoint);
                if (brains[seat] != null)
                    brains[seat].CallOutTarget = -1; // distracted: forget what they were about to say
            }
        }

        private void PutPhoneAway(int seat)
        {
            phones[seat] = null;
            avatars[seat].SetPhone(false);
            brains[seat]?.ReleaseGaze(Time.time);
        }

        private void TryHumanCall(int target)
        {
            float now = Time.time;
            if (!humanAlive || target == HumanSeat || OnPhone(HumanSeat))
                return;
            if (now < callCooldownUntil[HumanSeat])
            {
                ShowPopup("Phone", $"You can call again in {Mathf.CeilToInt(callCooldownUntil[HumanSeat] - now)}s.");
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
            if (!humanAlive || standing || OnPhone(HumanSeat) || now < callCooldownUntil[bot])
                return false;
            // Only worth it if you're the one watching (another bot staring is a different problem).
            for (int w = 0; w < HandState.SeatCount; w++)
                if (w != bot && w != HumanSeat && LookingAt(w, avatars[bot].CheatAnchor, 35f))
                    return false;
            if (brains[bot].Roll() > 0.5)
            {
                callCooldownUntil[bot] = now + 10f; // thought about it, not this time
                return false;
            }
            StartCall(bot, HumanSeat, now);
            return true;
        }

        // ------------------------------------------------------------------ phone UI

        /// <summary>Your phone, when it's out: the dog's text (reply to put it away) or a call.</summary>
        private void DrawPhoneOverlay()
        {
            PhoneState p = phones[HumanSeat];
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

            if (p.IsDogText)
            {
                if (now < p.MinUntil)
                {
                    GUI.Label(new Rect(x, y, iw, LineHeight), "typing…", logStyle);
                }
                else
                {
                    foreach (string reply in DogReplies)
                    {
                        if (GUI.Button(new Rect(x, y, iw, Unit * 2.2f), reply, buttonStyle))
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
            bool ready = now >= callCooldownUntil[HumanSeat];
            for (int s = 1; s < HandState.SeatCount; s++)
            {
                string label = ready ? $"{s}: Call {SeatName(s)}" : $"Cooldown {Mathf.CeilToInt(callCooldownUntil[HumanSeat] - now)}s";
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
            string call = now >= callCooldownUntil[HumanSeat] ? "<color=#8f8>ready</color>" : $"{Mathf.CeilToInt(callCooldownUntil[HumanSeat] - now)}s";
            var busy = Enumerable.Range(0, HandState.SeatCount).Where(s => s != HumanSeat && OnPhone(s)).Select(ShortName).ToList();
            return $"Phone (P / 1-3 to call): {call}" + (busy.Count > 0 ? $"   ·   On their phone: {string.Join(", ", busy)}" : "");
        }
    }
}
