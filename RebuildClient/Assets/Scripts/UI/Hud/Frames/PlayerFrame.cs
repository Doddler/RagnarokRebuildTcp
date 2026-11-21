using Assets.Scripts.PlayerControl;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class PlayerFrame : MonoBehaviour
    {
        public CharacterFrame Frame;

        private void Awake()
        {
            Frame.GetCharacter = () => PlayerState.Instance.Character;
            Frame.RightClicked += () => UiManager.Instance.RightClickMenuWindow.RightClickSelf();
        }

        private void OnEnable()
        {
            var state = PlayerState.Instance;
            state.VitalsChanged += RefreshVitals;
            state.CharacterInfoChanged += RefreshInfo;
            state.Party.LeaderChanged += RefreshInfo;
            state.AppearanceChanged += RefreshInfo;
            state.AppearanceChanged += RefreshPortrait;
            state.ExpChanged += RefreshExp;

            RefreshVitals();
            RefreshInfo();
            RefreshPortrait();
            RefreshExp();
        }

        private void OnDisable()
        {
            var state = PlayerState.Instance;
            state.VitalsChanged -= RefreshVitals;
            state.CharacterInfoChanged -= RefreshInfo;
            state.Party.LeaderChanged -= RefreshInfo;
            state.AppearanceChanged -= RefreshInfo;
            state.AppearanceChanged -= RefreshPortrait;
            state.ExpChanged -= RefreshExp;
        }

        private void RefreshVitals()
        {
            var state = PlayerState.Instance;
            Frame.SetHp(state.Hp, state.MaxHp);
            Frame.SetSp(state.Sp, state.MaxSp);
        }

        private void RefreshInfo()
        {
            var state = PlayerState.Instance;
            Frame.SetName(state.PlayerName);
            Frame.SetInfo($"Lv. {state.Level}/{state.JobLevel}");
            Frame.SetLeader(state.Party.IsLeader);
        }

        private void RefreshPortrait()
        {
            var state = PlayerState.Instance;
            if (!state.IsReadyForDisplay)
                return;

            Frame.Portrait.PrepareDisplayPlayerCharacter(state.Appearance);
        }

        private void RefreshExp()
        {
            var state = PlayerState.Instance;
            if (!state.IsReadyForDisplay)
                return;

            Frame.SetExp(state.BaseExpProgress, state.JobExpProgress);
        }
    }
}
