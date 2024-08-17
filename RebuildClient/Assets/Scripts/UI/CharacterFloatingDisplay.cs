using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    public class CharacterFloatingDisplay : MonoBehaviour
    {
        private TextMeshProUGUI namePlate;
        private SliderBar castBar;
        private SliderBar hpBar;
        private SliderBar mpBar;
        private CharacterChat chatBubble;

        public CharacterOverlayManager Manager;

        private string characterName;
        private int maxHp;
        private int maxMp;
        private bool isPlayer;
        private bool isMain;

        private float castStart;
        private float castEnd;
        private float chatEnd;

        public void Close()
        {
            if (Manager == null)
                return;
            Manager.ReturnFloatingDisplay(this);
        }

        public void ReturnToPool()
        {
            if (Manager == null)
            {
                Destroy(gameObject); //it's all fucked
                return;
            }

            if (namePlate != null) Manager.ReturnNamePlate(namePlate.gameObject);
            if (castBar != null) Manager.ReturnCastBar(castBar.gameObject);
            if (hpBar != null) Manager.ReturnHpBar(hpBar.gameObject);
            if (mpBar != null) Manager.ReturnMpBar(mpBar.gameObject);
            if (chatBubble != null) Manager.ReturnChatBubble(chatBubble.gameObject);

            namePlate = null;
            castBar = null;
            hpBar = null;
            mpBar = null;
            chatBubble = null;
        }

        public void SetUp(string name, int maxHp, int maxMp, bool isPlayer, bool isMain)
        {
            characterName = name;
            this.maxHp = maxHp;
            this.maxMp = maxMp;
            this.isPlayer = isPlayer;
            this.isMain = isMain;
            gameObject.SetActive(false);
        }

        public void UpdateName(string newName) => characterName = newName;

        public void ShowNamePlate()
        {
            if (namePlate != null)
                return;
            namePlate = Manager.AttachNamePlate(gameObject);
            namePlate.text = characterName;
            gameObject.SetActive(true);
        }

        public void HideNamePlate()
        {
            if (namePlate == null)
                return;
            Manager.ReturnNamePlate(namePlate.gameObject);
            namePlate = null;
        }

        public void StartCasting(float castTime)
        {
            if (castBar == null)
                castBar = Manager.AttachCastBar(gameObject);

            castBar.SetProgress(0);
            castStart = Time.timeSinceLevelLoad;
            castEnd = castStart + castTime;
            castBar.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }

        public void CancelCasting()
        {
            if (castBar != null)
            {
                Manager.ReturnCastBar(castBar.gameObject);
                castBar = null;
            }
        }

        public void ShowChatBubbleMessage(string message, float visibleTime = 5f)
        {
            if (chatBubble == null)
                chatBubble = Manager.AttachChatBubble(gameObject);

            chatBubble.SetText(message);
            chatEnd = Time.timeSinceLevelLoad + visibleTime;
            gameObject.SetActive(true);
        }
        
        
        public void HideHpBar()
        {
            if (hpBar == null)
                return;
            Manager.ReturnHpBar(hpBar.gameObject);
            hpBar = null;
        }

        public void ForceHpBarOn()
        {
            if (hpBar != null)
                return;
            // if(GameConfig.Data.AutoHideFullHPBars)

            hpBar = Manager.AttachHpBar(gameObject);
            gameObject.SetActive(true);
        }

        public void UpdateMaxHp(int maxHp) => this.maxHp = maxHp;

        public void UpdateHp(int hp)
        {
            if (hpBar == null)
            {
                if (hp == maxHp || (!isPlayer && !GameConfig.Data.ShowMonsterHpBars))
                    return;
                hpBar = Manager.AttachHpBar(gameObject);
                gameObject.SetActive(true);
            }

            hpBar.SetProgress((float)hp / maxHp);
            // Debug.Log($"Update HP on {characterName}: {hp}/{maxHp}");
            gameObject.SetActive(true);
            if (isPlayer)
            {
                hpBar.SetColor(new Color32(0x6C, 0xEA, 0x45, 255));
                if(isMain)
                    ((RectTransform)hpBar.transform).sizeDelta = new Vector2(100f, 10f);
                else
                    ((RectTransform)hpBar.transform).sizeDelta = new Vector2(100f, 10f);
            }
            else
            {
                if (GameConfig.Data.ShowMonsterHpBars)
                {
                    hpBar.SetColor(new Color32(0xC8, 0x45, 0xEA, 255));
                    ((RectTransform)hpBar.transform).sizeDelta = new Vector2(90f, 10f);
                }
                else
                {
                    Manager.ReturnHpBar(hpBar.gameObject);
                }
            }
        }

        public void Update()
        {
            if (chatBubble != null)
            {
                if (Time.timeSinceLevelLoad > chatEnd)
                {
                    Manager.ReturnChatBubble(chatBubble.gameObject);
                    chatBubble = null;
                } 
                else
                    chatBubble.RefreshBorder();
            }

            if (castBar != null)
            {
                if (Time.timeSinceLevelLoad > castEnd)
                    CancelCasting();
                else
                {
                    var pos = Time.timeSinceLevelLoad - castStart;
                    var end = castEnd - castStart;
                    castBar.SetProgress(pos / end);
                }
            }
        }
    }
}