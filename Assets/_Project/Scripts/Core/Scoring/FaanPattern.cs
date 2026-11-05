namespace MahjongNight.Core
{
    /// <summary>Every scoring pattern. Values live in <see cref="FaanTable"/>.</summary>
    public enum FaanPattern
    {
        // Way of winning
        SelfDrawn,          // 自摸
        ConcealedHand,      // 門前清 — no exposed melds, won on a discard
        LastTile,           // 海底撈月 — self-drawn with no tiles left
        KongReplacement,    // 槓上開花
        RobbingKong,        // 搶槓

        // Bonus tiles
        NoFlowers,          // 無花
        OwnFlower,          // 正花
        OwnSeason,          // 正季
        FlowerSet,          // 一台花 — all four flowers or all four seasons (counted per set)

        // Sets
        DragonPung,         // 三元牌 (per pung)
        SeatWindPung,       // 門風
        PrevailingWindPung, // 圈風
        AllChows,           // 平糊
        AllPungs,           // 對對糊
        SmallThreeDragons,  // 小三元

        // Suits & shapes
        MixedOneSuit,       // 混一色
        PureOneSuit,        // 清一色
        SevenPairs,         // 七對

        // Limit hands (爆棚)
        BigThreeDragons,    // 大三元
        SmallFourWinds,     // 小四喜
        BigFourWinds,       // 大四喜
        AllHonors,          // 字一色
        AllTerminals,       // 清么九
        ThirteenOrphans,    // 十三么
        NineGates,          // 九子連環
        FourConcealedPungs, // 坑坑糊
        FourKongs,          // 十八羅漢
        HeavenlyHand,       // 天糊
        EarthlyHand,        // 地糊
        EightFlowers,       // 八仙過海
    }

    public static class FaanPatterns
    {
        public static bool IsLimit(FaanPattern p) => p >= FaanPattern.BigThreeDragons;

        public static string EnglishName(FaanPattern p)
        {
            switch (p)
            {
                case FaanPattern.SelfDrawn: return "Self-drawn";
                case FaanPattern.ConcealedHand: return "Concealed hand";
                case FaanPattern.LastTile: return "Win on the last tile";
                case FaanPattern.KongReplacement: return "Win on kong replacement";
                case FaanPattern.RobbingKong: return "Robbing the kong";
                case FaanPattern.NoFlowers: return "No flowers";
                case FaanPattern.OwnFlower: return "Own flower";
                case FaanPattern.OwnSeason: return "Own season";
                case FaanPattern.FlowerSet: return "Full flower set";
                case FaanPattern.DragonPung: return "Dragon pung";
                case FaanPattern.SeatWindPung: return "Seat wind";
                case FaanPattern.PrevailingWindPung: return "Prevailing wind";
                case FaanPattern.AllChows: return "All chows";
                case FaanPattern.AllPungs: return "All pungs";
                case FaanPattern.SmallThreeDragons: return "Small three dragons";
                case FaanPattern.MixedOneSuit: return "Mixed one suit";
                case FaanPattern.PureOneSuit: return "Pure one suit";
                case FaanPattern.SevenPairs: return "Seven pairs";
                case FaanPattern.BigThreeDragons: return "Big three dragons";
                case FaanPattern.SmallFourWinds: return "Small four winds";
                case FaanPattern.BigFourWinds: return "Big four winds";
                case FaanPattern.AllHonors: return "All honours";
                case FaanPattern.AllTerminals: return "All terminals";
                case FaanPattern.ThirteenOrphans: return "Thirteen orphans";
                case FaanPattern.NineGates: return "Nine gates";
                case FaanPattern.FourConcealedPungs: return "Four concealed pungs";
                case FaanPattern.FourKongs: return "Four kongs";
                case FaanPattern.HeavenlyHand: return "Heavenly hand";
                case FaanPattern.EarthlyHand: return "Earthly hand";
                case FaanPattern.EightFlowers: return "Eight flowers";
                default: return p.ToString();
            }
        }

        public static string ChineseName(FaanPattern p)
        {
            switch (p)
            {
                case FaanPattern.SelfDrawn: return "自摸";
                case FaanPattern.ConcealedHand: return "門前清";
                case FaanPattern.LastTile: return "海底撈月";
                case FaanPattern.KongReplacement: return "槓上開花";
                case FaanPattern.RobbingKong: return "搶槓";
                case FaanPattern.NoFlowers: return "無花";
                case FaanPattern.OwnFlower: return "正花";
                case FaanPattern.OwnSeason: return "正季";
                case FaanPattern.FlowerSet: return "一台花";
                case FaanPattern.DragonPung: return "三元牌";
                case FaanPattern.SeatWindPung: return "門風";
                case FaanPattern.PrevailingWindPung: return "圈風";
                case FaanPattern.AllChows: return "平糊";
                case FaanPattern.AllPungs: return "對對糊";
                case FaanPattern.SmallThreeDragons: return "小三元";
                case FaanPattern.MixedOneSuit: return "混一色";
                case FaanPattern.PureOneSuit: return "清一色";
                case FaanPattern.SevenPairs: return "七對";
                case FaanPattern.BigThreeDragons: return "大三元";
                case FaanPattern.SmallFourWinds: return "小四喜";
                case FaanPattern.BigFourWinds: return "大四喜";
                case FaanPattern.AllHonors: return "字一色";
                case FaanPattern.AllTerminals: return "清么九";
                case FaanPattern.ThirteenOrphans: return "十三么";
                case FaanPattern.NineGates: return "九子連環";
                case FaanPattern.FourConcealedPungs: return "坑坑糊";
                case FaanPattern.FourKongs: return "十八羅漢";
                case FaanPattern.HeavenlyHand: return "天糊";
                case FaanPattern.EarthlyHand: return "地糊";
                case FaanPattern.EightFlowers: return "八仙過海";
                default: return p.ToString();
            }
        }
    }
}
