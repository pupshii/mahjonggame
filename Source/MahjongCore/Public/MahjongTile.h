#pragma once

#include "CoreMinimal.h"

/** The seven tile groups in a Hong Kong mahjong set. */
enum class ETileSuit : uint8
{
	Dots,        // 筒  ranks 1-9
	Bamboo,      // 索  ranks 1-9
	Characters,  // 萬  ranks 1-9
	Wind,        // 風  ranks 1-4 (see EWind)
	Dragon,      // 三元 ranks 1-3 (see EDragon)
	Flower,      // 花  ranks 1-4, bonus tile, one copy each
	Season       // 季  ranks 1-4, bonus tile, one copy each
};

/** Wind ranks. Also used for seat winds and the prevailing wind. */
enum class EWind : uint8
{
	East = 1,
	South = 2,
	West = 3,
	North = 4
};

/** Dragon ranks. */
enum class EDragon : uint8
{
	Red = 1,    // 中
	Green = 2,  // 發
	White = 3   // 白
};

namespace MahjongTiles
{
	/** Distinct playable tile kinds: 27 suited + 4 winds + 3 dragons. */
	inline constexpr int32 NumPlayableKinds = 34;
	/** Distinct bonus tile kinds: 4 flowers + 4 seasons. */
	inline constexpr int32 NumBonusKinds = 8;
	/** All distinct kinds; valid kind indices are [0, NumKinds). */
	inline constexpr int32 NumKinds = NumPlayableKinds + NumBonusKinds;
	/** Copies of each playable kind in a full set. Bonus kinds have one copy. */
	inline constexpr int32 CopiesPerPlayableKind = 4;
	/** Tiles in a full set: 34 * 4 + 8 = 144. */
	inline constexpr int32 TotalTiles = NumPlayableKinds * CopiesPerPlayableKind + NumBonusKinds;
}

/**
 * A tile *kind* (e.g. "5 Bamboo", "Red Dragon"), not a physical tile: the four
 * physical copies of 5 Bamboo all compare equal.
 *
 * Kind index layout (stable — used for count arrays and sorting):
 *   0-8   Dots 1-9        27-30  Winds E S W N
 *   9-17  Bamboo 1-9      31-33  Dragons Red Green White
 *   18-26 Characters 1-9  34-37  Flowers 1-4
 *                         38-41  Seasons 1-4
 */
struct MAHJONGCORE_API FMahjongTile
{
	ETileSuit Suit = ETileSuit::Dots;
	uint8 Rank = 1;

	constexpr FMahjongTile() = default;
	constexpr FMahjongTile(ETileSuit InSuit, uint8 InRank) : Suit(InSuit), Rank(InRank) {}

	static constexpr FMahjongTile Dots(uint8 InRank) { return FMahjongTile(ETileSuit::Dots, InRank); }
	static constexpr FMahjongTile Bamboo(uint8 InRank) { return FMahjongTile(ETileSuit::Bamboo, InRank); }
	static constexpr FMahjongTile Characters(uint8 InRank) { return FMahjongTile(ETileSuit::Characters, InRank); }
	static constexpr FMahjongTile Wind(EWind InWind) { return FMahjongTile(ETileSuit::Wind, static_cast<uint8>(InWind)); }
	static constexpr FMahjongTile Dragon(EDragon InDragon) { return FMahjongTile(ETileSuit::Dragon, static_cast<uint8>(InDragon)); }
	static constexpr FMahjongTile Flower(uint8 InRank) { return FMahjongTile(ETileSuit::Flower, InRank); }
	static constexpr FMahjongTile Season(uint8 InRank) { return FMahjongTile(ETileSuit::Season, InRank); }

	/** Rebuilds a tile from its kind index. Index must be in [0, NumKinds). */
	static FMahjongTile FromKindIndex(int32 KindIndex);

	/** True if Rank is in range for Suit. */
	bool IsValid() const;

	/** Dots, Bamboo or Characters. */
	bool IsSuited() const { return Suit == ETileSuit::Dots || Suit == ETileSuit::Bamboo || Suit == ETileSuit::Characters; }
	/** Winds or Dragons. */
	bool IsHonor() const { return Suit == ETileSuit::Wind || Suit == ETileSuit::Dragon; }
	/** Flowers or Seasons: set aside when drawn, never part of melds. */
	bool IsBonus() const { return Suit == ETileSuit::Flower || Suit == ETileSuit::Season; }
	/** Suited 1 or 9. */
	bool IsTerminal() const { return IsSuited() && (Rank == 1 || Rank == 9); }
	/** Terminal or honor (么九) — used by Thirteen Orphans and terminal hands. */
	bool IsTerminalOrHonor() const { return IsTerminal() || IsHonor(); }

	/** Stable index in [0, NumKinds); see layout above. */
	int32 GetKindIndex() const;

	/**
	 * Short code for logs and tests: "1D" "9B" "5C" (Dots/Bamboo/Characters),
	 * "E" "S" "W" "N" (winds), "RD" "GD" "WD" (dragons), "F1"-"F4", "S1"-"S4".
	 */
	FString ToString() const;

	bool operator==(const FMahjongTile& Other) const { return Suit == Other.Suit && Rank == Other.Rank; }
	bool operator!=(const FMahjongTile& Other) const { return !(*this == Other); }
	/** Orders by kind index — the natural hand-sorting order. */
	bool operator<(const FMahjongTile& Other) const { return GetKindIndex() < Other.GetKindIndex(); }

	friend uint32 GetTypeHash(const FMahjongTile& Tile) { return static_cast<uint32>(Tile.GetKindIndex()); }
};

namespace MahjongTiles
{
	/** Every tile in a full 144-tile set, in kind-index order (unshuffled). */
	MAHJONGCORE_API TArray<FMahjongTile> MakeFullSet();
}
