using UnityEngine;

namespace Aquaring.Managers
{
    /// <summary>
    /// Global runtime setup that should happen once, before any scene logic:
    /// frame-rate target and screen-sleep. Runs automatically – no scene wiring.
    /// </summary>
    public static class AppBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }
    }
}
