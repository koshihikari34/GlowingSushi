using System;
using GlowingSushi.Service;
using R3;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// AR平面検出の状態を公開し、最初の平面検出時に群れの出現をトリガーするViewModel。
    /// </summary>
    public sealed class ArPlacementViewModel : IDisposable
    {
        readonly ArPlaneDetectionService planeDetection;
        readonly SushiSchoolViewModel school;
        readonly ReactiveProperty<bool> isPlaced = new(false);
        IDisposable planeSubscription;

        /// <summary>群れが配置済みかどうか(UI表示などに使える)</summary>
        public ReadOnlyReactiveProperty<bool> IsPlaced => isPlaced;

        public ArPlacementViewModel(ArPlaneDetectionService planeDetection, SushiSchoolViewModel school)
        {
            this.planeDetection = planeDetection;
            this.school = school;
        }

        /// <summary>
        /// 平面検出の購読を開始する。エントリポイントから起動時に呼ぶ。
        /// </summary>
        public void Initialize()
        {
            planeSubscription ??= planeDetection.FirstPlaneDetected.Subscribe(pose =>
            {
                school.Spawn(pose);
                isPlaced.Value = true;
            });
        }

        public void Dispose()
        {
            planeSubscription?.Dispose();
            isPlaced.Dispose();
        }
    }
}
