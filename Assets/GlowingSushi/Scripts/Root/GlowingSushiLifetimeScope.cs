using GlowingSushi.Domain;
using GlowingSushi.Service;
using GlowingSushi.View;
using GlowingSushi.ViewModel;
using Immersal.XR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using VContainer;
using VContainer.Unity;

namespace GlowingSushi.Root
{
    /// <summary>
    /// アプリのコンポジションルート。全レイヤーの依存関係をここで一括登録する。
    /// VContainer型と全レイヤーを参照してよいのはこのRoot層のみ。
    /// </summary>
    public sealed class GlowingSushiLifetimeScope : LifetimeScope
    {
        [SerializeField]
        [Tooltip("寿司の挙動パラメータ")]
        SushiBehaviorSettings behaviorSettings;

        [SerializeField]
        [Tooltip("シーン内のARPlaneManager(XR Origin上)")]
        ARPlaneManager planeManager;

        [SerializeField]
        [Tooltip("ARカメラ(XR Origin配下のMain Camera)")]
        Camera arCamera;

        [SerializeField]
        [Tooltip("平面検出時に何を出すか(水族館/表面ふるまいデモ/両方/なし)")]
        PlacementMode placementMode = PlacementMode.Both;

        [SerializeField]
        [Tooltip("Immersal Localizer(VPSを使う場合のみ設定。未設定ならVPS機能は無効)")]
        Localizer immersalLocalizer;

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上の参照・設定
            builder.RegisterInstance(behaviorSettings);
            builder.RegisterInstance(placementMode);
            builder.RegisterComponent(planeManager);
            builder.RegisterComponent(arCamera);

            // Service層
            builder.Register<CameraPoseService>(Lifetime.Singleton).As<ICameraPoseService>();
            builder.Register<ArPlaneDetectionService>(Lifetime.Singleton);
            builder.Register<TouchInputService>(Lifetime.Singleton);
            builder.Register<SushiSpawnService>(Lifetime.Singleton);

            // ViewModel層(School/SpotのVMは親VMが生成するため登録しない)
            builder.Register<AquariumViewModel>(Lifetime.Singleton);
            builder.Register<SurfaceSpotsViewModel>(Lifetime.Singleton);
            builder.Register<ArPlacementViewModel>(Lifetime.Singleton);
            builder.Register<StatusViewModel>(Lifetime.Singleton);
            builder.Register<TitleViewModel>(Lifetime.Singleton);

            // View層(シーン内のコンポーネントへ注入)
            builder.RegisterComponentInHierarchy<AquariumView>();
            builder.RegisterComponentInHierarchy<TouchEffectView>();
            builder.RegisterComponentInHierarchy<SurfaceSpotsView>();
            builder.RegisterComponentInHierarchy<BattleEffectView>();
            builder.RegisterComponentInHierarchy<StatusHudView>();

            // タイトル画面はシーンに存在する場合のみ登録する
            // (無いシーンではエントリポイントが即時開始する)
            if (FindFirstObjectByType<TitleView>(FindObjectsInactive.Include) != null)
            {
                builder.RegisterComponentInHierarchy<TitleView>();
            }

            // VPS(Immersal)関連。Localizer未設定のシーンではVPS機能を丸ごと無効にする
            if (immersalLocalizer != null)
            {
                builder.RegisterComponent(immersalLocalizer);
                builder.Register<VpsLocalizationService>(Lifetime.Singleton);
                builder.Register<VpsPlacementViewModel>(Lifetime.Singleton);
            }

            // エントリポイント
            builder.RegisterEntryPoint<GlowingSushiEntryPoint>();
        }
    }
}
