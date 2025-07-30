using System.Collections.Generic;
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
        [Header("UI Elements")] public TMP_InputField nicknameInput;
        public TMP_InputField phoneInput;
        public TMP_InputField codeInput;
        public Button getConfirmCodeButton;
        public Button authButton;
        public Button resendCodeButton;
        public GameObject termsAndConditions;
        
        [Header("Texts")] 
        public TMP_Text authButtonText;
        public TMP_Text titleText;

        [Header("Resend Settings")] public int resendCooldownSeconds = 60;
        public string resendButtonText = "Resend";
        public string resendButtonTextWithTimer = "Resend ({0})";
        
        [Header("Results Handle")]
        public ModularPopupOpener popupPanel;

        private bool isResendTimerActive = false;
        private float resendTimer = 0f;
        private bool isRegistered = false; // Новый флаг: зарегистрирован ли номер

        private void OnEnable()
        {
            authButton.onClick.AddListener(OnAuthClicked);
            getConfirmCodeButton.onClick.AddListener(OnGetCodeClicked);
            resendCodeButton.onClick.AddListener(OnResendCodeClicked);
        }

        private void OnDisable()
        {
            authButton.onClick.RemoveListener(OnAuthClicked);
            getConfirmCodeButton.onClick.RemoveListener(OnGetCodeClicked);
            resendCodeButton.onClick.RemoveListener(OnResendCodeClicked);
        }

        private void Start()
        {
            ToStartState();
            InitializeResendButton();
        }

        private void Update()
        {
            UpdateResendTimer();
        }

        private void ToStartState()
        {
            phoneInput.interactable = true;
            phoneInput.text = PlayerPrefs.GetString("auth_phoneInput",  string.Empty);
            codeInput.text = string.Empty;
            if (nicknameInput != null)
                nicknameInput.text = PlayerPrefs.GetString("auth_nicknameInput",  string.Empty);
            
            getConfirmCodeButton.gameObject.SetActive(true);
            authButton.gameObject.SetActive(false);
            //codeInput.gameObject.SetActive(false);
            nicknameInput.gameObject.SetActive(false);
            resendCodeButton.gameObject.SetActive(false);
            isResendTimerActive = false;
            titleText.text = PlayerPrefs.HasKey("auth_phoneInput") ? "Welcome back" : "Welcome";
            authButtonText.text = isRegistered ? "Login" : "Sign up";
            termsAndConditions.SetActive(false);
        }

        private void OnGetCodeClicked()
        {
            getConfirmCodeButton.interactable = false;
            var phone = phoneInput.text;
            var checkPhoneRequest = new CheckPhoneRequest()
            {
                phone = phone
            };
            RestClient.Post(ApiRoutes.GetCheckNumberUrl(), checkPhoneRequest).Then(checkResponse =>
            {
                if (checkResponse.StatusCode != 200)
                {
                    HandleError(checkResponse.StatusCode.ToString(), checkResponse.Error);
                    getConfirmCodeButton.interactable = true;
                    return;
                }
                var checkResult = JsonUtility.FromJson<SuccessResponse<bool>>(checkResponse.Text);
                if (!checkResult.success)
                {
                    HandleError(checkResult.code, checkResult.detail);
                    getConfirmCodeButton.interactable = true;
                    return;
                }
                isRegistered = checkResult.data;
                // Теперь отправляем запрос на отправку кода
                var sendCodeRequest = new SendCodeRequest
                {
                    phone = phone,
                    requested_by = ""
                };
                RestClient.Post(ApiRoutes.GetSendCodeUrl(), sendCodeRequest).Then(sendCodeResponse =>
                {
                    if (sendCodeResponse.StatusCode != 200)
                    {
                        HandleError(sendCodeResponse.StatusCode.ToString(), sendCodeResponse.Error);
                        getConfirmCodeButton.interactable = true;
                        return;
                    }
                    var sendCodeResult = JsonUtility.FromJson<SuccessResponse<object>>(sendCodeResponse.Text);
                    if (!sendCodeResult.success)
                    {
                        HandleError(sendCodeResult.code, sendCodeResult.detail);
                        getConfirmCodeButton.interactable = true;
                        return;
                    }
                    // Только после успешной отправки кода показываем поля
                    phoneInput.interactable = false;
                    codeInput.gameObject.SetActive(true);
                    getConfirmCodeButton.gameObject.SetActive(false);
                    authButton.gameObject.SetActive(true);
                    nicknameInput.gameObject.SetActive(!isRegistered); // Показываем nickname только если не зарегистрирован
                    titleText.text = isRegistered ? "Login" : "Sign up";
                    authButtonText.text = isRegistered ? "Login" : "Sign up";
                    termsAndConditions.SetActive(!isRegistered);
                    StartResendTimer();
                }).Finally(() => getConfirmCodeButton.interactable = true);
            });
        }

        private void OnAuthClicked()
        {
            authButton.interactable = false;
            if (isRegistered)
                PerformLogin();
            else
                PerformRegister();
        }

        private void PerformLogin()
        {
            var loginRequest = new LoginRequest
            {
                phone = phoneInput.text,
                confirmation_code = codeInput.text
            };
            RestClient.Post(ApiRoutes.GetLoginUrl(), loginRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    HandleError(response.StatusCode.ToString(), response.Error);
                    return;
                }
                var responseData = JsonUtility.FromJson<SuccessResponse<AuthResponse>>(response.Text);
                if (responseData.success)
                {
                    OnAuthSuccess(responseData.data);
                }
                else
                {
                    Debug.LogWarning(responseData.detail);
                    HandleError(responseData.code, responseData.detail);
                    ToStartState();
                }
            }).Finally(() =>
            {
                authButton.interactable = true;
                PlayerPrefs.SetString("auth_phoneInput",  phoneInput.text);
            });
        }

        private void PerformRegister()
        {
            var signUpRequest = new SignUpRequest
            {
                username = nicknameInput.text,
                phone = phoneInput.text,
                confirmation_code = codeInput.text
            };
            RestClient.Post(ApiRoutes.GetSignUpUrl(), signUpRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    HandleError(response.StatusCode.ToString(), response.Error);
                    return;
                }
                var responseData = JsonUtility.FromJson<SuccessResponse<AuthResponse>>(response.Text);
                if (responseData.success)
                {
                    OnAuthSuccess(responseData.data);
                }
                else
                {
                    Debug.LogWarning(responseData.detail);
                    HandleError(responseData.code, responseData.detail);
                    ToStartState();
                }
            }).Finally(() =>
            {
                authButton.interactable = true;
                PlayerPrefs.SetString("auth_nicknameInput",  nicknameInput.text);
                PlayerPrefs.SetString("auth_phoneInput",  phoneInput.text);
            });
        }

        private void InitializeResendButton()
        {
            resendCodeButton.gameObject.SetActive(false);
            resendCodeButton.interactable = false;
        }

        private void StartResendTimer()
        {
            resendTimer = resendCooldownSeconds;
            isResendTimerActive = true;
            resendCodeButton.gameObject.SetActive(true);
            resendCodeButton.interactable = false;
            UpdateResendButtonText();
        }

        private void UpdateResendTimer()
        {
            if (!isResendTimerActive) return;
            resendTimer -= Time.deltaTime;
            if (resendTimer <= 0)
            {
                resendCodeButton.interactable = true;
                isResendTimerActive = false;
                UpdateResendButtonText();
            }
            else
            {
                UpdateResendButtonText();
            }
        }

        private void UpdateResendButtonText()
        {
            if (isResendTimerActive && resendTimer > 0)
            {
                int remainingSeconds = Mathf.CeilToInt(resendTimer);
                resendCodeButton.GetComponentInChildren<TMP_Text>().text =
                    string.Format(resendButtonTextWithTimer, remainingSeconds);
            }
            else
            {
                resendCodeButton.GetComponentInChildren<TMP_Text>().text = resendButtonText;
            }
        }

        private void OnResendCodeClicked()
        {
            if (!resendCodeButton.interactable) return;
            resendCodeButton.interactable = false;
            OnGetCodeClicked();
        }

        private void OnAuthSuccess(AuthResponse authResponse)
        {
            ClientDataStorage.AccessToken = authResponse.access_jwt;
            ClientDataStorage.RefreshToken = authResponse.refresh_jwt;
            Debug.Log(JsonUtility.ToJson(authResponse));
            var userDataRequest = new RequestHelper { 
                Uri = ApiRoutes.GetMeUrl(),
                Headers = ClientDataStorage.GetJwtHeader()
            };
            RestClient.Get(userDataRequest).Then(userDataResponse =>
            {
                if (userDataResponse.StatusCode != 200)
                    return;
                var successResponse = JsonUtility.FromJson<SuccessResponse<MeSchema>>(userDataResponse.Text);
                if (successResponse.success)
                {
                    ClientDataStorage.UserData = successResponse.data;
                    OnUserCanStartGame();
                }
                else
                {
                    Debug.LogWarning(successResponse.detail);
                    HandleError(successResponse.code, successResponse.detail);
                    ToStartState();
                }
            });
        }

        private void OnUserCanStartGame()
        {
            var savePath = "";
            savePath = Application.persistentDataPath + "/CharacterCustomizer.json";
#if UNITY_EDITOR
            savePath = Application.dataPath + "/CharacterCustomizer.json";
#endif
            if (File.Exists(savePath)) {
                string jsonLoad = File.ReadAllText(savePath);
                if (jsonLoad.Length > 200)
                {
                    SceneManager.LoadSceneAsync("Main");
                    return;
                }
            } 
            SceneManager.LoadSceneAsync("Character Customization");
        }

        private void HandleError(string title, string errorMessage)
        {
            popupPanel.Title = title;
            popupPanel.Message = errorMessage;
            popupPanel.OpenPopup();
        }
    }
}