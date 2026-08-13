using UnityEngine;

namespace TypingMe.Core
{
    /// <summary>
    /// Spawns the persistent services object (GameManager, ThemeManager, AudioManager) before the first
    /// scene loads.
    /// </summary>
    /// <remarks>
    /// Doing this from code rather than placing the services in the Menu scene means either scene can be
    /// entered directly from the Editor — useful when iterating on gameplay without going through Home.
    /// </remarks>
    public static class AppBootstrap
    {
        public const string ServicesResourcePath = "PersistentServices";

        private static GameObject _services;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialise()
        {
            if (_services != null) return;

            var prefab = Resources.Load<GameObject>(ServicesResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[AppBootstrap] Resources/{ServicesResourcePath} is missing — " +
                               "run Typing Me/Rebuild Project Assets to regenerate it.");
                return;
            }

            _services = Object.Instantiate(prefab);
            _services.name = prefab.name;
            Object.DontDestroyOnLoad(_services);
        }
    }
}
