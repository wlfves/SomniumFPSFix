using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SomniumFPSFix
{
    // Shows a small "Somnium FPS Fix vX active" label in the top-right corner of
    // the title screen, so players can confirm at a glance that the mod loaded.
    // The label stays up while the title screen is showing, then fades out once
    // the player leaves it (for example by starting or continuing a game).
    internal static class ModNotification
    {
        // The label only appears on the title screen.
        const string TargetScene = "Title";

        // Makes sure the label is only ever created once per game session.
        static bool _shown;

        // Called once at startup. Waits for the title screen to load, then builds
        // the label.
        public static void Register() { SceneManager.sceneLoaded += OnSceneLoaded; }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != TargetScene || _shown) return;
            _shown = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // The label lives on its own object that survives scene changes, so it
            // can fade out smoothly when the player leaves the title screen instead
            // of vanishing the instant the next scene loads.
            var go = new GameObject("SomniumFPSFix_Notification");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<NotificationBehaviour>();
        }
    }

    // Builds the on-screen label and handles its fade-out.
    internal class NotificationBehaviour : MonoBehaviour
    {
        // How long the fade-out takes, in seconds.
        const float FadeDuration = 1f;
        // Gap between the label and the edge of the screen, in pixels.
        const float Padding = 16f;

        bool  _fading;
        float _fadeStart;
        TextMeshProUGUI _tmp;

        void Start()
        {
            // A full-screen overlay that draws on top of the game.
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // high number = drawn above the game's own UI

            // Keep the label the same relative size and position on any screen
            // resolution, treating 1920x1080 as the reference.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 1f;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(transform, false);

            // Pin the label to the top-right corner.
            var rect = textGo.AddComponent<RectTransform>();
            rect.anchorMin        = new Vector2(1f, 1f);
            rect.anchorMax        = new Vector2(1f, 1f);
            rect.pivot            = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-Padding, -Padding);
            rect.sizeDelta        = new Vector2(500f, 50f);

            _tmp           = textGo.AddComponent<TextMeshProUGUI>();
            _tmp.text      = $"Somnium FPS Fix v{Plugin.Instance.Info.Metadata.Version} active";
            _tmp.fontSize  = 22f;
            _tmp.alignment = TextAlignmentOptions.TopRight;
            _tmp.color     = Color.white;

            // Begin fading as soon as the player leaves the title screen.
            SceneManager.sceneLoaded += OnNextSceneLoaded;
        }

        void OnDestroy() { SceneManager.sceneLoaded -= OnNextSceneLoaded; }

        // Fires when any scene loads after the title screen: start the fade.
        void OnNextSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_fading) return;
            _fading    = true;
            _fadeStart = Time.realtimeSinceStartup;
            SceneManager.sceneLoaded -= OnNextSceneLoaded;
        }

        void Update()
        {
            if (!_fading || _tmp == null) return;

            // Fade the label from fully visible to invisible, then remove it.
            // We measure real elapsed time (not in-game time) so the fade runs at a
            // steady pace even while a Somnium's slow-motion is active.
            float elapsed = Time.realtimeSinceStartup - _fadeStart;
            if (elapsed >= FadeDuration) { Destroy(gameObject); return; }
            _tmp.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, elapsed / FadeDuration));
        }
    }
}
