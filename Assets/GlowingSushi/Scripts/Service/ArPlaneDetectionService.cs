using System;
using R3;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace GlowingSushi.Service
{
    /// <summary>
    /// ARPlaneManagerの平面検出イベントをR3のObservableとして公開するサービス。
    /// Phase 1では最初に検出された平面のみを通知する(群れの出現アンカーに使う)。
    /// </summary>
    public sealed class ArPlaneDetectionService : IDisposable
    {
        readonly ARPlaneManager planeManager;
        readonly Subject<Pose> firstPlaneDetected = new();
        bool initialized;
        bool emitted;

        public ArPlaneDetectionService(ARPlaneManager planeManager)
        {
            this.planeManager = planeManager;
        }

        /// <summary>
        /// 最初の平面が検出されたときに、その平面の姿勢(中心位置+法線がupを向く回転)を1回だけ流すObservable。
        /// </summary>
        public Observable<Pose> FirstPlaneDetected => firstPlaneDetected;

        /// <summary>
        /// 平面検出イベントの購読を開始する。エントリポイントから起動時に呼ぶ。
        /// </summary>
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            // ARFoundation 6ではplanesChangedはobsoleteのためtrackablesChangedを使う
            planeManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            if (emitted) return;
            if (args.added.Count == 0) return;

            var plane = args.added[0];
            emitted = true;
            firstPlaneDetected.OnNext(new Pose(plane.center, plane.transform.rotation));
        }

        public void Dispose()
        {
            if (initialized && planeManager != null)
            {
                planeManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
            }
            firstPlaneDetected.Dispose();
        }
    }
}
