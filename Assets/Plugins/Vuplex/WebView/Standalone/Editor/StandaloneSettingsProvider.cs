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
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView.Editor {

    class StandaloneSettingsProvider : SettingsProvider {

        public StandaloneSettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope) {}

        [SettingsProvider]
        public static SettingsProvider CreateStandaloneSettingsProvider() {

            var provider = new StandaloneSettingsProvider("Project/Vuplex WebView/Windows and macOS", SettingsScope.Project);
            provider.keywords = GetSearchKeywordsFromGUIContentProperties<Styles>();
            return provider;
        }

        public override void OnInspectorUpdate() => _enableOrDisableMacPluginIfNeeded();

        // Called when the user clicks on the element in the settings window.
        public override void OnActivate(string searchContext, VisualElement rootElement) {

            _serializedSettings = StandaloneRuntimeSettings.GetOrCreateSerializedSettings();
            _macChromiumPluginDisabledProperty = _serializedSettings.FindProperty("MacChromiumPluginDisabled");
            _macChromiumPluginDisabledPreviousValue = _macChromiumPluginDisabledProperty.boolValue;
        }

        public override void OnGUI(string searchContext) {

            EditorGUILayout.Space(20);
            // Use a horizontal layout to increase the label width to 280 so that it doesn't get truncated.
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(Styles.MacChromiumPluginDisabled, GUILayout.Width(280));
            EditorGUILayout.PropertyField(_macChromiumPluginDisabledProperty, GUIContent.none, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();
            _serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorGUILayout.Space(10);
            var codecsButtonClicked = GUILayout.Button(
                "Enable proprietary video codecs...",
                new GUILayoutOption[] { GUILayout.ExpandWidth(false) }
            );
            if (codecsButtonClicked) {
                StandaloneVideoCodecsWindow.ShowWindow();
            }
        }

        bool _macChromiumPluginDisabledPreviousValue;
        SerializedProperty _macChromiumPluginDisabledProperty;
        SerializedObject _serializedSettings;

        class Styles {
            public static GUIContent MacChromiumPluginDisabled = new GUIContent("Disable macOS Chromium plugin and use WebKit");
        }

        void _enableOrDisableMacPluginIfNeeded() {

            var macChromiumPluginDisabled = _macChromiumPluginDisabledProperty.boolValue;
            if (_macChromiumPluginDisabledPreviousValue == macChromiumPluginDisabled) {
                return;
            }
            _macChromiumPluginDisabledPreviousValue = macChromiumPluginDisabled;
            EditorUtils.SetPluginEnabled("Vuplex/WebView/Standalone/Mac/Plugins/VuplexWebViewMac.bundle", !macChromiumPluginDisabled, BuildTarget.StandaloneOSX);
            EditorUtils.SetPluginEnabled("Vuplex/WebView/Standalone/Mac/Plugins/VuplexWebViewMac_with_codecs.bundle", false, BuildTarget.StandaloneOSX);
        }
    }
}
