using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Aquaring.EditorTools
{
    /// <summary>
    /// Configures the project for an Android portrait build. Menu:
    /// <c>Aquaring ▸ Configure Mobile (Android Portrait)</c>.
    /// Safe to run repeatedly.
    /// </summary>
    public static class MobileBuildConfigurator
    {
        [MenuItem("Aquaring/Configure Mobile (Android Portrait)", priority = 20)]
        public static void Configure()
        {
            // --- orientation: locked portrait ---
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // --- identity ---
            PlayerSettings.companyName = "Aquaring";
            PlayerSettings.productName = "Aquaring";
            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android, "com.aquaring.prototype");

            // --- Android specifics ---
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // --- rendering / frame pacing for a phone ---
            // vSync off so Application.targetFrameRate (set at runtime by AppBootstrap) takes effect.
            QualitySettings.vSyncCount = 0;

            // --- switch the active build target so Play/Profiler match the phone ---
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                bool ok = EditorUtility.DisplayDialog("Aquaring",
                    "Switch the active build target to Android now?\n" +
                    "(Needed for an accurate mobile test; can take a minute the first time.)",
                    "Switch to Android", "Keep current");
                if (ok)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Android, BuildTarget.Android);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#39c>Aquaring</color>: mobile settings applied (Android, portrait-locked, IL2CPP, ARMv7+ARM64, vSync off).");
        }
    }
}
