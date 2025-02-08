using System;
using System.Collections.Generic;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;
using Utility;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectPanel : MonoBehaviour
    {
        public GameObject StatusEffectPrefab;
        public Transform BuffPanel;
        public Transform DebuffPanel;
        
        [NonSerialized] private List<StatusEffectEntry> StatusEffects = new();
        private Dictionary<CharacterStatusEffect, StatusEffectEntry> StatusEffectLookup = new();

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

            var icon = ClientDataLoader.Instance.ItemIconAtlas.GetSprite($"status_{status}");
            if (icon == null)
            {
                Debug.LogWarning($"Status effect {status} could not find icon!");
                return;
            }

            var statusInfo = ClientDataLoader.Instance.GetStatusEffect((int)status);
            var target = statusInfo.Type == "Buff" ? BuffPanel : DebuffPanel;

            var go = Instantiate(StatusEffectPrefab);
            go.transform.SetParent(target);
            go.SetActive(true);
            go.transform.localScale = Vector3.one;
            var newEffect = go.GetComponent<StatusEffectEntry>();
            newEffect.Expiration = expiration;
            newEffect.UpdateTime();
            newEffect.StatusIcon.sprite = icon;
            
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
            
            BuffPanel.gameObject.SetActive(BuffPanel.childCount > 0);
            DebuffPanel.gameObject.SetActive(DebuffPanel.childCount > 0);
        }

        void Update()
        {
            foreach (var e in StatusEffects)
                e.UpdateTime();        
        }
    }
}
