using System;
using R3;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// タイトル画面の状態を管理するViewModel。
    /// タップスタートで開始状態になり、コンテンツ出現系(平面検出・VPS)の初期化はこれを待つ。
    /// </summary>
    public sealed class TitleViewModel : IDisposable
    {
        readonly ReactiveProperty<bool> isStarted = new(false);

        /// <summary>タップスタート済みかどうか。エントリポイントがこれを購読してAR開始をゲートする</summary>
        public ReadOnlyReactiveProperty<bool> IsStarted => isStarted;

        /// <summary>タイトル画面のタップ時にViewから呼ばれるコマンド</summary>
        public void Start()
        {
            isStarted.Value = true;
        }

        public void Dispose()
        {
            isStarted.Dispose();
        }
    }
}
