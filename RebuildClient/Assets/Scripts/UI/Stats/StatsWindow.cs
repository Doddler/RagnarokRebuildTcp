using System.Collections.Generic;
using Assets.Scripts.Network;
using RebuildSharedData.Enum.EntityStats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Stats
{
    public class StatsWindow : WindowBase
    {
        public List<TextMeshProUGUI> BaseStatText;
        public List<TextMeshProUGUI> AddStatText;
        public List<TextMeshProUGUI> StatPointCostText;
        public List<TextMeshProUGUI> AttributeText;

        public List<Button> IncreaseStatButtons;
        public List<Button> DecreaseStatButtons;

        public Button ResetButton;
        public Button ApplyButton;

        //public int[] MinValues;
        private readonly int[] adjustValue = new int[6];
        private int statPointsRequired;

        private static readonly int[] CumulativeStatPointCost = new[]
        {
            2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 23, 26, 29, 32, 35, 38, 41, 44, 47, 50,
            54, 58, 62, 66, 70, 74, 78, 82, 86, 90, 95, 100, 105, 110, 115, 120, 125, 130, 135, 140,
            146, 152, 158, 164, 170, 176, 182, 188, 194, 200, 207, 214, 221, 228, 235, 242, 249, 256, 263, 270,
            278, 286, 294, 302, 310, 318, 326, 334, 342, 350, 359, 368, 377, 386, 395, 404, 413, 422, 431, 440,
            450, 460, 470, 480, 490, 500, 510, 520, 530, 540, 551, 562, 573, 584, 595, 606, 617, 628, 639,
        };

        public void ResetStatChanges()
        {
            for (var i = 0; i < 6; i++)
                adjustValue[i] = 0;
            statPointsRequired = 0;
            UpdateCharacterStats();
        }

        public void SaveChanges()
        {
            if (statPointsRequired == 0)
                return;
            
            NetworkManager.Instance.SendApplyStatPoints(adjustValue);
        }

        public void AddStat(int stat) => ChangeStatValue(stat,  1);
        public void SubStat(int stat) => ChangeStatValue(stat, -1);
        
        private void ChangeStatValue(int stat, int change)
        {
            var count = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 10 : 1; 
            var state = NetworkManager.Instance.PlayerState;
            var curStatPoints = state.GetData(PlayerStat.StatPoints);
            var existing = NetworkManager.Instance.PlayerState.GetData(PlayerStat.Str + stat);

            for (var j = 0; j < count; j++)
            {
                if (existing + adjustValue[stat] + change < existing)
                    break;
                if (existing + adjustValue[stat] + change > 99 || existing + adjustValue[stat] + change < 1)
                    break;

                adjustValue[stat] += change;

                var totalChange = 0;
                for (var i = 0; i < 6; i++)
                {
                    var curStatCost = CumulativeStatPointCost[Mathf.Clamp(state.GetData(PlayerStat.Str + i), 1, 99) - 1];
                    var newStatCost = CumulativeStatPointCost[Mathf.Clamp(state.GetData(PlayerStat.Str + i) + adjustValue[i], 1, 99) - 1];
                    totalChange += newStatCost - curStatCost;
                }

                if (totalChange < 0 || totalChange > curStatPoints)
                {

                    adjustValue[stat] -= change;
                    break;
                }

                statPointsRequired = totalChange;
            }

            UpdateCharacterStats();
            
            // UpdateStat(stat, existing, NetworkManager.Instance.PlayerState.GetStat(CharacterStat.AddStr + stat), adjustValue[stat]);
            //
            // var changeSum = 0;
            // for (var i = 0; i < 6; i++)
            //     changeSum += adjustValue[i] != 0 ? 1 : 0;
            //
            // ResetButton.interactable = changeSum != 0;
            // ApplyButton.interactable = changeSum != 0;
            //
            // if(statPointsRequired == 0)
            //     AttributeText[8].text = $"{state.GetData(PlayerStat.StatPoints)}";
            // else
            //     AttributeText[8].text = $"<color=blue>{state.GetData(PlayerStat.StatPoints)-statPointsRequired}";
        }

        private void UpdateStat(int index, int stat, int bonus, int diff = 0)
        {
            if (diff == 0)
                BaseStatText[index].text = stat.ToString();
            if (diff < 0)
                BaseStatText[index].text = $"<color=red>{stat + diff}</color>";
            if (diff > 0)
                BaseStatText[index].text = $"<color=blue>{stat + diff}</color>";

            AddStatText[index].text = bonus > 0 ? $"+{bonus}" : "";
            var cost = 2 + (stat + diff) / 10;
            if (stat >= 99)
                StatPointCostText[index].text = "-";
            else
                StatPointCostText[index].text = cost.ToString();

            DecreaseStatButtons[index].interactable = diff > 0;
            IncreaseStatButtons[index].interactable = stat + diff < 99 && statPointsRequired + cost <= NetworkManager.Instance.PlayerState.GetData(PlayerStat.StatPoints);
        }

        public void UpdateCharacterStats()
        {
            var state = NetworkManager.Instance.PlayerState;

            UpdateStat(0, state.GetData(PlayerStat.Str), state.GetStat(CharacterStat.AddStr), adjustValue[0]);
            UpdateStat(1, state.GetData(PlayerStat.Agi), state.GetStat(CharacterStat.AddAgi), adjustValue[1]);
            UpdateStat(2, state.GetData(PlayerStat.Vit), state.GetStat(CharacterStat.AddVit), adjustValue[2]);
            UpdateStat(3, state.GetData(PlayerStat.Int), state.GetStat(CharacterStat.AddInt), adjustValue[3]);
            UpdateStat(4, state.GetData(PlayerStat.Dex), state.GetStat(CharacterStat.AddDex), adjustValue[4]);
            UpdateStat(5, state.GetData(PlayerStat.Luk), state.GetStat(CharacterStat.AddLuk), adjustValue[5]);

            var totalVit = state.GetData(PlayerStat.Vit) + state.GetStat(CharacterStat.AddVit);
            var totalAgi = state.GetData(PlayerStat.Agi) + state.GetStat(CharacterStat.AddAgi);
            var totalInt = state.GetData(PlayerStat.Int) + state.GetStat(CharacterStat.AddInt);
            var totalDex = state.GetData(PlayerStat.Dex) + state.GetStat(CharacterStat.AddDex);
            var totalLuk = state.GetData(PlayerStat.Luk) + state.GetStat(CharacterStat.AddLuk);
            
            var changeSum = 0;
            for (var i = 0; i < 6; i++)
                changeSum += adjustValue[i] != 0 ? 1 : 0;

            ResetButton.interactable = changeSum != 0;
            ApplyButton.interactable = changeSum != 0;

            AttributeText[0].text = $"{state.GetStat(CharacterStat.Attack)} ~ {state.GetStat(CharacterStat.Attack2)}";
            AttributeText[1].text = $"{state.GetStat(CharacterStat.MagicAtkMin)} ~ {state.GetStat(CharacterStat.MagicAtkMax)}";
            AttributeText[2].text = $"{totalDex + state.Level + state.GetStat(CharacterStat.AddHit)}";
            AttributeText[3].text = $"{(10 + totalLuk * 3 + state.GetStat(CharacterStat.AddCrit)) / 10}";
            AttributeText[4].text = $"{state.GetStat(CharacterStat.Def)} + {totalVit}";
            AttributeText[5].text = $"{state.GetStat(CharacterStat.MDef)} + {totalInt}";
            AttributeText[6].text = $"{totalAgi + state.Level + state.GetStat(CharacterStat.AddFlee)}";
            AttributeText[7].text = $"{(1 / state.AttackSpeed):F2}/sec";
            if(statPointsRequired == 0)
                AttributeText[8].text = $"{state.GetData(PlayerStat.StatPoints)}";
            else
                AttributeText[8].text = $"<color=blue>{state.GetData(PlayerStat.StatPoints)-statPointsRequired}";
            

        }
    }
}