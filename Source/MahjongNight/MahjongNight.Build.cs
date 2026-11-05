using UnrealBuildTool;

// Game module: Unreal gameplay framework (GameMode, GameState, pawns, table, UI glue).
// All mahjong rules live in MahjongCore; this module only drives and presents them.
public class MahjongNight : ModuleRules
{
	public MahjongNight(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"MahjongCore"
		});
	}
}
