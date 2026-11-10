using System;

namespace MahjongNight.Core
{
    /// <summary>Every hand on the HK-style Taiwanese sheet that the game can recognise.</summary>
    public enum TwHand
    {
        // Easy
        SelfDrawn, ExposedKong, ConcealedKong, NoFlowers, Flower, OwnFlower, NoHonours, ElephantEyes,
        TwoPairWait, FalseSingleWait, MixedChows2, DealerWin,
        WindPung, RoundWindPung, RobbingKong, SingleWait, DragonPung, FullyConcealed, NoHonoursNoFlowers,
        IdenticalChows2, ConcealedPungs2, TwoSisters, TwoBrothers, FourInOne, OldAndYoung, PingWu,
        MissingOneSuit, AllSimples,
        // Medium
        BigPingWu, FourInTwo, ConcealedSelfDraw, SmallThreeSisters, ExposedMixedDragon, HalfConcealed,
        ChickenHand, MixedOutside, ExposedDragon, FiveGates, MixedChows3, SmallThreeBrothers,
        SmallThreeDragons, SmallThreeWinds, ConcealedPungs3, AllByOthers,
        // Hard
        OneFlowerSet, ConcealedMixedDragon, PureOutside, IdenticalChows3, FourInFour, ThreeSisters,
        BigThreeBrothers, ConcealedDragon, SmallFourSisters, FourSameNumberChows, MixedTerminals,
        MixedOneSuit, ConcealedPungs4, IdenticalChows4, AllPungs, BigThreeDragons, BigThreeWinds,
        SixteenUnrelated, SixteenUnrelatedPure, LikGuSingleWait, LikGuEightPairs,
        // Very hard
        FourSisters, FiveSameNumberChows, SmallFiveSisters, SmallFourWinds, FullFlush, PureTerminals,
        ConcealedPungs5, FiveSisters, BigFourWinds, AllHonours, ConcealedAllPungs,
        // Luck & timing
        FlowerReplacement, KongReplacement, Within10Discards, Within7Discards, LastTileSelfDraw,
        Within4Discards, EarthlyWin, HeavenlyWin,
    }

    /// <summary>
    /// Fan for each hand on the HK-style Taiwanese sheet (HK_Taiwanese_Mahjong_By_Difficulty).
    /// "House" values default to 0 (off) — set them to your table's numbers. Add up every hand
    /// that applies; there's no cap.
    /// </summary>
    public sealed class TaiwaneseTable
    {
        private static readonly int Count = Enum.GetValues(typeof(TwHand)).Length;
        private readonly int[] fan = new int[Count];

        public int this[TwHand hand]
        {
            get => fan[(int)hand];
            set => fan[(int)hand] = value;
        }

        /// <summary>Each further consecutive dealer win adds this much more (1, then +3, +5, +7…).</summary>
        public int DealerStreakStep { get; set; } = 2;

