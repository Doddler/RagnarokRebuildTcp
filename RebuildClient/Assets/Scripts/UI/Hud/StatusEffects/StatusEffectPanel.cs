using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    public class StatusEffectPanel : MonoBehaviour
    {
        public GameObject StatusEffectPrefab;
        public Transform BuffPanel;
        public Transform DebuffPanel;

        private const float BuffSpacing = 15f;

        private StatusEffectIconList buffs;
        private StatusEffectIconList debuffs;

        void Awake()
        {
            StatusEffectPrefab.SetActive(false);
            //stays in the layout while empty, so the debuff column doesn't move
            buffs = new StatusEffectIconList(BuffPanel, StatusEffectPrefab) { AllowCancel = true, HideWhenEmpty = false };
            debuffs = new StatusEffectIconList(DebuffPanel, StatusEffectPrefab) { AllowCancel = true };

            PlayerState.Instance.StatusAdded += AddStatusEffect;
            PlayerState.Instance.StatusRemoved += RemoveStatusEffect;
            PlayerState.Instance.StatusesReset += ClearStatusEffects;
        }

        //the state outlives the scene
        private void OnDestroy()
        {
            PlayerState.Instance.StatusAdded -= AddStatusEffect;
            PlayerState.Instance.StatusRemoved -= RemoveStatusEffect;
            PlayerState.Instance.StatusesReset -= ClearStatusEffects;
        }

        private void ClearStatusEffects()
        {
            buffs.Clear();
            debuffs.Clear();
        }

        private void AddStatusEffect(CharacterStatusEffect status, float time)
        {
            //missing from the client data, as when the server's data is newer
            var data = ClientDataLoader.Instance.GetStatusEffect((int)status);
            if (data == null)
                return;

            (data.Type == "Buff" ? buffs : debuffs).Add(data, time);
        }

        private void RemoveStatusEffect(CharacterStatusEffect status)
        {
            buffs.Remove(status);
            debuffs.Remove(status);
        }

        private void LateUpdate()
        {
            var iconHeight = ((RectTransform)StatusEffectPrefab.transform).sizeDelta.y;
            buffs.MaxShown = Mathf.Max(1, (int)(((RectTransform)BuffPanel).sizeDelta.y / (iconHeight + BuffSpacing)));
        }

        void Update()
        {
            buffs.Tick();
            debuffs.Tick();
        }
    }
}
