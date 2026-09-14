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
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    /// <summary>
    /// The IWebPlugin implementation for Windows and macOS.
    /// </summary>
    public class StandaloneWebPlugin : MonoBehaviour, IWebPlugin {

        public ICookieManager CookieManager { get; } = StandaloneCookieManager.Instance;

        public static StandaloneWebPlugin Instance {
            get {
                if (_instance == null) {
                    _instance = new GameObject("StandaloneWebPlugin").AddComponent<StandaloneWebPlugin>();
                    DontDestroyOnLoad(_instance.gameObject);
                }
                return _instance;
            }
        }

        public WebPluginType Type { get; } = WebPluginType.Standalone;

        public void ClearAllData() => StandaloneWebView.ClearAllData();

        // Deprecated
        public void CreateMaterial(Action<Material> callback) => callback(VXUtils.CreateDefaultMaterial());

        public virtual IWebView CreateWebView() => StandaloneWebView.Instantiate();

        // 9222 is Chromium's default remote debugging port, which chrome://inspect already listens to by default.
        public void EnableRemoteDebugging() => StandaloneWebView.EnableRemoteDebugging(9222);

        public void SetAutoplayEnabled(bool enabled) => StandaloneWebView.SetAutoplayEnabled(enabled);

        public void SetCameraAndMicrophoneEnabled(bool enabled) => StandaloneWebView.SetCameraAndMicrophoneEnabled(enabled);

        public void SetIgnoreCertificateErrors(bool ignore)=> StandaloneWebView.SetIgnoreCertificateErrors(ignore);

        public void SetStorageEnabled(bool enabled) => StandaloneWebView.SetStorageEnabled(enabled);

        public void SetUserAgent(bool mobile) => StandaloneWebView.GloballySetUserAgent(mobile);

        public void SetUserAgent(string userAgent) => StandaloneWebView.GloballySetUserAgent(userAgent);

        static StandaloneWebPlugin _instance;

        // Note: This needs to be BeforeSceneLoad because using earlier callbacks (like BeforeSplashScreen),
        //       prevents the StandaloneWebPlugin script's MonoBehaviour methods like Start() from being called.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void _registerPlugin() {

            var chromiumPluginDisabled = (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                                         && StandaloneRuntimeSettings.Load().MacChromiumPluginDisabled;
            if (!chromiumPluginDisabled) {
                WebPluginFactory.RegisterStandalonePlugin(Instance);
            }
        }

    #if UNITY_2020_3_OR_NEWER
        void Start() {
            // - It's preferable to use Application.quitting instead
            //   of OnApplicationQuit(), because the latter is called even if
            //   the quit is cancelled by the application returning false from
            //   Application.wantsToQuit, and the former is called only when the
            //   application really quits.
            // - If the application attaches another Application.quitting handler that
            //   throws an exception, then it can prevent this Application.quitting handler from running,
            //   preventing TerminateBrowserProcess() from being called. So, TerminateBrowserProcess() logs
            //   a message when it's called to help diagnose this scenario.
            // - Application.quitting was added in 2018.1, but in 2020.1 and 2020.2
            //   it has a bug where it isn't raised when the application is quit with alt+f4:
            //   https://issuetracker.unity3d.com/issues/application-dot-quitting-event-is-not-raised-when-closing-build
            Application.quitting += () => StandaloneWebView.TerminateBrowserProcess();
        }
    #else
        void OnApplicationQuit() => StandaloneWebView.TerminateBrowserProcess();
    #endif
    }
}
#endif
