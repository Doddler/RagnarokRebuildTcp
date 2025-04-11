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
        public TMP_InputField UsernameBox;
        public TMP_InputField PasswordBox;
        public TMP_InputField PasswordRepeatBox;
        public TMP_InputField ServerInputBox;
        public RectTransform WindowRect;
        public TextMeshProUGUI UsernameLabelText;
        public TextMeshProUGUI SubmitButtonText;
        public GameObject PasswordRepeatRow;
        public GameObject LoginSection;
        public GameObject ServerSection;
        public Toggle RememberLoginToggle;
        public Button SubmitButton;
        
        public List<Button> Tabs;

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
            ServerInputBox.text = "wss://roserver.dodsrv.com/ws";
            #endif
            UsernameBox.text = PlayerPrefs.GetString("LoginUsername", "");
            if (!string.IsNullOrWhiteSpace(GameConfig.Data?.SavedLoginToken) && !string.IsNullOrWhiteSpace(UsernameBox.text) && UsernameBox.text != "ID")
            {
                PasswordBox.text = TokenLoginPass;
                RememberLoginToggle.isOn = true;
            }

            yield return new WaitForSeconds(0.1f);
            EventSystem.current.SetSelectedGameObject(UsernameBox.gameObject);
        }

        public void ChangeTabs(int id)
        {
            if (id == currentTab)
                return;
            
            Tabs[0].interactable = id != 0;
            Tabs[1].interactable = id != 1;
            Tabs[2].interactable = id != 2;

            if (currentTab == 0)
            {
                loginUserName = UsernameBox.text;
                loginPassword = PasswordBox.text; 
            }

            if (currentTab == 1)
            {
                createUserName = UsernameBox.text;
                createUserPassword = PasswordBox.text;
            }

            if (id == 0)
            {
                PasswordRepeatRow.SetActive(false);
                RememberLoginToggle.gameObject.SetActive(true);
                LoginSection.SetActive(true);
                ServerSection.SetActive(false);
                UsernameLabelText.text = "ID";
                SubmitButtonText.text = "Login";
                UsernameBox.text = loginUserName;
                PasswordBox.text = loginPassword;
                SubmitButton.gameObject.SetActive(true);
                WindowRect.sizeDelta = new Vector2(400, 230);
                EventSystem.current.SetSelectedGameObject(UsernameBox.gameObject);
            }

            if (id == 1)
            {
                PasswordRepeatRow.SetActive(true);
                RememberLoginToggle.gameObject.SetActive(false);
                LoginSection.SetActive(true);
                ServerSection.SetActive(false);
                UsernameLabelText.text = "New ID";
                SubmitButtonText.text = "Create";
                UsernameBox.text = createUserName;
                PasswordBox.text = createUserPassword;
                SubmitButton.gameObject.SetActive(true);
                WindowRect.sizeDelta = new Vector2(400, 270);
                EventSystem.current.SetSelectedGameObject(UsernameBox.gameObject);
            }

            if (id == 2)
            {
                PasswordRepeatRow.SetActive(false);
                RememberLoginToggle.gameObject.SetActive(false);
                LoginSection.SetActive(false);
                ServerSection.SetActive(true);
                SubmitButton.gameObject.SetActive(false);
                WindowRect.sizeDelta = new Vector2(400, 230);
                EventSystem.current.SetSelectedGameObject(ServerInputBox.gameObject);
            }

            currentTab = id;
        }

        public void AttemptLogin()
        {
            var url = ServerInputBox.text;
            var username = UsernameBox.text;
            var password = PasswordBox.text;

            AudioManager.Instance.PlaySystemSound(Parent.ButtonSound); //button sound
            
            if (currentTab == 0)
            {
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
                if (password == TokenLoginPass)
                {
                    NetworkManager.Instance.TitleScreen.LogInError($"Please use a different password.");
                    return;
                }
                
                var repeat = PasswordRepeatBox.text;
                if(password != repeat)
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
            if (currentTab == 2)
            {
                EventSystem.current.SetSelectedGameObject(ServerInputBox.gameObject);
                return;
            }
            
            var current = EventSystem.current.currentSelectedGameObject;
            var id = 0;
            if (current == UsernameBox.gameObject) id = 1;
            if (current == PasswordBox.gameObject) id = 2;
            if (current == PasswordRepeatBox.gameObject) id = 3;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                id--;
            else
                id++;

            if (currentTab == 1)
            {
                if (id < 1) id = 3;
                if (id > 3) id = 1;
            }
            else
            {
                if (id < 1) id = 2;
                if (id > 2) id = 1;
            }

            if(id == 1) EventSystem.current.SetSelectedGameObject(UsernameBox.gameObject);
            if(id == 2) EventSystem.current.SetSelectedGameObject(PasswordBox.gameObject);
            if(id == 3) EventSystem.current.SetSelectedGameObject(PasswordRepeatBox.gameObject);

        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
                HandleTabKey();
            
            if(Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.F1))
                ServerInputBox.text = "ws://127.0.0.1:5000/ws";

            if (currentTab == 0)
            {
                var username = UsernameBox.text;
                var password = PasswordBox.text;

                var isOk = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);
                if (SubmitButton.interactable != isOk)
                    SubmitButton.interactable = isOk;
                if(isOk && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    AttemptLogin();
            }

            if (currentTab == 1)
            {
                var username = UsernameBox.text;
                var password = PasswordBox.text;
                var repeat = PasswordRepeatBox.text;

                var isOk = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(repeat);
                if (SubmitButton.interactable != isOk)
                    SubmitButton.interactable = isOk;
                if(isOk && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    AttemptLogin();
            }
            //
            // if(currentTab != 2 && Input.GetKeyDown(KeyCode.Return))
            //     AttemptLogin();
        }
    }
}