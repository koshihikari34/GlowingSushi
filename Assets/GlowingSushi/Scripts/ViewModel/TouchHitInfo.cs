using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// タッチが寿司に命中したときのイベントデータ。
    /// Viewはこれを購読してエフェクト再生・効果音再生を行う。
    /// </summary>
    public readonly struct TouchHitInfo
    {
        /// <summary>命中したワールド座標</summary>
        public Vector3 Position { get; }

        /// <summary>命中した寿司が属する群れの発光色(エフェクトのティントに使う)</summary>
        public Color GlowColor { get; }

        public TouchHitInfo(Vector3 position, Color glowColor)
        {
            Position = position;
            GlowColor = glowColor;
        }
    }
}
