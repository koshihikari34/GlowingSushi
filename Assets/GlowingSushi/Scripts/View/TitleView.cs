using GlowingSushi.ViewModel;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// タイトル画面のView。
    /// タイトルの下に小さなマグロがふわふわ浮き、「タップスタート」が点滅する。
    /// 画面タップでテキストが消え、マグロが上に跳ねながら回転して消え、
    /// 画面全体がフェードアウトしてARへ遷移する(開始はTitleViewModel経由で通知)。
    /// </summary>
    public sealed class TitleView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("タイトル文字列")]
        string titleLabel = "GlowingSushi";

        [SerializeField]
        [Tooltip("タイトルのText")]
        Text titleText;

        [SerializeField]
        [Tooltip("タップスタートのText(点滅する)")]
        Text tapStartText;

        [SerializeField]
        [Tooltip("全画面の透明ボタン(タップ検出用)")]
        Button tapButton;

        [SerializeField]
        [Tooltip("フェードアウトに使うCanvasGroup")]
        CanvasGroup canvasGroup;

        [SerializeField]
        [Tooltip("タイトルに表示するマグロのプレハブ")]
        SushiView maguroPrefab;

        [Header("マグロの表示調整")]
        [SerializeField]
        [Tooltip("カメラからの相対位置")]
        Vector3 maguroLocalPosition = new(0f, 0.03f, 0.6f);

        [SerializeField]
        [Tooltip("表示スケール(小さく見せる)")]
        float maguroScale = 0.6f;

        [Header("開始演出")]
        [SerializeField]
        [Tooltip("ジャンプの初速(m/s)")]
        float jumpSpeed = 1.2f;

        [SerializeField]
        [Tooltip("ジャンプ中の重力(m/s^2)")]
        float jumpGravity = 2.5f;

        [SerializeField]
        [Tooltip("回転速度(度/秒)")]
        float spinSpeed = 540f;

        [SerializeField]
        [Tooltip("演出全体の時間(秒)。この時間でマグロが縮んで消える")]
        float startDuration = 0.9f;

        TitleViewModel viewModel;
        Transform maguro;
        Vector3 maguroBasePosition;
        Vector3 maguroBaseScale;
        float elapsedTime;
        bool starting;
        float startedAt;
        Vector3 jumpVelocity;

        [Inject]
        public void Construct(TitleViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            if (titleText != null) titleText.text = titleLabel;
            if (tapButton != null) tapButton.onClick.AddListener(OnTapped);
            SpawnMaguro();
        }

        /// <summary>タイトル用のマグロをカメラ前方へ生成する(発光・軌跡は使わない見た目専用)</summary>
        void SpawnMaguro()
        {
            var camera = Camera.main;
            if (camera == null || maguroPrefab == null) return;

            maguro = Instantiate(maguroPrefab, camera.transform).transform;
            maguro.localPosition = maguroLocalPosition;
            maguro.localRotation = Quaternion.Euler(0f, 200f, 0f); // 少し斜めにして立体感を出す
            maguro.localScale *= maguroScale;
            maguroBasePosition = maguro.localPosition;
            maguroBaseScale = maguro.localScale;

            // タイトルでは軌跡パーティクルを使わない(SushiViewはBindしないため発光もしない)
            foreach (var particles in maguro.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            elapsedTime += Time.deltaTime;
            if (!starting)
            {
                UpdateIdle();
            }
            else
            {
                UpdateStarting();
            }
        }

        /// <summary>待機中: マグロのふわふわ浮遊とタップスタートの点滅</summary>
        void UpdateIdle()
        {
            if (maguro != null)
            {
                var bob = Mathf.Sin(elapsedTime * 2f) * 0.012f;
                maguro.localPosition = maguroBasePosition + Vector3.up * bob;
                // ゆっくり揺れる(浮遊感)
                maguro.localRotation = Quaternion.Euler(
                    Mathf.Sin(elapsedTime * 1.3f) * 5f, 200f + Mathf.Sin(elapsedTime * 0.8f) * 10f, 0f);
            }

            if (tapStartText != null)
            {
                var color = tapStartText.color;
                color.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(elapsedTime * 2.5f));
                tapStartText.color = color;
            }
        }

        /// <summary>開始演出中: マグロが上に跳ねながら回転して縮み、UI全体がフェードアウトする</summary>
        void UpdateStarting()
        {
            var t = elapsedTime - startedAt;
            var deltaTime = Time.deltaTime;

            if (maguro != null)
            {
                maguro.localPosition += jumpVelocity * deltaTime;
                jumpVelocity.y -= jumpGravity * deltaTime;
                maguro.Rotate(0f, spinSpeed * deltaTime, 0f, Space.Self);
                // 不透明マテリアルのためフェードはスケール縮小で表現する
                maguro.localScale = maguroBaseScale * Mathf.Clamp01(1f - t / startDuration);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(1f - t / (startDuration * 0.7f));
            }

            if (t >= startDuration)
            {
                enabled = false; // 多重遷移の防止
                viewModel.CompleteIntro(); // メインシーンへ遷移(タイトルシーンごと破棄される)
            }
        }

        /// <summary>タップスタート: テキストを消し、開始演出へ移行してViewModelへ通知する</summary>
        void OnTapped()
        {
            if (starting) return;
            starting = true;
            startedAt = elapsedTime;
            jumpVelocity = Vector3.up * jumpSpeed;

            if (titleText != null) titleText.enabled = false;
            if (tapStartText != null) tapStartText.enabled = false;
            if (tapButton != null) tapButton.interactable = false;

            viewModel.Start();
        }
    }
}
