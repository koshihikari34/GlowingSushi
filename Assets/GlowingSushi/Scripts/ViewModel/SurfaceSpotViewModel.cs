using System;
using System.Collections.Generic;
using GlowingSushi.Domain;
using ObservableCollections;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 表面ふるまいのスポット1つ分のViewModel。
    /// 表面上のローカル2D座標系で個体を動かし、ワールド座標へ変換してReactivePropertyに反映する。
    /// この寿司たちは発光しない(IsGlowing=false)。
    /// </summary>
    public sealed class SurfaceSpotViewModel : IDisposable
    {
        readonly SushiBehaviorSettings settings;
        readonly System.Random random;
        readonly Subject<BattleClashInfo> clashSink;

        // 位置=スポット中心、up=表面の法線。VPSのローカライズ精度向上に追従して更新される
        Pose surfacePose;
        Vector3 right;      // 表面ローカルX軸
        Vector3 forward;    // 表面ローカルY軸

        // 個体ごとのローカル状態(Sushisと同じ並び順)
        readonly List<Vector2> positions2D = new();
        readonly List<Vector2> velocities2D = new();
        readonly List<float> headings = new();      // 散歩用の進行方向(ラジアン)
        readonly List<float> spinAngles = new();    // ベイブレード用のスピン角(度)
        readonly List<float> baseYaws = new();      // 昼寝用の寝る向き(度)
        readonly Dictionary<(int, int), float> clashCooldowns = new(); // ペアごとの衝突クールダウン

        float elapsedTime;

        /// <summary>このスポットのふるまい種別</summary>
        public SurfaceBehaviorType BehaviorType { get; }

        /// <summary>スポット中心のワールド座標(状態HUDの距離表示などに使う)</summary>
        public Vector3 Center => surfacePose.position;

        /// <summary>スポットの個体一覧。Viewは増減を購読してSushiViewを生成・破棄する。</summary>
        public ObservableList<SushiViewModel> Sushis { get; } = new();

        /// <param name="clashSink">Battle衝突イベントの送り先(SurfaceSpotsViewModelが集約する)</param>
        public SurfaceSpotViewModel(
            SushiBehaviorSettings settings,
            System.Random random,
            SurfaceBehaviorType behaviorType,
            Pose surfacePose,
            Subject<BattleClashInfo> clashSink)
        {
            this.settings = settings;
            this.random = random;
            this.clashSink = clashSink;
            this.surfacePose = surfacePose;
            BehaviorType = behaviorType;

            right = surfacePose.rotation * Vector3.right;
            forward = surfacePose.rotation * Vector3.forward;

            SpawnMembers();
        }

        /// <summary>
        /// スポットの表面姿勢を更新する。VPSのローカライズが繰り返し成功して
        /// XRSpaceの位置が補正された際、スポットも実世界へ追従させるために使う。
        /// 個体のローカル2D座標は保持されるため、群れごと新しい姿勢へ移動する。
        /// </summary>
        public void UpdateSurfacePose(Pose newPose)
        {
            surfacePose = newPose;
            right = newPose.rotation * Vector3.right;
            forward = newPose.rotation * Vector3.forward;
        }

        /// <summary>スポットのエリア内に個体をランダム配置する</summary>
        void SpawnMembers()
        {
            for (var i = 0; i < settings.membersPerSpot; i++)
            {
                // エリア内の重ならない位置(簡易: ランダム+半径の半分以内)
                var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                var distance = (float)random.NextDouble() * settings.spotRadius * 0.6f;
                var position2D = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                var heading = (float)(random.NextDouble() * Math.PI * 2.0);
                var velocity2D = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                velocity2D *= BehaviorType switch
                {
                    SurfaceBehaviorType.Rolling => settings.rollSpeed,
                    SurfaceBehaviorType.Battle => settings.battleMoveSpeed,
                    _ => 0f,
                };

                positions2D.Add(position2D);
                velocities2D.Add(velocity2D);
                headings.Add(heading);
                spinAngles.Add((float)(random.NextDouble() * 360.0));
                baseYaws.Add((float)(random.NextDouble() * 360.0));

                var spawnData = new SushiSpawnData(
                    ToWorld(position2D),
                    Vector3.zero,
                    (float)(random.NextDouble() * Math.PI * 2.0),
                    (float)(random.NextDouble() * 100.0));
                // 表面ふるまいの寿司は発光しない・接近もしない
                var sushi = new SushiViewModel(settings, random, spawnData, Color.white, canApproach: false, isGlowing: false);
                ApplyInitialRotation(sushi, i);
                Sushis.Add(sushi);
            }
        }

        /// <summary>種別ごとの初期姿勢を設定する(昼寝は横倒しにする)</summary>
        void ApplyInitialRotation(SushiViewModel sushi, int index)
        {
            var yaw = Quaternion.AngleAxis(baseYaws[index], surfacePose.up);
            if (BehaviorType == SurfaceBehaviorType.Napping)
            {
                // 横倒しで寝かせる
                sushi.Rotation.Value = surfacePose.rotation * Quaternion.Euler(0f, baseYaws[index], 90f);
            }
            else
            {
                sushi.Rotation.Value = yaw * surfacePose.rotation;
            }
        }

        /// <summary>ローカル2D座標をワールド座標へ変換する(表面から少し浮かせる)</summary>
        Vector3 ToWorld(Vector2 position2D)
        {
            return surfacePose.position
                   + right * position2D.x
                   + forward * position2D.y
                   + surfacePose.up * settings.surfaceOffset;
        }

        /// <summary>シミュレーションを1フレーム分進める</summary>
        public void Tick(float deltaTime)
        {
            elapsedTime += deltaTime;
            switch (BehaviorType)
            {
                case SurfaceBehaviorType.Rolling: TickRolling(deltaTime); break;
                case SurfaceBehaviorType.Napping: TickNapping(); break;
                case SurfaceBehaviorType.Strolling: TickStrolling(deltaTime); break;
                case SurfaceBehaviorType.Battle: TickBattle(deltaTime); break;
            }
        }

        /// <summary>転がり: 直進しながら縁で跳ね返り、移動量に応じて回転する</summary>
        void TickRolling(float deltaTime)
        {
            for (var i = 0; i < Sushis.Count; i++)
            {
                var position = positions2D[i];
                var velocity = velocities2D[i];

                position += velocity * deltaTime;
                SurfaceMotionMath.ReflectInsideCircle(ref position, ref velocity, settings.spotRadius);

                positions2D[i] = position;
                velocities2D[i] = velocity;

                var sushi = Sushis[i];
                var worldVelocity = right * velocity.x + forward * velocity.y;
                sushi.Position.Value = ToWorld(position);
                sushi.Rotation.Value =
                    SurfaceMotionMath.RollDelta(surfacePose.up, worldVelocity, settings.rollContactRadius, deltaTime)
                    * sushi.Rotation.Value;
            }
        }

        /// <summary>昼寝: 横倒しのまま呼吸のようにゆっくり上下する</summary>
        void TickNapping()
        {
            for (var i = 0; i < Sushis.Count; i++)
            {
                var sushi = Sushis[i];
                var breath = Mathf.Sin(elapsedTime * (2f * Mathf.PI) / settings.napBreathPeriod + sushi.GlowPhase)
                             * settings.napBreathAmplitude;
                sushi.Position.Value = ToWorld(positions2D[i]) + surfacePose.up * breath;
            }
        }

        /// <summary>散歩: 進行方向を揺らしながらゆっくり歩き、縁に近づいたら中心側へ向き直す</summary>
        void TickStrolling(float deltaTime)
        {
            for (var i = 0; i < Sushis.Count; i++)
            {
                var sushi = Sushis[i];
                var heading = SurfaceMotionMath.WanderHeading(
                    headings[i], sushi.WanderSeed, elapsedTime, settings.strollTurnRate, deltaTime);

                var position = positions2D[i];
                var direction = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                position += direction * (settings.strollSpeed * deltaTime);

                // 縁に近づいたら中心方向へ緩やかに向き直す
                if (position.magnitude > settings.spotRadius * 0.85f)
                {
                    var toCenter = Mathf.Atan2(-position.y, -position.x);
                    heading = Mathf.LerpAngle(heading * Mathf.Rad2Deg, toCenter * Mathf.Rad2Deg, deltaTime * 2f) * Mathf.Deg2Rad;
                }
                SurfaceMotionMath.ReflectInsideCircle(ref position, ref direction, settings.spotRadius);

                positions2D[i] = position;
                headings[i] = heading;

                sushi.Position.Value = ToWorld(position);
                // 進行方向を向き、歩調に合わせて小さく左右に揺れる
                var waddle = Mathf.Sin(elapsedTime * 8f + sushi.WanderSeed) * 8f;
                var worldDirection = right * direction.x + forward * direction.y;
                if (worldDirection.sqrMagnitude > 1e-6f)
                {
                    sushi.Rotation.Value = Quaternion.Slerp(
                        sushi.Rotation.Value,
                        Quaternion.LookRotation(worldDirection, surfacePose.up) * Quaternion.Euler(0f, 0f, waddle),
                        deltaTime * 4f);
                }
            }
        }

        /// <summary>ベイブレード: 高速スピンしながら動き回り、ぶつかると弾かれて衝突イベントを発行する</summary>
        void TickBattle(float deltaTime)
        {
            // 移動と縁の反射
            for (var i = 0; i < Sushis.Count; i++)
            {
                var position = positions2D[i];
                var velocity = velocities2D[i];

                position += velocity * deltaTime;
                SurfaceMotionMath.ReflectInsideCircle(ref position, ref velocity, settings.spotRadius);

                positions2D[i] = position;
                velocities2D[i] = velocity;
            }

            // ペアごとの衝突判定
            for (var i = 0; i < Sushis.Count; i++)
            {
                for (var j = i + 1; j < Sushis.Count; j++)
                {
                    if ((positions2D[i] - positions2D[j]).magnitude > settings.battleHitRadius * 2f) continue;

                    var velocityI = velocities2D[i];
                    var velocityJ = velocities2D[j];
                    var relativeSpeed = (velocityI - velocityJ).magnitude;
                    if (!SurfaceMotionMath.ResolveElasticCollision(
                            positions2D[i], positions2D[j], ref velocityI, ref velocityJ))
                    {
                        continue;
                    }
                    velocities2D[i] = velocityI;
                    velocities2D[j] = velocityJ;

                    // 同じペアの連続発火を防ぐ
                    var key = (i, j);
                    if (clashCooldowns.TryGetValue(key, out var lastTime)
                        && elapsedTime - lastTime < settings.battleClashCooldown)
                    {
                        continue;
                    }
                    clashCooldowns[key] = elapsedTime;

                    var midpoint = (positions2D[i] + positions2D[j]) * 0.5f;
                    var intensity = Mathf.Clamp01(relativeSpeed / (settings.battleMoveSpeed * 2f));
                    clashSink.OnNext(new BattleClashInfo(ToWorld(midpoint), intensity));
                }
            }

            // スピンと位置の反映
            for (var i = 0; i < Sushis.Count; i++)
            {
                spinAngles[i] += settings.spinSpeed * deltaTime;
                var sushi = Sushis[i];
                sushi.Position.Value = ToWorld(positions2D[i]);
                sushi.Rotation.Value = Quaternion.AngleAxis(spinAngles[i], surfacePose.up) * surfacePose.rotation;
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
