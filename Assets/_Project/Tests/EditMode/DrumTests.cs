using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class DrumTests
    {
        private static DrumSong TwoNotes() => new DrumSong("test", 120, 0, new[]
        {
            new DrumNote(1.0, DrumHit.Boom),
            new DrumNote(2.0, DrumHit.Tak),
        });

        [Test]
        public void Setlist_SongsAreOrderedSensibly_AndHarderMeansDenser()
        {
            var songs = DrumSong.Setlist();
            Assert.That(songs.Count, Is.EqualTo(3));
            foreach (DrumSong s in songs)
            {
                Assert.That(s.Notes, Is.Not.Empty);
                Assert.That(s.Notes.First().Time, Is.GreaterThanOrEqualTo(DrumSong.LeadIn));
                for (int i = 1; i < s.Notes.Count; i++)
                    Assert.That(s.Notes[i].Time, Is.GreaterThan(s.Notes[i - 1].Time));
                Assert.That(s.Length, Is.GreaterThan(s.Notes.Last().Time));
                Assert.That(s.Length, Is.InRange(20, 40), "About half a minute each");
            }
            double Density(DrumSong s) => s.Notes.Count / (s.Length - DrumSong.LeadIn);
            Assert.That(Density(songs[2]), Is.GreaterThan(Density(songs[1])));
            Assert.That(Density(songs[1]), Is.GreaterThan(Density(songs[0])));
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            var a = DrumSong.Generate("x", 140, 2, 8, 5);
            var b = DrumSong.Generate("x", 140, 2, 8, 5);
            Assert.That(a.Notes.Select(n => (n.Time, n.Kind)), Is.EqualTo(b.Notes.Select(n => (n.Time, n.Kind))));
        }

        [Test]
        public void Hits_AreJudgedByTiming_AndTheRightDrum()
        {
            var g = new DrumGame(TwoNotes());
            g.Advance(1.03);
            Assert.That(g.Hit(DrumHit.Boom), Is.EqualTo(DrumJudgement.Great));
            Assert.That(g.Combo, Is.EqualTo(1));

            g.Advance(0.5);                      // t = 1.53: nothing near
            Assert.That(g.Hit(DrumHit.Tak), Is.EqualTo(DrumJudgement.None), "A free tap");
            Assert.That(g.Combo, Is.EqualTo(1));

            g.Advance(0.39);                     // t = 1.92: 80 ms early
            Assert.That(g.Hit(DrumHit.Tak), Is.EqualTo(DrumJudgement.Good));
            Assert.That(g.Combo, Is.EqualTo(2));
            Assert.That(g.Score, Is.EqualTo(DrumGame.GreatPoints + DrumGame.GoodPoints));
        }

        [Test]
        public void WrongDrum_AndLetNotesPass_AreMisses()
        {
            var g = new DrumGame(TwoNotes());
            g.Advance(1.0);
            Assert.That(g.Hit(DrumHit.Tak), Is.EqualTo(DrumJudgement.Miss), "Wrong drum");
            Assert.That(g.Combo, Is.EqualTo(0));
            g.Advance(1.2);                      // the second note goes by
            Assert.That(g.Judgement(1), Is.EqualTo(DrumJudgement.Miss));
            Assert.That(g.Misses, Is.EqualTo(2));
            g.Advance(5);
            Assert.That(g.Finished, Is.True);
            Assert.That(g.FullCombo, Is.False);
            Assert.That(g.Rank, Is.EqualTo("D"));
        }

        [Test]
        public void PerfectPlay_IsAFullComboRankS()
        {
            DrumSong song = DrumSong.Setlist()[1];
            var g = new DrumGame(song);
            double t = 0;
            foreach (DrumNote n in song.Notes)
            {
                g.Advance(n.Time - t);
                t = n.Time;
                Assert.That(g.Hit(n.Kind), Is.EqualTo(DrumJudgement.Great));
            }
            g.Advance(song.Length);
            Assert.That(g.FullCombo, Is.True);
            Assert.That(g.MaxCombo, Is.EqualTo(song.Notes.Count));
            Assert.That(g.Rank, Is.EqualTo("S"));
            Assert.That(g.Accuracy, Is.EqualTo(1));
        }
    }
}
