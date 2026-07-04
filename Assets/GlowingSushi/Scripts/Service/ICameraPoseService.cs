using UnityEngine;

namespace GlowingSushi.Service
{
    /// <summary>
    /// ARカメラ(プレイヤー視点)の現在姿勢を提供するサービス。
    /// ViewModelが接近行動などでカメラ位置を参照するために使う。
    /// </summary>
    public interface ICameraPoseService
    {
        /// <summary>カメラのワールド座標</summary>
        Vector3 Position { get; }

        /// <summary>カメラのワールド回転</summary>
        Quaternion Rotation { get; }
    }
}
