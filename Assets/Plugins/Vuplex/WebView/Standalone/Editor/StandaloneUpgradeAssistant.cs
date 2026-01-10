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
using UnityEditor;

namespace Vuplex.WebView.Editor {

    /// <summary>
    /// Deletes old files from previous versions of 3D WebView that must
    /// be removed to prevent compiler errors.
    /// </summary>
    [InitializeOnLoad]
    class StandaloneUpgradeAssistant {

        static StandaloneUpgradeAssistant() {

            EditorUtils.DeleteAssets(new string[] {
                // The following files were removed in v4.14.
                // They must be deleted to avoid compiler errors due to the WebPluginType.Windows and Mac enum values being obsolete
                // and also to prevent the Registrant classes from trying to register the old plugin classes.
                "WindowsWebView",
                "WindowsWebPlugin",
                "WindowsWebPluginRegistrant",
                "MacWebView",
                "MacWebPlugin",
                "MacWebPluginRegistrant",
            });
        }
    }
}
