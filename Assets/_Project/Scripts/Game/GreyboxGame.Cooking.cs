using System.Collections.Generic;
using System.Linq;
using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// You're the club manager: now and then a customer (bot) orders chicken soup. Go to the
    /// kitchen, turn on the stove, fill the pot and boil the water, slice the chicken, onion,
    /// garlic and herbs, add them, let it simmer, ladle a bowl and carry it to the customer.
    /// About 1.5 minutes if you slice while the water heats — while every table's countdown
    /// keeps running.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private Kitchen kitchen;
        private OrderBook orders;
        private bool carryingSoup;
        private double carriedQuality;   // how good the bowl you're carrying is (0-100)
        private AvatarView lookingAtCustomer;
        private PotStage lastPotStage;
        private string toastText;
        private float toastUntil;

        private const float ServeRange = 3.5f;

        private static Ingredient? IngredientOf(InteractKind kind)
        {
            switch (kind)
            {
                case InteractKind.Chicken: return Ingredient.Chicken;
                case InteractKind.Onion: return Ingredient.Onion;
                case InteractKind.Garlic: return Ingredient.Garlic;
                case InteractKind.Herbs: return Ingredient.Herbs;
                default: return null;
            }
        }

        private static string Lower(Ingredient i) => i.ToString().ToLowerInvariant();

        private void ResetCooking()
        {
            kitchen = new Kitchen();
            orders = new OrderBook(OrderRules.Default(), (ulong)System.DateTime.Now.Ticks, Time.time);
            carryingSoup = false;
            CancelMiniGame();
            lastPotStage = PotStage.Empty;
            toastText = null;
            foreach (TableContext t in tables)
                for (int s = 0; s < HandState.SeatCount; s++)
                    t.Avatars[s].SetBubble(null, cam.transform.position, Color.white);
            UpdateKitchenVisuals();
        }

        private void UpdateCooking(float now, float dt)
        {
            kitchen.Tick(dt);
            if (kitchen.Stage != lastPotStage)
            {
                if (kitchen.Stage == PotStage.Boiling) ShowToast("The water is boiling.");
                else if (kitchen.Stage == PotStage.Done) ShowToast("The chicken soup is ready! Ladle it before it burns.");
                else if (kitchen.Stage == PotStage.Burnt) ShowToast("The soup burnt! Throw it out and start again.");
                lastPotStage = kitchen.Stage;
            }

            FoodOrder placed = orders.TryCreate(now, tables.Count, (t, s) => tables[t].Match.Phase != MatchPhase.MatchOver);
            if (placed != null)
            {
                string who = $"{BotName(placed.Seat, placed.TableIndex)} at table {placed.TableIndex + 1}";
                AddLog($"New order: {who} wants chicken soup.");
                ShowToast($"New order! {who} wants chicken soup.");
            }

            foreach (FoodOrder late in orders.Expire(now))
            {
                TableContext keep = ctx;
                ctx = tables[late.TableIndex];
                match.Transfer(HumanSeat, late.Seat, orders.Rules.LateRefund, PaymentReason.FoodRefund);
                FlushMatchEvents();
                ctx = keep;
                string who = BotName(late.Seat, late.TableIndex);
                AddLog($"{who} (table {late.TableIndex + 1}) gave up on their soup — you refund {orders.Rules.LateRefund} faan.");
                ShowToast($"Too slow! {who} at table {late.TableIndex + 1} gave up on their soup.");
            }

            UpdateKitchenVisuals();
            UpdateOrderBubbles(now);
        }

        private string BotName(int seat, int tableIndex) => $"Bot {seat} ({tables[tableIndex].Match.Hand?.Seats[seat].SeatWind})";

        // ------------------------------------------------------------------ using kitchen things

        private static Color IngredientColour(Ingredient i) =>
            i == Ingredient.Chicken ? new Color(0.98f, 0.8f, 0.72f)
            : i == Ingredient.Onion ? new Color(0.75f, 0.55f, 0.35f)
            : i == Ingredient.Garlic ? new Color(0.96f, 0.95f, 0.88f)
            : new Color(0.3f, 0.65f, 0.25f);

        private static int Pieces(Ingredient i) => i == Ingredient.Chicken ? 5 : i == Ingredient.Garlic ? 3 : 4;

        /// <summary>
        /// Handles E on a kitchen object — every step is a short mouse mini-game, scored, and the
        /// scores add up to how good the soup is (and so how big the tip). Returns false if it
        /// isn't a kitchen object.
        /// </summary>
        private bool TryCookingUse(Interactable thing, float now)
        {
            string error;
            switch (thing.Kind)
            {
                case InteractKind.Stove:
                    if (kitchen.StoveOn)
                    {
                        kitchen.ToggleStove();
                        ShowToast("You turn the stove off.");
                    }
                    else
                    {
                        StartMiniGame(new IgniteGame(Range(0.55f, 0.85f)), score =>
                        {
                            kitchen.ToggleStove();
                            kitchen.RecordScore(CookStep.Ignite, score);
                            ShowToast(kitchen.Stage == PotStage.Empty ? "The stove is on. Fill the pot!" : "The stove is on.");
                        });
                    }
                    return true;

                case InteractKind.Pot:
                    switch (kitchen.Stage)
                    {
                        case PotStage.Empty:
                            StartMiniGame(new PourGame("FILL THE POT", "water", Range(0.68f, 0.82f),
                                new Color(0.45f, 0.7f, 1f), new Color(0.55f, 0.57f, 0.6f), 0.32f), score =>
                            {
                                kitchen.TryFillPot(out _);
                                kitchen.RecordScore(CookStep.Fill, score);
                                ShowToast(kitchen.StoveOn ? "Now wait for it to boil — slice the ingredients meanwhile." : "Turn the stove on!");
                            });
                            break;
                        case PotStage.Boiling:
                            if (!kitchen.AllSliced)
                            {
                                kitchen.TryAddIngredients(out error);
                                ShowToast(error);
                                break;
                            }
                            StartMiniGame(new AddToPotGame(Kitchen.Recipe.Select(i => (i, IngredientColour(i))).ToList()), score =>
                            {
                                kitchen.TryAddIngredients(out _);
                                kitchen.RecordScore(CookStep.AddToPot, score);
                                ShowToast("Everything's in. Let it simmer — and give it a stir (E on the pot)!");
                            });
                            break;
                        case PotStage.Simmering:
                            if (kitchen.HasScore(CookStep.Stir))
                                ShowToast("You've stirred it. Now let it simmer.");
                            else
                                StartMiniGame(new StirGame(), score =>
                                {
                                    kitchen.RecordScore(CookStep.Stir, score);
                                    ShowToast("Smells amazing. Let it simmer.");
                                });
                            break;
                        case PotStage.Done:
                            if (carryingSoup)
                            {
                                ShowToast("Your hands are full — serve the bowl you're carrying first.");
                                break;
                            }
                            StartMiniGame(new PourGame("LADLE A BOWL", "the soup", Range(0.6f, 0.8f),
                                new Color(0.95f, 0.78f, 0.35f), new Color(0.95f, 0.95f, 0.95f), 0.42f), score =>
                            {
                                kitchen.RecordScore(CookStep.Ladle, score);
                                if (!kitchen.TryLadle(out string err))
                                {
                                    ShowToast(err);   // burnt while you were ladling
                                    return;
                                }
                                carryingSoup = true;
                                carriedQuality = kitchen.LastBowlQuality;
                                DishGrade g = CookingScore.Grade(carriedQuality);
                                ShowToast($"Chicken soup: {GradeWord(g)} ({Mathf.RoundToInt((float)carriedQuality)}/100). Take it to the customer!");
                            });
                            break;
                        case PotStage.Burnt:
                            kitchen.TryDump(out _);
                            ShowToast("You throw out the burnt soup.");
                            break;
                        default:
                            ShowToast(kitchen.NextStep() + ".");
                            break;
                    }
                    return true;

                default:
                    Ingredient? ingredient = IngredientOf(thing.Kind);
                    if (!ingredient.HasValue)
                        return false;
                    Ingredient ing = ingredient.Value;
                    if (kitchen.IsSliced(ing))
                        ShowToast($"The {Lower(ing)} is already sliced.");
                    else if (kitchen.Stage == PotStage.Simmering || kitchen.Stage == PotStage.Done)
                        ShowToast("This pot already has its ingredients.");
                    else
                        StartMiniGame(new SliceGame(ing, IngredientColour(ing), Pieces(ing), () => Range(0f, 1f)), score =>
                        {
                            if (kitchen.TrySlice(ing, out string err))
                            {
                                kitchen.RecordScore(CookStep.Slice, score);
                                if (kitchen.AllSliced)
                                    ShowToast("Everything's sliced. Add it once the water boils.");
                            }
                            else
                                ShowToast(err);
                        });
                    return true;
            }
        }

        private static string GradeWord(DishGrade g) =>
            g == DishGrade.Perfect ? "PERFECT" : g == DishGrade.Great ? "great" : g == DishGrade.Ok ? "OK" : "pretty bad";

        /// <summary>Called when a timed slicing action finishes.</summary>
        private void FinishSlicing(InteractKind kind)
        {
            Ingredient? ingredient = IngredientOf(kind);
            if (!ingredient.HasValue)
                return;
            if (kitchen.TrySlice(ingredient.Value, out string error))
                ShowToast($"Sliced the {Lower(ingredient.Value)}." + (kitchen.AllSliced ? " Everything's ready to go in the pot." : ""));
            else
                ShowToast(error);
        }

        private string CookingPrompt(InteractKind kind)
        {
            switch (kind)
            {
                case InteractKind.Stove: return kitchen.StoveOn ? "E — turn the stove off" : "E — light the stove";
                case InteractKind.Pot:
                    switch (kitchen.Stage)
                    {
                        case PotStage.Empty: return "E — fill the pot with water";
                        case PotStage.Heating: return kitchen.StoveOn ? $"Heating… {Mathf.RoundToInt((float)kitchen.Progress * 100)}%" : "The stove is off";
                        case PotStage.Boiling: return kitchen.AllSliced ? "E — add the ingredients" : "Boiling — slice the ingredients";
                        case PotStage.Simmering:
                            return kitchen.HasScore(CookStep.Stir)
                                ? $"Simmering… {Mathf.RoundToInt((float)kitchen.Progress * 100)}%"
                                : $"E — stir! (simmering {Mathf.RoundToInt((float)kitchen.Progress * 100)}%)";
                        case PotStage.Done: return carryingSoup ? "Serve the bowl you're carrying first" : "E — ladle a bowl of soup";
                        default: return "E — throw out the burnt soup";
                    }
                default:
                    Ingredient? i = IngredientOf(kind);
                    if (!i.HasValue) return null;
                    return kitchen.IsSliced(i.Value) ? $"The {Lower(i.Value)} is sliced" : $"E — slice the {Lower(i.Value)}";
            }
        }

        private static string UsingText(InteractKind kind)
        {
            Ingredient? i = IngredientOf(kind);
            return i.HasValue ? $"Slicing the {Lower(i.Value)}" : null;
        }

        // ------------------------------------------------------------------ serving

        /// <summary>The order (if any) of the customer you're looking at, when you carry a bowl.</summary>
        private FoodOrder CustomerOrder(AvatarView v) =>
            v == null || v.Seat == HumanSeat ? null : orders.Find(v.TableIndex, v.Seat);

        private void Serve(AvatarView customer)
        {
            FoodOrder order = CustomerOrder(customer);
            if (!carryingSoup || order == null)
            {
                ShowToast(order == null ? "They didn't order anything." : "You're not carrying any soup.");
                return;
            }
            orders.Deliver(order);
            carryingSoup = false;
            DishGrade grade = CookingScore.Grade(carriedQuality);
            int tip = Mathf.Max(1, orders.Rules.Tip + CookingScore.TipBonus(grade));
            TableContext keep = ctx;
            ctx = tables[order.TableIndex];
            match.Transfer(order.Seat, HumanSeat, tip, PaymentReason.FoodTip);
            FlushMatchEvents();
            ctx = keep;
            string who = BotName(order.Seat, order.TableIndex);
            string verdict = grade == DishGrade.Perfect ? "\"This is the best soup I've ever had!\""
                : grade == DishGrade.Great ? "\"Delicious!\""
                : grade == DishGrade.Ok ? "\"…It's soup.\""
                : "\"Is this… raw?\"";
            AddLog($"You serve {who} at table {order.TableIndex + 1} ({GradeWord(grade)} soup) — they tip you {tip} faan.");
            ShowToast($"Served! {verdict} {who} tips you {tip} faan.");
            ShowGrade(CookingScore.Shout(grade) + $"\n<size={Mathf.RoundToInt(Unit * 1.6f)}>+{tip} faan</size>", GradeColor(grade));
        }

        // ------------------------------------------------------------------ visuals & UI

        private void UpdateKitchenVisuals()
        {
            if (room == null || kitchen == null)
                return;
            room.Burner.sharedMaterial.color = kitchen.StoveOn ? new Color(1f, 0.3f, 0.1f) : new Color(0.12f, 0.12f, 0.12f);

            room.PotContents.SetActive(kitchen.Stage != PotStage.Empty);
            Color c;
            switch (kitchen.Stage)
            {
                case PotStage.Heating: c = new Color(0.55f, 0.75f, 1f); break;
                case PotStage.Boiling: c = new Color(0.85f, 0.93f, 1f); break;
                case PotStage.Simmering: c = new Color(0.85f, 0.6f, 0.3f); break;
                case PotStage.Done: c = new Color(0.95f, 0.78f, 0.35f); break;
                default: c = new Color(0.15f, 0.1f, 0.08f); break;
            }
            room.PotContents.GetComponent<Renderer>().sharedMaterial.color = c;

            foreach (KeyValuePair<Ingredient, GameObject> kv in room.Ingredients)
                kv.Value.SetActive(!kitchen.IsSliced(kv.Key) && kitchen.Stage != PotStage.Simmering && kitchen.Stage != PotStage.Done);
        }

        /// <summary>"CHICKEN SOUP 2:41" above every customer who's waiting.</summary>
        private void UpdateOrderBubbles(float now)
        {
            Vector3 camPos = cam.transform.position;
            foreach (TableContext t in tables)
            {
                for (int s = 1; s < HandState.SeatCount; s++)
                {
                    FoodOrder o = orders.Find(t.Index, s);
                    if (o == null)
                    {
                        t.Avatars[s].SetBubble(null, camPos, Color.white);
                        continue;
                    }
                    int secs = Mathf.CeilToInt((float)o.SecondsLeft(now));
                    Color color = secs <= 30 ? new Color(1f, 0.3f, 0.25f) : secs <= 90 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 0.85f);
                    t.Avatars[s].SetBubble($"CHICKEN SOUP\n{secs / 60}:{secs % 60:00}", camPos, color);
                }
            }
        }

        /// <summary>Open orders and the recipe card, on the right under the tables list.</summary>
        private void DrawKitchenCard(float top)
        {
            bool busy = kitchen.StoveOn || kitchen.Stage != PotStage.Empty || carryingSoup;
            if (orders.Open.Count == 0 && !busy)
                return;
            float now = Time.time;
            var lines = new List<string>();

            if (orders.Open.Count > 0)
            {
                lines.Add("<b>Orders</b>");
                foreach (FoodOrder o in orders.Open.OrderBy(o => o.Deadline))
                {
                    int secs = Mathf.CeilToInt((float)o.SecondsLeft(now));
                    string color = secs <= 30 ? "#ff5050" : secs <= 90 ? "#ffcc40" : "#ffffff";
                    lines.Add($"  Table {o.TableIndex + 1} · {BotName(o.Seat, o.TableIndex)} · chicken soup · <color={color}>{secs / 60}:{secs % 60:00}</color>");
                }
            }

            lines.Add("<b>Chicken soup</b>   " + (kitchen.StoveOn ? "<color=#ff8040>stove ON</color>" : "stove off"));
            string Check(bool done) => done ? "<color=#70e070>[x]</color>" : "<color=#888>[ ]</color>";
            bool boiled = kitchen.Stage >= PotStage.Boiling;
            bool added = kitchen.Stage >= PotStage.Simmering;
            bool cooked = kitchen.Stage == PotStage.Done;
            string heat = kitchen.Stage == PotStage.Heating ? $" ({Mathf.RoundToInt((float)kitchen.Progress * 100)}%)" : "";
            string simmer = kitchen.Stage == PotStage.Simmering ? $" ({Mathf.RoundToInt((float)kitchen.Progress * 100)}%)" : "";
            lines.Add($"  {Check(kitchen.StoveOn || added)} Turn on the stove");
            lines.Add($"  {Check(boiled)} Fill the pot & boil the water (30s){heat}");
            lines.Add($"  {Check(kitchen.AllSliced || added)} Slice: " + string.Join("  ", Kitchen.Recipe.Select(i =>
                (kitchen.IsSliced(i) || added ? "<color=#70e070>" : "<color=#aaa>") + Lower(i) + "</color>")));
            lines.Add($"  {Check(added)} Add everything to the boiling pot");
            lines.Add($"  {Check(kitchen.HasScore(CookStep.Stir) || cooked)} Stir it while it simmers (60s){simmer}");
            if (cooked)
                lines.Add($"  <color=#ffcc40>Ready! Ladle it — burns in {Mathf.CeilToInt((float)kitchen.SecondsUntilBurnt)}s</color>");
            if (kitchen.Stage == PotStage.Burnt)
                lines.Add("  <color=#ff5050>Burnt! Throw it out (E on the pot).</color>");
            if (kitchen.Quality > 0 && kitchen.Stage != PotStage.Empty)
            {
                DishGrade g = CookingScore.Grade(kitchen.Quality);
                lines.Add($"  Soup so far: <color=#{ColorUtility.ToHtmlStringRGB(GradeColor(g))}>{GradeWord(g)} ({Mathf.RoundToInt((float)kitchen.Quality)})</color>");
            }
            lines.Add(carryingSoup
                ? $"<color=#70e070>Carrying a bowl ({GradeWord(CookingScore.Grade(carriedQuality))}) — E on the customer to serve.</color>"
                : $"<color=#aaa>Next: {kitchen.NextStep()}</color>");

            float w = lines.Max(l => TextWidth(textStyle, l)) + Pad * 2;
            float h = Pad * 2 + LineHeight * lines.Count;
            Rect r = Panel(new Rect(Screen.width - w - Pad, top, w, h));
            float y = r.y + Pad;
            foreach (string line in lines)
            {
                GUI.Label(new Rect(r.x + Pad, y, r.width - Pad * 2, LineHeight), line, textStyle);
                y += LineHeight;
            }
        }

        /// <summary>A short notice at the top of the screen that doesn't need clicking.</summary>
        private void ShowToast(string text)
        {
            toastText = text;
            toastUntil = Time.time + 3.5f;
        }

        private void DrawToast()
        {
            if (toastText == null || Time.time > toastUntil)
                return;
            float w = TextWidth(titleStyle, toastText) + Pad * 3;
            var r = new Rect((Screen.width - w) / 2f, Screen.height * 0.16f, w, LineHeight * 1.6f);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Label(r, toastText, new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter });
        }
    }
}
