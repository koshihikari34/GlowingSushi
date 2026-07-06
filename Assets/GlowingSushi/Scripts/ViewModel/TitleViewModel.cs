using System;
using GlowingSushi.Service;
using R3;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// タイトルシーンのViewModel。
    /// タップスタートで開始状態になり、開始演出の完了後にメインシーンへ遷移する。
    /// メインシーン側では(タイトルViewが存在しないため)エントリポイントが即時開始に使う。
    /// </summary>
    public sealed class TitleViewModel : IDisposable
    {
        readonly SceneNavigationService navigation;
        readonly ReactiveProperty<bool> isStarted = new(false);

        public TitleViewModel(SceneNavigationService navigation)
        {
            this.navigation = navigation;
        }

        /// <summary>タップスタート済みかどうか</summary>
        public ReadOnlyReactiveProperty<bool> IsStarted => isStarted;

        /// <summary>タイトル画面のタップ時にViewから呼ばれるコマンド</summary>
        public void Start()
        {
            isStarted.Value = true;
        }

        /// <summary>開始演出の完了時にViewから呼ばれ、メインシーンへ遷移する</summary>
        public void CompleteIntro()
        {
            navigation.LoadMainScene();
        }

        public void Dispose()
        {
            isStarted.Dispose();
        }
    }
}
