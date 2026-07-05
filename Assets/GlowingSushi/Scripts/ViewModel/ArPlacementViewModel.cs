using System;
using GlowingSushi.Domain;
using GlowingSushi.Service;
using R3;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// AR平面検出の状態を公開し、最初の平面検出時にコンテンツの出現をトリガーするViewModel。
    /// PlacementModeに応じて水族館(泳ぐ寿司)と表面ふるまいスポットを出し分ける。
    /// </summary>
    public sealed class ArPlacementViewModel : IDisposable
    {
        readonly ArPlaneDetectionService planeDetection;
        readonly AquariumViewModel aquarium;
        readonly SurfaceSpotsViewModel surfaceSpots;
        readonly PlacementMode mode;
        readonly ReactiveProperty<bool> isPlaced = new(false);
        IDisposable planeSubscription;

        /// <summary>コンテンツが配置済みかどうか(UI表示などに使える)</summary>
        public ReadOnlyReactiveProperty<bool> IsPlaced => isPlaced;

        public ArPlacementViewModel(
            ArPlaneDetectionService planeDetection,
            AquariumViewModel aquarium,
            SurfaceSpotsViewModel surfaceSpots,
            PlacementMode mode)
        {
            this.planeDetection = planeDetection;
            this.aquarium = aquarium;
            this.surfaceSpots = surfaceSpots;
            this.mode = mode;
        }

        /// <summary>
        /// 平面検出の購読を開始する。エントリポイントから起動時に呼ぶ。
        /// </summary>
        public void Initialize()
        {
            planeSubscription ??= planeDetection.FirstPlaneDetected.Subscribe(pose =>
            {
                if (mode is PlacementMode.Aquarium or PlacementMode.Both)
                {
                    aquarium.Spawn(pose);
                }
                if (mode is PlacementMode.SurfaceSpotsDemo or PlacementMode.Both)
                {
                    surfaceSpots.SpawnDemo(pose);
                }
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
