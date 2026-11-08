using System;
using System.Collections.Generic;

namespace MahjongNight.Core
{
    /// <summary>The two drum hits: a deep one in the middle of the drum and a sharp one on the rim.</summary>
    public enum DrumHit : byte
    {
        Boom,  // red: the drum's face
        Tak,   // blue: the rim
    }

    public enum DrumJudgement : byte { None, Great, Good, Miss }

    public readonly struct DrumNote
    {
        public DrumNote(double time, DrumHit kind)
        {
            Time = time;
            Kind = kind;
        }

        public double Time { get; }
        public DrumHit Kind { get; }
    }

    /// <summary>
    /// A song for Drum Rush, the rhythm game on your phone: notes on an eighth-note grid, built
    /// bar by bar from a few patterns (denser patterns on harder songs). Deterministic per seed.
    /// </summary>
    public sealed class DrumSong
    {
        public const double LeadIn = 2.0;   // seconds before the first beat

        private static readonly string[][] Patterns =
        {
            new[] { "B...B...", "B...T...", "B.B.T...", "T...B...", "B...B.T." },                 // easy
            new[] { "B.T.B.T.", "B.BTB...", "T.T.B.B.", "B..TB.T.", "BB..T.T." },                 // medium
            new[] { "BTB.BTB.", "B.TTB.TT", "BBT.BBT.", "BTBTB.T.", "B.BBT.TB", "TBTBB.B." },   // hard
        };

        public DrumSong(string name, double bpm, int difficulty, IReadOnlyList<DrumNote> notes)
        {
            Name = name;
            Bpm = bpm;
            Difficulty = difficulty;
            Notes = notes;
            Length = notes.Count == 0 ? LeadIn : notes[notes.Count - 1].Time + 1.5;
        }

        public string Name { get; }
        public double Bpm { get; }
        /// <summary>0 = easy, 1 = medium, 2 = hard.</summary>
        public int Difficulty { get; }
        public IReadOnlyList<DrumNote> Notes { get; }
        /// <summary>Seconds, from the start to a little after the last note.</summary>
        public double Length { get; }
        public double BeatSeconds => 60.0 / Bpm;

        /// <summary>Builds a song: <paramref name="bars"/> bars of 4/4, each one a random pattern for the difficulty, ending on two booms.</summary>
        public static DrumSong Generate(string name, double bpm, int difficulty, int bars, ulong seed)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            difficulty = Math.Max(0, Math.Min(Patterns.Length - 1, difficulty));
            var rng = new SeededRandom(seed);
            var notes = new List<DrumNote>();
            double eighth = 60.0 / bpm / 2.0;
            for (int bar = 0; bar < bars; bar++)
            {
                // Ease in: the first bar is always from the easy set.
                string[] set = Patterns[bar == 0 ? 0 : difficulty];
                string pattern = bar == bars - 1 ? "B...B..." : set[rng.NextInt(set.Length)];
                for (int i = 0; i < 8; i++)
                {
                    char c = pattern[i];
                    if (c == '.') continue;
                    notes.Add(new DrumNote(LeadIn + (bar * 8 + i) * eighth, c == 'B' ? DrumHit.Boom : DrumHit.Tak));
                }
            }
            return new DrumSong(name, bpm, difficulty, notes);
        }

        /// <summary>The songs on the phone.</summary>
        public static IReadOnlyList<DrumSong> Setlist() => new[]
        {
            Generate("Mahjong Shuffle", 110, 0, 12, 11),
            Generate("Dim Sum Rush", 135, 1, 14, 22),
            Generate("Latte Zoomies", 160, 2, 16, 33),
        };
    }

    /// <summary>
    /// One play of a <see cref="DrumSong"/>: advance the clock, hit the drum, get judged.
    /// Hit within <see cref="GreatWindow"/> of a note for GREAT, <see cref="GoodWindow"/> for GOOD;
    /// the wrong drum, or letting a note go by, is a MISS and breaks your combo. Tapping when
    /// there's no note near is free.
    /// </summary>
    public sealed class DrumGame
    {
        public const double GreatWindow = 0.05;
        public const double GoodWindow = 0.11;
        public const int GreatPoints = 300, GoodPoints = 100;

        private readonly DrumJudgement[] judged;
        private int firstOpen;   // notes before this are all judged

        public DrumGame(DrumSong song)
        {
            Song = song ?? throw new ArgumentNullException(nameof(song));
            judged = new DrumJudgement[song.Notes.Count];
        }

        public DrumSong Song { get; }
        public double Time { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Greats { get; private set; }
        public int Goods { get; private set; }
        public int Misses { get; private set; }
        public bool Finished => Time >= Song.Length;

        public DrumJudgement Judgement(int note) => judged[note];

        /// <summary>0..1: GREATs count fully, GOODs half.</summary>
        public double Accuracy => Song.Notes.Count == 0 ? 1 : (Greats + 0.5 * Goods) / Song.Notes.Count;

        public bool FullCombo => Finished && Misses == 0;

        public string Rank =>
            Accuracy >= 0.95 ? "S" : Accuracy >= 0.85 ? "A" : Accuracy >= 0.7 ? "B" : Accuracy >= 0.5 ? "C" : "D";

        /// <summary>Moves the song on; notes that went by unhit become misses.</summary>
        public void Advance(double seconds)
        {
            if (seconds <= 0)
                return;
            Time += seconds;
            while (firstOpen < judged.Length && judged[firstOpen] != DrumJudgement.None)
                firstOpen++;
            for (int i = firstOpen; i < judged.Length; i++)
            {
                double t = Song.Notes[i].Time;
                if (t > Time - GoodWindow)
                    break;
                if (judged[i] == DrumJudgement.None)
                    Judge(i, DrumJudgement.Miss);
            }
        }

        /// <summary>Hit the drum now. Returns how it went, or None if there was no note near.</summary>
        public DrumJudgement Hit(DrumHit kind)
        {
            for (int i = firstOpen; i < judged.Length; i++)
            {
                if (judged[i] != DrumJudgement.None)
                    continue;
                double off = Math.Abs(Song.Notes[i].Time - Time);
                if (Song.Notes[i].Time - Time > GoodWindow)
                    break;                       // the next note's still too far off: a free tap
                if (off > GoodWindow)
                    continue;
                DrumJudgement j = Song.Notes[i].Kind != kind ? DrumJudgement.Miss
                    : off <= GreatWindow ? DrumJudgement.Great
                    : DrumJudgement.Good;
                Judge(i, j);
                return j;
            }
            return DrumJudgement.None;
        }

        private void Judge(int note, DrumJudgement j)
        {
            judged[note] = j;
            if (j == DrumJudgement.Miss)
            {
                Misses++;
                Combo = 0;
                return;
            }
            Combo++;
            MaxCombo = Math.Max(MaxCombo, Combo);
            int bonus = Math.Min(Combo, 100) / 10 * 10;   // up to +100 per note on a long combo
            if (j == DrumJudgement.Great) { Greats++; Score += GreatPoints + bonus; }
            else { Goods++; Score += GoodPoints + bonus; }
        }
    }
}
