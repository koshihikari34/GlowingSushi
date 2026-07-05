using System;
using GlowingSushi.Service;
using R3;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// アプリの動作状態(平面検出・VPSローカライズの統計)を集約してViewへ公開するViewModel。
    /// 現地検証で「動いているのか/待てばいいのか」を判断するための状態HUDに使う。
    /// VPSが無効なシーンでも動くよう、VPS情報はAttachVpsで後から接続する。
    /// </summary>
    public sealed class StatusViewModel : IDisposable
    {
        readonly ArPlacementViewModel placement;
        VpsLocalizationService vps;

        public StatusViewModel(ArPlacementViewModel placement)
        {
            this.placement = placement;
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

        /// <summary>
        /// VPSサービスを接続する。VPS有効なシーンでのみエントリポイントから呼ばれる。
        /// </summary>
        public void AttachVps(VpsLocalizationService vpsService)
        {
            vps = vpsService;
        }

        public void Dispose()
        {
            // 保持しているのは他所有のオブジェクトのみのため破棄処理なし
        }
    }
}
