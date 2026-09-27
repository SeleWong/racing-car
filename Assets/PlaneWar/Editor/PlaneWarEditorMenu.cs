using System.IO;
using System.Linq;
using PlaneWar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PlaneWarEditor
{
    /// <summary>编辑器工具：一键创建场景 / 配置文件 / 各平台推荐设置。</summary>
    [InitializeOnLoad]
    public static class PlaneWarEditorMenu
    {
        private const string ScenePath = "Assets/PlaneWar/Scenes/Main.unity";
        private const string ConfigPath = "Assets/PlaneWar/Resources/PlaneWarConfig.asset";
        private const string FirstRunKey = "PlaneWar.FirstRunDone";

        static PlaneWarEditorMenu()
        {
            // 首次打开工程：自动生成主场景并加入 Build Settings，并切到竖屏
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(FirstRunKey + "." + Application.dataPath, false)) return;
                EditorPrefs.SetBool(FirstRunKey + "." + Application.dataPath, true);
                // 仅在全新工程（Build Settings 为空）时自动处理，避免改动已有项目
                if (EditorBuildSettings.scenes.Length > 0) return;
                if (!File.Exists(ScenePath)) CreateMainScene(false);
                ApplyPortraitSettings();
                ApplyPerformanceSettings();
            };
        }

        [MenuItem("PlaneWar/创建主场景并加入 Build Settings", priority = 0)]
        public static void CreateMainSceneMenu() { CreateMainScene(true); }

        public static void CreateMainScene(bool open)
        {
            if (EditorApplication.isPlaying) return;
            if (open && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, open ? NewSceneMode.Single : NewSceneMode.Additive);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.76f, 0.79f, 0.8f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            EditorSceneManager.MoveGameObjectToScene(camGo, scene);

            var gmGo = new GameObject("PlaneWar");
            var gm = gmGo.AddComponent<GameManager>();
            gm.config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            EditorSceneManager.MoveGameObjectToScene(gmGo, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!open) EditorSceneManager.CloseScene(scene, true);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.Refresh();
            Debug.Log("[PlaneWar] 主场景已创建：" + ScenePath);
        }

        [MenuItem("PlaneWar/创建配置文件 (Resources/PlaneWarConfig)", priority = 1)]
        public static void CreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (existing == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                existing = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(existing, ConfigPath);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
        }

        [MenuItem("PlaneWar/应用竖屏 & 移动端推荐设置", priority = 20)]
        public static void ApplyPortraitSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultWebScreenWidth = 540;
            PlayerSettings.defaultWebScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "racing-car")
                PlayerSettings.productName = "飞机大战";
            Debug.Log("[PlaneWar] 已应用竖屏设置（Android / iOS / WebGL / PC 窗口 540x960）");
        }

        [MenuItem("PlaneWar/应用性能推荐设置（增量 GC 等）", priority = 21)]
        public static void ApplyPerformanceSettings()
        {
#if UNITY_2019_1_OR_NEWER
            // 增量 GC：把一次长 GC 拆到多帧执行，消除 GC 尖峰卡顿（WebGL 不支持，会自动忽略）
            PlayerSettings.gcIncremental = true;
#endif
            // 移动端多线程渲染，降低主线程耗时
            PlayerSettings.MTRendering = true;
#if UNITY_2019_3_OR_NEWER
            // Android 帧节奏优化（Swappy），帧间隔更均匀
            PlayerSettings.Android.optimizedFramePacing = true;
#endif
            Debug.Log("[PlaneWar] 已应用性能推荐设置：增量 GC / 多线程渲染 / Android 帧节奏优化");
        }

        [MenuItem("PlaneWar/微信小游戏：添加 WEIXINMINIGAME 宏 (WebGL)", priority = 40)]
        public static void AddWeChatDefine() { SetDefine(BuildTargetGroup.WebGL, "WEIXINMINIGAME", true); }

        [MenuItem("PlaneWar/微信小游戏：移除 WEIXINMINIGAME 宏 (WebGL)", priority = 41)]
        public static void RemoveWeChatDefine() { SetDefine(BuildTargetGroup.WebGL, "WEIXINMINIGAME", false); }

        private static void SetDefine(BuildTargetGroup group, string define, bool enable)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(';').Where(d => !string.IsNullOrEmpty(d)).ToList();
            defines.Remove(define);
            if (enable) defines.Add(define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines.ToArray()));
            Debug.Log("[PlaneWar] " + group + " Scripting Define Symbols: " + string.Join(";", defines.ToArray()));
        }
    }
}
