using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PawTown.EditorTools
{
    /// <summary>One-click browser build: PawTown > 5. Build Web (WebGL) writes a playable site to PawDelivery/WebBuild
    /// (gzip with a JS decompression fallback, so it runs on any static host such as GitHub Pages or itch.io), then
    /// switches the editor back to Android.</summary>
    public static class PawWebBuild
    {
        [MenuItem("PawTown/5. Build Web (WebGL)")]
        public static void Build()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../WebBuild"));
            PlayerSettings.productName = "Cat's Tale";
            PlayerSettings.WebGL.template = "PROJECT:CatsTale";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            ConfigureWebTextures();

            // the game is the PawTown demo scene; make it the (only) scene in the build list too
            const string scene = "Assets/PawTown/Scenes/PawTown_Demo.unity";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scene, true) };
            var scenes = new[] { scene };
            var opts = new BuildPlayerOptions { scenes = scenes, locationPathName = outDir, target = BuildTarget.WebGL, options = BuildOptions.None };
            BuildReport r = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[PawWebBuild] {r.summary.result}: {r.summary.totalErrors} errors, {r.summary.totalSize / (1024f * 1024f):0.0} MB, {r.summary.totalTime} -> {outDir}");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(outDir), "WebBuild_result.txt"), $"{r.summary.result}\n{r.summary.totalErrors} errors\n{r.summary.totalTime}\n");
        }

        /// <summary>WebGL-only import settings (Android/iOS untouched): crunched DXT5 so the download stays small, the
        /// full-screen paintings at 2K and everything else at 1K at most.</summary>
        [MenuItem("PawTown/5b. Configure Web Textures")]
        public static void ConfigureWebTextures()
        {
            int changed = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/PawTown" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    string name = Path.GetFileNameWithoutExtension(path);
                    bool big = name.StartsWith("bg_") || name == "htp_book";
                    int max = Mathf.Min(ti.maxTextureSize, big ? 2048 : 1024);
                    var ps = ti.GetPlatformTextureSettings("WebGL");
                    if (ps.overridden && ps.maxTextureSize == max && ps.format == TextureImporterFormat.DXT5Crunched) continue;
                    ps.overridden = true;
                    ps.maxTextureSize = max;
                    ps.format = TextureImporterFormat.DXT5Crunched;
                    ps.compressionQuality = 70;
                    ps.crunchedCompression = true;
                    ti.SetPlatformTextureSettings(ps);
                    ti.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[PawWebBuild] web texture overrides set on {changed} textures");
        }

        [MenuItem("PawTown/6. Back to Android")]
        public static void BackToAndroid() => EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
    }
}
