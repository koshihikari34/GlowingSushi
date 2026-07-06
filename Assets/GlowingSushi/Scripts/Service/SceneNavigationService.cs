using UnityEngine.SceneManagement;

namespace GlowingSushi.Service
{
    /// <summary>
    /// シーン遷移をラップするサービス。ViewModelがシーン名を直接知らずに遷移できるようにする。
    /// </summary>
    public sealed class SceneNavigationService
    {
        const string MainSceneName = "Main";

        /// <summary>メインシーン(ARコンテンツ)へ遷移する</summary>
        public void LoadMainScene()
        {
            SceneManager.LoadScene(MainSceneName);
        }
    }
}
