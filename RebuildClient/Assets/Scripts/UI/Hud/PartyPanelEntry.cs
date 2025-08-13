using System;
using System.Collections.Generic;
using Assets.Scripts.Data;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class PartyPanelEntry : UIBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TextMeshProUGUI NameArea;
        public TextMeshProUGUI LocationText;
        public TextMeshProUGUI HpValueText;
        public TextMeshProUGUI SpValueText;
        public Slider HpSlider;
        public Slider SpSlider;
        public RectTransform DebuffArea;
        public GameObject StatusEffectPrefab;
        public GameObject SkillTargetHover;
        
        private readonly List<StatusEffectEntry> statusEffects = new();
        private readonly Dictionary<CharacterStatusEffect, StatusEffectEntry> statusEffectLookup = new();
        private Stack<GameObject> unusedStatusEffects = new();
        private List<CharacterStatusEffect> expiredStatuses = new();

        [NonSerialized] public PartyPanel Parent;
        [NonSerialized] public PartyMemberInfo PartyMemberInfo;

        public void UpdatePlayerProximity()
        {
            if (PartyMemberInfo.Controllable == null)
            {
                NameArea.color = new Color(0.7f, 0.7f, 0.7f);
                return;
            }

            var player = CameraFollower.Instance.TargetControllable;
            if (player == null)
                return;

            if (Vector2.Distance(player.RealPosition2D, PartyMemberInfo.Controllable.RealPosition2D) <= 9.85f
                && Pathfinder.HasLineOfSight(PartyMemberInfo.Controllable.CellPosition, player.CellPosition))
                NameArea.color = Color.white;
            else
                NameArea.color = new Color(0.7f, 0.85f, 1f);
        }

        public void FullRefreshPartyMemberInfo()
        {
            var m = PartyMemberInfo;
            
            if (m.IsLeader)
                NameArea.text = $"★{m.PlayerName}";
            else
                NameArea.text = m.PlayerName;

            if (m.EntityId > 0)
            {
                if (!string.IsNullOrWhiteSpace(m.Map) && m.Map == PlayerState.Instance.MapName)
                {
                    HpSlider.gameObject.SetActive(true);
                    SpSlider.gameObject.SetActive(true);
                    LocationText.gameObject.SetActive(false);
                    HpValueText.text = $"HP: {m.Hp} / {m.MaxHp}";
                    SpValueText.text = $"SP: {m.Sp} / {m.MaxSp}";       
                    HpSlider.value = m.Hp / (float)m.MaxHp;
                    SpSlider.value = m.Sp / (float)m.MaxSp;
                }
                else
                {
                    HpSlider.gameObject.SetActive(false);
                    SpSlider.gameObject.SetActive(false);
                    LocationText.gameObject.SetActive(true);
                    DebuffArea.gameObject.SetActive(false);
                    ClearAllStatusEffects();
                    var fullMapName = ClientDataLoader.Instance.GetFullNameForMap(m.Map);
                    LocationText.text = $"({fullMapName})";
                    return;
                }
            }
            else
            {
                NameArea.text = $"<color=#aaaaaa>{m.PlayerName}";
                
                HpSlider.gameObject.SetActive(false);
                SpSlider.gameObject.SetActive(false);
                LocationText.gameObject.SetActive(true);
                LocationText.text = $"(Offline)";
                ClearAllStatusEffects();
                DebuffArea.gameObject.SetActive(false);
                return;
            }
            
            RefreshStatusEffects();
        }

        public void RefreshHpSp()
        {
            var m = PartyMemberInfo;
            if (!string.IsNullOrWhiteSpace(m.Map) && m.Map == PlayerState.Instance.MapName)
            {
                if (m.Controllable != null)
                {
                    m.Hp = m.Controllable.Hp;
                    m.MaxHp = m.Controllable.MaxHp;
                }
                HpSlider.gameObject.SetActive(true);
                SpSlider.gameObject.SetActive(true);
                LocationText.gameObject.SetActive(false);
                HpValueText.text = $"HP: {m.Hp} / {m.MaxHp}";
                SpValueText.text = $"SP: {m.Sp} / {m.MaxSp}";
                HpSlider.value = m.Hp / (float)m.MaxHp;
                SpSlider.value = m.Sp / (float)m.MaxSp;
            }
        }

        public void RefreshStatusEffects()
        {
            if (PartyMemberInfo.EntityId <= 0 || PartyMemberInfo.Controllable == null)
            {
                DebuffArea.gameObject.SetActive(false);
                ClearAllStatusEffects();
                return;
            }

            ClearAllStatusEffects();
            
            var activeEffects = PartyMemberInfo.Controllable.StatusEffectState?.GetStatusEffects();
            if (activeEffects == null)
            {
                // ClearAllStatusEffects();
                return;
            }
            DebuffArea.gameObject.SetActive(true);

            foreach (var (status, endTime) in activeEffects)
            {
                var duration = endTime - Time.timeSinceLevelLoad;
                if (duration < 0)
                    continue;
                
                AddStatusEffect(status, duration);
            }
        }

        private void ReScaleDebuffArea()
        {
            var scale = DebuffArea.transform.childCount switch
            {
                <= 6 => 0.45f,
                7 => 0.4f,
                >= 8 => 0.35f
            };

            DebuffArea.gameObject.transform.localScale = new Vector3(scale, scale, scale);
        }
        
        public void AddStatusEffect(CharacterStatusEffect status, float time)
        {
            var expiration = Time.timeSinceLevelLoad + time;
            if (statusEffectLookup.TryGetValue(status, out var existing))
            {
                existing.Expiration = expiration;
                return;
            }

            var icon = ClientDataLoader.Instance.GetIconAtlasSprite($"status_{status}");
            if (icon == null)
            {
                Debug.LogWarning($"Status effect {status} could not find icon!");
                return;
            }

            var statusInfo = ClientDataLoader.Instance.GetStatusEffect((int)status);
            var isBuff = statusInfo.Type == "Buff";

            if(!unusedStatusEffects.TryPop(out var go))
                go = Instantiate(StatusEffectPrefab);
            go.transform.SetParent(DebuffArea);
            if(!isBuff)
                go.transform.SetAsFirstSibling();
            else
                go.transform.SetAsLastSibling();
            go.SetActive(true);
            go.transform.localScale = Vector3.one;
            var newEffect = go.GetComponent<StatusEffectEntry>();
            newEffect.StatusEffect = status;
            newEffect.Expiration = expiration;
            newEffect.UpdateTime();
            newEffect.StatusIcon.sprite = icon;
            newEffect.CanCancel = false;
            newEffect.IsPartyMember = true;
            
            
            statusEffects.Add(newEffect);
            statusEffectLookup.Add(status, newEffect);

            DebuffArea.gameObject.SetActive(true);
            ReScaleDebuffArea();
        }

        public void RemoveStatusEffect(CharacterStatusEffect status)
        {
            if (!statusEffectLookup.Remove(status, out var existing))
                return;

            statusEffects.Remove(existing);
            existing.gameObject.SetActive(false);
            unusedStatusEffects.Push(existing.gameObject);
            
            DebuffArea.gameObject.SetActive(statusEffects.Count > 0);
            ReScaleDebuffArea();
        }

        public void ClearAllStatusEffects()
        {
            foreach (var effect in statusEffects)
            {
                effect.gameObject.SetActive(false);
                unusedStatusEffects.Push(effect.gameObject);
            }

            statusEffects.Clear();
            statusEffectLookup.Clear();
            DebuffArea.gameObject.SetActive(false);
        }

        public void Update()
        {
            foreach (var status in statusEffects)
            {
                status.UpdateTime();
                if(status.IsExpired && PartyMemberInfo.Controllable == null) //only remove expired statuses in cases where we wouldn't be notified by the server
                    expiredStatuses.Add(status.StatusEffect);
            }

            if (expiredStatuses.Count > 0)
            {
                foreach(var s in expiredStatuses)
                    RemoveStatusEffect(s);
                expiredStatuses.Clear();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (PartyMemberInfo.Controllable == null)
                return;
            
            if (CameraFollower.Instance.HasSkillOnCursor)
            {
                var target = CameraFollower.Instance.CursorSkillTarget;
                if(target == SkillTarget.Ally || target == SkillTarget.Any)
                    SkillTargetHover.gameObject.SetActive(true);
            }

            Parent.HoverEntry = this;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SkillTargetHover.gameObject.SetActive(false);
            if (Parent.HoverEntry == this)
                Parent.HoverEntry = null;
        }
    }
}