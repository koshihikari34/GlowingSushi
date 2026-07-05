using System;
using System.Collections.Generic;
using GlowingSushi.Domain;
using UnityEngine;

namespace GlowingSushi.Service
{
    /// <summary>
    /// 群れ1つ分の寿司の初期配置データ(SushiSpawnData)を生成するサービス。
    /// ViewModelの生成はViewModel層の責務のため、ここではデータのみを返す。
    /// </summary>
    public sealed class SushiSpawnService
    {
        readonly SushiBehaviorSettings settings;
        readonly System.Random random = new();

        public SushiSpawnService(SushiBehaviorSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>
        /// アンカー中心の球内にランダム配置した群れの初期データを生成する。
        /// </summary>
        /// <param name="anchorCenter">群れの中心となるワールド座標</param>
        /// <param name="count">生成する個体数(0以下なら設定のschoolSizeを使う)</param>
        public IReadOnlyList<SushiSpawnData> CreateSchool(Vector3 anchorCenter, int count = 0)
        {
            if (count <= 0) count = settings.schoolSize;
            var result = new List<SushiSpawnData>(count);
            // アンカーの引き戻し半径より内側に収めて出現させる
            var spawnRadius = settings.containmentRadius * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var position = anchorCenter + NextInsideSphere() * spawnRadius;
                var velocity = NextOnSphere() * (settings.maxSpeed * 0.5f);
                var glowPhase = (float)(random.NextDouble() * Math.PI * 2.0);
                var wanderSeed = (float)(random.NextDouble() * 100.0);
                result.Add(new SushiSpawnData(position, velocity, glowPhase, wanderSeed));
            }
            return result;
        }

        /// <summary>単位球内の一様ランダムな点を返す</summary>
        Vector3 NextInsideSphere()
        {
            // 棄却法: 立方体内の点を球内に収まるまで引き直す
            while (true)
            {
                var v = new Vector3(
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    (float)(random.NextDouble() * 2.0 - 1.0),
                    (float)(random.NextDouble() * 2.0 - 1.0));
                if (v.sqrMagnitude <= 1f) return v;
            }
        }

        /// <summary>単位球面上のランダムな方向を返す</summary>
        Vector3 NextOnSphere()
        {
            var v = NextInsideSphere();
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }
    }
}
