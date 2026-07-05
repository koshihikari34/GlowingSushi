using System;
using GlowingSushi.Domain;
using ObservableCollections;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 表面ふるまいスポット群を管理するViewModel。
    /// Phase 2aでは平面検出位置にデモスポット(転がり/昼寝/散歩/ベイブレード各1つ)を配置する。
    /// Phase 2bではVPSアンカーの位置・種別からスポットを構築する予定。
    /// </summary>
    public sealed class SurfaceSpotsViewModel : IDisposable
    {
        readonly SushiBehaviorSettings settings;
        readonly System.Random random = new();
        readonly Subject<BattleClashInfo> battleClash = new();

        /// <summary>スポット一覧。Viewは増減を購読して個体Viewを生成・破棄する。</summary>
        public ObservableList<SurfaceSpotViewModel> Spots { get; } = new();

        /// <summary>ベイブレードスポットの衝突イベント(全スポット集約)</summary>
        public Observable<BattleClashInfo> BattleClash => battleClash;

        public SurfaceSpotsViewModel(SushiBehaviorSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// 検出された平面の上にデモスポットを4種類並べて配置する(Phase 2a検証用)。
        /// </summary>
        public void SpawnDemo(Pose planePose)
        {
            if (Spots.Count > 0) return; // 二重配置を防ぐ

            // 平面の向きに合わせたローカル軸で、十字に4スポットを並べる
            var spacing = settings.spotRadius * 2.5f;
            AddSpot(SurfaceBehaviorType.Rolling, planePose, new Vector2(spacing, 0f));
            AddSpot(SurfaceBehaviorType.Napping, planePose, new Vector2(-spacing, 0f));
            AddSpot(SurfaceBehaviorType.Strolling, planePose, new Vector2(0f, spacing));
            AddSpot(SurfaceBehaviorType.Battle, planePose, new Vector2(0f, -spacing));
        }

        /// <summary>
        /// 指定した表面姿勢にスポットを1つ追加する(Phase 2bのVPSアンカーからも使う)。
        /// </summary>
        public void AddSpot(SurfaceBehaviorType type, Pose surfacePose, Vector2 localOffset)
        {
            var right = surfacePose.rotation * Vector3.right;
            var forward = surfacePose.rotation * Vector3.forward;
            var center = new Pose(
                surfacePose.position + right * localOffset.x + forward * localOffset.y,
                surfacePose.rotation);
            Spots.Add(new SurfaceSpotViewModel(settings, random, type, center, battleClash));
        }

        /// <summary>全スポットのシミュレーションを1フレーム分進める</summary>
        public void Tick(float deltaTime)
        {
            foreach (var spot in Spots)
            {
                spot.Tick(deltaTime);
            }
        }

        public void Dispose()
        {
            battleClash.Dispose();
            foreach (var spot in Spots)
            {
                spot.Dispose();
            }
            Spots.Clear();
        }
    }
}
