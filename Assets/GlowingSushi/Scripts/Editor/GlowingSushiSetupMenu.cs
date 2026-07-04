using GlowingSushi.Domain;
using GlowingSushi.Root;
using GlowingSushi.View;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace GlowingSushi.Editor
{
    /// <summary>
    /// Phase 1のマテリアル・プレハブ・シーン・プロジェクト設定を生成するエディタメニュー。
    /// YAMLの手書きを避け、AssetDatabase/PrefabUtility経由で参照を確実に結線する。
    /// メニューは 1→2→3 の順に実行すること。
    /// </summary>
    public static class GlowingSushiSetupMenu
    {
        const string FbxPath = "Assets/GlowingSushi/Models/J-food04/sushi02.fbx";
        const string ColorTexPath = "Assets/GlowingSushi/Models/J-food04/sushi02_color.jpg";
        const string NormalTexPath = "Assets/GlowingSushi/Models/J-food04/sushi02_nor.jpg";
        const string MaterialPath = "Assets/GlowingSushi/Materials/SushiEmissive.mat";
        const string PrefabFolder = "Assets/GlowingSushi/Prefabs";
        const string ObsoletePrefabPath = "Assets/GlowingSushi/Prefabs/Sushi.prefab";
        const string SettingsPath = "Assets/GlowingSushi/Settings/SushiBehaviorSettings.asset";

        /// <summary>
        /// sushi02モデル(盛り合わせ)から個別プレハブ化する寿司のノード名。
        /// ガリ(gari)は寿司ではないため除外する。
        /// </summary>
        static readonly string[] SushiPieceNames = { "maguro", "Salmon_", "ebi1", "ebi2", "negitoro", "engawa" };
        const string ScenePath = "Assets/GlowingSushi/Scenes/Main.unity";
        const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";

        /// <summary>寿司モデルの最長辺をこのサイズ(メートル)に合わせる</summary>
        const float TargetModelSize = 0.15f;

        // ------------------------------------------------------------
        // 1. アセット生成(マテリアル・プレハブ・挙動設定)
        // ------------------------------------------------------------
        [MenuItem("GlowingSushi/Setup/1. アセット生成(マテリアル・プレハブ・設定)")]
        public static void CreateAssets()
        {
            EnsureFolder("Assets/GlowingSushi", "Materials");
            EnsureFolder("Assets/GlowingSushi", "Prefabs");
            EnsureFolder("Assets/GlowingSushi", "Settings");

            EnsureNormalMapImport();
            var material = CreateEmissiveMaterial();
            CreateSushiPiecePrefabs(material);
            CreateBehaviorSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[GlowingSushi] アセット生成が完了しました。次に「2. シーンセットアップ」を実行してください。");
        }

        /// <summary>ノーマルマップのインポート設定をNormalMapタイプへ修正する</summary>
        static void EnsureNormalMapImport()
        {
            if (AssetImporter.GetAtPath(NormalTexPath) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        /// <summary>URP LitのEmission有効マテリアルを生成する</summary>
        static Material CreateEmissiveMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);

            var colorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ColorTexPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalTexPath);
            material.SetTexture("_BaseMap", colorTex);
            if (normalTex != null)
            {
                material.SetTexture("_BumpMap", normalTex);
                material.EnableKeyword("_NORMALMAP");
            }
            material.SetFloat("_Smoothness", 0.4f);

            // 発光: HDRの暖色。実行時はSushiViewがMaterialPropertyBlockで強度を上書きする
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 2f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>
        /// 盛り合わせモデルから寿司1貫ごとの個別プレハブを生成する。
        /// 各プレハブは対象のメッシュのみを持ち、中心を原点に合わせて泳ぐ向きの回転に耐える形にする。
        /// </summary>
        static void CreateSushiPiecePrefabs(Material material)
        {
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"[GlowingSushi] 寿司モデルが見つかりません: {FbxPath}");
                return;
            }

            // 盛り合わせ丸ごとの旧プレハブが残っていれば削除する
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ObsoletePrefabPath) != null)
            {
                AssetDatabase.DeleteAsset(ObsoletePrefabPath);
            }

            foreach (var pieceName in SushiPieceNames)
            {
                var prefabPath = $"{PrefabFolder}/Sushi_{pieceName.TrimEnd('_')}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;
                CreateSinglePiecePrefab(modelPrefab, pieceName, prefabPath, material);
            }
        }

        /// <summary>指定した名前のノードだけを取り出した1貫分のプレハブを生成する</summary>
        static void CreateSinglePiecePrefab(GameObject modelPrefab, string pieceName, string prefabPath, Material material)
        {
            var root = new GameObject($"Sushi_{pieceName.TrimEnd('_')}");
            try
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                // 子の組み替え・削除を行うため、プレハブ接続を完全に解除する
                PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                Transform piece = null;
                foreach (var child in modelInstance.GetComponentsInChildren<Transform>())
                {
                    if (child.name == pieceName)
                    {
                        piece = child;
                        break;
                    }
                }
                if (piece == null)
                {
                    Debug.LogError($"[GlowingSushi] モデル内にノード '{pieceName}' が見つかりません");
                    Object.DestroyImmediate(modelInstance);
                    return;
                }

                var renderer = piece.GetComponentInChildren<Renderer>();
                if (renderer == null)
                {
                    Debug.LogError($"[GlowingSushi] ノード '{pieceName}' にRendererがありません");
                    Object.DestroyImmediate(modelInstance);
                    return;
                }

                // 対象の1貫だけをルート直下へ移し、残り(他の寿司・ガリ)は破棄する
                piece.SetParent(root.transform, true);
                Object.DestroyImmediate(modelInstance);

                // 最長辺がTargetModelSizeになるようスケール調整
                var size = renderer.bounds.size;
                var maxDimension = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (maxDimension > 1e-5f)
                {
                    piece.localScale *= TargetModelSize / maxDimension;
                }

                // メッシュの中心をルート原点へ合わせる(回転時にその場で回るように)
                piece.position -= renderer.bounds.center;

                // 全マテリアルスロットを発光マテリアルへ差し替える
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;

                var view = root.AddComponent<SushiView>();
                var so = new SerializedObject(view);
                so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>挙動設定のScriptableObjectアセットを生成する</summary>
        static void CreateBehaviorSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<SushiBehaviorSettings>(SettingsPath) != null) return;
            var settings = ScriptableObject.CreateInstance<SushiBehaviorSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        // ------------------------------------------------------------
        // 2. シーンセットアップ(ARリグ・DIスコープ・群れView)
        // ------------------------------------------------------------
        [MenuItem("GlowingSushi/Setup/2. シーンセットアップ")]
        public static void SetupScene()
        {
            var prefabs = LoadSushiPiecePrefabs();
            var settings = AssetDatabase.LoadAssetAtPath<SushiBehaviorSettings>(SettingsPath);
            if (prefabs.Length == 0 || settings == null)
            {
                EditorUtility.DisplayDialog(
                    "GlowingSushi",
                    "寿司プレハブまたは設定アセットが見つかりません。先に「1. アセット生成」を実行してください。",
                    "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 既存のMain Camera(非ARカメラ)は削除してARリグに置き換える
            var oldCamera = GameObject.Find("Main Camera");
            if (oldCamera != null && oldCamera.GetComponentInParent<XROrigin>() == null)
            {
                Object.DestroyImmediate(oldCamera);
            }

            // --- AR Session ---
            if (Object.FindFirstObjectByType<ARSession>() == null)
            {
                var sessionGo = new GameObject("AR Session");
                sessionGo.AddComponent<ARSession>();
                sessionGo.AddComponent<ARInputManager>();
            }

            // --- XR Origin(カメラ+平面検出) ---
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                var originGo = new GameObject("XR Origin");
                var offsetGo = new GameObject("Camera Offset");
                offsetGo.transform.SetParent(originGo.transform, false);

                var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraGo.transform.SetParent(offsetGo.transform, false);

                var camera = cameraGo.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.1f;
                // BloomなどのポストプロセスをARカメラで有効化する
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

                cameraGo.AddComponent<ARCameraManager>();
                cameraGo.AddComponent<ARCameraBackground>();
                AddTrackedPoseDriver(cameraGo);

                origin = originGo.AddComponent<XROrigin>();
                origin.CameraFloorOffsetObject = offsetGo;
                origin.Camera = camera;

                originGo.AddComponent<ARPlaneManager>();
            }

            var planeManager = origin.GetComponent<ARPlaneManager>();
            var arCamera = origin.Camera;

            // --- 群れView ---
            var schoolView = Object.FindFirstObjectByType<SushiSchoolView>();
            if (schoolView == null)
            {
                schoolView = new GameObject("SushiSchoolView").AddComponent<SushiSchoolView>();
            }
            var schoolSo = new SerializedObject(schoolView);
            var prefabsProp = schoolSo.FindProperty("sushiPrefabs");
            prefabsProp.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
            schoolSo.ApplyModifiedPropertiesWithoutUndo();

            // --- DIスコープ ---
            var scope = Object.FindFirstObjectByType<GlowingSushiLifetimeScope>();
            if (scope == null)
            {
                scope = new GameObject("GlowingSushiLifetimeScope").AddComponent<GlowingSushiLifetimeScope>();
            }
            var scopeSo = new SerializedObject(scope);
            // 手動で結線済みの参照を誤って外さないよう、未設定の場合のみ書き込む
            var settingsProp = scopeSo.FindProperty("behaviorSettings");
            if (settingsProp.objectReferenceValue == null)
            {
                settingsProp.objectReferenceValue = settings;
            }
            scopeSo.FindProperty("planeManager").objectReferenceValue = planeManager;
            scopeSo.FindProperty("arCamera").objectReferenceValue = arCamera;
            scopeSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 結線結果を検証してログへ出す(失敗時の切り分け用)
            var wiredPrefabs = 0;
            for (var i = 0; i < prefabsProp.arraySize; i++)
            {
                if (prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue != null) wiredPrefabs++;
            }
            Debug.Log(
                $"[GlowingSushi] シーンセットアップ完了。結線状態: sushiPrefabs={wiredPrefabs}/{prefabs.Length}, " +
                $"behaviorSettings={(settingsProp.objectReferenceValue != null ? "OK" : "未設定")}, " +
                $"planeManager={(planeManager != null ? "OK" : "未設定")}, arCamera={(arCamera != null ? "OK" : "未設定")}");
        }

        /// <summary>ARカメラの姿勢追従用TrackedPoseDriverを追加する(実機+XR Simulation両対応のバインディング)</summary>
        static void AddTrackedPoseDriver(GameObject cameraGo)
        {
            var driver = cameraGo.AddComponent<TrackedPoseDriver>();

            var positionAction = new InputAction("Position", binding: "<HandheldARInputDevice>/devicePosition");
            positionAction.AddBinding("<XRHMD>/centerEyePosition");
            var rotationAction = new InputAction("Rotation", binding: "<HandheldARInputDevice>/deviceRotation");
            rotationAction.AddBinding("<XRHMD>/centerEyeRotation");

            driver.positionInput = new InputActionProperty(positionAction);
            driver.rotationInput = new InputActionProperty(rotationAction);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        }

        // ------------------------------------------------------------
        // 3. プロジェクト設定(URPレンダラー・iOS権限)
        // ------------------------------------------------------------
        [MenuItem("GlowingSushi/Setup/3. プロジェクト設定(ARレンダラー・iOS権限)")]
        public static void ConfigureProjectSettings()
        {
            // ARカメラ映像を背景描画するRendererFeatureを実機用・エディタ(XR Simulation)用の両方へ追加
            AddArBackgroundFeature(MobileRendererPath);
            AddArBackgroundFeature(PcRendererPath);

            // iOSのカメラ使用許可の説明文(未設定だと実機で起動時にクラッシュする)
            PlayerSettings.iOS.cameraUsageDescription = "ARで周囲を認識するためにカメラを使用します";

            AssetDatabase.SaveAssets();
            Debug.Log("[GlowingSushi] プロジェクト設定が完了しました。XR SimulationでPlayして動作確認できます。");
        }

        /// <summary>URPレンダラーへARBackgroundRendererFeatureを追加する(追加済みなら何もしない)</summary>
        static void AddArBackgroundFeature(string rendererDataPath)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererDataPath);
            if (rendererData == null)
            {
                Debug.LogError($"[GlowingSushi] レンダラーアセットが見つかりません: {rendererDataPath}");
                return;
            }

            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is ARBackgroundRendererFeature) return;
            }

            var newFeature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            newFeature.name = "ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(newFeature, rendererData);
            AssetDatabase.SaveAssets();

            // m_RendererFeatureMapにはサブアセットのローカルIDを記録する必要がある
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newFeature, out _, out long localId);

            var so = new SerializedObject(rendererData);
            var features = so.FindProperty("m_RendererFeatures");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = newFeature;
            var featureMap = so.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(rendererData);
        }

        /// <summary>生成済みの寿司1貫プレハブ(Sushi_*.prefab)をすべて読み込む</summary>
        static SushiView[] LoadSushiPiecePrefabs()
        {
            var result = new System.Collections.Generic.List<SushiView>();
            foreach (var pieceName in SushiPieceNames)
            {
                var path = $"{PrefabFolder}/Sushi_{pieceName.TrimEnd('_')}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<SushiView>(path);
                if (prefab != null)
                {
                    result.Add(prefab);
                }
            }
            return result.ToArray();
        }

        /// <summary>フォルダが無ければ作成する</summary>
        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
