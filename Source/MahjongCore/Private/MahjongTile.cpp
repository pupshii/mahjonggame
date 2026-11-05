#include "MahjongTile.h"

namespace
{
	// First kind index of each suit, indexed by ETileSuit.
	constexpr int32 SuitBaseIndex[] = { 0, 9, 18, 27, 31, 34, 38 };
	// Number of ranks in each suit, indexed by ETileSuit.
	constexpr uint8 SuitRankCount[] = { 9, 9, 9, 4, 3, 4, 4 };
	constexpr int32 NumSuits = UE_ARRAY_COUNT(SuitBaseIndex);
}

FMahjongTile FMahjongTile::FromKindIndex(int32 KindIndex)
{
	check(KindIndex >= 0 && KindIndex < MahjongTiles::NumKinds);

	for (int32 SuitIdx = NumSuits - 1; SuitIdx >= 0; --SuitIdx)
	{
		if (KindIndex >= SuitBaseIndex[SuitIdx])
		{
			return FMahjongTile(static_cast<ETileSuit>(SuitIdx), static_cast<uint8>(KindIndex - SuitBaseIndex[SuitIdx] + 1));
		}
	}
	return FMahjongTile();
}

bool FMahjongTile::IsValid() const
{
	const int32 SuitIdx = static_cast<int32>(Suit);
	return SuitIdx >= 0 && SuitIdx < NumSuits && Rank >= 1 && Rank <= SuitRankCount[SuitIdx];
}

int32 FMahjongTile::GetKindIndex() const
{
	checkSlow(IsValid());
	return SuitBaseIndex[static_cast<int32>(Suit)] + Rank - 1;
}

FString FMahjongTile::ToString() const
{
	if (!IsValid())
	{
		return TEXT("??");
	}

	switch (Suit)
	{
	case ETileSuit::Dots:       return FString::Printf(TEXT("%dD"), Rank);
	case ETileSuit::Bamboo:     return FString::Printf(TEXT("%dB"), Rank);
	case ETileSuit::Characters: return FString::Printf(TEXT("%dC"), Rank);
	case ETileSuit::Wind:
	{
		static const TCHAR* WindNames[] = { TEXT("E"), TEXT("S"), TEXT("W"), TEXT("N") };
		return WindNames[Rank - 1];
	}
	case ETileSuit::Dragon:
	{
		static const TCHAR* DragonNames[] = { TEXT("RD"), TEXT("GD"), TEXT("WD") };
		return DragonNames[Rank - 1];
	}
	case ETileSuit::Flower:     return FString::Printf(TEXT("F%d"), Rank);
	case ETileSuit::Season:     return FString::Printf(TEXT("S%d"), Rank);
	}
	return TEXT("??");
}

TArray<FMahjongTile> MahjongTiles::MakeFullSet()
{
	TArray<FMahjongTile> Tiles;
	Tiles.Reserve(TotalTiles);

	for (int32 KindIndex = 0; KindIndex < NumKinds; ++KindIndex)
	{
		const FMahjongTile Tile = FMahjongTile::FromKindIndex(KindIndex);
		const int32 Copies = Tile.IsBonus() ? 1 : CopiesPerPlayableKind;
		for (int32 Copy = 0; Copy < Copies; ++Copy)
		{
			Tiles.Add(Tile);
		}
	}
	return Tiles;
}
