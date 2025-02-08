using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterFloatingDisplay : MonoBehaviour
    {
        private ServerControllable controllable;
        private TextMeshProUGUI namePlate;
        private SliderBar castBar;
        private SliderBar hpBar;
        private SliderBar mpBar;
        private CharacterChat chatBubble;

        public CharacterOverlayManager Manager;
        public float StandingHeight;

        private string characterName;
        private int maxHp;
        private int maxMp;
        private bool isPlayer;
        private bool isMain;

        private float castStart;
        private float castEnd;
        private float chatEnd;

        private bool isHovering;
        private bool isTargeting;

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
            controllable = null;
            StandingHeight = 0;
        }

        public void SetUp(ServerControllable controllable, string name, int maxHp, int maxMp, bool isPlayer, bool isMain)
        {
            characterName = name;
            this.controllable = controllable;
            this.maxHp = maxHp;
            this.maxMp = maxMp;
            this.isPlayer = isPlayer;
            this.isMain = isMain;
            gameObject.SetActive(false);
        }

        public void UpdateName(string newName) => characterName = newName;

        public void HoverNamePlate()
        {
            ShowNamePlate();
            isHovering = true;
        }

        public void TargetingNamePlate()
        {
            ShowNamePlate();
            isTargeting = true;
        }

        public void EndHoverNamePlate()
        {
            isHovering = false;
            if (isTargeting)
                return; //we still need this plate to show
            HideNamePlate();
        }

        public void EndTargetingNamePlate()
        {
            isTargeting = false;
            if (isHovering)
                return; //we still need this plate to show
            HideNamePlate();
        }

        private void ShowNamePlate()
        {
            if (namePlate != null)
                return;
            namePlate = Manager.AttachNamePlate(gameObject);
            namePlate.text = characterName;
            gameObject.SetActive(true);
        }

        private void HideNamePlate()
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

            if (controllable.SpriteAnimator?.SpriteData != null && StandingHeight == 0)
            {
                StandingHeight = controllable.SpriteAnimator.SpriteData.StandingHeight;
                if (controllable.CharacterType == CharacterType.Player)
                    StandingHeight += 20;
            }
            
            castBar.transform.localPosition = new Vector3(0, StandingHeight * 2f, 0);
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

            //this is the biggest hack I've ever seen. End the chat bubble if it's showing monster cast name
            if (chatBubble != null && chatBubble.TextObject.text.Contains("<color=#FF8888>"))
            {
                Manager.ReturnChatBubble(chatBubble.gameObject);
                chatBubble = null;
            }
            
            //controllable.StopCastingAnimation();
        }

        public void ShowChatBubbleMessage(string message, float visibleTime = 5f)
        {
            if (chatBubble == null)
                chatBubble = Manager.AttachChatBubble(gameObject);

            if (controllable.SpriteAnimator?.SpriteData != null && StandingHeight == 0)
            {
                StandingHeight = controllable.SpriteAnimator.SpriteData.StandingHeight;
                if (controllable.CharacterType == CharacterType.Player)
                    StandingHeight += 20;
            }

            chatBubble.transform.localPosition = new Vector3(0, StandingHeight * 2f + 13, 0);

            chatBubble.SetText(message);
            chatEnd = Time.timeSinceLevelLoad + visibleTime;
            gameObject.SetActive(true);
        }

        public void HideMpBar()
        {
            if (mpBar == null)
                return;
            
            Manager.ReturnMpBar(mpBar.gameObject);
            mpBar = null;
        }

        public void ForceMpBarOn()
        {
            if (mpBar != null)
                return;

            mpBar = Manager.AttachMpBar(gameObject);
            gameObject.SetActive(true);
        }

        public void UpdateMaxMp(int maxMp) => this.maxMp = maxMp;

        public void UpdateMp(int mp)
        {
            if (mpBar == null)
                ForceMpBarOn();
            
            mpBar.SetProgress((float)mp / maxMp);
            
            if(isPlayer)
                ((RectTransform)mpBar.transform).sizeDelta = new Vector2(100f, 10f);
            else
                ((RectTransform)mpBar.transform).sizeDelta = new Vector2(90f, 10f); //when would this ever happen...?
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

        public void UpdateHp(int oldHp, int hp)
        {
            if (hpBar == null)
            {
                if (hp == maxHp || (!isPlayer && !GameConfig.Data.ShowMonsterHpBars))
                    return;
                hpBar = Manager.AttachHpBar(gameObject);
                gameObject.SetActive(true);
                hpBar.SetProgress((float)oldHp / maxHp);
            }

            if (oldHp >= 0)
            {
                hpBar.SetProgress((float)hp / maxHp, false);
            }
            else
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