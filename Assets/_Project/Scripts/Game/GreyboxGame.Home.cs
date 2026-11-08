using MahjongNight.Core;
using UnityEngine;

namespace MahjongNight.Game
{
    /// <summary>
    /// Latte, your dog, waits at home and gets hungrier as the night goes on. A content dog
    /// leaves you alone; a peckish one texts now and then; a hungry one texts a lot; a starving
    /// one phones you, howling. To make it stop: go out the club's front door (a few seconds'
    /// walk), scoop some dog food from the bag, fill the bowl, and head back. Bring a bowl of
    /// your chicken soup instead and they stay full for longer. Petting helps a little.
    /// Every table's clocks keep running while you're out.
    /// </summary>
    public sealed partial class GreyboxGame
    {
        private Dog dog;
        private HomeView home;
        private bool atHome;
        private bool carryingKibble;
        private DogMood lastDogMood;

        private const float TripSeconds = 6f;     // the walk between the club and home
        private const float ScoopSeconds = 2f;
        private const float PetSeconds = 1.5f;

        private void SetUpHome()
        {
            home = new HomeView(transform, room);
        }

        private void ResetHome()
        {
            dog = new Dog();
            atHome = false;
            carryingKibble = false;
            lastDogMood = dog.Mood;
        }

        private void UpdateHome(float now, float dt)
        {
            if (humanAlive)
                dog.Tick(dt);
            home.Dog.Tick(dt, dog, cam.transform.position, atHome);

            DogMood mood = dog.Mood;
            if (mood != lastDogMood && mood > lastDogMood)
            {
                if (mood == DogMood.Hungry) ShowToast("Latte is getting hungry. Expect a lot of texts.");
                else if (mood == DogMood.Starving) ShowToast("Latte is STARVING. Go home (front door) and feed them!");
            }
            lastDogMood = mood;
        }

        /// <summary>When the next message from Latte should come, given how hungry they are.</summary>
        private void ScheduleDogMessages(float now)
        {
            (double Min, double Max)? gap = dog.MessageGap;
            if (gap == null || atHome)
            {
                humanNextDogText = Mathf.Max(humanNextDogText, now + 3f); // content (or right there): nothing for now
                return;
            }
            // Got hungrier since the last message was scheduled? Bring the next one forward.
            humanNextDogText = Mathf.Min(humanNextDogText, now + (float)gap.Value.Max);
        }

        /// <summary>A message from Latte: a text, or — if they're starving — a howling phone call.</summary>
        private void StartLatteMessage(float now)
        {
            (double Min, double Max) gap = dog.MessageGap ?? (60, 120);
            humanNextDogText = now + Range((float)gap.Min, (float)gap.Max);
            if (dog.Calls)
            {
                SetPhone(HumanSeat, new PhoneState
                {
                    From = "Latte (home)",
                    Message = StarvingCalls[phoneRng.NextInt(StarvingCalls.Length)],
                    IsDogText = false,
                    IsLatte = true,
                    MinUntil = now,
                    Until = now + Range(5f, 6.5f),
                });
                OnPhoneStarted(HumanSeat);
                AddLog("Latte is calling you. They're starving.");
                return;
            }
            string[] texts = dog.Mood == DogMood.Hungry ? HungryTexts : DogTexts;
            SetPhone(HumanSeat, new PhoneState
            {
                From = "Latte (home)",
                Message = texts[phoneRng.NextInt(texts.Length)],
                IsDogText = true,
                IsLatte = true,
                MinUntil = now + DogTextMinRead,
                Until = now + Range(3f, 5f),
            });
            OnPhoneStarted(HumanSeat);
        }

        private static readonly string[] HungryTexts =
        {
            "DINNER. DINNER. DINNER. DINNER.",
            "i am so hungry i can see sounds",
            "i have started eating the couch. 2nd cushion",
            "u have a dog. remember. me. hungry",
            "the cat next door is eating. i am not",
        };

        private static readonly string[] StarvingCalls =
        {
            "*AWOOOOOOOOOOOO*",
            "*sad howling* … *sniff* … *AWOOOO*",
            "*heavy breathing into the phone* … *one tiny woof*",
            "*the sound of a couch being destroyed*",
        };

        private string DogStatusLine()
        {
            string color = dog.Mood == DogMood.Content ? "#8f8" : dog.Mood == DogMood.Peckish ? "#ffe880"
                : dog.Mood == DogMood.Hungry ? "#ffb040" : "#ff5050";
            string hint = atHome ? "  (you're home)"
                : dog.Mood >= DogMood.Hungry ? "  — go home (front door) and feed them!" : "";
            string treat = dog.TreatLeft > 0 ? "  · full of soup" : "";
            return $"Latte: <color={color}>{Dog.Describe(dog.Mood)}</color> {Bar(dog.Fullness)}{treat}{hint}";
        }

