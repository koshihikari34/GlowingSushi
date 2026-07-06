using GlowingSushi.ViewModel;
using R3;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// ベイブレード(Battle)スポットの衝突演出View。
    /// SurfaceSpotsViewModelのBattleClashを購読し、衝突位置で火花パーティクルと衝突音を再生する。
    /// 寿司本体は発光しないが、火花はHDRマテリアルでBloom発光させる。
    /// </summary>
    public sealed class BattleEffectView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("衝突時に再生する火花ParticleSystem")]
        ParticleSystem sparkParticles;

        [SerializeField]
        [Tooltip("衝突音を再生するAudioSource")]
        AudioSource audioSource;

        [SerializeField]
        [Tooltip("衝突時の効果音(金属的なキン音)")]
        AudioClip clashSound;

        SurfaceSpotsViewModel viewModel;

        [Inject]
        public void Construct(SurfaceSpotsViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            viewModel.BattleClash
                .Subscribe(PlayEffect)
                .AddTo(this);
        }

        /// <summary>衝突位置へ移動し、強度に応じた火花と音を再生する</summary>
        void PlayEffect(BattleClashInfo clash)
        {
            transform.position = clash.Position;

            if (sparkParticles != null)
            {
                sparkParticles.Play();
            }

            if (audioSource != null && clashSound != null)
            {
                // 強度で音量に変化をつける(0.5〜1.0)
                audioSource.PlayOneShot(clashSound, 0.5f + clash.Intensity * 0.5f);
            }
        }
    }
}
