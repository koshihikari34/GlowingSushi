using System;
using System.Collections.Generic;
using GlowingSushi.Domain;
using GlowingSushi.Service;
using ObservableCollections;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 寿司の群れ全体を管理するViewModel。
    /// ObservableListで個体群を公開し、毎フレームのTickでBoidシミュレーション・
    /// 状態遷移・発光強度の更新を駆動する。タッチによる逃走もここで処理する。
    /// </summary>
    public sealed class SushiSchoolViewModel : IDisposable
    {
        readonly SushiSpawnService spawnService;
        readonly ICameraPoseService cameraPose;
        readonly SushiBehaviorSettings settings;
        readonly System.Random random = new();
        readonly IDisposable touchSubscription;

        // Boid計算用の再利用バッファ(毎フレームのアロケーションを避ける)
        readonly List<Vector3> positionsBuffer = new();
        readonly List<Vector3> velocitiesBuffer = new();

        /// <summary>群れのアンカー(引き戻しの中心)</summary>
        Vector3 anchorCenter;

        /// <summary>シミュレーション経過時間(ふらつき・発光パルスの位相に使う)</summary>
        float elapsedTime;

        /// <summary>群れの個体一覧。ViewはObserveAdd/ObserveRemoveで生成・破棄に追従する。</summary>
        public ObservableList<SushiViewModel> Sushis { get; } = new();

        public SushiSchoolViewModel(
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
        /// 指定した平面姿勢の上方に群れを出現させる。
        /// </summary>
        /// <param name="planePose">検出された平面の姿勢(位置=中心、up=法線)</param>
        public void Spawn(Pose planePose)
        {
            anchorCenter = planePose.position + planePose.up * settings.spawnHeightAbovePlane;
            foreach (var data in spawnService.CreateSchool(anchorCenter))
            {
                Sushis.Add(new SushiViewModel(settings, random, data));
            }
        }

        /// <summary>
        /// シミュレーションを1フレーム分進める。エントリポイントのTickから呼ばれる。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Sushis.Count == 0) return;

            elapsedTime += deltaTime;
            var cameraPosition = cameraPose.Position;

            // 全個体の位置・速度をバッファへコピー(Boid計算の入力)
            positionsBuffer.Clear();
            velocitiesBuffer.Clear();
            foreach (var sushi in Sushis)
            {
                positionsBuffer.Add(sushi.Position.Value);
                velocitiesBuffer.Add(sushi.Velocity);
            }

            for (var i = 0; i < Sushis.Count; i++)
            {
                var sushi = Sushis[i];
                sushi.UpdateState(deltaTime, cameraPosition);

                var steer = ComputeSteering(i, sushi, cameraPosition);
                steer = Vector3.ClampMagnitude(steer, settings.maxSteerForce);

                // 速度を積分し、状態ごとの最大速度でクランプする
                var maxSpeed = sushi.State.Value switch
                {
                    SushiState.Fleeing => settings.fleeSpeed,
                    SushiState.Approaching => settings.approachSpeed,
                    _ => settings.maxSpeed,
                };
                sushi.Velocity = Vector3.ClampMagnitude(sushi.Velocity + steer * deltaTime, maxSpeed);
                sushi.Position.Value += sushi.Velocity * deltaTime;

                // 進行方向を向かせる
                if (sushi.Velocity.sqrMagnitude > 1e-6f)
                {
                    sushi.Rotation.Value = Quaternion.Slerp(
                        sushi.Rotation.Value,
                        Quaternion.LookRotation(sushi.Velocity.normalized),
                        deltaTime * 5f);
                }

                // 発光: sin波パルス×状態係数
                var pulse01 = (Mathf.Sin(elapsedTime * (2f * Mathf.PI) / settings.glowPulsePeriod + sushi.GlowPhase) + 1f) * 0.5f;
                var intensity = Mathf.Lerp(settings.glowMinIntensity, settings.glowMaxIntensity, pulse01);
                if (sushi.State.Value == SushiState.Fleeing)
                {
                    intensity *= settings.fleeGlowMultiplier;
                }
                sushi.GlowIntensity.Value = intensity;
            }
        }

        /// <summary>
        /// 状態に応じた操舵ベクトルを計算する。
        /// </summary>
        Vector3 ComputeSteering(int index, SushiViewModel sushi, Vector3 cameraPosition)
        {
            var position = positionsBuffer[index];
            var velocity = velocitiesBuffer[index];

            switch (sushi.State.Value)
            {
                case SushiState.Fleeing:
                {
                    // タッチ地点から離れる。起点と重なっている場合はカメラから離れる方向へ
                    var flee = BoidMath.Flee(position, velocity, sushi.FleeFrom, settings.fleeSpeed);
                    if (flee.sqrMagnitude < 1e-6f)
                    {
                        flee = BoidMath.Flee(position, velocity, cameraPosition, settings.fleeSpeed);
                    }
                    return flee + BoidMath.Wander(sushi.WanderSeed, elapsedTime) * settings.wanderWeight;
                }

                case SushiState.Approaching:
                    // カメラへ向かう(停止判定はUpdateState側)
                    return BoidMath.Seek(position, velocity, cameraPosition, settings.approachSpeed);

                default: // Schooling
                {
                    var steer =
                        BoidMath.Separation(index, positionsBuffer, settings.separationRadius) * settings.separationWeight +
                        BoidMath.Alignment(index, positionsBuffer, velocitiesBuffer, settings.neighborRadius) * settings.alignmentWeight +
                        BoidMath.Cohesion(index, positionsBuffer, settings.neighborRadius) * settings.cohesionWeight +
                        BoidMath.Wander(sushi.WanderSeed, elapsedTime) * settings.wanderWeight;

                    // アンカーから離れすぎたら引き戻す
                    if ((position - anchorCenter).sqrMagnitude > settings.containmentRadius * settings.containmentRadius)
                    {
                        steer += BoidMath.Seek(position, velocity, anchorCenter, settings.maxSpeed);
                    }
                    return steer;
                }
            }
        }

        /// <summary>
        /// タッチレイに対するヒット判定。最も手前の個体を逃走させ、周囲の仲間にも伝播させる。
        /// </summary>
        void OnTouch(Ray ray)
        {
            SushiViewModel hit = null;
            var hitDistance = float.MaxValue;
            var hitPoint = Vector3.zero;

            foreach (var sushi in Sushis)
            {
                if (!BoidMath.RayIntersectsSphere(
                        ray.origin, ray.direction, sushi.Position.Value, settings.touchHitRadius, out var distance))
                {
                    continue;
                }
                if (distance < hitDistance)
                {
                    hit = sushi;
                    hitDistance = distance;
                    hitPoint = ray.origin + ray.direction * distance;
                }
            }

            if (hit == null) return;

            hit.Interact(hitPoint);

            // タッチされた個体の周囲にいる仲間も一緒に逃がす
            var sqrPropagation = settings.fleePropagationRadius * settings.fleePropagationRadius;
            var hitPosition = hit.Position.Value;
            foreach (var sushi in Sushis)
            {
                if (sushi == hit) continue;
                if ((sushi.Position.Value - hitPosition).sqrMagnitude <= sqrPropagation)
                {
                    sushi.Interact(hitPoint);
                }
            }
        }

        public void Dispose()
        {
            touchSubscription.Dispose();
            foreach (var sushi in Sushis)
            {
                sushi.Dispose();
            }
            Sushis.Clear();
        }
    }
}
