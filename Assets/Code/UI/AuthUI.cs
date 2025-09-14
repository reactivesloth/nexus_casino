using System;
using System.IO;
using CC;
using Code.API;
using Code.API.Models;
using Code.Utility;
using Proyecto26;
using Ricimi;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    public class AuthUI : MonoBehaviour
    {
        [Header("UI Elements")] public TMP_InputField nicknameInput;
        public TMP_InputField phoneInput;
        public TMP_InputField codeInput;
        public Button getConfirmCodeButton;
        public Button authButton;
        public Button resendCodeButton;
        public Button exit;
        public Button logoutButton;

        public Button startGameButton;
        public GameObject loginPopup;

        [Header("Texts")] public TMP_Text authButtonText;
        public TMP_Text titleText;

        [Header("Resend Settings")] public int resendCooldownSeconds = 60;
        public string resendButtonText = "Resend";
        public string resendButtonTextWithTimer = "Resend ({0})";

        [Header("Results Handle")] public ModularPopupOpener popupPanel;

        private bool _isResendTimerActive;
        private float _resendTimer;
        private bool _isAuthorized;
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
            if (exit != null) exit.onClick.AddListener(OnExitClicked);
            if (startGameButton != null) startGameButton.onClick.AddListener(OnUserCanStartGame);
            if (logoutButton != null) logoutButton.onClick.AddListener(OnLogoutClicked);
        }

        private void OnDisable()
        {
            if (authButton != null) authButton.onClick.RemoveListener(OnAuthClicked);
            if (getConfirmCodeButton != null) getConfirmCodeButton.onClick.RemoveListener(OnGetCodeClicked);
            if (resendCodeButton != null) resendCodeButton.onClick.RemoveListener(OnResendCodeClicked);
            if (exit != null) exit.onClick.RemoveListener(OnExitClicked);
            if (startGameButton != null) startGameButton.onClick.RemoveListener(OnUserCanStartGame);
            if (logoutButton != null) logoutButton.onClick.RemoveListener(OnLogoutClicked);
        }

        private void Start()
        {
            InitializeResendButton();

            if (CursorManager.Instance != null)
                CursorManager.Instance.ShowCursor();

            // Проверка сохраненного токена
            string savedToken = PlayerPrefs.GetString("auth_accessToken", string.Empty);
            if (!string.IsNullOrEmpty(savedToken))
            {
                ClientDataStorage.AccessToken = savedToken;
                ClientDataStorage.RefreshToken = PlayerPrefs.GetString("auth_refreshToken", string.Empty);
                ValidateSavedToken();
            }
            else
            {
                ToStartState();
            }
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
                titleText.text =
                    _isAuthorized ? $"Welcome back, {ClientDataStorage.UserData.username}" : "Welcome to the Nexus Meta Club";


            if (authButtonText != null)
                authButtonText.text = _isRegistered ? "Login" : "Sign up";


            if (startGameButton != null)
                startGameButton.gameObject.SetActive(_isAuthorized);
            if (logoutButton != null)
                logoutButton.gameObject.SetActive(_isAuthorized);
            if (loginPopup != null)
                loginPopup.SetActive(!_isAuthorized);
        }

        private void ValidateSavedToken()
        {
            var userDataRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetMeUrl(),
                Headers = ClientDataStorage.GetJwtHeader()
            };


            RestClient.Get(userDataRequest).Then(userDataResponse =>
            {
                if (userDataResponse.StatusCode == 200
                    && JsonUtility.FromJson<SuccessResponse<MeSchema>>(userDataResponse.Text).success)
                {
                    var successResponse = JsonUtility.FromJson<SuccessResponse<MeSchema>>(userDataResponse.Text);
                    if (successResponse != null && successResponse.success)
                    {
                        ClientDataStorage.UserData = successResponse.data;
                        _isAuthorized = true;
                        ToStartState();
                        return;
                    }
                }

                PlayerPrefs.DeleteKey("auth_accessToken");
                PlayerPrefs.DeleteKey("auth_refreshToken");
                _isAuthorized = false;
                ToStartState();
            });
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
                    HandleError(checkResult != null ? checkResult.code : "Error",
                        checkResult != null ? checkResult.detail : "Invalid response");
                    if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                    return;
                }

                _isRegistered = checkResult.data;

                if (phoneInput.text.Length > 0 && phoneInput.text[0] == '0')
                {
                    OnGetCodeSuccess();
                    return;
                }

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
                        HandleError(sendCodeResult != null ? sendCodeResult.code : "Error",
                            sendCodeResult != null ? sendCodeResult.detail : "Invalid response");
                        if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                        return;
                    }

                    OnGetCodeSuccess();
                    StartResendTimer();
                }).Finally(() =>
                {
                    if (getConfirmCodeButton != null) getConfirmCodeButton.interactable = true;
                });
            });
        }

        private void OnGetCodeSuccess()
        {
            if (phoneInput != null) phoneInput.interactable = false;
            if (codeInput != null) codeInput.gameObject.SetActive(true);
            if (getConfirmCodeButton != null) getConfirmCodeButton.gameObject.SetActive(false);
            if (authButton != null) authButton.interactable = true;
            if (nicknameInput != null) nicknameInput.gameObject.SetActive(!_isRegistered);
            if (titleText != null)
                titleText.text =
                    _isAuthorized ? $"Welcome back, {ClientDataStorage.UserData.username}" : "Welcome to the Nexus Meta Club";
            if (authButtonText != null) authButtonText.text = _isRegistered ? "Login" : "Sign up";
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
                    HandleError(responseData != null ? responseData.code : "Error",
                        responseData != null ? responseData.detail : "Invalid response");
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
                    HandleError(responseData != null ? responseData.code : "Error",
                        responseData != null ? responseData.detail : "Invalid response");
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
            if (authResponse == null)
            {
                HandleError("Error", "Empty auth response");
                return;
            }

            ClientDataStorage.AccessToken = authResponse.access_jwt;
            ClientDataStorage.RefreshToken = authResponse.refresh_jwt;

            // Сохраняем токены
            PlayerPrefs.SetString("auth_accessToken", authResponse.access_jwt);
            PlayerPrefs.SetString("auth_refreshToken", authResponse.refresh_jwt);

            var userDataRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetMeUrl(),
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Get(userDataRequest).Then(userDataResponse =>
            {
                if (userDataResponse.StatusCode != 200)
                {
                    HandleError("Error", "Get user failed");
                    return;
                }

                var successResponse = JsonUtility.FromJson<SuccessResponse<MeSchema>>(userDataResponse.Text);
                if (successResponse != null && successResponse.success)
                {
                    ClientDataStorage.UserData = successResponse.data;
                    _isAuthorized = true;
                    ToStartState();
                }
                else
                {
                    HandleError(successResponse != null ? successResponse.code : "Error",
                        successResponse != null ? successResponse.detail : "Invalid response");
                    ToStartState();
                }
            });
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnUserCanStartGame()
        {
            var savePath = CharacterCustomization.SavePath;
            
            var meData = ClientDataStorage.UserData;

            var getAvatarRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetFileUrl($"Avatar_{meData.id}.json"),
                Headers = ClientDataStorage.GetJwtHeader()
            };
            
            var isAvatarLoaded = false;
            var isModelTypeLoaded = false;
            RestClient.Get(getAvatarRequest).Then(getAvatarResponse =>
            {
                var getModelTypeRequest = new RequestHelper
                {
                    Uri = ApiRoutes.GetFileUrl($"PlayerModelType_{ClientDataStorage.UserData.id}.txt"),
                    Headers = ClientDataStorage.GetJwtHeader(), Timeout = 5
                };
                
                if (getAvatarResponse.StatusCode != 200)
                    return RestClient.Get(getModelTypeRequest);

                isAvatarLoaded = true;
                try
                {
                    File.WriteAllText(savePath, getAvatarResponse.Text);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Ошибка при записи аватара в файл: {ex}");
                    isAvatarLoaded = false;
                }
                
                return RestClient.Get(getModelTypeRequest);
            }).Then(getModelTypeResponse =>
            {
                var getSpawnRequest = new RequestHelper
                {
                    Uri = ApiRoutes.GetFileUrl($"SpawnPoint_{ClientDataStorage.UserData.id}.txt"),
                    Headers = ClientDataStorage.GetJwtHeader(), Timeout = 5
                };
                
                if (getModelTypeResponse.StatusCode != 200)
                    return RestClient.Get(getSpawnRequest);

                isModelTypeLoaded = true;
                PlayerPrefs.SetString("PlayerModelType", getModelTypeResponse.Text);
                
                return RestClient.Get(getSpawnRequest);
            }).Then(getSpawnResponse =>
            {
                if (getSpawnResponse.StatusCode != 200)
                    return;

                var responseParts = getSpawnResponse.Text.Split(' ');
                var posX = float.Parse(responseParts[0]);
                var posY = float.Parse(responseParts[1]);
                var posZ = float.Parse(responseParts[2]);
                var rotX = float.Parse(responseParts[3]);
                var rotY = float.Parse(responseParts[4]);
                var rotZ = float.Parse(responseParts[5]);

                var p = new Vector3(posX, posY, posZ);
                var r = Quaternion.Euler(rotX, rotY, rotZ);
                
                PlayerPrefs.SetFloat("SavedSpawnPositionX", p.x);
                PlayerPrefs.SetFloat("SavedSpawnPositionY", p.y);
                PlayerPrefs.SetFloat("SavedSpawnPositionZ", p.z);
                PlayerPrefs.SetFloat("SavedSpawnRotationX", r.x);
                PlayerPrefs.SetFloat("SavedSpawnRotationY", r.y);
                PlayerPrefs.SetFloat("SavedSpawnRotationZ", r.z);
                PlayerPrefs.SetInt("SavedSpawnPosition", 1);
                PlayerPrefs.Save();
                
            }).Finally(() =>
            {
                var isLoadGame = isAvatarLoaded && isModelTypeLoaded;
                if (LoadingScreenUI.Instance != null)
                    LoadingScreenUI.Instance.LoadScene(isLoadGame ? "Main" : "Character Customization", "Please wait...",
                        "Loading...");
                else
                    SceneManager.LoadSceneAsync(isLoadGame ? "Main" : "Character Customization"); 
            });
        }

        private void HandleError(string title, string errorMessage)
        {
            if (popupPanel != null)
            {
                popupPanel.Title = title ?? "Error";
                popupPanel.Message = errorMessage ?? "Unknown error";
                popupPanel.Buttons.Clear();
                popupPanel.OpenPopup();
            }
            else
                Debug.LogWarning($"{title}: {errorMessage}");
        }

        private void OnLogoutClicked()
        {
            PlayerPrefs.DeleteKey("auth_accessToken");
            PlayerPrefs.DeleteKey("auth_refreshToken");
            ClientDataStorage.AccessToken = string.Empty;
            ClientDataStorage.RefreshToken = string.Empty;
            ClientDataStorage.UserData = default;
            _isAuthorized = false;
            ToStartState();
        }
    }
}