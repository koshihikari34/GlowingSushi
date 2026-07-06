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

        // VPS補正の平滑追従用の目標姿勢(瞬間移動に見えないよう指数補間で近づける)
        Pose targetSurfacePose;
        bool hasPendingPoseUpdate;

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
        /// スポットの表面姿勢の目標値を更新する。VPSのローカライズが繰り返し成功して
        /// XRSpaceの位置が補正された際、スポットも実世界へ追従させるために使う。
        /// 即時反映すると数cm〜数十cmの補正のたびに瞬間移動して見えるため、
        /// Tick内で滑らかに補間して近づける。
        /// </summary>
        public void UpdateSurfacePose(Pose newPose)
        {
            targetSurfacePose = newPose;
            hasPendingPoseUpdate = true;
        }

        /// <summary>目標姿勢へ滑らかに補間する(約0.3秒で大半を移動)</summary>
        void SmoothFollowTargetPose(float deltaTime)
        {
            if (!hasPendingPoseUpdate) return;

            var t = 1f - Mathf.Exp(-3f * deltaTime);
            var position = Vector3.Lerp(surfacePose.position, targetSurfacePose.position, t);
            var rotation = Quaternion.Slerp(surfacePose.rotation, targetSurfacePose.rotation, t);

            // 十分近づいたらスナップして補間終了
            if ((position - targetSurfacePose.position).sqrMagnitude < 1e-6f
                && Quaternion.Angle(rotation, targetSurfacePose.rotation) < 0.1f)
            {
                position = targetSurfacePose.position;
                rotation = targetSurfacePose.rotation;
                hasPendingPoseUpdate = false;
            }

            surfacePose = new Pose(position, rotation);
            right = rotation * Vector3.right;
            forward = rotation * Vector3.forward;
        }

        /// <summary>スポットのエリア内に個体をランダム配置する</summary>
        void SpawnMembers()
        {
            for (var i = 0; i < settings.membersPerSpot; i++)
            {
                // エリア内の重ならない位置(簡易: ランダム配置。転がりは往復の振れ幅ぶん内側に収める)
                var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                var maxDistance = BehaviorType == SurfaceBehaviorType.Rolling
                    ? Mathf.Max(0.05f, settings.spotRadius - settings.rollAmplitude)
                    : settings.spotRadius * 0.6f;
                var distance = (float)random.NextDouble() * maxDistance;
                var position2D = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                var heading = (float)(random.NextDouble() * Math.PI * 2.0);
                var velocity2D = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                velocity2D *= BehaviorType switch
                {
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
            SmoothFollowTargetPose(deltaTime);
            switch (BehaviorType)
            {
                case SurfaceBehaviorType.Rolling: TickRolling(); break;
                case SurfaceBehaviorType.Napping: TickNapping(); break;
                case SurfaceBehaviorType.Strolling: TickStrolling(deltaTime); break;
                case SurfaceBehaviorType.Battle: TickBattle(deltaTime); break;
            }
        }

        /// <summary>
        /// 転がり: 子供がおもちゃを転がすように、固定の向き(長軸)を保ったまま
        /// スポーン位置を中心に左右へ正弦波で往復し、移動量に同期して長軸まわりにロールする。
        /// </summary>
        void TickRolling()
        {
            for (var i = 0; i < Sushis.Count; i++)
            {
                var sushi = Sushis[i];

                // 個体の固定の向き(長軸=ローカルZ)。spinAnglesは往復の位相として流用
                var baseRotation = Quaternion.AngleAxis(baseYaws[i], surfacePose.up) * surfacePose.rotation;
                var sideWorld = baseRotation * Vector3.right;   // 往復する横方向
                var longAxis = baseRotation * Vector3.forward;  // ロールの軸(寿司の長手方向)

                var offset = Mathf.Sin(elapsedTime * (2f * Mathf.PI) / settings.rollPeriod + spinAngles[i])
                             * settings.rollAmplitude;

                sushi.Position.Value = ToWorld(positions2D[i]) + sideWorld * offset;

                // 移動量に同期したロール(接地半径ぶんの円周で角度換算)
                var rollDegrees = -offset / settings.rollContactRadius * Mathf.Rad2Deg;
                sushi.Rotation.Value = Quaternion.AngleAxis(rollDegrees, longAxis) * baseRotation;
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
