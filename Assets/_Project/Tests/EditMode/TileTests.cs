using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    // Run in Unity: Window > General > Test Runner > EditMode > Run All.
    public class TileTests
    {
        [Test]
        public void FullSet_Has144Tiles()
        {
            Assert.That(TileSet.CreateFull(), Has.Count.EqualTo(144));
            Assert.That(TileSet.TotalTiles, Is.EqualTo(144));
        }

        [Test]
        public void FullSet_HasCorrectGroupSizes()
        {
            List<Tile> set = TileSet.CreateFull();
            Assert.That(set.Count(t => t.IsSuited), Is.EqualTo(108));
            Assert.That(set.Count(t => t.Suit == TileSuit.Wind), Is.EqualTo(16));
            Assert.That(set.Count(t => t.Suit == TileSuit.Dragon), Is.EqualTo(12));
            Assert.That(set.Count(t => t.IsBonus), Is.EqualTo(8));
            Assert.That(set.All(t => t.IsValid), Is.True);
        }

        [Test]
        public void FullSet_HasFourOfEachPlayableAndOneOfEachBonus()
        {
            var counts = new int[TileSet.KindCount];
            foreach (Tile tile in TileSet.CreateFull())
                counts[tile.KindIndex]++;

            for (int kind = 0; kind < TileSet.KindCount; kind++)
            {
                int expected = kind < TileSet.PlayableKindCount ? 4 : 1;
                Assert.That(counts[kind], Is.EqualTo(expected), $"Copies of {Tile.FromKindIndex(kind)}");
            }
        }

        [Test]
        public void KindIndex_RoundTrips()
        {
            for (int kind = 0; kind < TileSet.KindCount; kind++)
                Assert.That(Tile.FromKindIndex(kind).KindIndex, Is.EqualTo(kind));
        }

        [Test]
        public void KindIndex_MatchesDocumentedLayout()
        {
            Assert.That(Tile.Dots(1).KindIndex, Is.EqualTo(0));
            Assert.That(Tile.Bamboo(1).KindIndex, Is.EqualTo(9));
            Assert.That(Tile.Characters(9).KindIndex, Is.EqualTo(26));
            Assert.That(Tile.WindTile(Wind.East).KindIndex, Is.EqualTo(27));
            Assert.That(Tile.DragonTile(Dragon.White).KindIndex, Is.EqualTo(33));
            Assert.That(Tile.Flower(1).KindIndex, Is.EqualTo(34));
            Assert.That(Tile.Season(4).KindIndex, Is.EqualTo(41));
        }

        [Test]
        public void FromKindIndex_RejectsOutOfRange()
        {
            Assert.That(() => Tile.FromKindIndex(-1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => Tile.FromKindIndex(42), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Classification()
        {
            Assert.That(Tile.Dots(1).IsTerminal, Is.True);
            Assert.That(Tile.Characters(9).IsTerminal, Is.True);
            Assert.That(Tile.Bamboo(5).IsTerminal, Is.False);
            Assert.That(Tile.WindTile(Wind.East).IsTerminal, Is.False);

            Assert.That(Tile.WindTile(Wind.East).IsHonor, Is.True);
            Assert.That(Tile.DragonTile(Dragon.Red).IsHonor, Is.True);
            Assert.That(Tile.WindTile(Wind.North).IsTerminalOrHonor, Is.True);
            Assert.That(Tile.Dots(2).IsTerminalOrHonor, Is.False);

            Assert.That(Tile.Flower(2).IsBonus, Is.True);
            Assert.That(Tile.Season(3).IsBonus, Is.True);
            Assert.That(Tile.Flower(2).IsHonor, Is.False);
            Assert.That(Tile.Flower(2).IsSuited, Is.False);
        }

        [Test]
        public void Validity()
        {
            Assert.That(default(Tile).IsValid, Is.False);
            Assert.That(Tile.Dots(0).IsValid, Is.False);
            Assert.That(Tile.Dots(10).IsValid, Is.False);
            Assert.That(new Tile(TileSuit.Dragon, 4).IsValid, Is.False);
            Assert.That(() => Tile.Dots(0).KindIndex, Throws.InvalidOperationException);
        }

        [TestCase(TileSuit.Dots, 1, "1D")]
        [TestCase(TileSuit.Bamboo, 9, "9B")]
        [TestCase(TileSuit.Characters, 5, "5C")]
        [TestCase(TileSuit.Wind, 2, "S")]
        [TestCase(TileSuit.Dragon, 2, "GD")]
        [TestCase(TileSuit.Flower, 3, "F3")]
        [TestCase(TileSuit.Season, 1, "S1")]
        [TestCase(TileSuit.Dots, 0, "??")]
        public void ToString_UsesShortCodes(TileSuit suit, int rank, string expected)
        {
            Assert.That(new Tile(suit, rank).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void Sorting_FollowsKindOrder()
        {
            var hand = new List<Tile>
            {
                Tile.DragonTile(Dragon.Red), Tile.Bamboo(3), Tile.Dots(7),
                Tile.WindTile(Wind.East), Tile.Characters(1), Tile.Dots(2)
            };
            hand.Sort();
            Assert.That(string.Join(" ", hand), Is.EqualTo("2D 7D 3B 1C E RD"));
        }

        [Test]
        public void Equality_IsByKind()
        {
            Assert.That(Tile.Bamboo(4) == Tile.Bamboo(4), Is.True);
            Assert.That(Tile.Bamboo(4) != Tile.Dots(4), Is.True);
            Assert.That(Tile.Bamboo(4).GetHashCode(), Is.EqualTo(Tile.Bamboo(4).GetHashCode()));
            Assert.That(new HashSet<Tile>(TileSet.CreateFull()), Has.Count.EqualTo(TileSet.KindCount));
        }
    }
}
