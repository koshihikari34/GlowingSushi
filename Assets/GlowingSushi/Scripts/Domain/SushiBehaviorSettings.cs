using UnityEngine;

namespace GlowingSushi.Domain
{
    /// <summary>
    /// 寿司の群泳・接近・逃走・発光の挙動パラメータ。
    /// インスペクタで調整できるよう ScriptableObject として保持する。
    /// </summary>
    [CreateAssetMenu(menuName = "GlowingSushi/SushiBehaviorSettings", fileName = "SushiBehaviorSettings")]
    public sealed class SushiBehaviorSettings : ScriptableObject
    {
        [Header("群れの構成")]
        [Tooltip("群れの寿司の数")]
        public int schoolSize = 10;

        [Tooltip("アンカーから群れが離れられる半径(超えると引き戻される)")]
        public float containmentRadius = 1.5f;

        [Tooltip("平面からどれだけ上に群れの中心を置くか(メートル)")]
        public float spawnHeightAbovePlane = 0.6f;

        [Header("Boid(群泳)")]
        [Tooltip("仲間として認識する距離")]
        public float neighborRadius = 1.0f;

        [Tooltip("これより近い仲間からは離れる")]
        public float separationRadius = 0.35f;

        [Tooltip("分離の重み")]
        public float separationWeight = 1.5f;

        [Tooltip("整列の重み")]
        public float alignmentWeight = 1.0f;

        [Tooltip("結合の重み")]
        public float cohesionWeight = 1.0f;

        [Tooltip("ふらつきの重み")]
        public float wanderWeight = 0.6f;

        [Header("速度・操舵")]
        [Tooltip("通常時の最大速度(m/s)")]
        public float maxSpeed = 0.6f;

        [Tooltip("逃走時の最大速度(m/s)")]
        public float fleeSpeed = 1.5f;

        [Tooltip("接近時の最大速度(m/s)")]
        public float approachSpeed = 0.8f;

        [Tooltip("操舵力の上限(加速度)")]
        public float maxSteerForce = 2.0f;

        [Header("接近行動")]
        [Tooltip("接近するかどうかを判定する間隔(秒)")]
        public float approachInterval = 5f;

        [Tooltip("判定ごとに接近を始める確率(0〜1)")]
        [Range(0f, 1f)]
        public float approachProbability = 0.3f;

        [Tooltip("接近を続ける最大時間(秒)")]
        public float approachDuration = 6f;

        [Tooltip("カメラにこの距離まで近づいたら群れに戻る(メートル)")]
        public float approachStopDistance = 0.5f;

        [Header("タッチ・逃走")]
        [Tooltip("タッチ判定に使う寿司の半径(メートル)")]
        public float touchHitRadius = 0.15f;

        [Tooltip("タッチされた個体の周囲この距離内の仲間も一緒に逃げる")]
        public float fleePropagationRadius = 0.4f;

        [Tooltip("逃走を続ける時間(秒)")]
        public float fleeDuration = 3f;

        [Header("発光")]
        [Tooltip("発光パルスの周期(秒)")]
        public float glowPulsePeriod = 2f;

        [Tooltip("発光強度の最小値")]
        public float glowMinIntensity = 0.5f;

        [Tooltip("発光強度の最大値")]
        public float glowMaxIntensity = 2.5f;

        [Tooltip("逃走時に発光強度へ掛ける係数")]
        public float fleeGlowMultiplier = 1.5f;
    }
}
