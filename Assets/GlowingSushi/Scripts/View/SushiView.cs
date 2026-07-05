using GlowingSushi.ViewModel;
using R3;
using UnityEngine;

namespace GlowingSushi.View
{
    /// <summary>
    /// 寿司1匹分のView。ViewModelのReactivePropertyを購読して
    /// Transform・発光(Emission)・軌跡パーティクルへ反映するだけの受動的なコンポーネント。
    /// 発光色は所属する群れの色(ViewModelのGlowColor)を使う。
    /// </summary>
    public sealed class SushiView : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        [Tooltip("発光させる寿司モデルのRenderer")]
        Renderer bodyRenderer;

        [SerializeField]
        [Tooltip("泳いだ軌跡に発光粒子を残すParticleSystem")]
        ParticleSystem trailParticles;

        MaterialPropertyBlock propertyBlock;
        Color glowColor = Color.white;

        /// <summary>
        /// ViewModelを結び付けて購読を開始する。生成直後にAquariumViewから呼ばれる。
        /// 購読はAddTo(this)でこのGameObjectの破棄と一緒に解除される。
        /// </summary>
        public void Bind(SushiViewModel viewModel)
        {
            glowColor = viewModel.GlowColor;

            // 軌跡パーティクルを群れの色にティントする
            if (trailParticles != null)
            {
                var main = trailParticles.main;
                main.startColor = glowColor;
            }

            viewModel.Position
                .Subscribe(position => transform.position = position)
                .AddTo(this);

            viewModel.Rotation
                .Subscribe(rotation => transform.rotation = rotation)
                .AddTo(this);

            viewModel.GlowIntensity
                .Subscribe(ApplyGlow)
                .AddTo(this);

            // State購読は未使用(状態別アニメーションを入れる際にここへ追加する)
        }

        /// <summary>
        /// 発光強度をMaterialPropertyBlock経由でEmissionへ反映する(マテリアル複製を避ける)。
        /// </summary>
        void ApplyGlow(float intensity)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            bodyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, glowColor * intensity);
            bodyRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
