using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// ベイブレード(Battle)スポットで寿司同士が衝突したときのイベントデータ。
    /// Viewはこれを購読して火花パーティクルと衝突音を再生する。
    /// </summary>
    public readonly struct BattleClashInfo
    {
        /// <summary>衝突位置(ワールド座標)</summary>
        public Vector3 Position { get; }

        /// <summary>衝突の強さ(相対速度ベース、0〜1程度)。音量や火花の量に使える</summary>
        public float Intensity { get; }

        public BattleClashInfo(Vector3 position, float intensity)
        {
            Position = position;
            Intensity = intensity;
        }
    }
}
