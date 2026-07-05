namespace GlowingSushi.Domain
{
    /// <summary>
    /// 寿司個体の行動状態。
    /// </summary>
    public enum SushiState
    {
        /// <summary>群れと一緒に泳いでいる通常状態</summary>
        Schooling,

        /// <summary>プレイヤー(カメラ)に近づいている状態</summary>
        Approaching,

        /// <summary>タッチされて逃走している状態</summary>
        Fleeing,
    }
}
