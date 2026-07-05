using System;
using System.Collections.Generic;
using GlowingSushi.Domain;
using GlowingSushi.Service;
using ObservableCollections;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 1つの群れを管理するViewModel。固有のアンカーと発光色を持ち、
    /// 毎フレームのTickでBoidシミュレーション・状態遷移・発光強度の更新を行う。
    /// 複数群れの束ねとタッチ入力の振り分けはAquariumViewModelが担当する。
    /// </summary>
    public sealed class SushiSchoolViewModel : IDisposable
    {
        readonly ICameraPoseService cameraPose;
        readonly SushiBehaviorSettings settings;
        readonly System.Random random;

        // Boid計算用の再利用バッファ(毎フレームのアロケーションを避ける)
        readonly List<Vector3> positionsBuffer = new();
        readonly List<Vector3> velocitiesBuffer = new();

        /// <summary>群れのアンカー(引き戻しの中心)</summary>
        readonly Vector3 anchorCenter;

        /// <summary>シミュレーション経過時間(ふらつき・発光パルスの位相に使う)</summary>
        float elapsedTime;

        /// <summary>この群れの発光色</summary>
        public Color GlowColor { get; }

        /// <summary>群れの個体一覧。Viewは増減を購読してSushiViewを生成・破棄する。</summary>
        public ObservableList<SushiViewModel> Sushis { get; } = new();

        /// <summary>
        /// 群れを生成する。アンカー中心の球内に個体を初期配置する。
        /// </summary>
        /// <param name="spawnService">初期配置データの生成サービス</param>
        /// <param name="cameraPose">カメラ姿勢(接近行動の目標)</param>
        /// <param name="settings">挙動パラメータ</param>
        /// <param name="random">乱数(全群れで共有)</param>
        /// <param name="anchorCenter">群れの中心となるワールド座標</param>
        /// <param name="glowColor">この群れの発光色</param>
        public SushiSchoolViewModel(
            SushiSpawnService spawnService,
            ICameraPoseService cameraPose,
            SushiBehaviorSettings settings,
            System.Random random,
            Vector3 anchorCenter,
            Color glowColor)
        {
            this.cameraPose = cameraPose;
            this.settings = settings;
            this.random = random;
            this.anchorCenter = anchorCenter;
            GlowColor = glowColor;

            foreach (var data in spawnService.CreateSchool(anchorCenter))
            {
                Sushis.Add(new SushiViewModel(settings, random, data, glowColor));
            }
        }

        /// <summary>
        /// シミュレーションを1フレーム分進める。AquariumViewModelのTickから呼ばれる。
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
                var velocity = Vector3.ClampMagnitude(sushi.Velocity + steer * deltaTime, maxSpeed);

                // 垂直方向の速度を減衰させ、主に水平に泳がせる(魚らしさ)
                velocity.y *= 1f - settings.verticalDamping * deltaTime;

                // 最低速度を下回らないようにする(魚は止まらない)
                var speed = velocity.magnitude;
                if (speed > 1e-5f && speed < settings.minSpeed)
                {
                    velocity = velocity / speed * settings.minSpeed;
                }

                sushi.Velocity = velocity;
                sushi.Position.Value += velocity * deltaTime;

                // 進行方向を向かせる
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    sushi.Rotation.Value = Quaternion.Slerp(
                        sushi.Rotation.Value,
                        Quaternion.LookRotation(velocity.normalized),
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
        /// タッチレイに対するこの群れ内のヒット判定。最も手前の個体を返す。
        /// </summary>
        /// <returns>ヒットしたかどうか</returns>
        public bool FindHit(Ray ray, out SushiViewModel hit, out float distance, out Vector3 hitPoint)
        {
            hit = null;
            distance = float.MaxValue;
            hitPoint = Vector3.zero;

            foreach (var sushi in Sushis)
            {
                if (!BoidMath.RayIntersectsSphere(
                        ray.origin, ray.direction, sushi.Position.Value, settings.touchHitRadius, out var d))
                {
                    continue;
                }
                if (d < distance)
                {
                    hit = sushi;
                    distance = d;
                    hitPoint = ray.origin + ray.direction * d;
                }
            }
            return hit != null;
        }

        /// <summary>
        /// 指定個体を逃走させ、その周囲の仲間にも伝播させる。
        /// </summary>
        public void FleeFrom(SushiViewModel hit, Vector3 hitPoint)
        {
            hit.Interact(hitPoint);

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
            foreach (var sushi in Sushis)
            {
                sushi.Dispose();
            }
            Sushis.Clear();
        }
    }
}
