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

        /// <summary>群れの生成設定(アンカー・軌道・接近可否)</summary>
        readonly SchoolConfig config;

        /// <summary>シミュレーション経過時間(ふらつき・発光パルス・軌道の位相に使う)</summary>
        float elapsedTime;

        /// <summary>この群れの発光色</summary>
        public Color GlowColor => config.GlowColor;

        /// <summary>群れの個体一覧。Viewは増減を購読してSushiViewを生成・破棄する。</summary>
        public ObservableList<SushiViewModel> Sushis { get; } = new();

        /// <summary>
        /// 群れを生成する。アンカー中心の球内に個体を初期配置する。
        /// </summary>
        /// <param name="spawnService">初期配置データの生成サービス</param>
        /// <param name="cameraPose">カメラ姿勢(接近行動の目標)</param>
        /// <param name="settings">挙動パラメータ</param>
        /// <param name="random">乱数(全群れで共有)</param>
        /// <param name="config">この群れの生成設定</param>
        public SushiSchoolViewModel(
            SushiSpawnService spawnService,
            ICameraPoseService cameraPose,
            SushiBehaviorSettings settings,
            System.Random random,
            SchoolConfig config)
        {
            this.cameraPose = cameraPose;
            this.settings = settings;
            this.random = random;
            this.config = config;

            foreach (var data in spawnService.CreateSchool(config.Anchor, config.MemberCount))
            {
                Sushis.Add(new SushiViewModel(settings, random, data, config.GlowColor, config.CanApproach));
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
            var orbitTarget = ComputeOrbitTarget();

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

                var steer = ComputeSteering(i, sushi, cameraPosition, orbitTarget);
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

                // 進行方向を向かせる+旋回時は内側へ傾ける(バンク)
                if (velocity.sqrMagnitude > 1e-6f)
                {
                    var forward = velocity.normalized;
                    var right = Vector3.Cross(Vector3.up, forward);
                    var bankAngle = Mathf.Clamp(-Vector3.Dot(steer, right) * settings.bankFactor, -40f, 40f);
                    var targetRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, bankAngle);
                    sushi.Rotation.Value = Quaternion.Slerp(sushi.Rotation.Value, targetRotation, deltaTime * 5f);
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
        /// 軌道アトラクタ(群れ全体が追いかける移動目標)の現在位置を計算する。
        /// アンカーを中心に円軌道でゆっくり周回し、上下にも小さく揺れる。
        /// </summary>
        Vector3 ComputeOrbitTarget()
        {
            if (!config.OrbitEnabled) return config.Anchor;

            var direction = config.OrbitClockwise ? -1f : 1f;
            var angle = elapsedTime / settings.orbitPeriod * (2f * Mathf.PI) * direction + config.OrbitPhase;
            var bob = Mathf.Sin(elapsedTime * 0.7f + config.OrbitPhase) * settings.orbitVerticalBob;
            return config.Anchor
                   + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * settings.orbitRadius
                   + Vector3.up * bob;
        }

        /// <summary>
        /// 状態に応じた操舵ベクトルを計算する。
        /// </summary>
        Vector3 ComputeSteering(int index, SushiViewModel sushi, Vector3 cameraPosition, Vector3 orbitTarget)
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

                    // 群れ全体で軌道目標を追いかけ、輪を描いて流れるように泳がせる
                    if (config.OrbitEnabled)
                    {
                        steer += BoidMath.Seek(position, velocity, orbitTarget, settings.maxSpeed) * settings.orbitTargetWeight;
                    }

                    // アンカーから離れすぎたら引き戻す(軌道半径より広い安全網)
                    if ((position - config.Anchor).sqrMagnitude > settings.containmentRadius * settings.containmentRadius)
                    {
                        steer += BoidMath.Seek(position, velocity, config.Anchor, settings.maxSpeed);
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
