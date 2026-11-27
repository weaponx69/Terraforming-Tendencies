using GameDevTV.RTS.Environment;
using GameDevTV.RTS.Player;
using UnityEngine;
namespace GameDevTV.RTS.MainGame
{
    /// <summary>
    /// MainGame.unity often loads without <see cref="PlayerInput"/> on the camera until controllers boot.
    /// Retry camera rig wiring so Play never sits at the default (0,1,-10) white void.
    /// </summary>
    public sealed class MainGameSliceCameraBootstrap : MonoBehaviour
    {
        const float RetrySeconds = 12f;
        float retryUntil;
        float nextRetry;
        static bool rigPrimedAfterPlanet;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => rigPrimedAfterPlanet = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            if (!MainGameController.IsMainGamePlayScene()) return;
            var go = new GameObject(nameof(MainGameSliceCameraBootstrap));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MainGameSliceCameraBootstrap>();
        }

        void OnEnable()
        {
            retryUntil = Time.unscaledTime + RetrySeconds;
            nextRetry = 0f;
            Tick(force: true);
        }

        void Update()
        {
            if (Time.unscaledTime > retryUntil) return;
            if (Time.unscaledTime < nextRetry) return;
            nextRetry = Time.unscaledTime + 0.35f;
            Tick(force: false);
        }

        void Tick(bool force)
        {
            if (!MainGameController.IsMainGamePlayScene()) return;

            MainGamePerformanceBudget.ApplyEarlyPresentationForPlayScene();
            PlayerInput.EnsureOnMainCameraForSlice();
            var input = PlayerInput.Instance;
            if (input == null) return;

            var planet = PlanetGenerator.Instance;
            bool planetReady = planet != null && planet.HasGenerated;
            bool stuck = PlayerInput.IsGameplayCameraAtDefaultPose();

            if (force || stuck)
                PlayerInput.EnsureMainGameCameraRigOnly();

            if (stuck)
                input.RefreshMainGameCameraAfterPlanet();
            else if ((force || planetReady) && !rigPrimedAfterPlanet)
            {
                rigPrimedAfterPlanet = true;
                input.RefreshMainGameCameraAfterPlanet();
            }

            if (stuck || force)
            {
                var ctrl = MainGameController.Instance;
                if (ctrl != null && ctrl.Colony != null && planetReady)
                    ctrl.FrameCameraOnCommandPost();
            }
        }
    }
}