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
    /// マップごとに独立したXRSpace配下にあるため、そのマップのローカライズが
    /// 成功して初めてアンカーのワールド姿勢が正しくなる。よって配置はマップ単位で行う。
    /// </summary>
    public sealed class VpsPlacementViewModel : IDisposable
    {
        readonly VpsLocalizationService localization;
        readonly SurfaceSpotsViewModel surfaceSpots;

        // マップID → そのマップに属するアンカー(種別+Transform)の一覧
        readonly Dictionary<int, List<(SurfaceBehaviorType type, Transform anchor)>> anchorsByMap = new();
        // スポット配置済みのマップID
        readonly HashSet<int> placedMaps = new();
        IDisposable subscription;

        /// <summary>一度でもローカライズに成功したかどうか(UI表示などに使える)</summary>
        public ReadOnlyReactiveProperty<bool> IsLocalized => localization.IsLocalized;

        /// <summary>登録されたアンカーの総数(状態HUD用。0ならアンカーの注入に失敗している)</summary>
        public int RegisteredAnchorCount { get; private set; }

        /// <summary>配置済みスポットの総数(状態HUD用)</summary>
        public int PlacedSpotCount { get; private set; }

        public VpsPlacementViewModel(VpsLocalizationService localization, SurfaceSpotsViewModel surfaceSpots)
        {
            this.localization = localization;
            this.surfaceSpots = surfaceSpots;
        }

        /// <summary>
        /// アンカーを登録する。VpsAnchorViewが起動時に呼ぶ。
        /// 対応するマップが既にローカライズ済みの場合は即座に配置する。
        /// </summary>
        /// <param name="mapId">アンカーが属するImmersalマップID</param>
        /// <param name="type">配置するふるまい種別</param>
        /// <param name="anchor">アンカーのTransform(XRSpace配下)</param>
        public void RegisterAnchor(int mapId, SurfaceBehaviorType type, Transform anchor)
        {
            RegisteredAnchorCount++;

            if (placedMaps.Contains(mapId))
            {
                PlaceSpot(type, anchor);
                return;
            }

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
            subscription ??= localization.SuccessfulLocalizations.Subscribe(OnLocalized);
        }

        /// <summary>ローカライズに成功したマップのアンカーへスポットを配置する(マップごとに1回だけ)</summary>
        void OnLocalized(int[] mapIds)
        {
            foreach (var mapId in mapIds)
            {
                if (placedMaps.Contains(mapId)) continue;
                placedMaps.Add(mapId);

                if (!anchorsByMap.TryGetValue(mapId, out var anchors)) continue;
                foreach (var (type, anchor) in anchors)
                {
                    PlaceSpot(type, anchor);
                }
                anchorsByMap.Remove(mapId);
            }
        }

        void PlaceSpot(SurfaceBehaviorType type, Transform anchor)
        {
            // XRSpaceがローカライズ済みなので、アンカーのワールド姿勢は実世界に一致している
            surfaceSpots.AddSpot(type, new Pose(anchor.position, anchor.rotation), Vector2.zero);
            PlacedSpotCount++;
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }
    }
}
