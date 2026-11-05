using UnrealBuildTool;

public class MahjongNightEditorTarget : TargetRules
{
	public MahjongNightEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.AddRange(new string[] { "MahjongNight", "MahjongCore" });
	}
}
