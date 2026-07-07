using GlowingSushi.Service;
using GlowingSushi.View;
using GlowingSushi.ViewModel;
using VContainer;
using VContainer.Unity;

namespace GlowingSushi.Root
{
    /// <summary>
    /// タイトルシーンのコンポジションルート。
    /// タイトル演出とメインシーンへの遷移に必要な最小限だけを登録する。
    /// </summary>
    public sealed class TitleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SceneNavigationService>(Lifetime.Singleton);
            builder.Register<TitleViewModel>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<TitleView>();
        }
    }
}
