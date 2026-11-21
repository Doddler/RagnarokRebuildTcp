using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    //A target that died or left stays for LingerSeconds; a deselected one, or one on a map that was left, hides at once.
    public class TargetFrame : MonoBehaviour
    {
        public CanvasGroup Group;
        public CharacterFrame Frame;
        public TextMeshProUGUI DamageText;
        public float LingerSeconds = 3f;

        private ServerControllable target;
        private float lingerUntil;

        private void Awake()
        {
            //target players only; null once the target is removed
            Frame.GetCharacter = () => target != null && target.CharacterType == CharacterType.Player ? target : null;
            Frame.RightClicked += OpenRightClickMenu;
        }

        //hidden with the canvas group so the subscriptions stay alive
        private void Start()
        {
            SetVisible(false);
            enabled = false;
            CameraFollower.Instance.SelectedTargetChanged += OnSelectedTargetChanged;
            PlayerState.Instance.MapChanged += Hide;
            ServerControllable.PlayerDealtDamage += OnPlayerDealtDamage;
        }

        private void OnDestroy()
        {
            if (CameraFollower.Instance != null)
                CameraFollower.Instance.SelectedTargetChanged -= OnSelectedTargetChanged;
            PlayerState.Instance.MapChanged -= Hide;
            ServerControllable.PlayerDealtDamage -= OnPlayerDealtDamage;
            SetTarget(null);
        }

        private void OnSelectedTargetChanged(ServerControllable selected, CameraFollower.SelectionEnd end)
        {
            if (selected != null)
            {
                enabled = false;
                SetTarget(selected);
            }
            else if (end == CameraFollower.SelectionEnd.Dismissed)
                Hide();
            else if (!ReferenceEquals(target, null))
            {
                lingerUntil = Time.time + LingerSeconds;
                enabled = true;
            }
        }

        private void Update()
        {
            if (Time.time >= lingerUntil)
                Hide();
        }

        private void Hide()
        {
            enabled = false;
            SetTarget(null);
        }

        private void OnPlayerDealtDamage(ServerControllable hit)
        {
            if (hit == target)
                RefreshDamage();
        }

        private void OpenRightClickMenu()
        {
            if (target == null)
                return;

            var menu = UiManager.Instance.RightClickMenuWindow;
            if (target.CharacterType == CharacterType.Monster)
                menu.RightClickMonster(target);
            else
                menu.RightClickPlayer(target);
        }

        //by reference, so a destroyed character is unsubscribed too
        private void SetTarget(ServerControllable newTarget)
        {
            if (ReferenceEquals(newTarget, target))
                return;

            if (!ReferenceEquals(target, null))
            {
                target.HpChanged -= RefreshHp;
                target.NameChanged -= RefreshName;
                target.LevelChanged -= RefreshLevel;
                target.AppearanceChanged -= RefreshPortrait;
            }

            target = newTarget;
            SetVisible(target != null);
            if (target == null)
                return;

            target.HpChanged += RefreshHp;
            target.NameChanged += RefreshName;
            target.LevelChanged += RefreshLevel;
            target.AppearanceChanged += RefreshPortrait;
            RefreshName();
            RefreshLevel();
            RefreshPortrait();
            RefreshHp();
            RefreshDamage();
        }

        private void RefreshHp() => Frame.SetHp(target.Hp, target.MaxHp);

        private void RefreshName() => Frame.SetName(target.Name);

        private void RefreshLevel() => Frame.SetInfo($"Lv. {target.Level}");

        //empty until hit; SetText avoids building a string on every hit
        private void RefreshDamage()
        {
            var damage = target.DamageFromPlayer;
            if (damage <= 0)
                DamageText.SetText(string.Empty);
            else if (target.MaxHp <= 0)
                DamageText.SetText("{0}", damage);
            else
                DamageText.SetText("{0} ({1}%)", damage, (long)damage * 100 / target.MaxHp);
        }

        //a disguised player shows the monster they're disguised as
        private void RefreshPortrait()
        {
            if (target.CharacterType == CharacterType.Player && target.HeadAnimator != null)
            {
                Frame.Portrait.PrepareDisplayPlayerCharacter(target.Appearance);
                return;
            }

            var classId = target.OverrideClassId > 0 ? target.OverrideClassId : target.ClassId;
            var data = ClientDataLoader.Instance.GetMonsterData(classId);
            if (data == null || string.IsNullOrEmpty(data.SpriteName))
            {
                Frame.Portrait.Clear();
                return;
            }

            Frame.Portrait.DisplaySprite(ClientDataLoader.GetMonsterSpritePath(classId, data.SpriteName));
        }

        private void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1f : 0f;
            Group.blocksRaycasts = visible;
        }
    }
}
