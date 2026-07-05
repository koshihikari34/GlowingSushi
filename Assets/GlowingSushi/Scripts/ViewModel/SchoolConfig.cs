using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 群れ1つ分の生成設定。AquariumViewModelが組み立ててSushiSchoolViewModelへ渡す。
    /// </summary>
    public readonly struct SchoolConfig
    {
        /// <summary>群れのアンカー(引き戻し・軌道の中心)</summary>
        public Vector3 Anchor { get; }

        /// <summary>群れの発光色</summary>
        public Color GlowColor { get; }

        /// <summary>個体数</summary>
        public int MemberCount { get; }

        /// <summary>軌道アトラクタを使うか(通常の群れ=true、接近専用グループ=false)</summary>
        public bool OrbitEnabled { get; }

        /// <summary>軌道の初期位相(ラジアン)。群れごとにずらして画に変化をつける</summary>
        public float OrbitPhase { get; }

        /// <summary>軌道の回転方向(true=時計回り)</summary>
        public bool OrbitClockwise { get; }

        /// <summary>メンバーがカメラへ接近してよいか(接近専用グループのみtrue)</summary>
        public bool CanApproach { get; }

        public SchoolConfig(
            Vector3 anchor,
            Color glowColor,
            int memberCount,
            bool orbitEnabled,
            float orbitPhase,
            bool orbitClockwise,
            bool canApproach)
        {
            Anchor = anchor;
            GlowColor = glowColor;
            MemberCount = memberCount;
            OrbitEnabled = orbitEnabled;
            OrbitPhase = orbitPhase;
            OrbitClockwise = orbitClockwise;
            CanApproach = canApproach;
        }
    }
}
