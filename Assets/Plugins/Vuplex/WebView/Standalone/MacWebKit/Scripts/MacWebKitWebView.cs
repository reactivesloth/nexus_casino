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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Vuplex.WebView.Internal;

namespace Vuplex.WebView {

    /// <summary>
    /// MacWebKitWebView is the optional WebKit-based implementation of IWebView in
    /// 3D WebView for Windows and macOS. It's powered by macOS's built-in WKWebView
    /// component and also includes additional APIs for WebKit-specific functionality.
    /// For details about the WebKit plugin and how to enable it, please see
    /// <see href="https://support.vuplex.com/articles/standalone-browser-engines">this article</see>.
    /// </summary>
    public class MacWebKitWebView : BaseWebView,
                                    IWebView,
                                    IWithDownloads,
                                    IWithKeyDownAndUp,
                                    IWithMovablePointer,
                                    IWithNative2DMode,
                                    IWithNativeJavaScriptDialogs,
                                    IWithPdfCreation,
                                    IWithPixelDensity,
                                    IWithPointerDownAndUp,
                                    IWithPopups,
                                    IWithSettableUserAgent {

        /// <see cref="IWithNative2DMode"/>
        public bool Native2DModeEnabled { get => _native2DModeEnabled; }

        /// <see cref="IWithPixelDensity"/>
        public float PixelDensity { get; private set; } = 1f;

        /// <summary>
        /// By default, support for changing the webview's pixel density (via WebViewPrefab.PixelDensity
        /// or IWithPixelDensity) is disabled for the macOS WebKit plugin because it negatively impacts
        /// performance, particularly on Macs with Intel processors. However, if you wish to override this
        /// to enable changing the pixel density, you can do so by setting this field to `true`. The default
        /// is `false`.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///         MacWebKitWebView.PixelDensityEnabled = true;
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static bool PixelDensityEnabled = false;

        public WebPluginType PluginType { get; } = WebPluginType.MacWebKit;

        /// <see cref="IWithNative2DMode"/>
        public Rect Rect { get => _rect; }

        /// <see cref="IWithNative2DMode"/>
        public bool Visible { get => _visible; }

        /// <see cref="IWithDownloads"/>
        public event EventHandler<DownloadChangedEventArgs> DownloadProgressChanged;

        /// <see cref="IWithPopups"/>
        public event EventHandler<PopupRequestedEventArgs> PopupRequested;

        /// <seealso cref="IWithNative2DMode"/>
        public void BringToFront() {

            _assertValidState();
            _assertNative2DModeEnabled();
            WebView_bringToFront(_nativeWebViewPtr);
        }

        public static void ClearAllData() => WebView_clearAllData();

        /// <see cref="IWithPdfCreation"/>
        public Task<string> CreatePdf() {

            _assertValidState();
            var taskSource = new TaskCompletionSource<string>();
            var resultCallbackId = Guid.NewGuid().ToString();
            _pendingCreatePdfTaskSources[resultCallbackId] = taskSource;
            var pdfSubdirectory = Path.Combine(Application.temporaryCachePath, "Vuplex.WebView", "pdfs");
            Directory.CreateDirectory(pdfSubdirectory);
            var pdfPath = Path.Combine(pdfSubdirectory, resultCallbackId + ".pdf");
            WebView_createPdf(_nativeWebViewPtr, resultCallbackId, pdfPath);
            return taskSource.Task;
        }

        public static Task<bool> DeleteCookies(string url, string cookieName = null) {

            if (url == null) {
                throw new ArgumentException("The url cannot be null.");
            }
            var taskSource = new TaskCompletionSource<bool>();
            var resultCallbackId = Guid.NewGuid().ToString();
            _pendingDeleteCookiesResultCallbacks[resultCallbackId] = taskSource.SetResult;
            WebView_deleteCookies(url, cookieName, resultCallbackId);
            return taskSource.Task;
        }

        public override void Dispose() {

            // Use a saved copy of the GameObject name here because trying to access gameObject.name can result in the following exception:
            // > MissingReferenceException: The object of type 'MacWebKitWebView' has been destroyed but you are still trying to access it.
            _webViewGameObjects.Remove(_gameObjectName);
            base.Dispose();
        }

        public static Task<Cookie[]> GetCookies(string url, string cookieName = null) {

            if (url == null) {
                throw new ArgumentException("The url cannot be null.");
            }
            var taskSource = new TaskCompletionSource<Cookie[]>();
            var resultCallbackId = Guid.NewGuid().ToString();
            _pendingGetCookiesResultCallbacks[resultCallbackId] = taskSource.SetResult;
            WebView_getCookies(url, cookieName, resultCallbackId);
            return taskSource.Task;
        }

        /// <summary>
        /// Returns an Objective-C pointer to the instance's underlying native <see href="https://developer.apple.com/documentation/webkit/wkwebview?language=objc">WKWebView</see>.
        /// The application can use this to utilize native macOS APIs for which 3D WebView doesn't yet have
        /// dedicated C# equivalents. To utilize the pointer, the application must pass it to a function
        /// defined in a native macOS library (.bundle file) like illustrated in the example below.
        /// </summary>
        /// <remarks>
        /// Warning: Adding code that interacts with the native WKWebView directly
        /// may interfere with 3D WebView's functionality
        /// and vice versa. So, it's highly recommended to stick to 3D WebView's
        /// C# APIs whenever possible and only use GetNativeWebView() if
        /// truly necessary. If 3D WebView is missing an API that you need,
        /// feel free to [contact us](https://vuplex.com/contact).
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example of defining a native Objective-C function that sets WKWebView.allowsLinkPreview.
        /// // Compile this function into a .bundle library file and include it in your project.
        /// #import &lt;Foundation/Foundation.h&gt;
        /// #import &lt;WebKit/WebKit.h&gt;
        ///
        /// void WebViewCustom_setAllowsLinkPreview(WKWebView *webView, BOOL allowsLinkPreview) {
        ///
        ///     webView.allowsLinkPreview = allowsLinkPreview;
        /// }
        /// </code>
        /// <code>
        /// // Example of calling the native Objective-C function from C#.
        /// async void EnableLinkPreviews(WebViewPrefab webViewPrefab) {
        ///
        ///     await webViewPrefab.WaitUntilInitialized();
        ///     #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///         var macWebView = webViewPrefab.WebView as MacWebKitWebView;
        ///         var wkWebViewPtr = macWebView.GetNativeWebView();
        ///         WebViewCustom_setAllowsLinkPreview(wkWebViewPtr, true);
        ///     #endif
        /// }
        ///
        /// [System.Runtime.InteropServices.DllImport("YourPluginName.bundle")]
        /// static extern void WebViewCustom_setAllowsLinkPreview(System.IntPtr webViewPtr, bool allowsLinkPreview);
        /// </code>
        /// </example>
        public IntPtr GetNativeWebView() {

            _assertValidState();
            return WebView_getNativeWebView(_nativeWebViewPtr);
        }

        public static void GloballySetUserAgent(bool mobile) => WebView_globallySetUserAgentToMobile(mobile);

        public static void GloballySetUserAgent(string userAgent) => WebView_globallySetUserAgent(userAgent);

        public async Task Init(int width, int height) => await _initMac3D(width, height, IntPtr.Zero);

        /// <see cref="IWithNative2DMode"/>
        public async Task InitInNative2DMode(Rect rect) =>  await _initMac2D(rect, IntPtr.Zero);

        public static MacWebKitWebView Instantiate() => new GameObject().AddComponent<MacWebKitWebView>();

        /// <see cref="IWithKeyDownAndUp"/>
        public void KeyDown(string key, KeyModifier modifiers) {

            _assertValidState();
            WebView_keyDown(_nativeWebViewPtr, key, (int)modifiers);
        }

        /// <see cref="IWithKeyDownAndUp"/>
        public void KeyUp(string key, KeyModifier modifiers) {

            _assertValidState();
            WebView_keyUp(_nativeWebViewPtr, key, (int)modifiers);
        }

        /// <see cref="IWithMovablePointer"/>
        public void MovePointer(Vector2 normalizedPoint, bool pointerLeave = false) {

            _assertValidState();
            var pixelsPoint = _normalizedToPointAssertValid(normalizedPoint);
            WebView_movePointer(_nativeWebViewPtr, pixelsPoint.x, pixelsPoint.y, pointerLeave);
        }

        /// <see cref="IWithPointerDownAndUp"/>
        public void PointerDown(Vector2 point) => _pointerDown(point, MouseButton.Left, 1, false);

        /// <see cref="IWithPointerDownAndUp"/>
        public void PointerDown(Vector2 point, PointerOptions options) {

            if (options == null) {
                options = new PointerOptions();
            }
            _pointerDown(point, options.Button, options.ClickCount, options.PreventStealingFocus);
        }

        /// <see cref="IWithPointerDownAndUp"/>
        public void PointerUp(Vector2 point) => _pointerUp(point, MouseButton.Left, 1, false);

        /// <see cref="IWithPointerDownAndUp"/>
        public void PointerUp(Vector2 point, PointerOptions options) {

            if (options == null) {
                options = new PointerOptions();
            }
            _pointerUp(point, options.Button, options.ClickCount, options.PreventStealingFocus);
        }

        /// <summary>
        /// Sets whether cross-origin requests in the context of a file scheme URL should be allowed to access content
        /// from other file scheme URLs. The default value is `false`. Note that some accesses such as image HTML
        /// elements don't follow same-origin rules and aren't affected by this setting.
        /// </summary>
        /// <example>
        /// <code>
        /// await webViewPrefab.WaitUntilInitialized();
        /// #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///     var macWebView = webViewPrefab.WebView as MacWebKitWebView;
        ///     macWebView.SetAllowFileAccessFromFileUrls(true);
        /// #endif
        /// </code>
        /// </example>
        public void SetAllowFileAccessFromFileUrls(bool allow) {

            _assertValidState();
            WebView_setAllowFileAccessFromFileUrls(_nativeWebViewPtr, allow);
        }

        /// <summary>
        /// When Native 2D Mode is enabled, this method sets whether horizontal swipe
        /// gestures trigger backward and forward page navigation. The default is `false`.
        /// When Native 2D Mode is disabled, this method has no effect.
        /// </summary>
        /// <example>
        /// <code>
        /// await canvasWebViewPrefab.WaitUntilInitialized();
        /// #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///     var macWebView = canvasWebViewPrefab.WebView as MacWebKitWebView;
        ///     macWebView.SetAllowsBackForwardNavigationGestures(true);
        /// #endif
        /// </code>
        /// </example>
        /// <seealso href="https://developer.apple.com/documentation/webkit/wkwebview/1414995-allowsbackforwardnavigationgestu">WKWebView.allowsBackForwardNavigationGestures</seealso>
        public void SetAllowsBackForwardNavigationGestures(bool allow) {

            _assertValidState();
            WebView_setAllowsBackForwardNavigationGestures(_nativeWebViewPtr, allow);
        }

        public static void SetAutoplayEnabled(bool enabled) => WebView_setAutoplayEnabled(enabled);

        public static void SetCameraAndMicrophoneEnabled(bool enabled) => WebView_setCameraAndMicrophoneEnabled(enabled);

        /// <summary>
        /// Like Web.SetCameraAndMicrophoneEnabled(), but enables only the camera without enabling the microphone.
        /// In addition to calling this method, you must also complete the additional steps described [here](https://support.vuplex.com/articles/webrtc)
        /// in order to successfully enable the camera.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///         MacWebKitWebView.SetCameraEnabled(true);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetCameraEnabled(bool enabled) => WebView_setCameraEnabled(enabled);

        public static Task<bool> SetCookie(Cookie cookie) {

            if (cookie == null) {
                throw new ArgumentException("Cookie cannot be null.");
            }
            if (!cookie.IsValid) {
                throw new ArgumentException("Cannot set invalid cookie: " + cookie);
            }
            WebView_setCookie(cookie.ToJson());
            return Task.FromResult(true);
        }

        /// <see cref="IWithDownloads"/>
        public void SetDownloadsEnabled(bool enabled) {

            _assertValidState();
            WebView_setDownloadsEnabled(_nativeWebViewPtr, enabled);
        }

        /// <summary>
        /// When Native 2D Mode is enabled, this method sets whether web pages can use the
        /// <see href="https://developer.mozilla.org/en-US/docs/Web/API/Fullscreen_API">JavaScript Fullscreen API</see>
        /// to make an HTML element occupy the device's entire screen. The default is `true`, meaning that the JavaScript
        /// Fullscreen API is enabled by default. When Native 2D Mode is disabled, this method has no effect because
        /// the JavaScript Fullscreen API is only supported in Native 2D Mode.
        /// </summary>
        /// <example>
        /// <code>
        /// #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///     await canvasWebViewPrefab.WaitUntilInitialized();
        ///     var macWebView = canvasWebViewPrefab.WebView as MacWebKitWebView;
        ///     // Disable the JavaScript Fullscreen API.
        ///     macWebView.SetFullscreenEnabled(false);
        /// #endif
        /// </code>
        /// </example>
        /// <seealso href="https://support.vuplex.com/articles/fullscreen">Fullscreen support in 3D WebView</seealso>
        public void SetFullscreenEnabled(bool enabled) {

            _assertValidState();
            WebView_setFullscreenEnabled(_nativeWebViewPtr, enabled);
        }

        public static void SetIgnoreCertificateErrors(bool ignore) => WebView_setIgnoreCertificateErrors(ignore);

        /// <summary>
        /// Like Web.SetCameraAndMicrophoneEnabled(), but enables only the microphone without enabling the camera.
        /// In addition to calling this method, you must also complete the additional steps described [here](https://support.vuplex.com/articles/webrtc)
        /// in order to successfully enable the microphone.
        /// </summary>
        /// <example>
        /// <code>
        /// void Awake() {
        ///     #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///         MacWebKitWebView.SetMicrophoneEnabled(true);
        ///     #endif
        /// }
        /// </code>
        /// </example>
        public static void SetMicrophoneEnabled(bool enabled) => WebView_setMicrophoneEnabled(enabled);

        /// <see cref="IWithNativeJavaScriptDialogs"/>
        public void SetNativeJavaScriptDialogsEnabled(bool enabled) {

            _assertValidState();
            WebView_setNativeJavaScriptDialogsEnabled(_nativeWebViewPtr, enabled);
        }

        /// <see cref="IWithNative2DMode"/>
        public void SetNativeZoomEnabled(bool enabled) {

            WebViewLogger.LogWarning("3D WebView for macOS with WebKit doesn't support native pinch-to-zoom gestures, so the call to IWithNative2DMode.SetNativeZoomEnabled() will be ignored.");
        }

        /// <see cref="IWithPixelDensity"/>
        public void SetPixelDensity(float pixelDensity) {

            if (_native2DModeEnabled) {
                return;
            }
            if (!PixelDensityEnabled && pixelDensity != 1f) {
                WebViewLogger.LogWarning($"The app set a custom PixelDensity for the webview (PixelDensity = {pixelDensity}), but support for changing the PixelDensity is disabled by default for the macOS WebKit plugin because it negatively impacts performance on Intel CPUs. If you wish to override this and set a custom PixelDensity anyways, you can do so by setting `MacWebKitWebView.PixelDensityEnabled = true` via a script: https://developer.vuplex.com/webview/MacWebKitWebView#PixelDensityEnabled");
                return;
            }
            if (pixelDensity <= 0f || pixelDensity > 10) {
                throw new ArgumentException($"Invalid pixel density: {pixelDensity}. The pixel density must be between 0 and 10 (exclusive).");
            }
            PixelDensity = pixelDensity;
            if (IsInitialized) {
                _resize();
            }
        }

        /// <see cref="IWithPopups"/>
        public void SetPopupMode(PopupMode popupMode) {

            _assertValidState();
            WebView_setPopupMode(_nativeWebViewPtr, (int)popupMode);
        }

        /// <see cref="IWithNative2DMode"/>
        public void SetRect(Rect rect) {

            _assertValidState();
            _assertNative2DModeEnabled();
            #if UNITY_EDITOR
                rect = EditorGameViewHelper.AdjustRectForNative2DMode(rect);
            #endif
            _rect = rect;
            WebView_setRect(_nativeWebViewPtr, (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }

        public static void SetRemoteDebuggingEnabled(bool enabled) {

            WebView_setRemoteDebuggingEnabled(enabled);
            if (enabled) {
                WebViewLogger.Log("Remote debugging is enabled for macOS. For instructions, please see https://support.vuplex.com/articles/how-to-debug-web-content#webkit.");
            }
        }

        public static void SetStorageEnabled(bool enabled) => WebView_setStorageEnabled(enabled);

        /// <summary>
        /// Sets the target web frame rate. The default is `30`, which is also the maximum value.
        /// This method can be used to lower the target web frame rate in order to decrease energy and CPU usage.
        /// 3D WebView's rendering speed is limited by the speed of the underlying macOS APIs, so
        /// the actual web frame rate achieved is always lower than the default target of 30 FPS.
        /// This method is only used for the default render mode and is ignored when Native 2D Mode is enabled.
        /// </summary>
        /// <example>
        /// <code>
        /// await webViewPrefab.WaitUntilInitialized();
        /// #if (UNITY_STANDALONE_OSX &amp;&amp; !UNITY_EDITOR) || UNITY_EDITOR_OSX
        ///     var macWebView = webViewPrefab.WebView as MacWebKitWebView;
        ///     macWebView.SetTargetFrameRate(15);
        /// #endif
        /// </code>
        /// </example>
        public void SetTargetFrameRate(uint targetFrameRate) {

            if (Native2DModeEnabled) {
                VXUtils.LogNative2DModeWarning("SetTargetFrameRate");
                return;
            }
            if (targetFrameRate == 0 || targetFrameRate > 30) {
                throw new ArgumentException($"SetTargetFrameRate() called with invalid frame rate: {targetFrameRate}. The target frame rate must be between 1 and 30.");
            }
            WebView_setTargetFrameRate(_nativeWebViewPtr, targetFrameRate);
        }

        /// <see cref="IWithSettableUserAgent"/>
        public void SetUserAgent(bool mobile) {

            _assertValidState();
            WebView_setUserAgentToMobile(_nativeWebViewPtr, mobile);
        }

        /// <see cref="IWithSettableUserAgent"/>
        public void SetUserAgent(string userAgent) {

            _assertValidState();
            WebView_setUserAgent(_nativeWebViewPtr, userAgent);
        }

        /// <see cref="IWithNative2DMode"/>
        public void SetVisible(bool visible) {

            _assertValidState();
            _assertNative2DModeEnabled();
            _visible = visible;
            WebView_setVisible(_nativeWebViewPtr, visible);
        }

    #region Non-public members
        new const string _dllName = MacWebKitNativeWebViewPlugin.DllName;
        #if UNITY_EDITOR
            static EditorGameViewHelper _editorGameViewHelper;
        #endif
        string _gameObjectName;
        Dictionary<string, TaskCompletionSource<string>> _pendingCreatePdfTaskSources = new Dictionary<string, TaskCompletionSource<string>>();
        static Dictionary<string, Action<bool>> _pendingDeleteCookiesResultCallbacks = new Dictionary<string, Action<bool>>();
        static Dictionary<string, Action<Cookie[]>> _pendingGetCookiesResultCallbacks = new Dictionary<string, Action<Cookie[]>>();
        readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
        static Dictionary<string, GameObject> _webViewGameObjects = new Dictionary<string, GameObject>();

        void _assertNotExpiredTrial() {

            if (_nativeWebViewPtr == IntPtr.Zero) {
                throw new TrialExpiredException("Your trial of 3D WebView for macOS has expired. Please purchase a license to continue using it.");
            }
        }

        protected override Material _createMaterial() => new Material(Resources.Load<Material>("MacWebKitWebMaterial"));

        static void EditorGameViewHelper_GameViewRectChanged(object sender, Rect rect) {

            WebView_setEditorGameViewRect((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        }

        static void EditorGameViewHelper_GameViewVisibilityChanged(object sender, bool gameViewVisible) {

            WebView_setEditorGameViewVisible(gameViewVisible);
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string>))]
        static void _handleDeleteCookiesResult(string resultCallbackId) {

            var callback = _pendingDeleteCookiesResultCallbacks[resultCallbackId];
            _pendingDeleteCookiesResultCallbacks.Remove(resultCallbackId);
            callback(true);
        }

        // Invoked by the native plugin.
        void HandleDownloadProgressChanged(string serializedMessage) {

            DownloadProgressChanged?.Invoke(this, DownloadMessage.FromJson(serializedMessage).ToEventArgs());
        }

        [AOT.MonoPInvokeCallback(typeof(Action<string, string>))]
        static void _handleGetCookiesResult(string resultCallbackId, string serializedCookies) {

            var callback = _pendingGetCookiesResultCallbacks[resultCallbackId];
            _pendingGetCookiesResultCallbacks.Remove(resultCallbackId);
            var cookies = Cookie.ArrayFromJson(serializedCookies);
            callback(cookies);
        }

        // Invoked by the native plugin.
        void HandlePdfCreated(string message) {

            var components = message.Split(new char[] { ',' }, 3);
            var resultCallbackId = components[0];
            var succeeded = Boolean.Parse(components[1]);
            var filePathOrErrorMessage = components[2];
            var taskSource = _pendingCreatePdfTaskSources[resultCallbackId];
            _pendingCreatePdfTaskSources.Remove(resultCallbackId);
            if (succeeded) {
                taskSource.SetResult(filePathOrErrorMessage);
            } else {
                taskSource.SetException(new Exception("Error while creating the PDF: " + filePathOrErrorMessage));
            }
        }

        // Invoked by the native plugin.
        async void HandlePopup(string message) {

            var parameters = message.Split(new char[] { ',' });
            if (!(parameters.Length == 1 || parameters.Length == 2)) {
                WebViewLogger.LogError($"HandlePopup received an unexpected number of parameters ({parameters.Length}): {message}");
                return;
            }
            var url = parameters[0];
            MacWebKitWebView popupWebView = null;
            if (parameters.Length == 2) {
                var nativePopupWebViewPtr = VXUtils.ParseIntPtr(parameters[1]);
                popupWebView = Instantiate();
                if (Native2DModeEnabled) {
                    await popupWebView._initMac2D(Rect, nativePopupWebViewPtr);
                } else {
                    popupWebView.PixelDensity = PixelDensity;
                    await popupWebView._initMac3D(Size.x, Size.y, nativePopupWebViewPtr);
                }
            }
            PopupRequested?.Invoke(this, new PopupRequestedEventArgs(url, popupWebView));
        }

        // Execute as early as possible to help ensure it runs before user code.
        [RuntimeInitializeOnLoadMethod(
            #if UNITY_2019_2_OR_NEWER
                RuntimeInitializeLoadType.SubsystemRegistration
            #else
                RuntimeInitializeLoadType.BeforeSceneLoad
            #endif
        )]
        static void _initializePlugin() {

            WebView_initializePlugin(
                Marshal.GetFunctionPointerForDelegate<Action<string, string, string>>(_unitySendMessage),
                Application.isEditor,
                Marshal.GetFunctionPointerForDelegate<Action<string, string>>(_handleGetCookiesResult),
                Marshal.GetFunctionPointerForDelegate<Action<string>>(_handleDeleteCookiesResult)
            );
        }

        async Task _initMac2D(Rect rect, IntPtr popupNativeWebView) {

            _initMacCommon();
            await _initInNative2DModeBase(rect);
            _gameObjectName = gameObject.name;
            _webViewGameObjects[gameObject.name] = gameObject;
            _nativeWebViewPtr = WebView_newInNative2DMode(
                gameObject.name,
                (int)rect.x,
                (int)rect.y,
                (int)rect.width,
                (int)rect.height,
                popupNativeWebView
            );
            _assertNotExpiredTrial();
        }

        async Task _initMac3D(int width, int height, IntPtr popupNativeWebView) {

            _initMacCommon();
            await _initBase(width, height);
            _gameObjectName = gameObject.name;
            _webViewGameObjects[gameObject.name] = gameObject;
            _nativeWebViewPtr = WebView_new(
                gameObject.name,
                width,
                height,
                PixelDensity,
                popupNativeWebView
            );
            _assertNotExpiredTrial();
        }

        void _initMacCommon() {

            _nativePlugin = new MacWebKitNativeWebViewPlugin();
            #if UNITY_EDITOR
                if (_editorGameViewHelper == null) {
                    _editorGameViewHelper = new GameObject("Vuplex EditorGameViewHelper (used only in the editor for macOS WebKit)").AddComponent<EditorGameViewHelper>();
                    DontDestroyOnLoad(_editorGameViewHelper.gameObject);
                    _editorGameViewHelper.GameViewRectChanged += EditorGameViewHelper_GameViewRectChanged;
                    _editorGameViewHelper.GameViewVisibilityChanged += EditorGameViewHelper_GameViewVisibilityChanged;
                }
                // Manually trigger a sync of the GameView rect before creating a new webview.
                _editorGameViewHelper.CheckIfGameViewRectChanged();
            #endif
        }

        // Start the coroutine from OnEnable so that the coroutine
        // is restarted if the object is deactivated and then reactivated.
        void OnEnable() => StartCoroutine(_renderPluginOncePerFrame());

        void _pointerDown(Vector2 normalizedPoint, MouseButton mouseButton, int clickCount, bool preventStealingFocus) {

            _assertValidState();
            var pixelsPoint = _normalizedToPointAssertValid(normalizedPoint);
            WebView_pointerDown(_nativeWebViewPtr, pixelsPoint.x, pixelsPoint.y, (int)mouseButton, clickCount, preventStealingFocus);
        }

        void _pointerUp(Vector2 normalizedPoint, MouseButton mouseButton, int clickCount, bool preventStealingFocus) {

            _assertValidState();
            var pixelsPoint = _normalizedToPointAssertValid(normalizedPoint);
            WebView_pointerUp(_nativeWebViewPtr, pixelsPoint.x, pixelsPoint.y, (int)mouseButton, clickCount, preventStealingFocus);
        }

        IEnumerator _renderPluginOncePerFrame() {

            while (true) {
                yield return _waitForEndOfFrame;
                if (Native2DModeEnabled) {
                    break;
                }
                if (!_renderingEnabled || IsDisposed) {
                    continue;
                }
                int pointerId = WebView_depositPointer(_nativeWebViewPtr);
                GL.IssuePluginEvent(WebView_getRenderFunction(), pointerId);
            }
        }

        protected override void _resize() => WebView_resizeWithPixelDensity(_nativeWebViewPtr, Size.x, Size.y, PixelDensity);

        [AOT.MonoPInvokeCallback(typeof(Action<string, string, string>))]
        static void _unitySendMessage(string gameObjectName, string methodName, string message) {

            ThreadDispatcher.RunOnMainThread(() => {
                try {
                    // Don't look up the GameObject via GameObject.Find() because it negatively impacts performance,
                    // especially if the scene contains a large number of objects. For example, if a scene contains
                    // thousands of objects, calling GameObject.Find() can cause a significant frame rate drop.
                    // Instead, webview GameObjects are stored / looked up via this _webViewGameObjects dictionary.
                    if (_webViewGameObjects.TryGetValue(gameObjectName, out GameObject gameObj)) {
                        gameObj.SendMessage(methodName, message);
                    } else {
                        WebViewLogger.LogWarning($"Unable to deliver a message from the native plugin to a webview GameObject because there is no longer a GameObject named '{gameObjectName}'. This can sometimes happen directly after destroying a webview. In that case, it is benign and this message can be ignored.");
                    }
                } catch (Exception exception) {
                    // Catch exceptions triggered by invoking the method with SendMessage()
                    // because some applications terminate the application on uncaught exceptions.
                    Debug.LogException(exception);
                }
            });
        }

        [DllImport(_dllName)]
        static extern void WebView_bringToFront(IntPtr webViewPtr);

        [DllImport(_dllName)]
        static extern void WebView_clearAllData();

        [DllImport(_dllName)]
        static extern void WebView_createPdf(IntPtr webViewPtr, string resultCallbackId, string filePath);

        [DllImport(_dllName)]
        static extern void WebView_deleteCookies(string url, string cookieName, string resultCallbackId);

        [DllImport(_dllName)]
        static extern int WebView_depositPointer(IntPtr pointer);

        [DllImport(_dllName)]
        static extern void WebView_getCookies(string url, string cookieName, string resultCallbackId);

        [DllImport(_dllName)]
        static extern IntPtr WebView_getNativeWebView(IntPtr webViewPtr);

        [DllImport(_dllName)]
        static extern IntPtr WebView_getRenderFunction();

        [DllImport(_dllName)]
        static extern void WebView_globallySetUserAgentToMobile(bool mobile);

        [DllImport(_dllName)]
        static extern void WebView_globallySetUserAgent(string userAgent);

        [DllImport(_dllName)]
        static extern int WebView_initializePlugin(IntPtr unitySendMessageFunction, bool isEditor, IntPtr getCookiesCallback, IntPtr deleteCookiesCallback);

        [DllImport(_dllName)]
        static extern void WebView_keyDown(IntPtr webViewPtr, string key, int modifiers);

        [DllImport(_dllName)]
        static extern void WebView_keyUp(IntPtr webViewPtr, string key, int modifiers);

        [DllImport (_dllName)]
        static extern void WebView_movePointer(IntPtr webViewPtr, int x, int y, bool pointerLeave);

        [DllImport(_dllName)]
        static extern IntPtr WebView_new(
            string gameObjectName,
            int width,
            int height,
            float pixelDensity,
            IntPtr popupNativeWebView
        );

        [DllImport(_dllName)]
        static extern IntPtr WebView_newInNative2DMode(
            string gameObjectName,
            int x,
            int y,
            int width,
            int height,
            IntPtr popupNativeWebView
        );

        [DllImport (_dllName)]
        static extern void WebView_pointerDown(IntPtr webViewPtr, int x, int y, int mouseButton, int clickCount, bool preventStealingFocus);

        [DllImport (_dllName)]
        static extern void WebView_pointerUp(IntPtr webViewPtr, int x, int y, int mouseButton, int clickCount, bool preventStealingFocus);

        [DllImport (_dllName)]
        static extern void WebView_resizeWithPixelDensity(IntPtr webViewPtr, int width, int height, float pixelDensity);

        [DllImport(_dllName)]
        static extern void WebView_setAllowFileAccessFromFileUrls(IntPtr webViewPtr, bool allow);

        [DllImport(_dllName)]
        static extern void WebView_setAllowsBackForwardNavigationGestures(IntPtr webViewPtr, bool allow);

        [DllImport(_dllName)]
        static extern void WebView_setAuthChallengeReceivedEnabled(IntPtr webViewPtr, bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setAutoplayEnabled(bool ignore);

        [DllImport(_dllName)]
        static extern void WebView_setCameraAndMicrophoneEnabled(bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setCameraEnabled(bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setCookie(string serializedCookie);

        [DllImport(_dllName)]
        static extern void WebView_setDownloadsEnabled(IntPtr webViewPtr, bool enabled);

        [DllImport (_dllName)]
        static extern void WebView_setEditorGameViewRect(int x, int y, int width, int height);

        [DllImport (_dllName)]
        static extern void WebView_setEditorGameViewVisible(bool visible);

        [DllImport(_dllName)]
        static extern void WebView_setFullscreenEnabled(IntPtr webViewPtr, bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setIgnoreCertificateErrors(bool ignore);

        [DllImport(_dllName)]
        static extern void WebView_setMicrophoneEnabled(bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setNativeJavaScriptDialogsEnabled(IntPtr webViewPtr, bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setNativeOnScreenKeyboardEnabled(IntPtr webViewPtr, bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setPopupMode(IntPtr webViewPtr, int popupMode);

        [DllImport (_dllName)]
        static extern void WebView_setRect(IntPtr webViewPtr, int x, int y, int width, int height);

        [DllImport (_dllName)]
        static extern void WebView_setRemoteDebuggingEnabled(bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setStorageEnabled(bool enabled);

        [DllImport(_dllName)]
        static extern void WebView_setTargetFrameRate(IntPtr webViewPtr, uint targetFrameRate);

        [DllImport(_dllName)]
        static extern void WebView_setUserAgentToMobile(IntPtr webViewPtr, bool mobile);

        [DllImport(_dllName)]
        static extern void WebView_setUserAgent(IntPtr webViewPtr, string userAgent);

        [DllImport (_dllName)]
        static extern void WebView_setVisible(IntPtr webViewPtr, bool visible);
    #endregion
    }
}
#else
namespace Vuplex.WebView {
    [System.Obsolete("The MacWebKitWebView class is only available on macOS. So, when building for other platforms (e.g. Windows, Android, iOS, WebGL), it's necessary to use the directive `#if (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || UNITY_EDITOR_OSX` like described here: https://support.vuplex.com/articles/how-to-call-platform-specific-apis . Note: MacWebKitWebView isn't actually obsolete. This compiler error just reports it's obsolete because 3D WebView generated the error with System.ObsoleteAttribute.", true)]
    public class MacWebKitWebView {}
}
#endif
