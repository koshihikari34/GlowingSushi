using GlowingSushi.Domain;
using GlowingSushi.ViewModel;
using UnityEngine;
using VContainer;

namespace GlowingSushi.View
{
    /// <summary>
    /// VPSマップ内の配置ポイント。XRSpace(マップごと)の子として置き、
    /// マップ座標系での位置・向き(Y軸=表面の法線)をエディタで調整する。
    /// 起動時に自分の情報をVpsPlacementViewModelへ登録し、
    /// 対応マップのローカライズ成功後にこの位置へ表面ふるまいスポットが出現する。
    /// </summary>
    public sealed class VpsAnchorView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("このアンカーが属するImmersalマップID")]
        int mapId;

        [SerializeField]
        [Tooltip("この位置に出現させるふるまい種別")]
        SurfaceBehaviorType behaviorType;

        VpsPlacementViewModel viewModel;

        [Inject]
        public void Construct(VpsPlacementViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        void Start()
        {
            if (viewModel == null)
            {
                Debug.LogWarning($"[GlowingSushi] VpsAnchorView({name})が注入されていません。LifetimeScopeのAuto Inject Game Objectsを確認してください。");
                return;
            }
            viewModel.RegisterAnchor(mapId, behaviorType, transform);
        }

#if UNITY_EDITOR
        /// <summary>エディタでの配置作業用に、位置と向きを可視化する</summary>
        void OnDrawGizmos()
        {
            Gizmos.color = behaviorType switch
            {
                SurfaceBehaviorType.Rolling => Color.cyan,
                SurfaceBehaviorType.Napping => Color.green,
                SurfaceBehaviorType.Strolling => Color.yellow,
                SurfaceBehaviorType.Battle => Color.red,
                _ => Color.white,
            };
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.3f);
        }
#endif
    }
}
