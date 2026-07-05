using System;
using GlowingSushi.Service;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// アプリの動作状態(平面検出・VPSローカライズ・スポット配置の統計)を集約してViewへ公開するViewModel。
    /// 現地検証で「動いているのか/どこに出たのか」を判断するための状態HUDに使う。
    /// VPSが無効なシーンでも動くよう、VPS情報はAttachVpsで後から接続する。
    /// </summary>
    public sealed class StatusViewModel : IDisposable
    {
        readonly ArPlacementViewModel placement;
        readonly SurfaceSpotsViewModel surfaceSpots;
        readonly ICameraPoseService cameraPose;
        VpsLocalizationService vps;
        VpsPlacementViewModel vpsPlacement;

        public StatusViewModel(
            ArPlacementViewModel placement,
            SurfaceSpotsViewModel surfaceSpots,
            ICameraPoseService cameraPose)
        {
            this.placement = placement;
            this.surfaceSpots = surfaceSpots;
            this.cameraPose = cameraPose;
        }

        /// <summary>平面検出によるコンテンツ配置が済んだか</summary>
        public ReadOnlyReactiveProperty<bool> IsPlaneContentPlaced => placement.IsPlaced;

        /// <summary>VPSが有効かどうか(AttachVps済みか)</summary>
        public bool HasVps => vps != null;

        /// <summary>SDK初期化状態(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<string> SdkStatus => vps?.SdkStatus;

        /// <summary>一度でもローカライズに成功したか(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<bool> IsLocalized => vps?.IsLocalized;

        /// <summary>試行回数(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<int> AttemptCount => vps?.AttemptCount;

        /// <summary>成功回数(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<int> SuccessCount => vps?.SuccessCount;

        /// <summary>失敗回数(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<int> FailureCount => vps?.FailureCount;

        /// <summary>最後に成功したマップID(VPS無効時はnull)</summary>
        public ReadOnlyReactiveProperty<string> LastLocalizedMaps => vps?.LastLocalizedMaps;

        /// <summary>登録済みVPSアンカー数(VPS無効時は-1。0ならアンカーの注入に失敗している)</summary>
        public int RegisteredAnchorCount => vpsPlacement?.RegisteredAnchorCount ?? -1;

        /// <summary>VPS経由で配置されたスポット数(VPS無効時は-1)</summary>
        public int VpsPlacedSpotCount => vpsPlacement?.PlacedSpotCount ?? -1;

        /// <summary>現在存在する表面スポットの総数(平面デモ含む)</summary>
        public int TotalSpotCount => surfaceSpots.Spots.Count;

        /// <summary>
        /// カメラから最寄りスポット中心までの距離(メートル)。スポットが無い場合は-1。
        /// 「配置はされたが遠くにいる」ケースの発見に使う。
        /// </summary>
        public float NearestSpotDistance
        {
            get
            {
                if (surfaceSpots.Spots.Count == 0) return -1f;
                var camera = cameraPose.Position;
                var nearest = float.MaxValue;
                foreach (var spot in surfaceSpots.Spots)
                {
                    var distance = Vector3.Distance(camera, spot.Center);
                    if (distance < nearest) nearest = distance;
                }
                return nearest;
            }
        }

        /// <summary>VPSサービスを接続する。VPS有効なシーンでのみエントリポイントから呼ばれる。</summary>
        public void AttachVps(VpsLocalizationService vpsService, VpsPlacementViewModel vpsPlacementViewModel)
        {
            vps = vpsService;
            vpsPlacement = vpsPlacementViewModel;
        }

        public void Dispose()
        {
            // 保持しているのは他所有のオブジェクトのみのため破棄処理なし
        }
    }
}
