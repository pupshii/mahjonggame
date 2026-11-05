using UnrealBuildTool;

// Pure rules module: tiles, wall, turns, claims, win detection, faan, party rules.
// Rule: depend on Core only. No UObjects, no Engine, no networking — so every rule
// can be unit-tested headlessly and runs identically on the host for every match.
public class MahjongCore : ModuleRules
{
	public MahjongCore(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core"
		});
	}
}
