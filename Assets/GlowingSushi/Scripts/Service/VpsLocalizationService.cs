using System;
using Immersal.XR;
using R3;

namespace GlowingSushi.Service
{
    /// <summary>
    /// Immersal Localizer のUnityEventをR3のObservableへ変換するサービス。
    /// VPSローカライズの成功(マップID付き)と状態を公開する。
    /// </summary>
    public sealed class VpsLocalizationService : IDisposable
    {
        readonly Localizer localizer;
        readonly Subject<int[]> successfulLocalizations = new();
        readonly ReactiveProperty<bool> isLocalized = new(false);
        bool initialized;

        public VpsLocalizationService(Localizer localizer)
        {
            this.localizer = localizer;
        }

        /// <summary>ローカライズに成功したマップIDの配列が成功ごとに流れるObservable</summary>
        public Observable<int[]> SuccessfulLocalizations => successfulLocalizations;

        /// <summary>一度でもローカライズに成功したかどうか</summary>
        public ReadOnlyReactiveProperty<bool> IsLocalized => isLocalized;

        /// <summary>Localizerイベントの購読を開始する。エントリポイントから起動時に呼ぶ。</summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            localizer.OnFirstSuccessfulLocalization.AddListener(OnFirstSuccess);
            localizer.OnSuccessfulLocalizations.AddListener(OnSuccess);
        }

        void OnFirstSuccess()
        {
            isLocalized.Value = true;
        }

        void OnSuccess(int[] mapIds)
        {
            successfulLocalizations.OnNext(mapIds);
        }

        public void Dispose()
        {
            if (initialized && localizer != null)
            {
                localizer.OnFirstSuccessfulLocalization.RemoveListener(OnFirstSuccess);
                localizer.OnSuccessfulLocalizations.RemoveListener(OnSuccess);
            }
            successfulLocalizations.Dispose();
            isLocalized.Dispose();
        }
    }
}
