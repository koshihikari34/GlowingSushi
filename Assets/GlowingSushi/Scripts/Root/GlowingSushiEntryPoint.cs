using GlowingSushi.Service;
using GlowingSushi.ViewModel;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GlowingSushi.Root
{
    /// <summary>
    /// アプリのエントリポイント。起動時に各サービス・ViewModelを初期化し、
    /// 毎フレームのTickでSushiSchoolViewModelのシミュレーションを駆動する。
    /// VContainerのPlayerLoopからIStartable/ITickableとして呼ばれる。
    /// </summary>
    public sealed class GlowingSushiEntryPoint : IStartable, ITickable
    {
        readonly TouchInputService touchInput;
        readonly ArPlaneDetectionService planeDetection;
        readonly ArPlacementViewModel placement;
        readonly AquariumViewModel aquarium;
        readonly SurfaceSpotsViewModel surfaceSpots;
        readonly StatusViewModel status;
        readonly IObjectResolver resolver;

        public GlowingSushiEntryPoint(
            TouchInputService touchInput,
            ArPlaneDetectionService planeDetection,
            ArPlacementViewModel placement,
            AquariumViewModel aquarium,
            SurfaceSpotsViewModel surfaceSpots,
            StatusViewModel status,
            IObjectResolver resolver)
        {
            this.touchInput = touchInput;
            this.planeDetection = planeDetection;
            this.placement = placement;
            this.aquarium = aquarium;
            this.surfaceSpots = surfaceSpots;
            this.status = status;
            this.resolver = resolver;
        }

        public void Start()
        {
            // 購読の開始順: 入力→平面検出→配置(検出イベントを取りこぼさないよう配置購読を先に確立)
            touchInput.Initialize();
            placement.Initialize();
            planeDetection.Initialize();

            // VPS(Immersal)はLocalizer設定済みのシーンでのみ登録されているため、任意解決で初期化する
            if (resolver.TryResolve<VpsPlacementViewModel>(out var vpsPlacement))
            {
                vpsPlacement.Initialize();
            }
            if (resolver.TryResolve<VpsLocalizationService>(out var vpsLocalization))
            {
                vpsLocalization.Initialize();
                status.AttachVps(vpsLocalization);
            }
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            aquarium.Tick(deltaTime);
            surfaceSpots.Tick(deltaTime);
        }
    }
}
