using System;
using Immersal;
using Immersal.XR;
using R3;

namespace GlowingSushi.Service
{
    /// <summary>
    /// Immersal Localizer のUnityEventをR3のObservableへ変換するサービス。
    /// VPSローカライズの成功(マップID付き)・状態・デバッグ用の統計を公開する。
    /// </summary>
    public sealed class VpsLocalizationService : IDisposable
    {
        readonly Localizer localizer;
        readonly Subject<int[]> successfulLocalizations = new();
        readonly ReactiveProperty<bool> isLocalized = new(false);
        readonly ReactiveProperty<string> sdkStatus = new("初期化待ち");
        readonly ReactiveProperty<int> attemptCount = new(0);
        readonly ReactiveProperty<int> successCount = new(0);
        readonly ReactiveProperty<int> failureCount = new(0);
        readonly ReactiveProperty<string> lastLocalizedMaps = new("-");
        bool initialized;

        public VpsLocalizationService(Localizer localizer)
        {
            this.localizer = localizer;
        }

        /// <summary>ローカライズに成功したマップIDの配列が成功ごとに流れるObservable</summary>
        public Observable<int[]> SuccessfulLocalizations => successfulLocalizations;

        /// <summary>一度でもローカライズに成功したかどうか</summary>
        public ReadOnlyReactiveProperty<bool> IsLocalized => isLocalized;

        // ---- 以下は状態HUD用のデバッグ情報 ----

        /// <summary>ImmersalSDKの初期化状態(表示用)</summary>
        public ReadOnlyReactiveProperty<string> SdkStatus => sdkStatus;

        /// <summary>ローカライズの試行回数</summary>
        public ReadOnlyReactiveProperty<int> AttemptCount => attemptCount;

        /// <summary>ローカライズの成功回数</summary>
        public ReadOnlyReactiveProperty<int> SuccessCount => successCount;

        /// <summary>全マップ失敗の回数</summary>
        public ReadOnlyReactiveProperty<int> FailureCount => failureCount;

        /// <summary>最後に成功したマップID(表示用)</summary>
        public ReadOnlyReactiveProperty<string> LastLocalizedMaps => lastLocalizedMaps;

        /// <summary>LocalizerとSDKのイベント購読を開始する。エントリポイントから起動時に呼ぶ。</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;

            localizer.OnFirstSuccessfulLocalization.AddListener(OnFirstSuccess);
            localizer.OnSuccessfulLocalizations.AddListener(OnSuccess);
            localizer.OnLocalizationResult.AddListener(OnResult);
            localizer.OnFailedLocalizations.AddListener(OnFailed);

            var sdk = ImmersalSDK.Instance;
            if (sdk != null)
            {
                sdk.OnInitializationComplete.AddListener(OnSdkInitialized);
                sdk.OnUserValidationComplete.AddListener(OnUserValidated);
            }
            else
            {
                sdkStatus.Value = "SDK未検出";
            }
        }

        void OnSdkInitialized() => sdkStatus.Value = "初期化OK";

        void OnUserValidated() => sdkStatus.Value = "初期化OK/トークンOK";

        void OnFirstSuccess()
        {
            isLocalized.Value = true;
        }

        void OnSuccess(int[] mapIds)
        {
            successCount.Value++;
            lastLocalizedMaps.Value = string.Join(",", mapIds);
            successfulLocalizations.OnNext(mapIds);
        }

        void OnResult(ILocalizationResults results)
        {
            attemptCount.Value++;
        }

        void OnFailed()
        {
            failureCount.Value++;
        }

        public void Dispose()
        {
            if (initialized && localizer != null)
            {
                localizer.OnFirstSuccessfulLocalization.RemoveListener(OnFirstSuccess);
                localizer.OnSuccessfulLocalizations.RemoveListener(OnSuccess);
                localizer.OnLocalizationResult.RemoveListener(OnResult);
                localizer.OnFailedLocalizations.RemoveListener(OnFailed);
            }
            var sdk = ImmersalSDK.Instance;
            if (initialized && sdk != null)
            {
                sdk.OnInitializationComplete.RemoveListener(OnSdkInitialized);
                sdk.OnUserValidationComplete.RemoveListener(OnUserValidated);
            }
            successfulLocalizations.Dispose();
            isLocalized.Dispose();
            sdkStatus.Dispose();
            attemptCount.Dispose();
            successCount.Dispose();
            failureCount.Dispose();
            lastLocalizedMaps.Dispose();
        }
    }
}
