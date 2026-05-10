using System;
using System.Collections.Generic;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectPanel : MonoBehaviour
    {
        public GameObject StatusEffectPrefab;
        public Transform BuffPanel;
        public Transform DebuffPanel;

        [NonSerialized] private List<StatusEffectEntry> StatusEffects = new();
        private Dictionary<CharacterStatusEffect, StatusEffectEntry> StatusEffectLookup = new();

        private RectTransform mainRect;
        private RectTransform buffRect;

        private int buffCount;
        private int debuffCount;

        public static StatusEffectPanel Instance;

        void Awake()
        {
            Instance = this;
            StatusEffectPrefab.SetActive(false);
        }

        public void AddStatusEffect(CharacterStatusEffect status, float time)
        {
            var expiration = Time.timeSinceLevelLoad + time;
            if (StatusEffectLookup.TryGetValue(status, out var existing))
            {
                existing.Expiration = expiration;
                return;
            }

            var icon = ClientDataLoader.Instance.GetIconAtlasSprite($"status_{status}");
            if (icon == null)
            {
#if UNITY_EDITOR
                Debug.Log($"Status effect {status} could not find icon, will not display entry.");
#endif
                return;
            }

            var statusInfo = ClientDataLoader.Instance.GetStatusEffect((int)status);
            var isBuff = statusInfo.Type == "Buff";
            var target = isBuff ? BuffPanel : DebuffPanel;

            if (isBuff)
                buffCount++;
            else
                debuffCount++;

            var go = Instantiate(StatusEffectPrefab);
            go.transform.SetParent(target);
            go.SetActive(true);
            go.transform.localScale = Vector3.one;
            var newEffect = go.GetComponent<StatusEffectEntry>();
            newEffect.StatusEffect = status;
            newEffect.Expiration = expiration;
            newEffect.UpdateTime();
            newEffect.StatusIcon.sprite = icon;
            newEffect.IsBuff = isBuff;
            if (statusInfo.CanDisable)
                newEffect.CanCancel = true;

            StatusEffects.Add(newEffect);
            StatusEffectLookup.Add(status, newEffect);

            BuffPanel.gameObject.SetActive(BuffPanel.childCount > 0);
            DebuffPanel.gameObject.SetActive(DebuffPanel.childCount > 0);
        }

        public void RemoveStatusEffect(CharacterStatusEffect status)
        {
            if (!StatusEffectLookup.TryGetValue(status, out var existing))
                return;

            StatusEffectLookup.Remove(status);
            StatusEffects.Remove(existing);
            Destroy(existing.gameObject);

            var statusInfo = ClientDataLoader.Instance.GetStatusEffect((int)status);
            if (statusInfo.Type == "Buff")
                buffCount--;
            else
                debuffCount--;

            BuffPanel.gameObject.SetActive(BuffPanel.childCount > 0);
            DebuffPanel.gameObject.SetActive(DebuffPanel.childCount > 0);
        }

        private void LateUpdate()
        {
            if (mainRect == null)
            {
                mainRect = transform as RectTransform;
                buffRect = BuffPanel as RectTransform;
            }

            if (buffRect.childCount == 0)
                return;

            var first = buffRect.GetChild(0) as RectTransform;
            var fitCount = buffRect.sizeDelta.y / (first.sizeDelta.y + 15);

            var c = 0;

            foreach (var status in StatusEffects)
            {
                if (status.IsBuff)
                    c++;
                status.gameObject.SetActive(c < fitCount);
            }
        }

        void Update()
        {
            foreach (var e in StatusEffects)
                e.UpdateTime();
        }
    }
}