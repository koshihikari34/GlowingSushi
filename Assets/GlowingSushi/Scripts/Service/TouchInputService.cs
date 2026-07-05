using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace GlowingSushi.Service
{
    /// <summary>
    /// Input System(EnhancedTouch)のタッチ入力を、ARカメラから伸びるレイの
    /// Observableとして公開するサービス。エディタではマウスクリックをタッチとして扱う。
    /// </summary>
    public sealed class TouchInputService : IDisposable
    {
        readonly Camera arCamera;
        readonly Subject<Ray> touchRays = new();
        bool initialized;

        public TouchInputService(Camera arCamera)
        {
            this.arCamera = arCamera;
        }

        /// <summary>
        /// タッチ(指が触れた瞬間)ごとに、その画面位置からのワールドレイを流すObservable。
        /// </summary>
        public Observable<Ray> TouchRays => touchRays;

        /// <summary>
        /// タッチ入力の購読を開始する。エントリポイントから起動時に呼ぶ。
        /// </summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // エディタのPlayモードではマウスクリックをタッチとして扱えるようにする
            TouchSimulation.Enable();
#endif
            Touch.onFingerDown += OnFingerDown;
        }

        void OnFingerDown(Finger finger)
        {
            touchRays.OnNext(arCamera.ScreenPointToRay(finger.screenPosition));
        }

        public void Dispose()
        {
            if (initialized)
            {
                Touch.onFingerDown -= OnFingerDown;
#if UNITY_EDITOR
                TouchSimulation.Disable();
#endif
                EnhancedTouchSupport.Disable();
            }
            touchRays.Dispose();
        }
    }
}
