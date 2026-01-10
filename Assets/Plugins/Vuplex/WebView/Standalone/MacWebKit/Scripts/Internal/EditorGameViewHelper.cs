// Copyright (c) 2025 Vuplex Inc. All rights reserved.
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
using UnityEngine;
using UnityEditor;

namespace Vuplex.WebView.Internal {

    class EditorGameViewHelper : MonoBehaviour {

        public event EventHandler<Rect> GameViewRectChanged;

        public event EventHandler<bool> GameViewVisibilityChanged;

        public void CheckIfGameViewRectChanged() {

            var gameView = _getGameView();
            // If there is no GameView, return Rect.zero.
            Rect currentRect = gameView == null ? Rect.zero : gameView.position;
            if (_previousRect != currentRect) {
                _previousRect = currentRect;
                GameViewRectChanged?.Invoke(this, currentRect);
            }
        }

        static Type _gameViewType;
        bool _gameViewVisible = true;
        Rect _previousRect;

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

        EditorWindow _getGameView() {

            if (_gameViewType == null) {
                _gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            }
            var windows = Resources.FindObjectsOfTypeAll(_gameViewType) as EditorWindow[];
            // If no GameView exists, a new GameView can be created with the following code, but currently seems unnecessary:
            //     var gameView = EditorWindow.GetWindow(_gameViewType);
            return windows.Length > 0 ? windows[0] : null;
        }

        void Update() => _checkIfGameViewVisibilityChanged();
    }
}
#endif
