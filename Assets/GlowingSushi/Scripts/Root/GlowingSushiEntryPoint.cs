using GlowingSushi.Service;
using GlowingSushi.ViewModel;
using UnityEngine;
using VContainer.Unity;

namespace GlowingSushi.Root
{
    /// <summary>
    /// アプリのエントリポイント。起動時に各サービス・ViewModelを初期化し、
    /// 毎フレームのTickでSushiSchoolViewModelのシミュレーションを駆動する。
    /// VContainerのPlayerLoopからIStartable/ITickableとして呼ばれる。
    /// </summary>
    public sealed class GlowingSushiEntryPoint : IStartable, ITickable
    {
        readonly TouchInputService touchInput;
        readonly ArPlaneDetectionService planeDetection;
        readonly ArPlacementViewModel placement;
        readonly SushiSchoolViewModel school;

        public GlowingSushiEntryPoint(
            TouchInputService touchInput,
            ArPlaneDetectionService planeDetection,
            ArPlacementViewModel placement,
            SushiSchoolViewModel school)
        {
            this.touchInput = touchInput;
            this.planeDetection = planeDetection;
            this.placement = placement;
            this.school = school;
        }

        public void Start()
        {
            // 購読の開始順: 入力→平面検出→配置(検出イベントを取りこぼさないよう配置購読を先に確立)
            touchInput.Initialize();
            placement.Initialize();
            planeDetection.Initialize();
        }

        public void Tick()
        {
            school.Tick(Time.deltaTime);
        }
    }
}
