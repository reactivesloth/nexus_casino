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
#if UNITY_STANDALONE_OSX
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    partial class CanvasWebViewPrefab {

        partial void OnInit() {

            var version = VXUnityVersion.Instance;
            var isUnityVersionWithScreenSpaceOverlayBugFixed = version.Major >= 7000 ||
                                                               (version.String.StartsWith("6000.0") && version.Patch >= 22) ||
                                                               (version.String.StartsWith("2022.3") && version.Patch >= 51) ||
                                                               (version.String.StartsWith("2021.3") && version.Patch >= 45);
            if (!isUnityVersionWithScreenSpaceOverlayBugFixed) {
                WebViewLogger.LogWarning($"This version of Unity ({version.String}) has a bug on macOS that sometimes prevents 3D WebView's external textures from appearing correctly in a \"Screen Space - Overlay\" Canvas, especially on Apple Silicon Macs. To avoid the issue, please either upgrade to the latest patch release of your Unity LTS version or switch the Canvas's render mode to \"Screen Space - Camera\". <em>https://issuetracker.unity3d.com/issues/raw-image-that-uses-a-material-with-a-custom-shader-is-invisible-in-canvas-when-screen-space-overlay-is-set-and-the-player-is-in-windowed-mode</em>");
            }
        }
    }
}
#endif
