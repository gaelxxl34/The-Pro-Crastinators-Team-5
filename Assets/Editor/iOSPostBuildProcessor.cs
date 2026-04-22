using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using System.IO;

public class iOSPostBuildProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
    {
        if (buildTarget != BuildTarget.iOS) return;

        string pbxPath = PBXProject.GetPBXProjectPath(buildPath);
        PBXProject project = new PBXProject();
        project.ReadFromFile(pbxPath);

        // Parse Unity version into VER/MAJ/MIN integers
        // e.g. "6000.3.4f1" -> VER=6000, MAJ=3, MIN=4
        string unityVer = Application.unityVersion; // e.g. "6000.3.4f1"
        int ver = 0, maj = 0, min = 0;
        try
        {
            string[] parts = unityVer.Split('.');
            if (parts.Length >= 1) int.TryParse(parts[0], out ver);
            if (parts.Length >= 2) int.TryParse(parts[1], out maj);
            if (parts.Length >= 3)
            {
                string minStr = parts[2];
                // strip trailing letters like "4f1" -> "4"
                int idx = 0;
                while (idx < minStr.Length && char.IsDigit(minStr[idx])) idx++;
                int.TryParse(minStr.Substring(0, idx), out min);
            }
        }
        catch { ver = 6000; maj = 3; min = 4; }

        // Apply to all relevant targets
        string[] targetGuids = new string[]
        {
            project.GetUnityMainTargetGuid(),
            project.GetUnityFrameworkTargetGuid()
        };

        foreach (string guid in targetGuids)
        {
            // Inject UNITY_VERSION macros required by UnityTrampolineConfiguration.h
            // (missing when building onto a stale/appended Xcode project)
            string versionMacros = $"UNITY_VERSION_VER={ver} UNITY_VERSION_MAJ={maj} UNITY_VERSION_MIN={min}";
            project.UpdateBuildProperty(guid, "GCC_PREPROCESSOR_DEFINITIONS", new string[] { $"UNITY_VERSION_VER={ver}", $"UNITY_VERSION_MAJ={maj}", $"UNITY_VERSION_MIN={min}" }, new string[] {});

            // Suppress -Wnonnull warnings that IL2CPP triggers in StringUtils.cpp
            project.AddBuildProperty(guid, "OTHER_CFLAGS", "-Wno-nonnull");
            project.AddBuildProperty(guid, "OTHER_CPLUSPLUSFLAGS", "-Wno-nonnull");

            // Prevent warnings being promoted to build-breaking errors
            project.SetBuildProperty(guid, "GCC_TREAT_WARNINGS_AS_ERRORS", "NO");
        }

        project.WriteToFile(pbxPath);
        Debug.Log($"[iOSPostBuild] Injected Unity {ver}.{maj}.{min} macros + -Wno-nonnull into Xcode project.");

        // Remove nlp_env from the iOS app bundle.
        // It contains absolute symlinks which iOS rejects during installation.
        // Python cannot run on iOS anyway so the folder should never be bundled.
        string nlpEnvPath = Path.Combine(buildPath, "Data", "Raw", "nlp_env");
        if (Directory.Exists(nlpEnvPath))
        {
            Directory.Delete(nlpEnvPath, true);
            Debug.Log("[iOSPostBuild] Removed nlp_env from iOS build (absolute symlinks not allowed on iOS).");
        }
    }
}
#endif
