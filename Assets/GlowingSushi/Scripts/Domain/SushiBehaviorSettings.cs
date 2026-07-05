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
        [Header("水族館(群れの複数配置)")]
        [Tooltip("群れの数")]
        public int schoolCount = 3;

        [Tooltip("群れごとの発光色(HDR)。群れ数がパレット数を超えたら循環する")]
        [ColorUsage(false, true)]
        public Color[] schoolColors =
        {
            new(0.2f, 0.9f, 1.0f), // シアン(深海の発光クラゲ風)
            new(1.0f, 0.6f, 0.2f), // オレンジ(暖色の提灯風)
            new(1.0f, 0.3f, 0.9f), // マゼンタ(ネオン風)
        };

        [Tooltip("群れ同士の水平方向の間隔(メートル)。行動半径より大きくして群れ同士が混ざらないようにする")]
        public float schoolSpacing = 1.6f;

        [Tooltip("群れごとの高さの差(メートル)")]
        public float schoolHeightStep = 0.4f;

        [Header("群れの構成")]
        [Tooltip("1つの群れの寿司の数")]
        public int schoolSize = 14;

        [Tooltip("アンカーから群れが離れられる半径(超えると引き戻される)")]
        public float containmentRadius = 0.9f;

        [Tooltip("平面からどれだけ上に群れの中心を置くか(メートル)")]
        public float spawnHeightAbovePlane = 0.6f;

        [Header("Boid(群泳)")]
        [Tooltip("仲間として認識する距離")]
        public float neighborRadius = 0.8f;

        [Tooltip("これより近い仲間からは離れる")]
        public float separationRadius = 0.25f;

        [Tooltip("分離の重み")]
        public float separationWeight = 1.8f;

        [Tooltip("整列の重み(強めると群れで同じ方向に泳ぐ魚群らしさが出る)")]
        public float alignmentWeight = 1.8f;

        [Tooltip("結合の重み(強めると群れが密集してまとまる)")]
        public float cohesionWeight = 1.8f;

        [Tooltip("ふらつきの重み")]
        public float wanderWeight = 0.15f;

        [Header("軌道アトラクタ(流れるような群泳)")]
        [Tooltip("群れが追いかける軌道目標の半径(メートル)")]
        public float orbitRadius = 0.5f;

        [Tooltip("軌道目標が1周する時間(秒)")]
        public float orbitPeriod = 18f;

        [Tooltip("軌道目標へのSeek重み")]
        public float orbitTargetWeight = 1.5f;

        [Tooltip("軌道目標の上下の揺れ幅(メートル)")]
        public float orbitVerticalBob = 0.1f;

        [Tooltip("旋回時に機体を内側へ傾ける(バンク)強度")]
        public float bankFactor = 30f;

        [Header("接近専用個体(お客好き寿司)")]
        [Tooltip("群れとは別にカメラへ寄ってくる個体の数")]
        public int curiousCount = 2;

        [Tooltip("接近専用個体の発光色(HDR)")]
        [ColorUsage(false, true)]
        public Color curiousColor = new(1.0f, 0.85f, 0.3f); // 金色

        [Header("速度・操舵")]
        [Tooltip("通常時の最大速度(m/s)")]
        public float maxSpeed = 0.45f;

        [Tooltip("最低速度(m/s)。魚のように止まらず泳ぎ続けさせる")]
        public float minSpeed = 0.2f;

        [Tooltip("垂直方向の速度減衰(0=減衰なし〜1=強)。主に水平に泳がせる")]
        [Range(0f, 1f)]
        public float verticalDamping = 0.6f;

        [Tooltip("逃走時の最大速度(m/s)")]
        public float fleeSpeed = 1.5f;

        [Tooltip("接近時の最大速度(m/s)")]
        public float approachSpeed = 0.8f;

        [Tooltip("操舵力の上限(加速度)")]
        public float maxSteerForce = 2.0f;

        [Header("接近行動(接近専用個体のみが使用)")]
        [Tooltip("接近するかどうかを判定する間隔(秒)")]
        public float approachInterval = 4f;

        [Tooltip("判定ごとに接近を始める確率(0〜1)")]
        [Range(0f, 1f)]
        public float approachProbability = 0.5f;

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

        [Header("表面ふるまい(共通)")]
        [Tooltip("1スポットのエリア半径(メートル)")]
        public float spotRadius = 0.25f;

        [Tooltip("1スポットあたりの寿司の数")]
        public int membersPerSpot = 3;

        [Tooltip("表面から寿司の中心までの高さ(メートル)")]
        public float surfaceOffset = 0.035f;

        [Header("転がり(Rolling)")]
        [Tooltip("転がる速さ(m/s)")]
        public float rollSpeed = 0.15f;

        [Tooltip("接地半径。小さいほどよく回転する(メートル)")]
        public float rollContactRadius = 0.04f;

        [Header("昼寝(Napping)")]
        [Tooltip("呼吸のような上下の周期(秒)")]
        public float napBreathPeriod = 3.5f;

        [Tooltip("呼吸の上下幅(メートル)")]
        public float napBreathAmplitude = 0.004f;

        [Header("散歩(Strolling)")]
        [Tooltip("歩く速さ(m/s)")]
        public float strollSpeed = 0.05f;

        [Tooltip("進行方向の変わりやすさ(rad/s)")]
        public float strollTurnRate = 1.2f;

        [Header("ベイブレード(Battle)")]
        [Tooltip("スピンの回転速度(度/秒)")]
        public float spinSpeed = 720f;

        [Tooltip("移動の速さ(m/s)")]
        public float battleMoveSpeed = 0.25f;

        [Tooltip("衝突判定の半径(メートル)")]
        public float battleHitRadius = 0.08f;

        [Tooltip("同じペアの連続衝突を無視する時間(秒)")]
        public float battleClashCooldown = 0.3f;

        [Header("発光")]
        [Tooltip("発光パルスの周期(秒)")]
        public float glowPulsePeriod = 2f;

        [Tooltip("発光強度の最小値")]
        public float glowMinIntensity = 0.35f;

        [Tooltip("発光強度の最大値")]
        public float glowMaxIntensity = 1.5f;

        [Tooltip("逃走時に発光強度へ掛ける係数")]
        public float fleeGlowMultiplier = 1.5f;
    }
}
