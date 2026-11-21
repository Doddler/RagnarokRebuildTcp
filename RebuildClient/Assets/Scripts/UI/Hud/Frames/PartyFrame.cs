using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.Network;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.Hud
{
    //Statuses come from the member's entity, so only while it's in view.
    public class PartyFrame : UIBehaviour
    {
        public CharacterFrame Frame;
        public RectTransform DebuffArea;
        public GameObject StatusEffectPrefab;
        public int MaxStatusEffects = 5;
        public CanvasGroup CanvasGroup;
        public float OfflineAlpha = 0.4f;
        //where the info begins while it shows a map name, which needs more room than the level
        [Range(0f, 1f)] public float OffMapTitleSplit = 0.5f;

        private float titleSplit;
        private StatusEffectIconList statusIcons;
        private ServerControllable entity;

        [NonSerialized] public PartyMemberInfo PartyMemberInfo;

        //looked up again only when the player's job changes
        private static int playerJobId = -1;
        private static string playerJobName;

        private static string PlayerJobName
        {
            get
            {
                var jobId = PlayerState.Instance.JobId;
                if (jobId != playerJobId)
                {
                    playerJobId = jobId;
                    playerJobName = ClientDataLoader.Instance.GetJobNameForId(jobId);
                }
                return playerJobName;
            }
        }

        //members out of view have no entity, so skills can't be cast on their row
        protected override void Awake()
        {
            Frame.GetCharacter = () => entity;
            Frame.RightClicked += () => UiManager.Instance.RightClickMenuWindow.RightClickPartyMember(PartyMemberInfo);
            titleSplit = Frame.TitleSplit;
            statusIcons = new StatusEffectIconList(DebuffArea, StatusEffectPrefab)
            {
                SortByExpiration = true,
                IsPartyMember = true,
                MaxShown = MaxStatusEffects,
            };
        }

        //a hidden row catches up when shown
        protected override void OnEnable()
        {
            if (PartyMemberInfo != null)
                FullRefreshPartyMemberInfo();
        }

        protected override void OnDisable() => Bind(null);

        public void FullRefreshPartyMemberInfo()
        {
            var m = PartyMemberInfo;
            var isOnline = m.IsOnline;
            var isOnOtherMap = isOnline && !string.IsNullOrWhiteSpace(m.Map) && m.Map != PlayerState.Instance.MapName;

            Frame.SetName(m.PlayerName);
            Frame.SetInfo(GetInfo(m, isOnline, isOnOtherMap));
            Frame.SetLeader(m.IsLeader);
            Frame.TitleSplit = isOnOtherMap ? OffMapTitleSplit : titleSplit;
            Frame.Portrait.PrepareDisplayPlayerCharacter(m.Appearance);
            CanvasGroup.alpha = isOnline ? 1f : OfflineAlpha;

            RefreshHpSp();
            RefreshEntity();
        }

        public void RefreshEntity()
        {
            if (!isActiveAndEnabled)
                return;

            var m = PartyMemberInfo;
            Bind(m.IsOnline ? m.Controllable : null);
            RefreshStatusEffects();
        }

        //PartyState unlinks an entity when it leaves the entity list, before it's destroyed
        private void Bind(ServerControllable newEntity)
        {
            if (ReferenceEquals(newEntity, entity))
                return;

            if (!ReferenceEquals(entity, null))
            {
                entity.StatusAdded -= AddStatusEffect;
                entity.StatusRemoved -= RemoveStatusEffect;
            }

            entity = newEntity;
            if (ReferenceEquals(entity, null))
                return;

            entity.StatusAdded += AddStatusEffect;
            entity.StatusRemoved += RemoveStatusEffect;
        }

        //vitals aren't sent for offline members
        public void RefreshHpSp()
        {
            var m = PartyMemberInfo;
            Frame.SetHp(m.Hp, m.MaxHp);
            Frame.SetSp(m.Sp, m.MaxSp);
        }

        private static string GetInfo(PartyMemberInfo m, bool isOnline, bool isOnOtherMap)
        {
            if (!isOnline)
                return "offline";
            if (isOnOtherMap)
                return ClientDataLoader.Instance.GetFullNameForMap(m.Map);
            return $"Lv. {m.Level}/{m.JobLevel}";
        }

        private void RefreshStatusEffects()
        {
            if (entity == null || entity.StatusEffectState == null)
            {
                statusIcons.Clear();
                return;
            }

            statusIcons.ReplaceAll(GetShownStatuses(entity.StatusEffectState));
        }

        private static IEnumerable<(StatusEffectData, float)> GetShownStatuses(StatusEffectState state)
        {
            foreach (var (status, endTime) in state.GetStatusEffects())
            {
                var duration = endTime - Time.timeSinceLevelLoad;
                if (duration >= 0 && TryGetImportantToPlayer(status, out var data))
                    yield return (data, duration);
            }
        }

        private void AddStatusEffect(CharacterStatusEffect status, float time)
        {
            if (TryGetImportantToPlayer(status, out var data))
                statusIcons.Add(data, time);
        }

        //only statuses the player's job keeps up on others are listed, like a priest's blessing
        private static bool TryGetImportantToPlayer(CharacterStatusEffect status, out StatusEffectData data)
        {
            data = ClientDataLoader.Instance.GetStatusEffect((int)status);
            return data?.ImportantJobs != null && Array.IndexOf(data.ImportantJobs, PlayerJobName) >= 0;
        }

        private void RemoveStatusEffect(CharacterStatusEffect status) => statusIcons.Remove(status);

        private void Update() => statusIcons.Tick();
    }
}
