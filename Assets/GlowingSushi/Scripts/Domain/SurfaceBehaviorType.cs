namespace GlowingSushi.Domain
{
    /// <summary>
    /// 表面(自販機の天面・ベンチ・テーブルなど)に紐づいた寿司のふるまい種別。
    /// </summary>
    public enum SurfaceBehaviorType
    {
        /// <summary>ゴロゴロ転がる(自販機の上など)</summary>
        Rolling,

        /// <summary>横になって昼寝する(ベンチの上など)</summary>
        Napping,

        /// <summary>ゆっくり歩き回る(ベンチの上など)</summary>
        Strolling,

        /// <summary>ベイブレードのように高速回転してぶつかり合う(テーブルの上など)</summary>
        Battle,
    }

    /// <summary>
    /// 平面検出時に何を出現させるかの動作モード。
    /// </summary>
    public enum PlacementMode
    {
        /// <summary>Phase 1の水族館(泳ぐ寿司)のみ</summary>
        Aquarium,

        /// <summary>表面ふるまいのデモスポットのみ(Phase 2aの検証用)</summary>
        SurfaceSpotsDemo,

        /// <summary>両方</summary>
        Both,

        /// <summary>平面検出では何も出さない(VPSアンカーのみで配置する現地ビルド用)</summary>
        None,
    }
}
