using Immersal;
using UnityEngine;

namespace GlowingSushi.Root
{
    /// <summary>
    /// Immersal Developer TokenをGit管理外のResourcesファイルから読み込み、
    /// ImmersalSDKの自動初期化(Awake)より前にセットするブートストラップ。
    /// トークンをシーンへシリアライズするとリポジトリにコミットされてしまうため、
    /// 実行時読み込みにしている。
    /// </summary>
    [DefaultExecutionOrder(-5000)] // ImmersalSDKのAwakeより先に実行する
    public sealed class ImmersalTokenLoader : MonoBehaviour
    {
        /// <summary>Resources内のトークンファイル名(拡張子なし)</summary>
        const string TokenResourceName = "ImmersalToken";

        [SerializeField]
        [Tooltip("トークンを設定するImmersalSDKコンポーネント")]
        ImmersalSDK immersalSdk;

        void Awake()
        {
            if (immersalSdk == null)
            {
                Debug.LogError("[GlowingSushi] ImmersalSDKが未設定のためトークンを設定できません");
                return;
            }

            var tokenAsset = Resources.Load<TextAsset>(TokenResourceName);
            if (tokenAsset == null || string.IsNullOrWhiteSpace(tokenAsset.text))
            {
                Debug.LogError(
                    $"[GlowingSushi] トークンファイルが見つかりません: Assets/GlowingSushi/Resources/{TokenResourceName}.txt " +
                    "にImmersal Developer Tokenを保存してください(Git管理外)");
                return;
            }

            immersalSdk.developerToken = tokenAsset.text.Trim();
        }
    }
}
