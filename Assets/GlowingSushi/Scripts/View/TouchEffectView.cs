using GlowingSushi.ViewModel;
using R3;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// タッチが寿司に命中したときの演出View。
    /// AquariumViewModelのTouchHitを購読し、命中位置でパーティクルバーストと効果音を再生する。
    /// </summary>
    public sealed class TouchEffectView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("命中時に再生するバーストParticleSystem")]
        ParticleSystem burstParticles;

        [SerializeField]
        [Tooltip("命中音を再生するAudioSource")]
        AudioSource audioSource;

        [SerializeField]
        [Tooltip("命中時の効果音")]
        AudioClip hitSound;

        AquariumViewModel viewModel;

        [Inject]
        public void Construct(AquariumViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            viewModel.TouchHit
                .Subscribe(PlayEffect)
                .AddTo(this);
        }

        /// <summary>命中位置へ移動し、群れの色にティントしたバーストと効果音を再生する</summary>
        void PlayEffect(TouchHitInfo hit)
        {
            transform.position = hit.Position;

            if (burstParticles != null)
            {
                var main = burstParticles.main;
                main.startColor = hit.GlowColor;
                burstParticles.Play();
            }

            if (audioSource != null && hitSound != null)
            {
                audioSource.PlayOneShot(hitSound);
            }
        }
    }
}