        // ------------------------------------------------------------------ using things

        /// <summary>Handles E on the front doors, the dog food, the bowl and the dog. Returns false if it isn't one.</summary>
        private bool TryHomeUse(Interactable thing, float now)
        {
            switch (thing.Kind)
            {
                case InteractKind.FrontDoor:
                case InteractKind.HomeDoor:
                    StartTimedUse(thing.Kind, now, TripSeconds);
                    return true;
                case InteractKind.DogFood:
                    if (carryingKibble)
                        ShowToast("You already have a scoop of dog food. Fill the bowl!");
                    else if (carryingSoup)
                        ShowToast("Your hands are full — you're carrying chicken soup (Latte would love it…).");
                    else
                        StartTimedUse(thing.Kind, now, ScoopSeconds);
                    return true;
                case InteractKind.DogBowl:
                    if (dog.IsEating)
                        ShowToast("Latte is busy eating.");
                    else if (carryingSoup)
                    {
                        carryingSoup = false;
                        dog.FeedTreat();
                        lastDogMood = dog.Mood;
                        ShowToast("You give Latte the chicken soup. Best. Night. Ever. They'll stay full for longer.");
                        AddLog("Latte had your chicken soup. Very happy dog.");
                    }
                    else if (carryingKibble)
                    {
                        carryingKibble = false;
                        dog.Feed();
                        lastDogMood = dog.Mood;
                        ShowToast("You fill the bowl. Latte dives in. No more texts for a while.");
                        AddLog("You fed Latte.");
                    }
                    else
                    {
                        ShowToast("The bowl is empty. Take a scoop from the dog food bag first.");
                    }
                    return true;
                case InteractKind.Dog:
                    StartTimedUse(thing.Kind, now, PetSeconds);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Called when a timed home action finishes. Returns false if it wasn't one.</summary>
        private bool FinishHomeAction(InteractKind kind)
        {
            switch (kind)
            {
                case InteractKind.FrontDoor:
                    atHome = true;
                    walker.Place(home.Spawn, 0f);
                    home.Dog.Greet(dog.Mood != DogMood.Content);   // runs to the door, stands up and claps
                    ShowToast($"You're home. Latte is {Dog.Describe(dog.Mood)}.");
                    AddLog("You head home to see Latte. The tables play on without you.");
                    return true;
                case InteractKind.HomeDoor:
                    atHome = false;
                    walker.Place(room.DoorSpawn, 0f);
                    ShowToast("Back at the club.");
                    AddLog("You're back at the club.");
                    return true;
                case InteractKind.DogFood:
                    carryingKibble = true;
                    ShowToast("You scoop some dog food. Now fill the bowl.");
                    return true;
                case InteractKind.Dog:
                    if (dog.TryPet())
                    {
                        home.Dog.Hop();
                        ShowToast("You pet Latte. Good dog. (a little less hungry)");
                    }
                    else
                    {
                        home.Dog.Hop();
                        ShowToast("Latte loves it, but they still want food.");
                    }
                    return true;
                default:
                    return false;
            }
        }

        private string HomePrompt(InteractKind kind)
        {
            switch (kind)
            {
                case InteractKind.FrontDoor:
                    return $"E — go home to Latte ({TripSeconds:0}s walk; Latte is {Dog.Describe(dog.Mood)})";
                case InteractKind.HomeDoor:
                    return $"E — head back to the club ({TripSeconds:0}s walk)";
                case InteractKind.DogFood:
                    return carryingKibble ? "You've got a scoop — fill the bowl" : "E — take a scoop of dog food";
                case InteractKind.DogBowl:
                    return dog.IsEating ? "Latte is eating"
                        : carryingSoup ? "E — give Latte your chicken soup (a treat: stays full longer)"
                        : carryingKibble ? "E — fill the bowl"
                        : "Empty bowl — get a scoop from the dog food bag";
                case InteractKind.Dog:
                    return $"E — pet Latte ({Dog.Describe(dog.Mood)})";
                default:
                    return null;
            }
        }

        private static string HomeUsingText(InteractKind kind)
        {
            switch (kind)
            {
                case InteractKind.FrontDoor: return "Walking home";
                case InteractKind.HomeDoor: return "Walking back to the club";
                case InteractKind.DogFood: return "Scooping dog food";
                case InteractKind.Dog: return "Petting Latte";
                default: return null;
            }
        }
    }
}
