using System;
using GlowingSushi.Domain;
using GlowingSushi.Service;
using ObservableCollections;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 水族館全体を管理するViewModel。複数の群れ(SushiSchoolViewModel)を生成・駆動し、
    /// タッチ入力を全群れへ振り分ける。タッチ成功はTouchHitとして公開し、
    /// Viewがエフェクト・効果音の再生に使う。
    /// </summary>
    public sealed class AquariumViewModel : IDisposable
    {
        readonly SushiSpawnService spawnService;
        readonly ICameraPoseService cameraPose;
        readonly SushiBehaviorSettings settings;
        readonly System.Random random = new();
        readonly IDisposable touchSubscription;
        readonly Subject<TouchHitInfo> touchHit = new();

        /// <summary>群れの一覧。Viewは増減を購読して群れ配下のSushiViewを生成・破棄する。</summary>
        public ObservableList<SushiSchoolViewModel> Schools { get; } = new();

        /// <summary>タッチが寿司に命中したときに流れるイベント(位置+群れ色)</summary>
        public Observable<TouchHitInfo> TouchHit => touchHit;

        public AquariumViewModel(
            SushiSpawnService spawnService,
            ICameraPoseService cameraPose,
            TouchInputService touchInput,
            SushiBehaviorSettings settings)
        {
            this.spawnService = spawnService;
            this.cameraPose = cameraPose;
            this.settings = settings;

            touchSubscription = touchInput.TouchRays.Subscribe(OnTouch);
        }

        /// <summary>
        /// 検出された平面を基準に、設定された数の群れと接近専用個体を配置する。
        /// 群れは平面中心の周囲に水平オフセットと高さ差をつけて散らし、色はパレットから順に割り当てる。
        /// </summary>
        public void Spawn(Pose planePose)
        {
            if (Schools.Count > 0) return; // 二重配置を防ぐ

            var baseCenter = planePose.position + planePose.up * settings.spawnHeightAbovePlane;

            // --- 通常の群れ(軌道アトラクタで輪を描いて泳ぐ。接近はしない) ---
            for (var i = 0; i < settings.schoolCount; i++)
            {
                // 群れを円周上に等間隔で散らす(1つ目は中心)
                var offset = Vector3.zero;
                if (i > 0)
                {
                    var angle = (i - 1) * (2f * Mathf.PI / Mathf.Max(1, settings.schoolCount - 1));
                    offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * settings.schoolSpacing;
                }
                var anchor = baseCenter + offset + Vector3.up * (settings.schoolHeightStep * i);

                var color = settings.schoolColors.Length > 0
                    ? settings.schoolColors[i % settings.schoolColors.Length]
                    : Color.white;

                var config = new SchoolConfig(
                    anchor,
                    color,
                    settings.schoolSize,
                    orbitEnabled: true,
                    // 位相と回転方向を群れごとに変えて画に変化をつける
                    orbitPhase: i * (2f * Mathf.PI / Mathf.Max(1, settings.schoolCount)),
                    orbitClockwise: i % 2 == 0,
                    canApproach: false);
                Schools.Add(new SushiSchoolViewModel(spawnService, cameraPose, settings, random, config));
            }

            // --- 接近専用個体(お客好き寿司)。群れとは別の小グループで時々カメラへ寄ってくる ---
            if (settings.curiousCount > 0)
            {
                var curiousConfig = new SchoolConfig(
                    baseCenter + Vector3.up * (settings.schoolHeightStep * 0.5f),
                    settings.curiousColor,
                    settings.curiousCount,
                    orbitEnabled: false,
                    orbitPhase: 0f,
                    orbitClockwise: false,
                    canApproach: true);
                Schools.Add(new SushiSchoolViewModel(spawnService, cameraPose, settings, random, curiousConfig));
            }
        }

        /// <summary>
        /// 全群れのシミュレーションを1フレーム分進める。エントリポイントのTickから呼ばれる。
        /// </summary>
        public void Tick(float deltaTime)
        {
            foreach (var school in Schools)
            {
                school.Tick(deltaTime);
            }
        }

        /// <summary>
        /// タッチレイを全群れへ問い合わせ、最も手前の個体を逃走させてイベントを発行する。
        /// </summary>
        void OnTouch(Ray ray)
        {
            SushiSchoolViewModel hitSchool = null;
            SushiViewModel hitSushi = null;
            var nearest = float.MaxValue;
            var hitPoint = Vector3.zero;

            foreach (var school in Schools)
            {
                if (!school.FindHit(ray, out var sushi, out var distance, out var point)) continue;
                if (distance < nearest)
                {
                    hitSchool = school;
                    hitSushi = sushi;
                    nearest = distance;
                    hitPoint = point;
                }
            }

            if (hitSchool == null) return;

            hitSchool.FleeFrom(hitSushi, hitPoint);
            touchHit.OnNext(new TouchHitInfo(hitPoint, hitSchool.GlowColor));
        }

        public void Dispose()
        {
            touchSubscription.Dispose();
            touchHit.Dispose();
            foreach (var school in Schools)
            {
                school.Dispose();
            }
            Schools.Clear();
        }
    }
}
