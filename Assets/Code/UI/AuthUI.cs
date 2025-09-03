using System.IO;
using Code.API;
using Code.API.Models;
using Proyecto26;
using Ricimi;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    public class AuthUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_InputField nicknameInput;
        public TMP_InputField phoneInput;
        public TMP_InputField codeInput;
        public Button getConfirmCodeButton;
        public Button authButton;
        public Button resendCodeButton;
        public GameObject termsAndConditions;

        [Header("Texts")]
        public TMP_Text authButtonText;
        public TMP_Text titleText;

        [Header("Resend Settings")]
        public int resendCooldownSeconds = 60;
        public string resendButtonText = "Resend";
        public string resendButtonTextWithTimer = "Resend ({0})";

        [Header("Results Handle")]
        public ModularPopupOpener popupPanel;

        private bool _isResendTimerActive;
        private float _resendTimer;
        private bool _isRegistered;
        private TMP_Text _resendText;

        private void Awake()
        {
            if (resendCodeButton != null)
                _resendText = resendCodeButton.GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            if (authButton != null) authButton.onClick.AddListener(OnAuthClicked);
            if (getConfirmCodeButton != null) getConfirmCodeButton.onClick.AddListener(OnGetCodeClicked);
            if (resendCodeButton != null) resendCodeButton.onClick.AddListener(OnResendCodeClicked);
        }

        private void OnDisable()
        {
            if (authButton != null) authButton.onClick.RemoveListener(OnAuthClicked);
            if (getConfirmCodeButton != null) getConfirmCodeButton.onClick.RemoveListener(OnGetCodeClicked);
            if (resendCodeButton != null) resendCodeButton.onClick.RemoveListener(OnResendCodeClicked);
        }

        private void Start()
        {
            ToStartState();
            InitializeResendButton();

            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();
        }

        private void Update()
        {
            UpdateResendTimer();
        }

        private void ToStartState()
        {
            if (phoneInput != null)
            {
                phoneInput.interactable = true;
                phoneInput.text = PlayerPrefs.GetString("auth_phoneInput", string.Empty);
            }

            if (codeInput != null) codeInput.text = string.Empty;
            if (nicknameInput != null)
                nicknameInput.text = PlayerPrefs.GetString("auth_nicknameInput", string.Empty);

            if (getConfirmCodeButton != null) getConfirmCodeButton.gameObject.SetActive(true);
            if (authButton != null) authButton.interactable = false;
            if (nicknameInput != null) nicknameInput.gameObject.SetActive(false);
            if (resendCodeButton != null) resendCodeButton.gameObject.SetActive(false);
            _isResendTimerActive = false;

            if (titleText != null)
                titleText.text = PlayerPrefs.HasKey("auth_phoneInput") ? "Welcome back" : "Welcome";

            if (authButtonText != null) authButtonText.text = PlayerPrefs.HasKey("auth_phoneInput") ? "Login" : "Sign up";

            if (termsAndConditions != null)
                termsAndConditions.SetActive(false);
        }

        private void OnGetCodeClicked()
        {
            if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = false;

            var phone = phoneInput != null ? phoneInput.text : string.Empty;
            if (string.IsNullOrEmpty(phone))
            {
                HandleError("Error", "Phone is empty");
                if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                return;
            }

            var checkPhoneRequest = new CheckPhoneRequest { phone = phone };
            RestClient.Post(ApiRoutes.GetCheckNumberUrl(), checkPhoneRequest).Then(checkResponse =>
            {
                if (checkResponse.StatusCode != 200)
                {
                    HandleError(checkResponse.StatusCode.ToString(), checkResponse.Error);
                    if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                    return;
                }

                var checkResult = JsonUtility.FromJson<SuccessResponse<bool>>(checkResponse.Text);
                if (checkResult == null || !checkResult.success)
                {
                    HandleError(checkResult != null ? checkResult.code : "Error", checkResult != null ? checkResult.detail : "Invalid response");
                    if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                    return;
                }

                _isRegistered = checkResult.data;

                var sendCodeRequest = new SendCodeRequest { phone = phone, requested_by = "" };
                RestClient.Post(ApiRoutes.GetSendCodeUrl(), sendCodeRequest).Then(sendCodeResponse =>
                {
                    if (sendCodeResponse.StatusCode != 200)
                    {
                        HandleError(sendCodeResponse.StatusCode.ToString(), sendCodeResponse.Error);
                        if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                        return;
                    }

                    var sendCodeResult = JsonUtility.FromJson<SuccessResponse<object>>(sendCodeResponse.Text);
                    if (sendCodeResult == null || !sendCodeResult.success)
                    {
                        HandleError(sendCodeResult != null ? sendCodeResult.code : "Error", sendCodeResult != null ? sendCodeResult.detail : "Invalid response");
                        if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                        return;
                    }

                    if (phoneInput != null) phoneInput.interactable = false;
                    if (codeInput != null) codeInput.gameObject.SetActive(true);
                    if (getConfirmCodeButton != null) getConfirmCodeButton.gameObject.SetActive(false);
                    if (authButton != null) authButton.interactable = true;
                    if (nicknameInput != null) nicknameInput.gameObject.SetActive(!_isRegistered);
                    if (titleText != null) titleText.text = _isRegistered ? "Login" : "Sign up";
                    if (authButtonText != null) authButtonText.text = _isRegistered ? "Login" : "Sign up";
                    if (termsAndConditions != null) termsAndConditions.SetActive(!_isRegistered);

                    StartResendTimer();
                }).Finally(() =>
                {
                    if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                });
            });
        }

        private void OnAuthClicked()
        {
            if (authButton != null) authButton.interactable = false;
            if (_isRegistered) PerformLogin();
            else PerformRegister();
        }

        private void PerformLogin()
        {
            var loginRequest = new LoginRequest
            {
                phone = phoneInput != null ? phoneInput.text : string.Empty,
                confirmation_code = codeInput != null ? codeInput.text : string.Empty
            };

            RestClient.Post(ApiRoutes.GetLoginUrl(), loginRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    HandleError(response.StatusCode.ToString(), response.Error);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<AuthResponse>>(response.Text);
                if (responseData != null && responseData.success)
                    OnAuthSuccess(responseData.data);
                else
                {
                    HandleError(responseData != null ? responseData.code : "Error", responseData != null ? responseData.detail : "Invalid response");
                    ToStartState();
                }
            }).Finally(() =>
            {
                if (authButton != null) authButton.interactable = true;
                if (phoneInput != null) PlayerPrefs.SetString("auth_phoneInput", phoneInput.text);
            });
        }

        private void PerformRegister()
        {
            var signUpRequest = new SignUpRequest
            {
                username = nicknameInput != null ? nicknameInput.text : string.Empty,
                phone = phoneInput != null ? phoneInput.text : string.Empty,
                confirmation_code = codeInput != null ? codeInput.text : string.Empty
            };

            RestClient.Post(ApiRoutes.GetSignUpUrl(), signUpRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    HandleError(response.StatusCode.ToString(), response.Error);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<AuthResponse>>(response.Text);
                if (responseData != null && responseData.success)
                    OnAuthSuccess(responseData.data);
                else
                {
                    HandleError(responseData != null ? responseData.code : "Error", responseData != null ? responseData.detail : "Invalid response");
                    ToStartState();
                }
            }).Finally(() =>
            {
                if (authButton != null) authButton.interactable = true;
                if (nicknameInput != null) PlayerPrefs.SetString("auth_nicknameInput", nicknameInput.text);
                if (phoneInput != null) PlayerPrefs.SetString("auth_phoneInput", phoneInput.text);
            });
        }

        private void InitializeResendButton()
        {
            if (resendCodeButton == null) return;
            resendCodeButton.gameObject.SetActive(false);
            resendCodeButton.interactable = false;
            if (_resendText != null) _resendText.text = resendButtonText;
        }

        private void StartResendTimer()
        {
            if (resendCodeButton == null) return;

            _resendTimer = resendCooldownSeconds;
            _isResendTimerActive = true;

            resendCodeButton.gameObject.SetActive(true);
            resendCodeButton.interactable = false;
            UpdateResendButtonText();
        }

        private void UpdateResendTimer()
        {
            if (!_isResendTimerActive) return;

            _resendTimer -= Time.deltaTime;
            if (_resendTimer <= 0f)
            {
                _isResendTimerActive = false;
                if (resendCodeButton != null) resendCodeButton.interactable = true;
            }
            UpdateResendButtonText();
        }

        private void UpdateResendButtonText()
        {
            if (_resendText == null) return;

            if (_isResendTimerActive && _resendTimer > 0f)
            {
                int remainingSeconds = Mathf.CeilToInt(_resendTimer);
                _resendText.text = string.Format(resendButtonTextWithTimer, remainingSeconds);
            }
            else
            {
                _resendText.text = resendButtonText;
            }
        }

        private void OnResendCodeClicked()
        {
            if (resendCodeButton == null || !resendCodeButton.interactable) return;
            resendCodeButton.interactable = false;
            OnGetCodeClicked();
        }

        private void OnAuthSuccess(AuthResponse authResponse)
        {
            if (authResponse == null) { HandleError("Error", "Empty auth response"); return; }

            ClientDataStorage.AccessToken = authResponse.access_jwt;
            ClientDataStorage.RefreshToken = authResponse.refresh_jwt;

            var userDataRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetMeUrl(),
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Get(userDataRequest).Then(userDataResponse =>
            {
                if (userDataResponse.StatusCode != 200) { HandleError("Error", "Get user failed"); return; }

                var successResponse = JsonUtility.FromJson<SuccessResponse<MeSchema>>(userDataResponse.Text);
                if (successResponse != null && successResponse.success)
                {
                    ClientDataStorage.UserData = successResponse.data;
                    OnUserCanStartGame();
                }
                else
                {
                    HandleError(successResponse != null ? successResponse.code : "Error", successResponse != null ? successResponse.detail : "Invalid response");
                    ToStartState();
                }
            });
        }

        private void OnUserCanStartGame()
        {
            string savePath = Application.persistentDataPath + "/CharacterCustomizer.json";
#if UNITY_EDITOR
            savePath = Application.dataPath + "/CharacterCustomizer.json";
#endif
            bool hasCC = false;
            if (File.Exists(savePath))
            {
                string jsonLoad = File.ReadAllText(savePath);
                hasCC = !string.IsNullOrEmpty(jsonLoad) && jsonLoad.Length > 200;
            }

            SceneManager.LoadSceneAsync(hasCC ? "Main" : "Character Customization");
        }

        private void HandleError(string title, string errorMessage)
        {
            if (popupPanel != null)
            {
                popupPanel.Title = title ?? "Error";
                popupPanel.Message = errorMessage ?? "Unknown error";
                popupPanel.OpenPopup();
            }
            else
                Debug.LogWarning($"{title}: {errorMessage}");
        }
    }
}
