using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.TitleScreen
{
    public class LoginBox : MonoBehaviour
    {
        public TitleScreen Parent;
        public TMP_InputField LoginUsernameBox;
        public TMP_InputField LoginPasswordBox;
        public TMP_InputField CreateUsernameBox;
        public TMP_InputField CreatePasswordBox;
        public TMP_InputField PasswordRepeatBox;
        public TMP_InputField ServerInputBox;
        public RectTransform WindowRect;
        public TextMeshProUGUI SubmitButtonText;
        public GameObject PasswordRepeatRow;
        public Toggle RememberLoginToggle;
        public Button SubmitButton;

        private int currentTab = 0;

        private string loginUserName;
        private string loginPassword;
        private string createUserName;
        private string createUserPassword;

        private const string TokenLoginPass = "Token!!WoW!Such_Pass";

        public bool IsSetToStorePasswordToken => RememberLoginToggle.isOn;

        void Awake()
        {
        }

        public void Init()
        {
            StartCoroutine(StartEvent());
        }

        private IEnumerator StartEvent()
        {
#if !UNITY_EDITOR
            ServerInputBox.text = PlayerPrefs.GetString($"ConnectServer", "ws://127.0.0.1:5000/ws");
#endif
            LoginUsernameBox.text = PlayerPrefs.GetString("LoginUsername", "");
            if (!string.IsNullOrWhiteSpace(GameConfig.Data?.SavedLoginToken) && !string.IsNullOrWhiteSpace(LoginUsernameBox.text) && LoginUsernameBox.text != "ID")
            {
                LoginPasswordBox.text = TokenLoginPass;
                RememberLoginToggle.isOn = true;
            }

            yield return new WaitForSeconds(0.1f);
            EventSystem.current.SetSelectedGameObject(LoginUsernameBox.gameObject);
        }

        public void ReturnFocus()
        {
            if (currentTab == 0 || currentTab == 1)
                EventSystem.current.SetSelectedGameObject(LoginUsernameBox.gameObject);
            if (currentTab == 2)
                EventSystem.current.SetSelectedGameObject(ServerInputBox.gameObject);
        }

        public void ChangeTabs(int id)
        {
            if (currentTab == 0)
            {
                loginUserName = LoginUsernameBox.text;
                loginPassword = LoginPasswordBox.text;
            }
            else if (currentTab == 1)
            {
                createUserName = CreateUsernameBox.text;
                createUserPassword = CreatePasswordBox.text;
            }

            switch (id)
            {
                case 0:
                    SubmitButtonText.text = "Login";
                    LoginUsernameBox.text = loginUserName;
                    LoginPasswordBox.text = loginPassword;
                    PasswordRepeatRow.SetActive(false);
                    RememberLoginToggle.gameObject.SetActive(true);
                    SubmitButton.gameObject.SetActive(true);
                    WindowRect.sizeDelta = new Vector2(400, 230);
                    EventSystem.current.SetSelectedGameObject(LoginUsernameBox.gameObject);
                    break;
                case 1:
                    SubmitButtonText.text = "Create";
                    CreateUsernameBox.text = createUserName;
                    CreatePasswordBox.text = createUserPassword;
                    PasswordRepeatRow.SetActive(true);
                    RememberLoginToggle.gameObject.SetActive(false);
                    SubmitButton.gameObject.SetActive(true);
                    WindowRect.sizeDelta = new Vector2(400, 270);
                    EventSystem.current.SetSelectedGameObject(CreateUsernameBox.gameObject);
                    break;
                case 2:
                    PasswordRepeatRow.SetActive(false);
                    RememberLoginToggle.gameObject.SetActive(false);
                    SubmitButton.gameObject.SetActive(false);
                    WindowRect.sizeDelta = new Vector2(400, 230);
                    EventSystem.current.SetSelectedGameObject(ServerInputBox.gameObject);
                    break;
            }

            currentTab = id;
        }

        public void AttemptLogin()
        {
            var url = ServerInputBox.text;

            PlayerPrefs.SetString($"ConnectServer", ServerInputBox.text);
            AudioManager.Instance.PlaySystemSound(Parent.ButtonSound); //button sound

            if (currentTab == 0)
            {
                var username = LoginUsernameBox.text;
                var password = LoginPasswordBox.text;
                var requestToken = RememberLoginToggle.isOn;
                if (password == TokenLoginPass && IsSetToStorePasswordToken)
                {
                    var login = GameConfig.Data?.SavedLoginToken;
                    if (string.IsNullOrWhiteSpace(login))
                    {
                        NetworkManager.Instance.TitleScreen.LogInError($"Stored user password unavailable, please input your password normally.");
                        return;
                    }

                    NetworkManager.Instance.StartConnectWithRegularLogin(url, username, login, true, requestToken);
                }
                else
                    NetworkManager.Instance.StartConnectWithRegularLogin(url, username, password, false, requestToken);

                PlayerPrefs.SetString("LoginUsername", username);
                NetworkManager.Instance.TitleScreen.StartLoginTimer();
            }

            if (currentTab == 1)
            {
                var username = CreateUsernameBox.text;
                var password = CreatePasswordBox.text;
                if (password == TokenLoginPass)
                {
                    NetworkManager.Instance.TitleScreen.LogInError($"Please use a different password.");
                    return;
                }

                var repeat = PasswordRepeatBox.text;
                if (password != repeat)
                    NetworkManager.Instance.TitleScreen.LogInError($"Passwords do not match between the password and verification boxes.");
                else
                {
                    PlayerPrefs.SetString("LoginUsername", username);
                    NetworkManager.Instance.StartConnectWithNewAccount(url, username, password);
                    NetworkManager.Instance.TitleScreen.StartLoginTimer();
                }
            }
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        private void HandleTabKey()
        {
            var fields = currentTab switch
            {
                1 => new[] { CreateUsernameBox, CreatePasswordBox, PasswordRepeatBox },
                2 => new[] { ServerInputBox },
                _ => new[] { LoginUsernameBox, LoginPasswordBox },
            };

            var current = EventSystem.current.currentSelectedGameObject;
            var index = System.Array.FindIndex(fields, f => f.gameObject == current);
            var forward = !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            if (index < 0)
                index = forward ? 0 : fields.Length - 1;
            else
                index = (index + (forward ? 1 : -1) + fields.Length) % fields.Length;

            EventSystem.current.SetSelectedGameObject(fields[index].gameObject);
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                HandleTabKey();

// #if(UNITY_EDITOR)

            //simplify testing stuff
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F1))
            {
                if (ServerInputBox.text == "ws://127.0.0.1:5000/ws")
                    ServerInputBox.text = "wss://roserver.dodsrv.com/ws";
                else
                    ServerInputBox.text = "ws://127.0.0.1:5000/ws";
                PlayerPrefs.SetString($"ConnectServer", ServerInputBox.text);
            }
// #else
//             if(Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F1))
//                 ServerInputBox.text = "ws://127.0.0.1:5000/ws";
// #endif

            if (currentTab == 0)
            {
                var username = LoginUsernameBox.text;
                var password = LoginPasswordBox.text;

                var isOk = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
                if (SubmitButton.interactable != isOk)
                    SubmitButton.interactable = isOk;
                if (isOk && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    AttemptLogin();
            }

            if (currentTab == 1)
            {
                var username = CreateUsernameBox.text;
                var password = CreatePasswordBox.text;
                var repeat = PasswordRepeatBox.text;

                var isOk = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(repeat);
                if (SubmitButton.interactable != isOk)
                    SubmitButton.interactable = isOk;
                if (isOk && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    AttemptLogin();
            }
            //
            // if(currentTab != 2 && Input.GetKeyDown(KeyCode.Return))
            //     AttemptLogin();
        }
    }
}