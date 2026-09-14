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
#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX
using System;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    class MacWebKitWebPlugin : IWebPlugin {

        public ICookieManager CookieManager { get; } = MacWebKitCookieManager.Instance;

        public static MacWebKitWebPlugin Instance {
            get {
                if (_instance == null) {
                    _instance = new MacWebKitWebPlugin();
                }
                return _instance;
            }
        }

        public WebPluginType Type { get; } = WebPluginType.MacWebKit;

        public void ClearAllData() => MacWebKitWebView.ClearAllData();

        // Deprecated
        public void CreateMaterial(Action<Material> callback) {

            var material = new Material(Resources.Load<Material>("AppleWebMaterial"));
            callback(material);
        }

        public IWebView CreateWebView() => MacWebKitWebView.Instantiate();

        public void EnableRemoteDebugging() => MacWebKitWebView.SetRemoteDebuggingEnabled(true);

        public void SetAutoplayEnabled(bool enabled) => MacWebKitWebView.SetAutoplayEnabled(enabled);

        public void SetCameraAndMicrophoneEnabled(bool enabled) => MacWebKitWebView.SetCameraAndMicrophoneEnabled(enabled);

        public void SetIgnoreCertificateErrors(bool ignore) => MacWebKitWebView.SetIgnoreCertificateErrors(ignore);

        public void SetStorageEnabled(bool enabled) => MacWebKitWebView.SetStorageEnabled(enabled);

        public void SetUserAgent(bool mobile) => MacWebKitWebView.GloballySetUserAgent(mobile);

        public void SetUserAgent(string userAgent) => MacWebKitWebView.GloballySetUserAgent(userAgent);

        static MacWebKitWebPlugin _instance;
    }
}
#endif