        public static TaiwaneseTable Default()
        {
            var t = new TaiwaneseTable();
            void Set(TwHand h, int v) => t[h] = v;
            // Easy
            Set(TwHand.SelfDrawn, 1); Set(TwHand.ExposedKong, 1); Set(TwHand.ConcealedKong, 2);
            Set(TwHand.NoFlowers, 1); Set(TwHand.Flower, 1); Set(TwHand.OwnFlower, 2);
            Set(TwHand.NoHonours, 1); Set(TwHand.ElephantEyes, 1); Set(TwHand.TwoPairWait, 1);
            Set(TwHand.FalseSingleWait, 1); Set(TwHand.MixedChows2, 1); Set(TwHand.DealerWin, 1);
            Set(TwHand.WindPung, 1); Set(TwHand.RoundWindPung, 2); Set(TwHand.RobbingKong, 1);
            Set(TwHand.SingleWait, 2); Set(TwHand.DragonPung, 2); Set(TwHand.FullyConcealed, 3);
            Set(TwHand.NoHonoursNoFlowers, 3); Set(TwHand.IdenticalChows2, 3); Set(TwHand.ConcealedPungs2, 3);
            Set(TwHand.TwoSisters, 3); Set(TwHand.TwoBrothers, 3); Set(TwHand.FourInOne, 3);
            Set(TwHand.OldAndYoung, 0);                         // house rules
            Set(TwHand.PingWu, 5); Set(TwHand.MissingOneSuit, 5); Set(TwHand.AllSimples, 5);
            // Medium
            Set(TwHand.BigPingWu, 0);                           // house rules (Ping Wu + No Honours & No Flowers still add up to 8)
            Set(TwHand.FourInTwo, 5); Set(TwHand.ConcealedSelfDraw, 5); Set(TwHand.SmallThreeSisters, 8);
            Set(TwHand.ExposedMixedDragon, 8); Set(TwHand.HalfConcealed, 8); Set(TwHand.ChickenHand, 10);
            Set(TwHand.MixedOutside, 10); Set(TwHand.ExposedDragon, 10); Set(TwHand.FiveGates, 10);
            Set(TwHand.MixedChows3, 10); Set(TwHand.SmallThreeBrothers, 10); Set(TwHand.SmallThreeDragons, 10);
            Set(TwHand.SmallThreeWinds, 10); Set(TwHand.ConcealedPungs3, 10); Set(TwHand.AllByOthers, 15);
            // Hard
            Set(TwHand.OneFlowerSet, 10); Set(TwHand.ConcealedMixedDragon, 15); Set(TwHand.PureOutside, 15);
            Set(TwHand.IdenticalChows3, 15); Set(TwHand.FourInFour, 15); Set(TwHand.ThreeSisters, 15);
            Set(TwHand.BigThreeBrothers, 15); Set(TwHand.ConcealedDragon, 20); Set(TwHand.SmallFourSisters, 20);
            Set(TwHand.FourSameNumberChows, 20); Set(TwHand.MixedTerminals, 30); Set(TwHand.MixedOneSuit, 30);
            Set(TwHand.ConcealedPungs4, 30); Set(TwHand.IdenticalChows4, 30); Set(TwHand.AllPungs, 40);
            Set(TwHand.BigThreeDragons, 40); Set(TwHand.BigThreeWinds, 40); Set(TwHand.SixteenUnrelated, 40);
            Set(TwHand.SixteenUnrelatedPure, 50); Set(TwHand.LikGuSingleWait, 40); Set(TwHand.LikGuEightPairs, 50);
            // Very hard
            Set(TwHand.FourSisters, 40); Set(TwHand.FiveSameNumberChows, 40); Set(TwHand.SmallFiveSisters, 60);
            Set(TwHand.SmallFourWinds, 60); Set(TwHand.FullFlush, 80); Set(TwHand.PureTerminals, 80);
            Set(TwHand.ConcealedPungs5, 80); Set(TwHand.FiveSisters, 80); Set(TwHand.BigFourWinds, 100);
            Set(TwHand.AllHonours, 100); Set(TwHand.ConcealedAllPungs, 100);
            // Luck & timing
            Set(TwHand.FlowerReplacement, 1); Set(TwHand.KongReplacement, 1); Set(TwHand.Within10Discards, 10);
            Set(TwHand.Within7Discards, 20); Set(TwHand.LastTileSelfDraw, 20); Set(TwHand.Within4Discards, 60);
            Set(TwHand.EarthlyWin, 80); Set(TwHand.HeavenlyWin, 120);
            return t;
        }

