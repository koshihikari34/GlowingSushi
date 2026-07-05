using System.Collections.Generic;
using UnityEngine;

namespace GlowingSushi.Domain
{
    /// <summary>
    /// Boid(群泳)の操舵ベクトルとタッチ判定を計算する純粋な静的関数群。
    /// MonoBehaviour・シーン状態には一切依存しない。
    /// </summary>
    public static class BoidMath
    {
        /// <summary>
        /// 分離: 近すぎる仲間から離れる方向のベクトルを返す。
        /// </summary>
        /// <param name="selfIndex">自分のインデックス</param>
        /// <param name="positions">全個体の位置</param>
        /// <param name="separationRadius">この距離より近い仲間を避ける</param>
        public static Vector3 Separation(int selfIndex, IReadOnlyList<Vector3> positions, float separationRadius)
        {
            var self = positions[selfIndex];
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < positions.Count; i++)
            {
                if (i == selfIndex) continue;
                var diff = self - positions[i];
                var sqrDist = diff.sqrMagnitude;
                if (sqrDist <= 0f || sqrDist > separationRadius * separationRadius) continue;
                // 近いほど強く反発させる(距離の逆数で重み付け)
                sum += diff.normalized / Mathf.Sqrt(sqrDist);
                count++;
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        /// <summary>
        /// 整列: 近傍の仲間の平均速度方向へ合わせるベクトルを返す。
        /// </summary>
        public static Vector3 Alignment(int selfIndex, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> velocities, float neighborRadius)
        {
            var self = positions[selfIndex];
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < positions.Count; i++)
            {
                if (i == selfIndex) continue;
                if ((positions[i] - self).sqrMagnitude > neighborRadius * neighborRadius) continue;
                sum += velocities[i];
                count++;
            }
            if (count == 0) return Vector3.zero;
            return (sum / count) - velocities[selfIndex];
        }

        /// <summary>
        /// 結合: 近傍の仲間の重心へ向かうベクトルを返す。
        /// </summary>
        public static Vector3 Cohesion(int selfIndex, IReadOnlyList<Vector3> positions, float neighborRadius)
        {
            var self = positions[selfIndex];
            var sum = Vector3.zero;
            var count = 0;
            for (var i = 0; i < positions.Count; i++)
            {
                if (i == selfIndex) continue;
                if ((positions[i] - self).sqrMagnitude > neighborRadius * neighborRadius) continue;
                sum += positions[i];
                count++;
            }
            if (count == 0) return Vector3.zero;
            return (sum / count) - self;
        }

        /// <summary>
        /// 追跡: 目標地点へ最大速度で向かうための操舵ベクトルを返す。
        /// </summary>
        public static Vector3 Seek(Vector3 position, Vector3 velocity, Vector3 target, float maxSpeed)
        {
            var desired = target - position;
            if (desired.sqrMagnitude < 1e-6f) return Vector3.zero;
            return desired.normalized * maxSpeed - velocity;
        }

        /// <summary>
        /// 逃走: 脅威地点から最大速度で離れるための操舵ベクトルを返す。
        /// 脅威と位置がほぼ一致する場合はゼロベクトルを返す(呼び出し側でフォールバックすること)。
        /// </summary>
        public static Vector3 Flee(Vector3 position, Vector3 velocity, Vector3 threat, float maxSpeed)
        {
            var away = position - threat;
            if (away.sqrMagnitude < 1e-6f) return Vector3.zero;
            return away.normalized * maxSpeed - velocity;
        }

        /// <summary>
        /// ふらつき: 個体ごとの位相(seed)と時刻から滑らかに変化する擬似ランダム方向を返す。
        /// 単調な周回を避けるための微小な揺らぎとして加算する。
        /// </summary>
        public static Vector3 Wander(float seed, float time)
        {
            return new Vector3(
                Mathf.Sin(time * 0.9f + seed),
                Mathf.Sin(time * 1.3f + seed * 2.1f) * 0.5f,
                Mathf.Cos(time * 1.1f + seed * 0.7f));
        }

        /// <summary>
        /// レイと球の交差判定。タッチによる寿司のヒット判定に使う(コライダー不使用)。
        /// </summary>
        /// <param name="rayOrigin">レイの始点</param>
        /// <param name="rayDirection">レイの方向(正規化済みであること)</param>
        /// <param name="center">球の中心</param>
        /// <param name="radius">球の半径</param>
        /// <param name="distance">交差した場合、始点から交点までの距離</param>
        public static bool RayIntersectsSphere(Vector3 rayOrigin, Vector3 rayDirection, Vector3 center, float radius, out float distance)
        {
            distance = 0f;
            var toCenter = center - rayOrigin;
            var projection = Vector3.Dot(toCenter, rayDirection);
            // 球が始点より後方にある場合は不交差扱い
            if (projection < 0f && toCenter.sqrMagnitude > radius * radius) return false;
            var closestSqr = toCenter.sqrMagnitude - projection * projection;
            if (closestSqr > radius * radius) return false;
            var halfChord = Mathf.Sqrt(radius * radius - closestSqr);
            distance = Mathf.Max(0f, projection - halfChord);
            return true;
        }
    }
}
