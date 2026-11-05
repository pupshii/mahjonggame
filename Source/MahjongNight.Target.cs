using UnrealBuildTool;

public class MahjongNightTarget : TargetRules
{
	public MahjongNightTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.AddRange(new string[] { "MahjongNight", "MahjongCore" });
	}
}
