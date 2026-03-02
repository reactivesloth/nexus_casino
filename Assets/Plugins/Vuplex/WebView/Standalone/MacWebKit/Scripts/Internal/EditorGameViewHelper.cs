// Copyright (c) 2026 Vuplex Inc. All rights reserved.
//
// Licensed under the Vuplex Commercial Software Library License, you may
// not use this file except in compliance with the License. You may obtain
// a copy of the License at
//
//     https://vuplex.com/commercial-library-license
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#if UNITY_EDITOR_OSX
using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace Vuplex.WebView.Internal {

    class EditorGameViewHelper : MonoBehaviour {

        public event EventHandler<Rect> GameViewRectChanged;

        public event EventHandler<bool> GameViewVisibilityChanged;

        // Adjusts the rect by adding extra padding added to the GameView.
        // Even when the GameView's scale is set to "Free Aspect", there's extra
        // padding at the top of the view where the tab UI is located. When the
        // GameView's scale is set to a specific aspect ratio like "Full HD", there's
        // also additional padding.
        public static Rect AdjustRectForNative2DMode(Rect rect) {

            var gameView = _getGameView();
            // Originally this code used GameView.viewPadding:
            // https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Editor/Mono/PlayModeView/PlayModeView.cs#L58
            // However, I discovered during testing that viewPadding doesn't exist for Unity 2020.3:
            // https://github.com/Unity-Technologies/UnityCsReference/blob/2020.3/Editor/Mono/PlayModeView/PlayModeView.cs
            // The value of viewPadding is targetInParent.position, so this code now uses that instead.
            // https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Editor/Mono/GameView/GameView.cs#L1050
            var targetInParent = (Rect)_targetInParentProperty.GetValue(gameView, null);
            var viewPadding = targetInParent.position;
            // Access the zoomArea to determine the value that the scale slider is actually set to.
            var zoomArea = _zoomAreaField.GetValue(gameView);
            var actualScale = (Vector2)_zoomableAreaScaleProperty.GetValue(zoomArea, null);
            var scaledViewPadding = EditorGUIUtility.pixelsPerPoint * viewPadding / actualScale;
            var updatedRect = new Rect(
                rect.position + scaledViewPadding,
                rect.size
            );
            // defaultScale is what the scale slider defaults to if the user doesn't manually change it.
            var defaultScale = (float)_defaultScaleField.GetValue(gameView);
            if (actualScale.x != defaultScale && !_scaleWarningLogged) {
                _scaleWarningLogged = true;
                WebViewLogger.LogWarning($"The Game View's scale is set to a value ({actualScale.x.ToString("f2")}x) that is not the default scale ({defaultScale.ToString("f2")}x). Since Native 2D Mode is enabled for a CanvasWebViewPrefab, this can cause the native webview to extend outside the bounds of the Game View in the editor. To avoid that, it is recommended to set the Game View's scale to {defaultScale.ToString("f2")}x.");
            }
            return new Rect(actualScale * updatedRect.position, actualScale * updatedRect.size);
        }

        public void CheckIfGameViewRectChanged() {

            var gameView = _getGameView();
            // If there is no GameView, return Rect.zero.
            Rect currentRect = gameView == null ? Rect.zero : gameView.position;
            if (_previousRect != currentRect) {
                _previousRect = currentRect;
                GameViewRectChanged?.Invoke(this, currentRect);
            }
        }

        static FieldInfo _defaultScaleField;
        static Type _gameViewType;
        bool _gameViewVisible = true;
        Rect _previousRect;
        static bool _scaleWarningLogged;
        static PropertyInfo _targetInParentProperty;
        static FieldInfo _zoomAreaField;
        static PropertyInfo _zoomableAreaScaleProperty;

        void Start() {

            // Call CheckIfGameViewRectChanged once per second.
            var interval = 1.0f;
            InvokeRepeating(nameof(CheckIfGameViewRectChanged), interval, interval);
        }

        EditorWindow _previousFocusedWindow = null;

        void _checkIfGameViewVisibilityChanged() {

            // The Editor.windowFocusChanged event requires Unity 2023.2 or newer,
            // so just check EditorWindow.focusedWindow in Update().
            var focusedWindow = EditorWindow.focusedWindow;
            if (_previousFocusedWindow == focusedWindow || focusedWindow == null) {
                return;
            }
            _previousFocusedWindow = focusedWindow;
            var gameView = _getGameView();
            var focusedWindowIsGameView = focusedWindow == gameView;
            if (_gameViewVisible && gameView == null) {
                // The GameView tab was closed.
                _gameViewVisible = false;
                GameViewVisibilityChanged?.Invoke(this, false);
            } else if (_gameViewVisible && !focusedWindowIsGameView && focusedWindow.position == gameView.position) {
                // The focused window is not the GameView, but it has the same position as the GameView, which
                // means that the GameView is hidden behind it (i.e. they are tabs that share the same native view).
                _gameViewVisible = false;
                GameViewVisibilityChanged?.Invoke(this, false);
            } else if (!_gameViewVisible && focusedWindowIsGameView) {
                _gameViewVisible = true;
                GameViewVisibilityChanged?.Invoke(this, true);
            }
        }

        static EditorWindow _getGameView() {

            _initializeReflectionFieldsIfNeeded();
            var windows = Resources.FindObjectsOfTypeAll(_gameViewType) as EditorWindow[];
            // If no GameView exists, a new GameView can be created with the following code, but currently seems unnecessary:
            //     var gameView = EditorWindow.GetWindow(_gameViewType);
            return windows.Length > 0 ? windows[0] : null;
        }

        // Initialize and cache the PropertyInfo and FieldInfo results so they don't have to be queried every time.
        static void _initializeReflectionFieldsIfNeeded() {

            if (_gameViewType != null) {
                return;
            }
            // GameView.cs C# source: https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Editor/Mono/GameView/GameView.cs
            _gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            _targetInParentProperty = _gameViewType.GetProperty("targetInParent", BindingFlags.NonPublic | BindingFlags.Instance);
            _defaultScaleField = _gameViewType.GetField("m_defaultScale", BindingFlags.NonPublic | BindingFlags.Instance);
            _zoomAreaField = _gameViewType.GetField("m_ZoomArea", BindingFlags.NonPublic | BindingFlags.Instance);
            // ZoomableArea.cs C# source: https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Editor/Mono/Animation/ZoomableArea.cs
            var zoomableAreaType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ZoomableArea");
            _zoomableAreaScaleProperty = zoomableAreaType.GetProperty("scale");
        }

        void Update() => _checkIfGameViewVisibilityChanged();
    }
}
#endif
