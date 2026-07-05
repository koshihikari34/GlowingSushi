using GlowingSushi.Domain;
using GlowingSushi.Root;
using GlowingSushi.View;
using Immersal;
using Immersal.XR;
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
        const string ParticleMaterialPath = "Assets/GlowingSushi/Materials/GlowParticle.mat";
        const string PrefabFolder = "Assets/GlowingSushi/Prefabs";
        const string ObsoletePrefabPath = "Assets/GlowingSushi/Prefabs/Sushi.prefab";
        const string SettingsPath = "Assets/GlowingSushi/Settings/SushiBehaviorSettings.asset";
        const string AudioFolder = "Assets/GlowingSushi/Audio";
        const string TouchSoundPath = "Assets/GlowingSushi/Audio/TouchPop.wav";
        const string BgmPath = "Assets/GlowingSushi/Audio/AquariumBgm.wav";
        const string ClashSoundPath = "Assets/GlowingSushi/Audio/BattleClash.wav";

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
            EnsureFolder("Assets/GlowingSushi", "Audio");

            EnsureNormalMapImport();
            var material = CreateEmissiveMaterial();
            var particleMaterial = CreateGlowParticleMaterial();
            CreateSushiPiecePrefabs(material, particleMaterial);
            CreateBehaviorSettings();
            CreateTouchSound();
            CreateBgm();
            CreateBattleClashSound();

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
        /// 軌跡・バースト用の加算合成パーティクルマテリアルを生成・再設定する。
        /// 色はParticleSystem側のstartColor(群れの発光色)で乗算される。
        /// startColor(頂点カラー)はLDRにクランプされるため、Bloomで光らせるための
        /// HDR強度はマテリアルのBaseColor側で与える。
        /// </summary>
        static Material CreateGlowParticleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
            var isNew = material == null;
            if (isNew)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            }

            material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            // HDR強度(×2.5)でBloomの閾値を超えさせ、粒子を発光させる
            material.SetColor("_BaseColor", Color.white * 2.5f);
            // 加算合成(透明サーフェス+SrcAlpha/One)で発光粒子らしく見せる
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (isNew)
            {
                AssetDatabase.CreateAsset(material, ParticleMaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        /// <summary>
        /// 盛り合わせモデルから寿司1貫ごとの個別プレハブを生成する。
        /// 各プレハブは対象のメッシュのみを持ち、中心を原点に合わせて泳ぐ向きの回転に耐える形にする。
        /// 既存プレハブには軌跡パーティクルの追加のみ行う(GUIDを保つため削除しない)。
        /// </summary>
        static void CreateSushiPiecePrefabs(Material material, Material particleMaterial)
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
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
                {
                    // 既存プレハブは参照GUIDを保ったまま軌跡パーティクルを追加する
                    UpgradePrefabWithTrail(prefabPath, particleMaterial);
                    continue;
                }
                CreateSinglePiecePrefab(modelPrefab, pieceName, prefabPath, material, particleMaterial);
            }
        }

        /// <summary>
        /// 既存プレハブの軌跡パーティクルを追加・再設定し、SushiViewへ結線する。
        /// パラメータ調整を既存プレハブへ反映できるよう、設定は毎回適用し直す。
        /// </summary>
        static void UpgradePrefabWithTrail(string prefabPath, Material particleMaterial)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var view = contents.GetComponent<SushiView>();
                if (view == null)
                {
                    Debug.LogError($"[GlowingSushi] SushiViewがありません: {prefabPath}");
                    return;
                }

                var so = new SerializedObject(view);
                var trailProp = so.FindProperty("trailParticles");
                var trail = trailProp.objectReferenceValue as ParticleSystem;
                if (trail == null)
                {
                    var child = contents.transform.Find("TrailParticles");
                    trail = child != null ? child.GetComponent<ParticleSystem>() : null;
                }
                if (trail == null)
                {
                    trail = CreateTrailParticleSystem(contents.transform, particleMaterial);
                }
                else
                {
                    ConfigureTrailParticles(trail, particleMaterial);
                }

                trailProp.objectReferenceValue = trail;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>泳いだ軌跡に発光粒子を残すParticleSystemを子として生成する</summary>
        static ParticleSystem CreateTrailParticleSystem(Transform parent, Material particleMaterial)
        {
            var go = new GameObject("TrailParticles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ConfigureTrailParticles(ps, particleMaterial);
            return ps;
        }

        /// <summary>軌跡パーティクルの設定を適用する(生成時・再設定時の両方で使う)</summary>
        static void ConfigureTrailParticles(ParticleSystem ps, Material particleMaterial)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // 粒子をその場に残す
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            // わずかな初速でゆっくり漂わせる(一列の点線にならないように)
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            main.gravityModifier = 0f;
            main.maxParticles = 1500;

            // 移動距離に応じて放出することで「軌跡」になる
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 150f;

            // 球状に散らして放出し、軌跡に幅を持たせる
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            // 時間経過でフェードアウト
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            // 時間経過で縮小
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
        }

        /// <summary>指定した名前のノードだけを取り出した1貫分のプレハブを生成する</summary>
        static void CreateSinglePiecePrefab(GameObject modelPrefab, string pieceName, string prefabPath, Material material, Material particleMaterial)
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

                var trail = CreateTrailParticleSystem(root.transform, particleMaterial);

                var view = root.AddComponent<SushiView>();
                var so = new SerializedObject(view);
                so.FindProperty("bodyRenderer").objectReferenceValue = renderer;
                so.FindProperty("trailParticles").objectReferenceValue = trail;
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

        /// <summary>
        /// タッチ命中音(水泡ポップ音)のWAVを合成して生成する。
        /// 周波数が下がるサイン波+倍音に指数減衰をかけた約0.25秒のモノラル16bit。
        /// 好みの音源に差し替えてよい(このファイルを置き換えるだけ)。
        /// </summary>
        static void CreateTouchSound()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(TouchSoundPath) != null) return;

            const int sampleRate = 44100;
            const float duration = 0.25f;
            var sampleCount = (int)(sampleRate * duration);
            var samples = new float[sampleCount];

            var phase = 0.0;
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                // 900Hz→300Hzへ滑らかに下がる「ポコッ」というスイープ
                var frequency = Mathf.Lerp(900f, 300f, t / duration);
                phase += 2.0 * System.Math.PI * frequency / sampleRate;
                var envelope = Mathf.Exp(-18f * t);
                samples[i] = (Mathf.Sin((float)phase) * 0.8f + Mathf.Sin((float)(phase * 2.0)) * 0.2f) * envelope;
            }

            WriteWav(TouchSoundPath, samples, sampleRate);
            AssetDatabase.ImportAsset(TouchSoundPath);
        }

        /// <summary>
        /// 環境音パッド風のループBGM(Am-F-C-G、約24秒)を合成して生成する。
        /// 各周波数をループ長の整数倍周期に量子化することで、つなぎ目のない完全ループにしている。
        /// 好みの曲に差し替えてよい(このファイルを置き換えるだけ)。
        /// </summary>
        static void CreateBgm()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath) != null) return;

            const int sampleRate = 44100;
            const float chordDuration = 6f;
            const float crossfadeRatio = 0.25f; // コード末尾25%でクロスフェード
            var chords = new[]
            {
                new[] { 110.00f, 164.81f, 220.00f, 261.63f }, // Am
                new[] { 87.31f, 130.81f, 174.61f, 220.00f },  // F
                new[] { 130.81f, 196.00f, 261.63f, 329.63f }, // C
                new[] { 98.00f, 146.83f, 196.00f, 246.94f },  // G
            };
            var loopLength = chordDuration * chords.Length;
            var sampleCount = (int)(sampleRate * loopLength);
            var samples = new float[sampleCount];

            // ループ長で位相が一致するよう周波数を量子化(つなぎ目のクリック防止)
            float Quantize(float f) => Mathf.Round(f * loopLength) / loopLength;

            float ChordSample(int chordIndex, float t)
            {
                var notes = chords[chordIndex % chords.Length];
                var sum = 0f;
                foreach (var note in notes)
                {
                    var f = Quantize(note);
                    var w = 2f * Mathf.PI * f * t;
                    // 基音+弱い2倍音+微小デチューンで柔らかいパッドにする
                    sum += Mathf.Sin(w) + 0.3f * Mathf.Sin(w * 2f) + 0.5f * Mathf.Sin(2f * Mathf.PI * Quantize(note * 1.003f) * t);
                }
                return sum / (notes.Length * 1.8f);
            }

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var progress = t / chordDuration;
                var index = (int)progress % chords.Length;
                var frac = progress - Mathf.Floor(progress);

                float value;
                if (frac > 1f - crossfadeRatio)
                {
                    // 次のコードへ等パワークロスフェード(最後のコードは先頭へ戻る)
                    var x = (frac - (1f - crossfadeRatio)) / crossfadeRatio * (Mathf.PI * 0.5f);
                    value = ChordSample(index, t) * Mathf.Cos(x) + ChordSample(index + 1, t) * Mathf.Sin(x);
                }
                else
                {
                    value = ChordSample(index, t);
                }

                // ゆっくりした音量の揺らぎ(トレモロ)で単調さを消す
                var tremolo = 1f + 0.08f * Mathf.Sin(2f * Mathf.PI * (2f / loopLength) * t);
                samples[i] = value * tremolo * 0.35f;
            }

            WriteWav(BgmPath, samples, sampleRate);
            AssetDatabase.ImportAsset(BgmPath);
        }

        /// <summary>
        /// ベイブレード衝突音(金属的な「キン」音)を合成して生成する。
        /// 非整数倍の高次部分音+速い減衰で金属打撃音を模す。差し替え可能。
        /// </summary>
        static void CreateBattleClashSound()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(ClashSoundPath) != null) return;

            const int sampleRate = 44100;
            const float duration = 0.18f;
            var sampleCount = (int)(sampleRate * duration);
            var samples = new float[sampleCount];

            // 金属音らしい非整数倍の部分音(周波数, 振幅, 減衰速度)
            var partials = new[]
            {
                (frequency: 2500f, amplitude: 0.5f, decay: 40f),
                (frequency: 3730f, amplitude: 0.3f, decay: 55f),
                (frequency: 5170f, amplitude: 0.2f, decay: 70f),
            };

            var noiseRandom = new System.Random(12345);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var value = 0f;
                foreach (var (frequency, amplitude, decay) in partials)
                {
                    value += Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude * Mathf.Exp(-decay * t);
                }
                // 打撃感を出す最初の数msのノイズ
                if (t < 0.006f)
                {
                    value += ((float)noiseRandom.NextDouble() * 2f - 1f) * 0.4f * (1f - t / 0.006f);
                }
                samples[i] = value * 0.7f;
            }

            WriteWav(ClashSoundPath, samples, sampleRate);
            AssetDatabase.ImportAsset(ClashSoundPath);
        }

        /// <summary>float配列をモノラル16bit PCMのWAVファイルとして書き出す</summary>
        static void WriteWav(string path, float[] samples, int sampleRate)
        {
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Create);
            using var writer = new System.IO.BinaryWriter(stream);

            var dataSize = samples.Length * 2; // 16bit = 2バイト/サンプル

            // RIFFヘッダ
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            // fmtチャンク(PCM, モノラル)
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            // dataチャンク
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (var sample in samples)
            {
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
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
                cameraGo.AddComponent<AudioListener>(); // 効果音の再生に必須(シーン内に1つ)
                AddTrackedPoseDriver(cameraGo);

                origin = originGo.AddComponent<XROrigin>();
                origin.CameraFloorOffsetObject = offsetGo;
                origin.Camera = camera;

                originGo.AddComponent<ARPlaneManager>();
            }

            var planeManager = origin.GetComponent<ARPlaneManager>();
            var arCamera = origin.Camera;

            // 既存シーンの修復: ARカメラにAudioListenerが無ければ追加する(無いと音が鳴らない)
            if (arCamera != null && arCamera.GetComponent<AudioListener>() == null)
            {
                arCamera.gameObject.AddComponent<AudioListener>();
            }

            // --- 旧構成(SushiSchoolView)の残骸を掃除 ---
            var staleSchool = GameObject.Find("SushiSchoolView");
            if (staleSchool != null)
            {
                Object.DestroyImmediate(staleSchool);
            }

            // --- 水族館View ---
            var aquariumView = Object.FindFirstObjectByType<AquariumView>();
            if (aquariumView == null)
            {
                aquariumView = new GameObject("AquariumView").AddComponent<AquariumView>();
            }
            var aquariumSo = new SerializedObject(aquariumView);
            var prefabsProp = aquariumSo.FindProperty("sushiPrefabs");
            prefabsProp.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
            aquariumSo.ApplyModifiedPropertiesWithoutUndo();

            // --- タッチ演出View(バーストパーティクル+効果音) ---
            SetupTouchEffectView();

            // --- BGMプレイヤー(2Dループ再生) ---
            SetupBgmPlayer();

            // --- 表面ふるまいView+ベイブレード衝突演出 ---
            SetupSurfaceSpotsView(prefabs);
            SetupBattleEffectView();

            // --- 状態HUD(現地検証用のデバッグ表示) ---
            SetupStatusHud();

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
                $"planeManager={(planeManager != null ? "OK" : "未設定")}, arCamera={(arCamera != null ? "OK" : "未設定")}。" +
                "アセット参照が未設定の場合はインスペクタから手動でドラッグしてください。");
        }

        /// <summary>タッチ演出View(バーストパーティクル+AudioSource)をシーンへ構築する</summary>
        static void SetupTouchEffectView()
        {
            var effectView = Object.FindFirstObjectByType<TouchEffectView>();
            if (effectView == null)
            {
                var go = new GameObject("TouchEffectView");
                effectView = go.AddComponent<TouchEffectView>();

                // バーストパーティクル(命中位置で球状に弾ける発光粒子)
                var psGo = new GameObject("BurstParticles");
                psGo.transform.SetParent(go.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.6f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.6f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
                main.startSize = 0.03f;
                main.gravityModifier = 0f;
                main.maxParticles = 100;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.03f;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

                var sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

                var renderer = psGo.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);

                // 効果音(3D音源として命中位置で鳴らす)
                var audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;

                var so = new SerializedObject(effectView);
                so.FindProperty("burstParticles").objectReferenceValue = ps;
                so.FindProperty("audioSource").objectReferenceValue = audioSource;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // 効果音のアセット参照(未設定の場合のみ結線を試みる)
            var effectSo = new SerializedObject(effectView);
            var soundProp = effectSo.FindProperty("hitSound");
            if (soundProp.objectReferenceValue == null)
            {
                soundProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(TouchSoundPath);
                effectSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>状態HUD(Canvas+Text)をシーンへ構築する</summary>
        static void SetupStatusHud()
        {
            if (Object.FindFirstObjectByType<StatusHudView>() != null) return;

            var canvasGo = new GameObject("StatusHud");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var hud = canvasGo.AddComponent<StatusHudView>();

            // 左上に半透明背景+テキスト
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImage = panelGo.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.5f);
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(10f, -50f);
            panelRect.sizeDelta = new Vector2(560f, 150f);

            var textGo = new GameObject("StatusText");
            textGo.transform.SetParent(panelGo.transform, false);
            var text = textGo.AddComponent<UnityEngine.UI.Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 5f);
            textRect.offsetMax = new Vector2(-10f, -5f);

            var so = new SerializedObject(hud);
            so.FindProperty("statusText").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>表面ふるまいスポットViewをシーンへ構築し、寿司プレハブ配列を結線する</summary>
        static void SetupSurfaceSpotsView(SushiView[] prefabs)
        {
            var spotsView = Object.FindFirstObjectByType<SurfaceSpotsView>();
            if (spotsView == null)
            {
                spotsView = new GameObject("SurfaceSpotsView").AddComponent<SurfaceSpotsView>();
            }
            var so = new SerializedObject(spotsView);
            var prefabsProp = so.FindProperty("sushiPrefabs");
            prefabsProp.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
            {
                prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>ベイブレード衝突演出View(火花パーティクル+衝突音)をシーンへ構築する</summary>
        static void SetupBattleEffectView()
        {
            var effectView = Object.FindFirstObjectByType<BattleEffectView>();
            if (effectView == null)
            {
                var go = new GameObject("BattleEffectView");
                effectView = go.AddComponent<BattleEffectView>();

                // 火花パーティクル(金属衝突らしいオレンジの線状スパーク、HDRマテリアルでBloom発光)
                var psGo = new GameObject("SparkParticles");
                psGo.transform.SetParent(go.transform, false);
                var ps = psGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.4f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f);
                main.startColor = new Color(1f, 0.85f, 0.4f); // 火花のオレンジ
                main.gravityModifier = 0.5f; // 火花は落ちる
                main.maxParticles = 200;

                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 25) });

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.01f;

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

                // 進行方向に伸ばして「線状の火花」に見せる
                var renderer = psGo.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 4f;

                // 衝突音(3D音源)
                var audioSource = go.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;

                var newSo = new SerializedObject(effectView);
                newSo.FindProperty("sparkParticles").objectReferenceValue = ps;
                newSo.FindProperty("audioSource").objectReferenceValue = audioSource;
                newSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 衝突音のアセット参照(未設定の場合のみ結線を試みる)
            var effectSo = new SerializedObject(effectView);
            var soundProp = effectSo.FindProperty("clashSound");
            if (soundProp.objectReferenceValue == null)
            {
                soundProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClashSoundPath);
                effectSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// BGMプレイヤーをシーンへ構築する。
        /// 状態を持たない単純なループ再生のためViewModelは介さない(シーン直置きのAudioSource)。
        /// </summary>
        static void SetupBgmPlayer()
        {
            var existing = GameObject.Find("BgmPlayer");
            var audioSource = existing != null ? existing.GetComponent<AudioSource>() : null;
            if (audioSource == null)
            {
                var go = existing != null ? existing : new GameObject("BgmPlayer");
                audioSource = go.AddComponent<AudioSource>();
            }

            audioSource.loop = true;
            audioSource.playOnAwake = true;
            audioSource.spatialBlend = 0f; // 2D再生(位置に依存しない)
            audioSource.volume = 0.35f;

            // クリップは未設定の場合のみ結線する(手動差し替えを上書きしない)
            if (audioSource.clip == null)
            {
                audioSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(BgmPath);
            }
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

        // ------------------------------------------------------------
        // 5. VPSセットアップ(Immersal)
        // ------------------------------------------------------------

        const string ImmersalPrefabPath = "Packages/com.immersal.core/Runtime/Resources/Prefabs/ImmersalSDK.prefab";

        /// <summary>マップ定義: (マップID, 名前, そのマップに置くアンカーの[名前+種別]一覧)</summary>
        static readonly (int mapId, string mapName, (string anchorName, SurfaceBehaviorType type)[] anchors)[] VpsMaps =
        {
            (148692, "bench", new[]
            {
                ("BenchNapAnchor", SurfaceBehaviorType.Napping),
                ("BenchStrollAnchor", SurfaceBehaviorType.Strolling),
            }),
            (148693, "table", new[]
            {
                ("TableBattleAnchor", SurfaceBehaviorType.Battle),
            }),
            (148694, "vendingmachine", new[]
            {
                ("VendingRollingAnchor", SurfaceBehaviorType.Rolling),
            }),
        };

        /// <summary>
        /// ImmersalのVPS構成をシーンへ構築する:
        /// ImmersalSDKプレハブ・トークンローダー・マップごとのXRSpace+XRMap+アンカー。
        /// 実行後、各XRMapのインスペクタからDownload(Visualization)で点群を落とし、
        /// 点群を目印にアンカーの位置を調整すること。
        /// </summary>
        [MenuItem("GlowingSushi/Setup/5. VPSセットアップ(Immersal)")]
        public static void SetupVps()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // --- ImmersalSDK本体(プレハブ) ---
            var sdk = Object.FindFirstObjectByType<ImmersalSDK>();
            if (sdk == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImmersalPrefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[GlowingSushi] ImmersalSDKプレハブが見つかりません: {ImmersalPrefabPath}");
                    return;
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                sdk = instance.GetComponent<ImmersalSDK>();
            }

            var serverLocalization = sdk.GetComponentInChildren<ServerLocalization>(true);
            var localizer = sdk.GetComponentInChildren<Localizer>(true);
            if (serverLocalization == null || localizer == null)
            {
                Debug.LogError("[GlowingSushi] ImmersalSDK配下にServerLocalization/Localizerが見つかりません");
                return;
            }

            // --- トークンローダー(Git管理外のトークンを実行時に読み込む) ---
            var tokenLoader = Object.FindFirstObjectByType<ImmersalTokenLoader>();
            if (tokenLoader == null)
            {
                tokenLoader = new GameObject("ImmersalTokenLoader").AddComponent<ImmersalTokenLoader>();
            }
            var loaderSo = new SerializedObject(tokenLoader);
            loaderSo.FindProperty("immersalSdk").objectReferenceValue = sdk;
            loaderSo.ApplyModifiedPropertiesWithoutUndo();

            // --- マップごとのXRSpace+XRMap+アンカー ---
            var spaceObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var (mapId, mapName, anchors) in VpsMaps)
            {
                var spaceName = $"XR Space {mapName}";
                var spaceGo = GameObject.Find(spaceName);
                if (spaceGo == null)
                {
                    spaceGo = new GameObject(spaceName);
                    spaceGo.AddComponent<XRSpace>();
                }
                spaceObjects.Add(spaceGo);

                // XRMap(サーバーローカライズなのでマップファイル埋め込みは不要)
                var mapGoName = $"XR Map {mapId}-{mapName}";
                if (spaceGo.transform.Find(mapGoName) == null)
                {
                    var mapGo = new GameObject(mapGoName);
                    mapGo.transform.SetParent(spaceGo.transform, false);
                    var map = mapGo.AddComponent<XRMap>();
                    var mapSo = new SerializedObject(map);
                    mapSo.FindProperty("m_MapId").intValue = mapId;
                    mapSo.FindProperty("m_MapName").stringValue = mapName;
                    mapSo.FindProperty("IsConfigured").boolValue = true;
                    mapSo.FindProperty("m_LocalizationMethodObject").objectReferenceValue = serverLocalization;
                    mapSo.ApplyModifiedPropertiesWithoutUndo();
                }

                // アンカー(位置はマップ点群を見ながら手動調整する前提で原点に置く)
                foreach (var (anchorName, type) in anchors)
                {
                    if (spaceGo.transform.Find(anchorName) != null) continue;
                    var anchorGo = new GameObject(anchorName);
                    anchorGo.transform.SetParent(spaceGo.transform, false);
                    var anchor = anchorGo.AddComponent<VpsAnchorView>();
                    var anchorSo = new SerializedObject(anchor);
                    anchorSo.FindProperty("mapId").intValue = mapId;
                    anchorSo.FindProperty("behaviorType").enumValueIndex = (int)type;
                    anchorSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // --- LifetimeScopeへの結線(Localizer参照+アンカーへの注入対象登録) ---
            var scope = Object.FindFirstObjectByType<GlowingSushiLifetimeScope>();
            if (scope != null)
            {
                var scopeSo = new SerializedObject(scope);
                scopeSo.FindProperty("immersalLocalizer").objectReferenceValue = localizer;

                // XRSpace配下のVpsAnchorViewへ[Inject]が効くようautoInjectGameObjectsへ登録
                var autoInject = scopeSo.FindProperty("autoInjectGameObjects");
                foreach (var spaceGo in spaceObjects)
                {
                    var exists = false;
                    for (var i = 0; i < autoInject.arraySize; i++)
                    {
                        if (autoInject.GetArrayElementAtIndex(i).objectReferenceValue == spaceGo)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        autoInject.arraySize++;
                        autoInject.GetArrayElementAtIndex(autoInject.arraySize - 1).objectReferenceValue = spaceGo;
                    }
                }
                scopeSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogError("[GlowingSushi] GlowingSushiLifetimeScopeがシーンにありません。先に「2. シーンセットアップ」を実行してください。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                "[GlowingSushi] VPSセットアップ完了。次の手順:\n" +
                "1. 各「XR Map」を選択しインスペクタのDownloadでVisualization(点群)を取得\n" +
                "2. 点群を目印に各アンカー(ギズモ表示あり)を実際の面の上へ移動・回転(Y軸=面の法線)\n" +
                "3. LifetimeScopeのPlacement Modeを「None」にして現地ビルド(平面デモを止めてVPSのみにする場合)");
        }

        // ------------------------------------------------------------
        // 4. 挙動パラメータを推奨値へ更新
        // ------------------------------------------------------------
        /// <summary>
        /// 既存のSushiBehaviorSettings.assetへ現在の推奨値を一括適用する。
        /// アセットは生成時の値を保持し続けるため、スクリプトのデフォルト変更後はこれで反映する。
        /// </summary>
        [MenuItem("GlowingSushi/Setup/4. 挙動パラメータを推奨値へ更新")]
        public static void ApplyRecommendedBehaviorSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<SushiBehaviorSettings>(SettingsPath);
            if (settings == null)
            {
                EditorUtility.DisplayDialog(
                    "GlowingSushi",
                    "設定アセットが見つかりません。先に「1. アセット生成」を実行してください。",
                    "OK");
                return;
            }

            // 新規生成したインスタンスのデフォルト値(=スクリプトの推奨値)をコピーする
            var assetName = settings.name;
            var defaults = ScriptableObject.CreateInstance<SushiBehaviorSettings>();
            try
            {
                EditorUtility.CopySerialized(defaults, settings);
                settings.name = assetName; // CopySerializedで名前まで上書きされるため戻す
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[GlowingSushi] SushiBehaviorSettings.assetへ推奨値を適用しました。");
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
