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
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vuplex.WebView.Internal {

    public class StandaloneRuntimeSettings : ScriptableObject {

        public bool MacChromiumPluginDisabled;

        #if UNITY_EDITOR
            public static SerializedObject GetOrCreateSerializedSettings() {

                var settings = AssetDatabase.LoadAssetAtPath<StandaloneRuntimeSettings>(_assetFilePath);
                if (settings == null) {
                    settings = CreateInstance<StandaloneRuntimeSettings>();
                    var directoryRelativePathInsideAssets = Path.GetDirectoryName(_assetFilePath).Split(new char[] {Path.DirectorySeparatorChar}, 2)[1];
                    // AssetDatabase.CreateAsset() throws an exception if the destination directory doesn't exist.
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, directoryRelativePathInsideAssets));
                    AssetDatabase.CreateAsset(settings, _assetFilePath);
                    AssetDatabase.SaveAssets();
                }
                return new SerializedObject(settings);
            }
        #endif

        public static StandaloneRuntimeSettings Load() {

            var settings = Resources.Load<StandaloneRuntimeSettings>(Path.GetFileNameWithoutExtension(_assetFilePath));
            if (settings != null) {
                return settings;
            }
            // The settings asset doesn't exist yet the user hasn't viewed the settings panel, so return the defaults.
            return CreateInstance<StandaloneRuntimeSettings>();
        }

        const string _assetFilePath = "Assets/Resources/VuplexWebViewStandaloneRuntimeSettings.asset";
    }
}
