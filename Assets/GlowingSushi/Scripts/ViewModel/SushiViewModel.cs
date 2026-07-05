using System;
using GlowingSushi.Domain;
using R3;
using UnityEngine;

namespace GlowingSushi.ViewModel
{
    /// <summary>
    /// 寿司1匹分のViewModel。位置・回転・状態・発光強度をReactivePropertyで公開する。
    /// Viewへの参照は一切持たない。
    /// </summary>
    public sealed class SushiViewModel : IDisposable
    {
        readonly SushiBehaviorSettings settings;
        readonly System.Random random;
        readonly bool canApproach;

        /// <summary>ワールド座標</summary>
        public ReactiveProperty<Vector3> Position { get; }

        /// <summary>ワールド回転(進行方向を向く)</summary>
        public ReactiveProperty<Quaternion> Rotation { get; }

        /// <summary>行動状態</summary>
        public ReactiveProperty<SushiState> State { get; }

        /// <summary>発光強度(Viewはこの値をEmissionに写像するだけ)</summary>
        public ReactiveProperty<float> GlowIntensity { get; }

        /// <summary>発光色(所属する群れの色。生成時に固定)</summary>
        public Color GlowColor { get; }

        // ---- 以下はBoidシミュレーション用の非リアクティブな内部状態 ----

        /// <summary>現在速度(毎フレーム更新されるためリアクティブにしない)</summary>
        public Vector3 Velocity;

        /// <summary>現在状態の残り時間や次回判定までのタイマー</summary>
        public float StateTimer;

        /// <summary>発光パルスの位相オフセット</summary>
        public float GlowPhase;

        /// <summary>ふらつき計算用の個体固有シード</summary>
        public float WanderSeed;

        /// <summary>逃走の起点(タッチされたワールド座標)</summary>
        public Vector3 FleeFrom;

        /// <param name="canApproach">trueの場合のみ、時々カメラへ接近する(接近専用個体)</param>
        public SushiViewModel(SushiBehaviorSettings settings, System.Random random, SushiSpawnData spawnData, Color glowColor, bool canApproach)
        {
            this.settings = settings;
            this.random = random;
            this.canApproach = canApproach;
            GlowColor = glowColor;

            Position = new ReactiveProperty<Vector3>(spawnData.Position);
            Rotation = new ReactiveProperty<Quaternion>(
                spawnData.Velocity.sqrMagnitude > 1e-6f
                    ? Quaternion.LookRotation(spawnData.Velocity.normalized)
                    : Quaternion.identity);
            State = new ReactiveProperty<SushiState>(SushiState.Schooling);
            GlowIntensity = new ReactiveProperty<float>(settings.glowMinIntensity);

            Velocity = spawnData.Velocity;
            GlowPhase = spawnData.GlowPhase;
            WanderSeed = spawnData.WanderSeed;
            StateTimer = settings.approachInterval;
        }

        /// <summary>
        /// タッチされたときのコマンド。逃走状態へ遷移する。
        /// </summary>
        /// <param name="touchWorldPos">タッチのワールド座標(逃走の起点)</param>
        public void Interact(Vector3 touchWorldPos)
        {
            FleeFrom = touchWorldPos;
            StateTimer = settings.fleeDuration;
            State.Value = SushiState.Fleeing;
        }

        /// <summary>
        /// タイマー駆動の状態遷移を1ステップ進める。毎フレームTickから呼ばれる。
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過時間</param>
        /// <param name="cameraPosition">カメラ(プレイヤー)のワールド座標</param>
        public void UpdateState(float deltaTime, Vector3 cameraPosition)
        {
            StateTimer -= deltaTime;

            switch (State.Value)
            {
                case SushiState.Schooling:
                    // 接近専用個体のみ、一定間隔ごとに確率で接近を開始する
                    // (群れのメンバーは隊列を保つため接近しない)
                    if (StateTimer <= 0f)
                    {
                        if (canApproach && random.NextDouble() < settings.approachProbability)
                        {
                            State.Value = SushiState.Approaching;
                            StateTimer = settings.approachDuration;
                        }
                        else
                        {
                            StateTimer = settings.approachInterval;
                        }
                    }
                    break;

                case SushiState.Approaching:
                    // 時間切れ、またはカメラに十分近づいたら群れに戻る
                    var sqrStop = settings.approachStopDistance * settings.approachStopDistance;
                    if (StateTimer <= 0f || (cameraPosition - Position.Value).sqrMagnitude < sqrStop)
                    {
                        State.Value = SushiState.Schooling;
                        StateTimer = settings.approachInterval;
                    }
                    break;

                case SushiState.Fleeing:
                    // 逃走時間が終わったら群れに戻る
                    if (StateTimer <= 0f)
                    {
                        State.Value = SushiState.Schooling;
                        StateTimer = settings.approachInterval;
                    }
                    break;
            }
        }

        public void Dispose()
        {
            Position.Dispose();
            Rotation.Dispose();
            State.Dispose();
            GlowIntensity.Dispose();
        }
    }
}