        public static string English(TwHand h)
        {
            switch (h)
            {
                case TwHand.SelfDrawn: return "Self-drawn";
                case TwHand.ExposedKong: return "Kong";
                case TwHand.ConcealedKong: return "Concealed kong";
                case TwHand.NoFlowers: return "No flowers";
                case TwHand.Flower: return "Flower";
                case TwHand.OwnFlower: return "Own flower";
                case TwHand.NoHonours: return "No honours";
                case TwHand.ElephantEyes: return "Elephant eyes";
                case TwHand.TwoPairWait: return "Two-pair wait";
                case TwHand.FalseSingleWait: return "False single wait";
                case TwHand.MixedChows2: return "Mixed chows ×2";
                case TwHand.DealerWin: return "Dealer win";
                case TwHand.WindPung: return "Wind pung";
                case TwHand.RoundWindPung: return "Round wind pung";
                case TwHand.RobbingKong: return "Robbing a kong";
                case TwHand.SingleWait: return "Single wait";
                case TwHand.DragonPung: return "Dragon pung";
                case TwHand.FullyConcealed: return "Fully concealed";
                case TwHand.NoHonoursNoFlowers: return "No honours & no flowers";
                case TwHand.IdenticalChows2: return "Identical chows ×2";
                case TwHand.ConcealedPungs2: return "2 concealed pungs";
                case TwHand.TwoSisters: return "Two sisters";
                case TwHand.TwoBrothers: return "Two brothers";
                case TwHand.FourInOne: return "Four-in-one";
                case TwHand.OldAndYoung: return "Old & young";
                case TwHand.PingWu: return "All chows (Ping Wu)";
                case TwHand.MissingOneSuit: return "Missing one suit";
                case TwHand.AllSimples: return "All simples";
                case TwHand.BigPingWu: return "Big Ping Wu";
                case TwHand.FourInTwo: return "Four-in-two";
                case TwHand.ConcealedSelfDraw: return "Concealed self-draw";
                case TwHand.SmallThreeSisters: return "Small three sisters";
                case TwHand.ExposedMixedDragon: return "Exposed mixed dragon";
                case TwHand.HalfConcealed: return "Half concealed (Ban Qiu Ren)";
                case TwHand.ChickenHand: return "Chicken hand";
                case TwHand.MixedOutside: return "Mixed outside hand";
                case TwHand.ExposedDragon: return "Exposed dragon";
                case TwHand.FiveGates: return "Five gates";
                case TwHand.MixedChows3: return "Mixed chows ×3";
                case TwHand.SmallThreeBrothers: return "Small three brothers";
                case TwHand.SmallThreeDragons: return "Small three dragons";
                case TwHand.SmallThreeWinds: return "Small three winds";
                case TwHand.ConcealedPungs3: return "3 concealed pungs";
                case TwHand.AllByOthers: return "All by others";
                case TwHand.OneFlowerSet: return "One flower set";
                case TwHand.ConcealedMixedDragon: return "Concealed mixed dragon";
                case TwHand.PureOutside: return "Pure outside hand";
                case TwHand.IdenticalChows3: return "Identical chows ×3";
                case TwHand.FourInFour: return "Four-in-four";
                case TwHand.ThreeSisters: return "Three sisters";
                case TwHand.BigThreeBrothers: return "Big three brothers";
                case TwHand.ConcealedDragon: return "Concealed dragon";
                case TwHand.SmallFourSisters: return "Small four sisters";
                case TwHand.FourSameNumberChows: return "Four same-number chows";
                case TwHand.MixedTerminals: return "Mixed terminals";
                case TwHand.MixedOneSuit: return "Mixed one suit";
                case TwHand.ConcealedPungs4: return "4 concealed pungs";
                case TwHand.IdenticalChows4: return "Identical chows ×4";
                case TwHand.AllPungs: return "All pungs";
                case TwHand.BigThreeDragons: return "Big three dragons";
                case TwHand.BigThreeWinds: return "Big three winds";
                case TwHand.SixteenUnrelated: return "Sixteen unrelated";
                case TwHand.SixteenUnrelatedPure: return "Sixteen unrelated (pure)";
                case TwHand.LikGuSingleWait: return "Lik Gu Lik Gu";
                case TwHand.LikGuEightPairs: return "Lik Gu Lik Gu (8-pair wait)";
                case TwHand.FourSisters: return "Four sisters";
                case TwHand.FiveSameNumberChows: return "Five same-number chows";
                case TwHand.SmallFiveSisters: return "Small five sisters";
                case TwHand.SmallFourWinds: return "Small four winds";
                case TwHand.FullFlush: return "Full flush";
                case TwHand.PureTerminals: return "Pure terminals";
                case TwHand.ConcealedPungs5: return "5 concealed pungs";
                case TwHand.FiveSisters: return "Five sisters";
                case TwHand.BigFourWinds: return "Big four winds";
                case TwHand.AllHonours: return "All honours";
                case TwHand.ConcealedAllPungs: return "Concealed all pungs";
                case TwHand.FlowerReplacement: return "Win on flower replacement";
                case TwHand.KongReplacement: return "Win on kong replacement";
                case TwHand.Within10Discards: return "Within 10 discards";
                case TwHand.Within7Discards: return "Within 7 discards";
                case TwHand.LastTileSelfDraw: return "Last tile self-draw";
                case TwHand.Within4Discards: return "Within 4 discards (Human win)";
                case TwHand.EarthlyWin: return "Earthly win";
                case TwHand.HeavenlyWin: return "Heavenly win";
                default: return h.ToString();
            }
        }

