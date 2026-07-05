using UnityEngine;

namespace GlowingSushi.Domain
{
    /// <summary>
    /// 表面ふるまい(転がり・散歩・スピン衝突)の運動計算を行う純粋な静的関数群。
    /// スポット表面上のローカル2D座標系(x=right, y=forward)で扱う。
    /// </summary>
    public static class SurfaceMotionMath
    {
        /// <summary>
        /// 円形エリアの縁で速度を反射させる。位置が縁を越えていたら内側へ戻す。
        /// </summary>
        /// <param name="position">ローカル2D位置(変更される)</param>
        /// <param name="velocity">ローカル2D速度(変更される)</param>
        /// <param name="areaRadius">エリア半径</param>
        public static void ReflectInsideCircle(ref Vector2 position, ref Vector2 velocity, float areaRadius)
        {
            var distance = position.magnitude;
            if (distance <= areaRadius || distance < 1e-6f) return;

            var normal = -position / distance; // 縁から中心へ向かう法線
            velocity = Vector2.Reflect(velocity, normal);
            position = position.normalized * areaRadius; // 縁の内側へクランプ
        }

        /// <summary>
        /// 転がり運動の回転差分を計算する。移動量と接地半径から回転角を求め、
        /// 進行方向に対して垂直な軸まわりに回す。
        /// </summary>
        /// <param name="up">表面の法線</param>
        /// <param name="worldVelocity">ワールド空間での移動速度</param>
        /// <param name="contactRadius">接地半径(小さいほどよく回る)</param>
        /// <param name="deltaTime">経過時間</param>
        public static Quaternion RollDelta(Vector3 up, Vector3 worldVelocity, float contactRadius, float deltaTime)
        {
            var speed = worldVelocity.magnitude;
            if (speed < 1e-6f || contactRadius < 1e-6f) return Quaternion.identity;

            var axis = Vector3.Cross(up, worldVelocity / speed).normalized;
            var angleDeg = speed * deltaTime / contactRadius * Mathf.Rad2Deg;
            // ワールド軸まわりの回転として返す(呼び出し側で rotation の左から掛ける)
            return Quaternion.AngleAxis(-angleDeg, axis);
        }

        /// <summary>
        /// 散歩用の進行方向(ラジアン)を滑らかに揺らす。
        /// </summary>
        /// <param name="heading">現在の進行方向(ラジアン)</param>
        /// <param name="seed">個体固有シード</param>
        /// <param name="time">経過時間</param>
        /// <param name="turnRate">方向の変わりやすさ(rad/s)</param>
        /// <param name="deltaTime">経過時間差分</param>
        public static float WanderHeading(float heading, float seed, float time, float turnRate, float deltaTime)
        {
            // 滑らかなノイズとしてsinの合成を使う(擬似Perlin)
            var noise = Mathf.Sin(time * 0.7f + seed) + 0.5f * Mathf.Sin(time * 1.9f + seed * 2.3f);
            return heading + noise * turnRate * deltaTime;
        }

        /// <summary>
        /// 2個体の弾性衝突(等質量)。衝突法線方向の速度成分を入れ替える。
        /// 離れる方向に動いている場合は何もしない(めり込み二重反応の防止)。
        /// </summary>
        /// <param name="position1">個体1の位置</param>
        /// <param name="position2">個体2の位置</param>
        /// <param name="velocity1">個体1の速度(変更される)</param>
        /// <param name="velocity2">個体2の速度(変更される)</param>
        /// <returns>衝突処理を行ったかどうか</returns>
        public static bool ResolveElasticCollision(
            Vector2 position1, Vector2 position2, ref Vector2 velocity1, ref Vector2 velocity2)
        {
            var delta = position2 - position1;
            var distance = delta.magnitude;
            if (distance < 1e-6f) return false;

            var normal = delta / distance;
            var relativeSpeed = Vector2.Dot(velocity1 - velocity2, normal);
            if (relativeSpeed <= 0f) return false; // すでに離れる方向

            // 等質量の弾性衝突: 法線方向成分を交換する
            var exchange = relativeSpeed * normal;
            velocity1 -= exchange;
            velocity2 += exchange;
            return true;
        }
    }
}
