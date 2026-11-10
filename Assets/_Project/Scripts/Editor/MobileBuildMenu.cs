using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MahjongNight.EditorTools
{
    /// <summary>
    /// Menu items for getting the game onto an iPhone:
    /// <b>Mahjong Night → iOS → 1. Set Up for iPhone</b> configures the project (landscape,
    /// full screen, bundle id, iOS 15+, automatic signing) and switches the build target to iOS;
    /// <b>2. Build Xcode Project</b> builds it into Builds/iOS, ready to open in Xcode and run
    /// on your phone. Needs the "iOS Build Support" module (Unity Hub → Installs → Add modules).
    /// </summary>
    public static class MobileBuildMenu
    {
        private const string ScenePath = "Assets/_Project/Scenes/Greybox.unity";
        private const string DefaultBundleId = "com.pupshii.mahjongnight";
        private const string IosFolder = "Builds/iOS";

        private static bool IosModuleInstalled => BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS);

        [MenuItem("Mahjong Night/iOS/1. Set Up for iPhone")]
        public static void SetUpForIphone()
        {
            if (!CheckReady())
                return;
            Configure();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
            EditorUtility.DisplayDialog("Ready for iPhone",
                "The project is set up for iOS (landscape, full screen, touch controls on).\n\n" +
                $"Bundle ID: {PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS)}\n" +
                "(Change it in Project Settings → Player → iOS if Xcode says it's taken.)\n\n" +
                "Next: Mahjong Night → iOS → 2. Build Xcode Project.\n" +
                "Commit the changed ProjectSettings files.", "OK");
        }

        [MenuItem("Mahjong Night/iOS/2. Build Xcode Project")]
        public static void BuildXcodeProject()
        {
            if (!CheckReady())
                return;
            Configure();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = IosFolder,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog("iOS build failed", $"The build {report.summary.result}. See the Console for the errors.", "OK");
                return;
            }
            EditorUtility.RevealInFinder(IosFolder);
            EditorUtility.DisplayDialog("Xcode project built",
                $"Built into {IosFolder} (it just opened in Finder).\n\n" +
                "1. Open Unity-iPhone.xcodeproj in Xcode.\n" +
                "2. Click Unity-iPhone (top of the left list) → Signing & Capabilities → Team: pick your Apple ID " +
                "(Xcode → Settings → Accounts to add it).\n" +
                "3. Plug in your iPhone, pick it at the top of Xcode, press ▶ Run.\n" +
                "4. First time only, on the phone: Settings → General → VPN & Device Management → trust your Apple ID; " +
                "and Settings → Privacy & Security → Developer Mode → on.\n\n" +
                "With a free Apple ID the app stops opening after 7 days — just Run it from Xcode again.", "OK");
        }

        private static bool CheckReady()
        {
            if (!IosModuleInstalled)
            {
                EditorUtility.DisplayDialog("iOS Build Support isn't installed",
                    "Close Unity, open Unity Hub → Installs → your Unity 6 version → ⚙ → Add modules → tick " +
                    "\"iOS Build Support\" → Install. Then reopen the project and run this again.", "OK");
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog("No greybox scene yet",
                    "Create it first: Mahjong Night → Create Greybox Scene.", "OK");
                return false;
            }
            return true;
        }

        /// <summary>Player settings for a landscape, full-screen phone game, and the scene in the build.</summary>
        private static void Configure()
        {
            PlayerSettings.productName = "Mahjong Night";
            if (PlayerSettings.companyName == "DefaultCompany")
                PlayerSettings.companyName = "pupshii";
            string id = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);
            if (string.IsNullOrEmpty(id) || id.StartsWith("com.DefaultCompany") || id.StartsWith("com.Company") || id.Contains("mahjonggame"))
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, DefaultBundleId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.statusBarHidden = true;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.requiresFullScreen = true;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
