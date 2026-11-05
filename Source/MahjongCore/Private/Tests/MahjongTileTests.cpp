#include "Misc/AutomationTest.h"
#include "MahjongTile.h"

#if WITH_DEV_AUTOMATION_TESTS

// Run in editor: Tools > Session Frontend > Automation, filter "MahjongCore".

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMahjongTileFullSetTest, "MahjongCore.Tiles.FullSetComposition",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FMahjongTileFullSetTest::RunTest(const FString& Parameters)
{
	const TArray<FMahjongTile> Set = MahjongTiles::MakeFullSet();
	TestEqual(TEXT("Full set has 144 tiles"), Set.Num(), 144);

	int32 Counts[MahjongTiles::NumKinds] = {};
	int32 Suited = 0, Winds = 0, Dragons = 0, Bonus = 0;
	for (const FMahjongTile& Tile : Set)
	{
		TestTrue(*FString::Printf(TEXT("%s is valid"), *Tile.ToString()), Tile.IsValid());
		++Counts[Tile.GetKindIndex()];
		if (Tile.IsSuited()) { ++Suited; }
		if (Tile.Suit == ETileSuit::Wind) { ++Winds; }
		if (Tile.Suit == ETileSuit::Dragon) { ++Dragons; }
		if (Tile.IsBonus()) { ++Bonus; }
	}

	TestEqual(TEXT("108 suited tiles"), Suited, 108);
	TestEqual(TEXT("16 wind tiles"), Winds, 16);
	TestEqual(TEXT("12 dragon tiles"), Dragons, 12);
	TestEqual(TEXT("8 bonus tiles"), Bonus, 8);

	for (int32 KindIndex = 0; KindIndex < MahjongTiles::NumKinds; ++KindIndex)
	{
		const int32 Expected = KindIndex < MahjongTiles::NumPlayableKinds ? 4 : 1;
		TestEqual(*FString::Printf(TEXT("Copies of %s"), *FMahjongTile::FromKindIndex(KindIndex).ToString()),
			Counts[KindIndex], Expected);
	}
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMahjongTileKindIndexTest, "MahjongCore.Tiles.KindIndexRoundTrip",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FMahjongTileKindIndexTest::RunTest(const FString& Parameters)
{
	for (int32 KindIndex = 0; KindIndex < MahjongTiles::NumKinds; ++KindIndex)
	{
		const FMahjongTile Tile = FMahjongTile::FromKindIndex(KindIndex);
		TestEqual(*FString::Printf(TEXT("Round trip of index %d"), KindIndex), Tile.GetKindIndex(), KindIndex);
	}

	// Spot-check the documented layout.
	TestEqual(TEXT("1 Dots is 0"), FMahjongTile::Dots(1).GetKindIndex(), 0);
	TestEqual(TEXT("1 Bamboo is 9"), FMahjongTile::Bamboo(1).GetKindIndex(), 9);
	TestEqual(TEXT("9 Characters is 26"), FMahjongTile::Characters(9).GetKindIndex(), 26);
	TestEqual(TEXT("East is 27"), FMahjongTile::Wind(EWind::East).GetKindIndex(), 27);
	TestEqual(TEXT("White Dragon is 33"), FMahjongTile::Dragon(EDragon::White).GetKindIndex(), 33);
	TestEqual(TEXT("Flower 1 is 34"), FMahjongTile::Flower(1).GetKindIndex(), 34);
	TestEqual(TEXT("Season 4 is 41"), FMahjongTile::Season(4).GetKindIndex(), 41);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMahjongTileClassificationTest, "MahjongCore.Tiles.Classification",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FMahjongTileClassificationTest::RunTest(const FString& Parameters)
{
	TestTrue(TEXT("1D is terminal"), FMahjongTile::Dots(1).IsTerminal());
	TestTrue(TEXT("9C is terminal"), FMahjongTile::Characters(9).IsTerminal());
	TestFalse(TEXT("5B is not terminal"), FMahjongTile::Bamboo(5).IsTerminal());
	TestFalse(TEXT("East is not terminal"), FMahjongTile::Wind(EWind::East).IsTerminal());

	TestTrue(TEXT("East is honor"), FMahjongTile::Wind(EWind::East).IsHonor());
	TestTrue(TEXT("Red Dragon is honor"), FMahjongTile::Dragon(EDragon::Red).IsHonor());
	TestTrue(TEXT("North is terminal-or-honor"), FMahjongTile::Wind(EWind::North).IsTerminalOrHonor());
	TestFalse(TEXT("2D is not terminal-or-honor"), FMahjongTile::Dots(2).IsTerminalOrHonor());

	TestTrue(TEXT("Flower is bonus"), FMahjongTile::Flower(2).IsBonus());
	TestTrue(TEXT("Season is bonus"), FMahjongTile::Season(3).IsBonus());
	TestFalse(TEXT("Flower is not honor"), FMahjongTile::Flower(2).IsHonor());
	TestFalse(TEXT("Flower is not suited"), FMahjongTile::Flower(2).IsSuited());

	TestFalse(TEXT("Dots 0 is invalid"), FMahjongTile::Dots(0).IsValid());
	TestFalse(TEXT("Dots 10 is invalid"), FMahjongTile::Dots(10).IsValid());
	TestFalse(TEXT("Dragon 4 is invalid"), FMahjongTile(ETileSuit::Dragon, 4).IsValid());
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMahjongTileToStringTest, "MahjongCore.Tiles.ToString",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FMahjongTileToStringTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("1 Dots"), FMahjongTile::Dots(1).ToString(), FString(TEXT("1D")));
	TestEqual(TEXT("9 Bamboo"), FMahjongTile::Bamboo(9).ToString(), FString(TEXT("9B")));
	TestEqual(TEXT("5 Characters"), FMahjongTile::Characters(5).ToString(), FString(TEXT("5C")));
	TestEqual(TEXT("South"), FMahjongTile::Wind(EWind::South).ToString(), FString(TEXT("S")));
	TestEqual(TEXT("Green Dragon"), FMahjongTile::Dragon(EDragon::Green).ToString(), FString(TEXT("GD")));
	TestEqual(TEXT("Flower 3"), FMahjongTile::Flower(3).ToString(), FString(TEXT("F3")));
	TestEqual(TEXT("Season 1"), FMahjongTile::Season(1).ToString(), FString(TEXT("S1")));
	TestEqual(TEXT("Invalid tile"), FMahjongTile::Dots(0).ToString(), FString(TEXT("??")));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FMahjongTileOrderingTest, "MahjongCore.Tiles.SortOrder",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)

bool FMahjongTileOrderingTest::RunTest(const FString& Parameters)
{
	TArray<FMahjongTile> Hand = {
		FMahjongTile::Dragon(EDragon::Red), FMahjongTile::Bamboo(3), FMahjongTile::Dots(7),
		FMahjongTile::Wind(EWind::East), FMahjongTile::Characters(1), FMahjongTile::Dots(2)
	};
	Hand.Sort();

	FString Joined;
	for (const FMahjongTile& Tile : Hand)
	{
		Joined += Tile.ToString() + TEXT(" ");
	}
	TestEqual(TEXT("Sorted hand"), Joined, FString(TEXT("2D 7D 3B 1C E RD ")));
	TestTrue(TEXT("Equal kinds compare equal"), FMahjongTile::Bamboo(4) == FMahjongTile::Bamboo(4));
	TestTrue(TEXT("Different kinds differ"), FMahjongTile::Bamboo(4) != FMahjongTile::Dots(4));
	return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
