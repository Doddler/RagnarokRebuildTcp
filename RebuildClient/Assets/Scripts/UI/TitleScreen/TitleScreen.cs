using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Assets.Scripts.UI.TitleScreen
{
    public class TitleScreen : MonoBehaviour
    {
        public enum TitleScreenState
        {
            LogIn,
            CharacterSelect,
            CharacterCreation,
            NoticeBox,
            Waiting
        }

        public TitleScreenState TitleState = TitleScreenState.LogIn;
        public TitleScreenState LastTitleState = TitleScreenState.LogIn;
        //public List<Sprite> Backgrounds;
        public TextAsset BackgroundList;
        public Image BackgroundArea;

        public LoginBox LoginBox;
        public CharacterSelectWindow CharacterSelectWindow;
        public CharacterCreatorWindow CharacterCreatorWindow;

        public TextMeshProUGUI ProjectTitleText;
        public TextMeshProUGUI NoticeBoxText;
        public GameObject NoticeBox;

        [NonSerialized] public int SelectedSlot;

        private bool isInErrorState;
        private float loginTimeoutTime;
        private bool isLoginTimerActive;

        private List<string> bgNames = new();

        [NonSerialized] public AudioClip ButtonSound;

        private const float timeoutLength = 30f;


        public void OpenCharacterCreator(int slot)
        {
            isLoginTimerActive = false;
            CharacterSelectWindow.gameObject.SetActive(true);
            CharacterSelectWindow.HidePane();
            CharacterCreatorWindow.Open();
            SelectedSlot = slot;

            LastTitleState = TitleState;
            TitleState = TitleScreenState.CharacterCreation;
        }

        public void ReturnToCharacterSelect()
        {
            CharacterSelectWindow.gameObject.SetActive(true);
            TitleState = TitleScreenState.CharacterSelect;

            CharacterCreatorWindow.HidePane();
            CharacterSelectWindow.ShowPane();
        }

        public void OpenCharacterSelect(List<ClientCharacterSummary> characters)
        {
            isLoginTimerActive = false;
            CharacterSelectWindow.gameObject.SetActive(true);
            TitleState = TitleScreenState.CharacterSelect;

            CharacterCreatorWindow.HidePane();
            CharacterSelectWindow.PrepareSelectWindow(this, characters);
        }

        public void StartLoginTimer()
        {
            loginTimeoutTime = Time.timeSinceLevelLoad + timeoutLength;
            isLoginTimerActive = true;
            LoginBox.gameObject.SetActive(false);
        }

        public void LogInError(string message)
        {
            Debug.Log($"LogInError: " + message);
            NoticeBox.gameObject.SetActive(true);
            LoginBox.gameObject.SetActive(false);
            CharacterSelectWindow.gameObject.SetActive(false);
            NoticeBoxText.text = message;
            isInErrorState = true;
            isLoginTimerActive = false;
            TitleState = TitleScreenState.LogIn; //we return to login state now
        }

        public void DisconnectAndReturnToLogin()
        {
            NetworkManager.Instance.Disconnect();
            LoginBox.gameObject.SetActive(true);
            NoticeBox.gameObject.SetActive(false);
            CharacterSelectWindow.gameObject.SetActive(false);
            isInErrorState = false;
            isLoginTimerActive = false;
            TitleState = TitleScreenState.LogIn;
        }

        public void ErrorMessage(string message)
        {
            if (TitleState != TitleScreenState.Waiting)
                LastTitleState = TitleState;
            TitleState = TitleScreenState.NoticeBox;
            NoticeBoxText.text = message;
            NoticeBox.gameObject.SetActive(true);
        }

        public void NoticeBoxOk()
        {
            isInErrorState = false;
            switch (TitleState)
            {
                case TitleScreenState.LogIn:
                    NoticeBox.gameObject.SetActive(false);
                    LoginBox.gameObject.SetActive(true);
                    return;
                case TitleScreenState.NoticeBox:
                    TitleState = LastTitleState;
                    NoticeBox.gameObject.SetActive(false);
                    if (TitleState == TitleScreenState.CharacterCreation) CharacterSelectWindow.gameObject.SetActive(true);
                    if (TitleState == TitleScreenState.CharacterSelect) CharacterSelectWindow.gameObject.SetActive(true);
                    return;
            }
        }

        // Start is called before the first frame update
        void Awake()
        {
            foreach (var l in BackgroundList.text.Split('\n'))
                bgNames.Add(l.Trim());

            BackgroundArea.sprite = Resources.Load<Sprite>(bgNames[Random.Range(0, bgNames.Count)]);
            NoticeBox.gameObject.SetActive(false);
            CharacterSelectWindow.gameObject.SetActive(false);

            AddressableUtility.Load<AudioClip>(gameObject, "Assets/Sounds/Effects/버튼소리.ogg", a => ButtonSound = a);
        }

        void Start()
        {
            //do this in start instead of awake to make sure DataLoader has loaded everything
            ProjectTitleText.text = $"Ragnarok Online Rebuild Project (Protocol v{ClientDataLoader.Instance.ServerVersion})";
        }

        // Update is called once per frame
        void Update()
        {
            if (isInErrorState && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                NoticeBoxOk();

            if (isLoginTimerActive && Time.timeSinceLevelLoad > loginTimeoutTime)
            {
                isLoginTimerActive = false;
                LogInError($"Unable to connect to server, the connection timed out.");
            }
        }
    }
}