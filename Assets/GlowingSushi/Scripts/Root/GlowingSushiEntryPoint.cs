using System;
using GlowingSushi.Service;
using GlowingSushi.View;
using GlowingSushi.ViewModel;
using R3;
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
    public sealed class GlowingSushiEntryPoint : IStartable, ITickable, IDisposable
    {
        readonly TouchInputService touchInput;
        readonly ArPlaneDetectionService planeDetection;
        readonly ArPlacementViewModel placement;
        readonly AquariumViewModel aquarium;
        readonly SurfaceSpotsViewModel surfaceSpots;
        readonly StatusViewModel status;
        readonly TitleViewModel title;
        readonly IObjectResolver resolver;
        VpsPlacementViewModel vpsPlacement;
        IDisposable startGate;

        public GlowingSushiEntryPoint(
            TouchInputService touchInput,
            ArPlaneDetectionService planeDetection,
            ArPlacementViewModel placement,
            AquariumViewModel aquarium,
            SurfaceSpotsViewModel surfaceSpots,
            StatusViewModel status,
            TitleViewModel title,
            IObjectResolver resolver)
        {
            this.touchInput = touchInput;
            this.planeDetection = planeDetection;
            this.placement = placement;
            this.aquarium = aquarium;
            this.surfaceSpots = surfaceSpots;
            this.status = status;
            this.title = title;
            this.resolver = resolver;
        }

        public void Start()
        {
            touchInput.Initialize();

            // コンテンツ出現系(平面検出・VPS)はタイトルのタップスタート後に開始する
            startGate = title.IsStarted
                .Where(started => started)
                .Take(1)
                .Subscribe(_ => StartContent());

            // タイトル画面が無いシーンでは即時開始する
            if (!resolver.TryResolve<TitleView>(out _))
            {
                title.Start();
            }
        }

        /// <summary>コンテンツ出現系の初期化。タップスタート後(またはタイトル無しシーンでは起動時)に1回だけ呼ばれる</summary>
        void StartContent()
        {
            // 購読の開始順: 配置→平面検出(検出イベントを取りこぼさないよう配置購読を先に確立)
            placement.Initialize();
            planeDetection.Initialize();

            // VPS(Immersal)はLocalizer設定済みのシーンでのみ登録されているため、任意解決で初期化する
            if (resolver.TryResolve<VpsPlacementViewModel>(out vpsPlacement))
            {
                vpsPlacement.Initialize();
            }
            if (resolver.TryResolve<VpsLocalizationService>(out var vpsLocalization))
            {
                vpsLocalization.Initialize();
                status.AttachVps(vpsLocalization, vpsPlacement);
            }
        }

        public void Dispose()
        {
            startGate?.Dispose();
        }

        public void Tick()
        {
            var deltaTime = Time.deltaTime;
            aquarium.Tick(deltaTime);
            surfaceSpots.Tick(deltaTime);
            // VPSスポットの配置/追従更新(成功イベントの翌フレームに処理される)
            vpsPlacement?.Tick(deltaTime);
        }
    }
}