        public static string Chinese(TwHand h)
        {
            switch (h)
            {
                case TwHand.SelfDrawn: return "自摸";
                case TwHand.ExposedKong: return "明槓";
                case TwHand.ConcealedKong: return "暗槓";
                case TwHand.NoFlowers: return "無花";
                case TwHand.Flower: return "花";
                case TwHand.OwnFlower: return "正花";
                case TwHand.NoHonours: return "無字";
                case TwHand.ElephantEyes: return "二五八將";
                case TwHand.TwoPairWait: return "對碰";
                case TwHand.FalseSingleWait: return "假獨";
                case TwHand.MixedChows2: return "二相逢";
                case TwHand.DealerWin: return "莊家";
                case TwHand.WindPung: return "風刻";
                case TwHand.RoundWindPung: return "圈風";
                case TwHand.RobbingKong: return "搶槓";
                case TwHand.SingleWait: return "獨聽";
                case TwHand.DragonPung: return "三元刻";
                case TwHand.FullyConcealed: return "門清";
                case TwHand.NoHonoursNoFlowers: return "無字無花";
                case TwHand.IdenticalChows2: return "一般高";
                case TwHand.ConcealedPungs2: return "二暗刻";
                case TwHand.TwoSisters: return "二姊妹";
                case TwHand.TwoBrothers: return "二兄弟";
                case TwHand.FourInOne: return "四歸一";
                case TwHand.OldAndYoung: return "老少";
                case TwHand.PingWu: return "平糊";
                case TwHand.MissingOneSuit: return "缺一門";
                case TwHand.AllSimples: return "斷么";
                case TwHand.BigPingWu: return "大平糊";
                case TwHand.FourInTwo: return "四歸二";
                case TwHand.ConcealedSelfDraw: return "門清自摸";
                case TwHand.SmallThreeSisters: return "小三姊妹";
                case TwHand.ExposedMixedDragon: return "明雜龍";
                case TwHand.HalfConcealed: return "半求人";
                case TwHand.ChickenHand: return "雞糊";
                case TwHand.MixedOutside: return "混全帶么";
                case TwHand.ExposedDragon: return "明龍";
                case TwHand.FiveGates: return "五門齊";
                case TwHand.MixedChows3: return "三相逢";
                case TwHand.SmallThreeBrothers: return "小三兄弟";
                case TwHand.SmallThreeDragons: return "小三元";
                case TwHand.SmallThreeWinds: return "小三風";
                case TwHand.ConcealedPungs3: return "三暗刻";
                case TwHand.AllByOthers: return "全求人";
                case TwHand.OneFlowerSet: return "一台花";
                case TwHand.ConcealedMixedDragon: return "暗雜龍";
                case TwHand.PureOutside: return "純全帶么";
                case TwHand.IdenticalChows3: return "一色三同順";
                case TwHand.FourInFour: return "四歸四";
                case TwHand.ThreeSisters: return "三姊妹";
                case TwHand.BigThreeBrothers: return "大三兄弟";
                case TwHand.ConcealedDragon: return "暗龍";
                case TwHand.SmallFourSisters: return "小四姊妹";
                case TwHand.FourSameNumberChows: return "四同順";
                case TwHand.MixedTerminals: return "混么九";
                case TwHand.MixedOneSuit: return "混一色";
                case TwHand.ConcealedPungs4: return "四暗刻";
                case TwHand.IdenticalChows4: return "一色四同順";
                case TwHand.AllPungs: return "對對糊";
                case TwHand.BigThreeDragons: return "大三元";
                case TwHand.BigThreeWinds: return "大三風";
                case TwHand.SixteenUnrelated: return "十六不搭";
                case TwHand.SixteenUnrelatedPure: return "純十六不搭";
                case TwHand.LikGuSingleWait: return "嚦咕嚦咕";
                case TwHand.LikGuEightPairs: return "嚦咕嚦咕 (八對)";
                case TwHand.FourSisters: return "四姊妹";
                case TwHand.FiveSameNumberChows: return "五同順";
                case TwHand.SmallFiveSisters: return "小五姊妹";
                case TwHand.SmallFourWinds: return "小四喜";
                case TwHand.FullFlush: return "清一色";
                case TwHand.PureTerminals: return "清么九";
                case TwHand.ConcealedPungs5: return "五暗刻";
                case TwHand.FiveSisters: return "五姊妹";
                case TwHand.BigFourWinds: return "大四喜";
                case TwHand.AllHonours: return "字一色";
                case TwHand.ConcealedAllPungs: return "坎坎糊";
                case TwHand.FlowerReplacement: return "花上自摸";
                case TwHand.KongReplacement: return "槓上開花";
                case TwHand.Within10Discards: return "十隻內";
                case TwHand.Within7Discards: return "七隻內";
                case TwHand.LastTileSelfDraw: return "海底撈月";
                case TwHand.Within4Discards: return "人糊";
                case TwHand.EarthlyWin: return "地糊";
                case TwHand.HeavenlyWin: return "天糊";
                default: return "";
            }
        }
    }
}
