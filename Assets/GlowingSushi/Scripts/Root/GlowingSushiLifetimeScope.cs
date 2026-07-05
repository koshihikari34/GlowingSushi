using GlowingSushi.Domain;
using GlowingSushi.Service;
using GlowingSushi.View;
using GlowingSushi.ViewModel;
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

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上の参照
            builder.RegisterInstance(behaviorSettings);
            builder.RegisterComponent(planeManager);
            builder.RegisterComponent(arCamera);

            // Service層
            builder.Register<CameraPoseService>(Lifetime.Singleton).As<ICameraPoseService>();
            builder.Register<ArPlaneDetectionService>(Lifetime.Singleton);
            builder.Register<TouchInputService>(Lifetime.Singleton);
            builder.Register<SushiSpawnService>(Lifetime.Singleton);

            // ViewModel層(SushiSchoolViewModelはAquariumViewModelが生成するため登録しない)
            builder.Register<AquariumViewModel>(Lifetime.Singleton);
            builder.Register<ArPlacementViewModel>(Lifetime.Singleton);

            // View層(シーン内のコンポーネントへ注入)
            builder.RegisterComponentInHierarchy<AquariumView>();
            builder.RegisterComponentInHierarchy<TouchEffectView>();

            // エントリポイント
            builder.RegisterEntryPoint<GlowingSushiEntryPoint>();
        }
    }
}
