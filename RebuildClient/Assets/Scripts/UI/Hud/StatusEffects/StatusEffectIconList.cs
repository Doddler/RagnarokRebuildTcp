using System.Collections.Generic;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    //The owner picks the statuses and calls Tick every frame.
    public class StatusEffectIconList
    {
        private readonly Transform container;
        private readonly GameObject iconPrefab;
        private readonly List<StatusEffectIcon> icons = new();
        private readonly Dictionary<CharacterStatusEffect, StatusEffectIcon> lookup = new();
        private readonly Stack<StatusEffectIcon> unusedIcons = new();
        private int maxShown;

        public bool SortByExpiration;
        public bool AllowCancel;
        //tooltips only show while shift is held
        public bool IsPartyMember;
        public bool HideWhenEmpty = true;

        //icons past this are kept hidden and move up as earlier ones end; 0 shows all
        public int MaxShown
        {
            get => maxShown;
            set
            {
                if (maxShown == value)
                    return;
                maxShown = value;
                Arrange();
            }
        }

        public StatusEffectIconList(Transform container, GameObject iconPrefab)
        {
            this.container = container;
            this.iconPrefab = iconPrefab;
        }

        //a status already shown restarts its countdown; one without an icon isn't shown
        public void Add(StatusEffectData status, float duration)
        {
            AddIcon(status, duration);
            Arrange();
        }

        public void Remove(CharacterStatusEffect status)
        {
            if (!lookup.Remove(status, out var icon))
                return;

            icons.Remove(icon);
            icon.gameObject.SetActive(false);
            unusedIcons.Push(icon);
            Arrange();
        }

        public void Clear()
        {
            ClearIcons();
            Arrange();
        }

        public void ReplaceAll(IEnumerable<(StatusEffectData status, float duration)> statuses)
        {
            ClearIcons();
            foreach (var (status, duration) in statuses)
                AddIcon(status, duration);
            Arrange();
        }

        public void Tick()
        {
            foreach (var icon in icons)
                icon.UpdateTime();
        }

        private void AddIcon(StatusEffectData status, float duration)
        {
            var expiration = Time.timeSinceLevelLoad + duration;
            if (lookup.TryGetValue(status.StatusEffect, out var existing))
            {
                existing.Expiration = expiration;
                return;
            }

            var sprite = ClientDataLoader.Instance.GetIconAtlasSprite($"status_{status.StatusEffect}");
            if (sprite == null)
            {
#if UNITY_EDITOR
                Debug.Log($"Status effect {status.StatusEffect} could not find icon, will not display entry.");
#endif
                return;
            }

            if (!unusedIcons.TryPop(out var icon))
                icon = Object.Instantiate(iconPrefab, container, false).GetComponent<StatusEffectIcon>();

            icon.Show(status, expiration, sprite, AllowCancel && status.CanDisable, IsPartyMember);

            icons.Add(icon);
            lookup.Add(status.StatusEffect, icon);
        }

        private void ClearIcons()
        {
            foreach (var icon in icons)
            {
                icon.gameObject.SetActive(false);
                unusedIcons.Push(icon);
            }

            icons.Clear();
            lookup.Clear();
        }

        private void Arrange()
        {
            if (SortByExpiration)
                icons.Sort((a, b) => a.Expiration.CompareTo(b.Expiration));

            for (var i = 0; i < icons.Count; i++)
            {
                var iconTransform = icons[i].transform;
                if (iconTransform.GetSiblingIndex() != i)
                    iconTransform.SetSiblingIndex(i);
                icons[i].gameObject.SetActive(maxShown <= 0 || i < maxShown);
            }

            if (HideWhenEmpty)
                container.gameObject.SetActive(icons.Count > 0);
        }
    }
}
