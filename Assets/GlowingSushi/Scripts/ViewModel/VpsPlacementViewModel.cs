using System;
using System.Collections.Generic;
using GlowingSushi.Domain;
using GlowingSushi.Service;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// VPSローカライズ成功時に、対応するマップのアンカー位置へ表面ふるまいスポットを配置するViewModel。
    /// アンカーはVpsAnchorView(View層)が起動時にRegisterAnchorで登録してくる。
    ///
    /// 重要: Immersalの成功イベントはSceneUpdaterがXRSpaceを動かす「前」に発火するため、
    /// イベント時点でアンカー姿勢を読むとローカライズ前のずれた位置になる。
    /// そのため配置はイベントの次フレーム(Tick)で行い、以降も成功のたびに
    /// スポット姿勢をアンカーへ追従更新する(ローカライズ精度の向上にも追従する)。
    /// </summary>
    public sealed class VpsPlacementViewModel : IDisposable
    {
        readonly VpsLocalizationService localization;
        readonly SurfaceSpotsViewModel surfaceSpots;
        readonly SushiBehaviorSettings settings;

        // マップID → そのマップに属するアンカー(種別+Transform)の一覧
        readonly Dictionary<int, List<(SurfaceBehaviorType type, Transform anchor)>> anchorsByMap = new();
        // マップID → 配置時刻と配置済みスポット+対応アンカー(追従更新用)
        readonly Dictionary<int, (float placedAt, List<(SurfaceSpotViewModel spot, Transform anchor)> spots)> placedByMap = new();
        // 直近のローカライズで成功し、次のTickで配置/更新すべきマップID
        readonly HashSet<int> pendingMaps = new();
        IDisposable subscription;
        float elapsedTime;

        /// <summary>一度でもローカライズに成功したかどうか(UI表示などに使える)</summary>
        public ReadOnlyReactiveProperty<bool> IsLocalized => localization.IsLocalized;

        /// <summary>登録されたアンカーの総数(状態HUD用。0ならアンカーの注入に失敗している)</summary>
        public int RegisteredAnchorCount { get; private set; }

        /// <summary>配置済みスポットの総数(状態HUD用)</summary>
        public int PlacedSpotCount { get; private set; }

        public VpsPlacementViewModel(
            VpsLocalizationService localization,
            SurfaceSpotsViewModel surfaceSpots,
            SushiBehaviorSettings settings)
        {
            this.localization = localization;
            this.surfaceSpots = surfaceSpots;
            this.settings = settings;
        }

        /// <summary>
        /// アンカーを登録する。VpsAnchorViewが起動時に呼ぶ。
        /// </summary>
        /// <param name="mapId">アンカーが属するImmersalマップID</param>
        /// <param name="type">配置するふるまい種別</param>
        /// <param name="anchor">アンカーのTransform(XRSpace配下)</param>
        public void RegisterAnchor(int mapId, SurfaceBehaviorType type, Transform anchor)
        {
            RegisteredAnchorCount++;

            if (!anchorsByMap.TryGetValue(mapId, out var list))
            {
                list = new List<(SurfaceBehaviorType, Transform)>();
                anchorsByMap.Add(mapId, list);
            }
            list.Add((type, anchor));
        }

        /// <summary>ローカライズ成功の購読を開始する。エントリポイントから起動時に呼ぶ。</summary>
        public void Initialize()
        {
            // イベント時点ではXRSpace未更新のため、ここでは記録だけして次のTickで処理する
            subscription ??= localization.SuccessfulLocalizations.Subscribe(mapIds =>
            {
                foreach (var mapId in mapIds)
                {
                    pendingMaps.Add(mapId);
                }
            });
        }

        /// <summary>
        /// 成功イベントの翌フレームに呼ばれ、スポットの配置(初回)または追従更新(2回目以降)を行う。
        /// 追従は配置後 vpsSettleDuration 秒で停止し、以降は位置を固定する
        /// (継続する微小補正の揺れを避けるため。バーストモードの高精度化はこの時間内に完了する)。
        /// エントリポイントのTickから毎フレーム呼ばれる。
        /// </summary>
        public void Tick(float deltaTime)
        {
            elapsedTime += deltaTime;
            if (pendingMaps.Count == 0) return;

            foreach (var mapId in pendingMaps)
            {
                if (placedByMap.TryGetValue(mapId, out var placed))
                {
                    // 配置済み: 整定時間内ならスポットをアンカーの最新姿勢へ追従させる
                    if (elapsedTime - placed.placedAt > settings.vpsSettleDuration) continue; // 以降は固定
                    foreach (var (spot, anchor) in placed.spots)
                    {
                        spot.UpdateSurfacePose(new Pose(anchor.position, anchor.rotation));
                    }
                    continue;
                }

                // 初回: このマップのアンカーへスポットを配置する
                if (!anchorsByMap.TryGetValue(mapId, out var anchors)) continue;
                var newPlaced = new List<(SurfaceSpotViewModel, Transform)>();
                foreach (var (type, anchor) in anchors)
                {
                    var spot = surfaceSpots.AddSpot(type, new Pose(anchor.position, anchor.rotation), Vector2.zero);
                    newPlaced.Add((spot, anchor));
                    PlacedSpotCount++;
                }
                placedByMap.Add(mapId, (elapsedTime, newPlaced));
            }
            pendingMaps.Clear();
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
