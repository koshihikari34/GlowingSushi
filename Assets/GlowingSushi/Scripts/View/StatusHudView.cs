using GlowingSushi.ViewModel;
using R3;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// 動作状態を画面へ常時表示するデバッグHUD。
    /// 現地検証で「SDKが初期化できたか」「ローカライズが試行/成功/失敗しているか」を
    /// その場で判断できるようにする。
    /// </summary>
    public sealed class StatusHudView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("状態を表示するText")]
        Text statusText;

        StatusViewModel viewModel;
        float elapsedTime;

        [Inject]
        public void Construct(StatusViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Update()
        {
            if (statusText == null || viewModel == null) return;
            elapsedTime += Time.deltaTime;
            statusText.text = BuildStatusText();
        }

        string BuildStatusText()
        {
            var plane = viewModel.IsPlaneContentPlaced.CurrentValue ? "配置済み" : "検出待ち";

            if (!viewModel.HasVps)
            {
                return $"経過 {elapsedTime:F0}s | 平面: {plane} | VPS: 無効";
            }

            var localized = viewModel.IsLocalized.CurrentValue ? "成功" : "未成功";
            return
                $"経過 {elapsedTime:F0}s | 平面: {plane}\n" +
                $"SDK: {viewModel.SdkStatus.CurrentValue}\n" +
                $"VPS: {localized} | 試行 {viewModel.AttemptCount.CurrentValue} " +
                $"/ 成功 {viewModel.SuccessCount.CurrentValue} / 失敗 {viewModel.FailureCount.CurrentValue}\n" +
                $"成功マップ: {viewModel.LastLocalizedMaps.CurrentValue}";
        }
    }
}
