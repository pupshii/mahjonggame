using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MahjongNight.Core.Tests
{
    public class SeededRandomTests
    {
        [Test]
        public void MatchesSplitMix64ReferenceValues()
        {
            var rng = new SeededRandom(0);
            Assert.That(rng.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void NextInt_StaysInRange()
        {
            var rng = new SeededRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                int v = rng.NextInt(7);
                Assert.That(v, Is.InRange(0, 6));
            }
        }

        [Test]
        public void NextInt_RejectsNonPositiveBound()
        {
            var rng = new SeededRandom(1);
            Assert.That(() => rng.NextInt(0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }
    }

    public class WallTests
    {
        [Test]
        public void Shuffled_HasFullSet()
        {
            List<Tile> tiles = Wall.CreateShuffled(123).RemainingTiles().ToList();
            Assert.That(tiles, Has.Count.EqualTo(144));
            Assert.That(tiles.OrderBy(t => t), Is.EqualTo(TileSet.CreateFull()));
        }

        [Test]
        public void Shuffled_IsDeterministicPerSeed()
        {
            List<Tile> a = Wall.CreateShuffled(7).RemainingTiles().ToList();
            List<Tile> b = Wall.CreateShuffled(7).RemainingTiles().ToList();
            List<Tile> c = Wall.CreateShuffled(8).RemainingTiles().ToList();
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
            Assert.That(a, Is.Not.EqualTo(TileSet.CreateFull()), "Should actually be shuffled");
        }

        [Test]
        public void DrawsFromFrontAndBack()
        {
            var wall = new Wall(Tile.ParseList("1D 2D 3D 4D 5D"));
            Assert.That(wall.DrawFront(), Is.EqualTo(Tile.Dots(1)));
            Assert.That(wall.DrawBack(), Is.EqualTo(Tile.Dots(5)));
            Assert.That(wall.DrawFront(), Is.EqualTo(Tile.Dots(2)));
            Assert.That(wall.DrawBack(), Is.EqualTo(Tile.Dots(4)));
            Assert.That(wall.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void DeadWall_IsNeverDrawn()
        {
            var wall = new Wall(Tile.ParseList("1D 2D 3D 4D 5D"), deadWallSize: 3);
            Assert.That(wall.DrawableCount, Is.EqualTo(2));
            wall.DrawFront();
            wall.DrawBack();
            Assert.That(wall.CanDraw, Is.False);
            Assert.That(wall.Remaining, Is.EqualTo(3));
            Assert.That(() => wall.DrawFront(), Throws.InvalidOperationException);
            Assert.That(() => wall.DrawBack(), Throws.InvalidOperationException);
        }
    }

    public class TileParseTests
    {
        [TestCase("1D")]
        [TestCase("9B")]
        [TestCase("5C")]
        [TestCase("E")]
        [TestCase("S")]
        [TestCase("W")]
        [TestCase("N")]
        [TestCase("RD")]
        [TestCase("GD")]
        [TestCase("WD")]
        [TestCase("F4")]
        [TestCase("S1")]
        public void RoundTripsWithToString(string code)
        {
            Assert.That(Tile.Parse(code).ToString(), Is.EqualTo(code));
        }

        [Test]
        public void EveryKindRoundTrips()
        {
            for (int k = 0; k < TileSet.KindCount; k++)
            {
                Tile t = Tile.FromKindIndex(k);
                Assert.That(Tile.Parse(t.ToString()), Is.EqualTo(t));
            }
        }

        [TestCase("")]
        [TestCase("0D")]
        [TestCase("1X")]
        [TestCase("F5")]
        [TestCase("10D")]
        [TestCase("Z")]
        public void RejectsBadCodes(string code)
        {
            Assert.That(Tile.TryParse(code, out _), Is.False);
            Assert.That(() => Tile.Parse(code), Throws.TypeOf<System.FormatException>());
        }

        [Test]
        public void ParseList_SplitsOnSpacesAndCommas()
        {
            Assert.That(Tile.ParseList("1d, 2D  rd"), Is.EqualTo(new[] { Tile.Dots(1), Tile.Dots(2), Tile.DragonTile(Dragon.Red) }));
        }
    }
}
