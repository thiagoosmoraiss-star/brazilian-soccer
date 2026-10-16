using System;
using Game.UI;
using Game.UI.Presenters;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game.App
{
    /// <summary>
    /// Shell scene (C3a; TECHNICAL_SPEC §15 "Shell | todas as telas de menu e carreira (UI Toolkit), retrato"): a runtime
    /// <see cref="UIDocument"/> with the <see cref="ShellView"/> over the career session. "Jogar" loads the Match scene;
    /// coming back from it, the Shell opens on the post-match screen.
    /// </summary>
    public sealed class ShellScreen : MonoBehaviour
    {
        private ShellView _view;
        private UIDocument _doc;
        private Rect _safeArea;

        private void Start()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            if (!CareerContext.Ready) { SceneManager.LoadScene("Boot"); return; }

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("AcessoRuntimeTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int((int)UiKit.RefWidth, (int)UiKit.RefHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f; // portrait: scale by width, the content scrolls vertically
            var go = new GameObject("Shell UI");
            go.SetActive(false);
            _doc = go.AddComponent<UIDocument>();
            _doc.panelSettings = settings;
            go.SetActive(true);

            var presenter = new ShellPresenter(CareerContext.Session, new ShellNavigator(), () => (ulong)DateTime.UtcNow.Ticks);
            presenter.Start(CareerContext.ReturningFromMatch);
            CareerContext.ReturningFromMatch = false;
            _view = new ShellView(_doc.rootVisualElement, presenter, () => SceneManager.LoadScene(BootLoader.MatchScene));
            _doc.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(true));
        }

        private void Update()
        {
            if (_view != null && Screen.safeArea != _safeArea) ApplySafeArea(false);
        }

        /// <summary>Keeps the header clear of the status bar / notch (Screen.safeArea, in panel pixels).</summary>
        private void ApplySafeArea(bool force)
        {
            var area = Screen.safeArea;
            if (!force && area == _safeArea) return;
            _safeArea = area;
            float panelHeight = _doc.rootVisualElement.layout.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0 || Screen.height <= 0) return;
            float topPixels = Screen.height - area.yMax;
            _view.SetTopInset(topPixels / Screen.height * panelHeight);
        }

        private void OnDestroy() => _view?.Dispose();
    }
}
