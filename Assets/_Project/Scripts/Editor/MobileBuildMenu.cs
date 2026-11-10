using System;
using System.Collections.Generic;
using System.IO;
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
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("iOS build failed", ModuleHelp + "\n\n(" + e.Message + ")", "OK");
                return;
            }
            // Only call it built if the Xcode project is really there.
            string xcodeProject = Path.Combine(IosFolder, "Unity-iPhone.xcodeproj");
            if (report == null || report.summary.result != BuildResult.Succeeded || !Directory.Exists(xcodeProject))
            {
                string result = report == null ? "didn't run" : report.summary.result.ToString();
                EditorUtility.DisplayDialog("iOS build failed",
                    $"The build {result} and there's no {xcodeProject}.\n\n" +
                    "If the Console says \"Build target 'iOS' not supported\": " + ModuleHelp, "OK");
                return;
            }
            Debug.Log("Xcode project: " + Path.GetFullPath(xcodeProject));
            EditorUtility.RevealInFinder(IosFolder);
            EditorUtility.DisplayDialog("Xcode project built",
                $"Built: {Path.GetFullPath(xcodeProject)}\n\n" +
                "1. Open Unity-iPhone.xcodeproj in Xcode.\n" +
                "2. Click Unity-iPhone (top of the left list) → Signing & Capabilities → Team: pick your Apple ID " +
                "(Xcode → Settings → Accounts to add it).\n" +
                "3. Plug in your iPhone, pick it at the top of Xcode, press ▶ Run.\n" +
                "4. First time, on the phone: Developer Mode on, and trust your Apple ID.", "OK");
        }

        private static string ModuleHelp =>
            $"the iOS module isn't loaded. Quit Unity, open Unity Hub → Installs → {Application.unityVersion} " +
            "(this exact version) → ⚙ → Add modules → tick \"iOS Build Support\" → Install. Then reopen the project " +
            "(a restart is needed after installing a module) and run this again.";

        /// <summary>Adds shaders to Project Settings → Graphics → Always Included Shaders.</summary>
        private static void AlwaysInclude(params string[] shaderNames)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
                return;
            var settings = new SerializedObject(assets[0]);
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in shaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                    continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                        present = true;
                if (present)
                    continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            settings.ApplyModifiedProperties();
        }

        /// <summary>Removes shaders from Always Included Shaders (e.g. ones an earlier version of this menu added).</summary>
        private static void NeverAlwaysInclude(params string[] shaderNames)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0)
                return;
            var settings = new SerializedObject(assets[0]);
            SerializedProperty list = settings.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in shaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null)
                    continue;
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != shader)
                        continue;
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;   // clear first, then remove the slot
                    list.DeleteArrayElementAtIndex(i);
                }
            }
            settings.ApplyModifiedProperties();
        }

        private static bool CheckReady()
        {
            if (!IosModuleInstalled)
            {
                EditorUtility.DisplayDialog("iOS Build Support isn't installed", "It looks like " + ModuleHelp, "OK");
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

            // The game builds everything in code at runtime (primitives, colliders, text, audio,
            // materials by shader name) — don't let the build strip what it can't see being used.
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Minimal);
            AlwaysInclude("Legacy Shaders/Diffuse", "GUI/Text Shader", "Unlit/Color");
            NeverAlwaysInclude("Standard");   // all its variants would be compiled — huge, slow builds that can crash

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
