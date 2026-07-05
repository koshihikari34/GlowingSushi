using UnityEngine;

namespace GlowingSushi.Domain
{
    /// <summary>
    /// 寿司1匹分の初期配置データ。
    /// Service層はこのデータを生成するだけで、ViewModelの生成はViewModel層が行う
    /// (Service→ViewModelの逆方向依存を作らないため)。
    /// </summary>
    public readonly struct SushiSpawnData
    {
        /// <summary>初期位置(ワールド座標)</summary>
        public Vector3 Position { get; }

        /// <summary>初期速度</summary>
        public Vector3 Velocity { get; }

        /// <summary>発光パルスの位相オフセット(個体ごとに発光タイミングをずらす)</summary>
        public float GlowPhase { get; }

        /// <summary>ふらつき計算用の個体固有シード</summary>
        public float WanderSeed { get; }

        public SushiSpawnData(Vector3 position, Vector3 velocity, float glowPhase, float wanderSeed)
        {
            Position = position;
            Velocity = velocity;
            GlowPhase = glowPhase;
            WanderSeed = wanderSeed;
        }
    }
}
