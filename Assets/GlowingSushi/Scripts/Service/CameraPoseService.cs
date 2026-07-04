using UnityEngine;

namespace GlowingSushi.Service
{
    /// <summary>
    /// ARカメラのTransformをラップして現在姿勢を公開する実装。
    /// DIコンテナからCamera(ARカメラ)を受け取る。
    /// </summary>
    public sealed class CameraPoseService : ICameraPoseService
    {
        readonly Transform cameraTransform;

        public CameraPoseService(Camera arCamera)
        {
            cameraTransform = arCamera.transform;
        }

        public Vector3 Position => cameraTransform.position;

        public Quaternion Rotation => cameraTransform.rotation;
    }
}
