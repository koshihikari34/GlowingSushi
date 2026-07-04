using GlowingSushi.ViewModel;
using R3;
using UnityEngine;

namespace GlowingSushi.View
{
    /// <summary>
    /// 寿司1匹分のView。ViewModelのReactivePropertyを購読して
    /// Transformと発光(Emission)へ反映するだけの受動的なコンポーネント。
    /// </summary>
    public sealed class SushiView : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        [Tooltip("発光させる寿司モデルのRenderer")]
        Renderer bodyRenderer;

        [SerializeField]
        [Tooltip("発光の基本色(GlowIntensityが掛け算される)")]
        [ColorUsage(false, true)]
        Color baseEmissionColor = new(1f, 0.6f, 0.2f);

        MaterialPropertyBlock propertyBlock;

        /// <summary>
        /// ViewModelを結び付けて購読を開始する。生成直後にSushiSchoolViewから呼ばれる。
        /// 購読はAddTo(this)でこのGameObjectの破棄と一緒に解除される。
        /// </summary>
        public void Bind(SushiViewModel viewModel)
        {
            viewModel.Position
                .Subscribe(position => transform.position = position)
                .AddTo(this);

            viewModel.Rotation
                .Subscribe(rotation => transform.rotation = rotation)
                .AddTo(this);

            viewModel.GlowIntensity
                .Subscribe(ApplyGlow)
                .AddTo(this);

            // State購読はPhase 1では未使用(状態別アニメーションを入れる際にここへ追加する)
        }

        /// <summary>
        /// 発光強度をMaterialPropertyBlock経由でEmissionへ反映する(マテリアル複製を避ける)。
        /// </summary>
        void ApplyGlow(float intensity)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, baseEmissionColor * intensity);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
