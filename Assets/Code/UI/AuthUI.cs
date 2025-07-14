using System;
using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network;
using Code.Network.Lobby;
using Proyecto26;
using Proyecto26.Helper;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.UI
{
    public enum AuthMode
    {
        Login,
        Register
    }

    public class AuthUI : MonoBehaviour
    {
        [Header("UI Elements")] public TMP_InputField nicknameInput;
        public TMP_InputField phoneInput;
        public TMP_InputField codeInput;
        public Button getConfirmCodeButton;
        public Button authButton;
        public Button resendCodeButton;

        [Header("Mode Switching")] public Button switchModeButton;
        public TMP_Text switchModeText;
        public TMP_Text authButtonText;
        public TMP_Text titleText;

        [Header("Mode Settings")] public string loginTitle = "Вход";
        public string registerTitle = "Регистрация";
        public string loginButtonText = "Войти";
        public string registerButtonText = "Зарегистрироваться";
        public string switchToLoginText = "Уже есть аккаунт? Войти";
        public string switchToRegisterText = "Нет аккаунта? Зарегистрироваться";

        [Header("Resend Settings")] public int resendCooldownSeconds = 60;
        public string resendButtonText = "Отправить код повторно";
        public string resendButtonTextWithTimer = "Отправить код повторно ({0})";
        
        [Header("Results Handle")]
        public AuthPopupPanel popupPanel;

        private AuthMode currentMode = AuthMode.Login;
        private float resendTimer = 0f;
        private bool isResendTimerActive = false;

        private void OnEnable()
        {
            authButton.onClick.AddListener(OnAuthClicked);
            getConfirmCodeButton.onClick.AddListener(OnGetCodeClicked);
            switchModeButton.onClick.AddListener(OnSwitchModeClicked);
            resendCodeButton.onClick.AddListener(OnResendCodeClicked);
        }

        private void OnDisable()
        {
            authButton.onClick.RemoveListener(OnAuthClicked);
            getConfirmCodeButton.onClick.RemoveListener(OnGetCodeClicked);
            switchModeButton.onClick.RemoveListener(OnSwitchModeClicked);
            resendCodeButton.onClick.RemoveListener(OnResendCodeClicked);
        }

        private void Start()
        {
            SetMode(AuthMode.Login);
            InitializeResendButton();
        }

        private void Update()
        {
            UpdateResendTimer();
        }

        private void SetMode(AuthMode mode)
        {
            currentMode = mode;

            // Обновляем UI элементы в зависимости от режима
            switch (mode)
            {
                case AuthMode.Login:
                    titleText.text = loginTitle;
                    authButtonText.text = loginButtonText;
                    switchModeText.text = switchToRegisterText;
                    nicknameInput.gameObject.SetActive(false);
                    break;

                case AuthMode.Register:
                    titleText.text = registerTitle;
                    authButtonText.text = registerButtonText;
                    switchModeText.text = switchToLoginText;
                    nicknameInput.gameObject.SetActive(true);
                    break;
            }

            ToStartState();
        }

        private void OnSwitchModeClicked()
        {
            AuthMode newMode = currentMode == AuthMode.Login ? AuthMode.Register : AuthMode.Login;
            SetMode(newMode);
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
            codeInput.gameObject.SetActive(false);

            // Скрываем кнопку повторной отправки при сбросе состояния
            resendCodeButton.gameObject.SetActive(false);
            isResendTimerActive = false;
        }

        private void OnGetCodeClicked()
        {
            getConfirmCodeButton.interactable = false;

            var sendCodeRequest = new SendCodeRequest
            {
                phone = phoneInput.text,
                requested_by = ""
            };

            RestClient.Post(ApiRoutes.GetSendCodeUrl(), sendCodeRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    HandleError(response.StatusCode.ToString(), response.Error);
                    return;
                }

                var responseData = JsonUtility.FromJson<SuccessResponse<object>>(response.Text);
                if (responseData.success)
                {
                    phoneInput.interactable = false;

                    codeInput.gameObject.SetActive(true);

                    getConfirmCodeButton.gameObject.SetActive(false);
                    authButton.gameObject.SetActive(true);

                    StartResendTimer();
                }
                else
                {
                    HandleError(responseData.code, responseData.detail);
                    Debug.LogWarning(responseData.detail);
                }
            }).Finally(() => getConfirmCodeButton.interactable = true);
        }

        private void OnAuthClicked()
        {
            authButton.interactable = false;

            if (currentMode == AuthMode.Login)
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
                // Таймер истек, активируем кнопку
                resendCodeButton.interactable = true;
                isResendTimerActive = false;
                UpdateResendButtonText();
            }
            else
            {
                // Обновляем текст кнопки с оставшимся временем
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
            SceneManager.LoadSceneAsync("Character Customization");
        }

        private void HandleError(string title, string errorMessage)
        {
            popupPanel.Show(title, errorMessage);
        }
    }
}