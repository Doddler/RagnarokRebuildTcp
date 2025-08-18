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
              2,   4,   6,   8,  10,  12,  14,  16,  18,  20,  22,  25,  28,  31,  34,  37,  40,  43,  46,  49,
             52,  56,  60,  64,  68,  72,  76,  80,  84,  88,  92,  97, 102, 107, 112, 117, 122, 127, 132, 137,
            142, 148, 154, 160, 166, 172, 178, 184, 190, 196, 202, 209, 216, 223, 230, 237, 244, 251, 258, 265,
            272, 280, 288, 296, 304, 312, 320, 328, 336, 344, 352, 361, 370, 379, 388, 397, 406, 415, 424, 433,
            442, 452, 462, 472, 482, 492, 502, 512, 522, 532, 542, 553, 564, 575, 586, 597, 608, 619, 630
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

        public void AddStat(int stat) => ChangeStatValue(stat, 1);
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
            AddStatText[index].text = bonus switch
            {
                > 0 => $"+{bonus}",
                < 0 => $"{bonus}",
                _ => ""
            };
            var cost = 2 + (stat + diff - 1) / 10;
            if (stat >= 99)
                StatPointCostText[index].text = "-";
            else
                StatPointCostText[index].text = cost.ToString();

            DecreaseStatButtons[index].interactable = diff > 0;
            IncreaseStatButtons[index].interactable =
                stat + diff < 99 && statPointsRequired + cost <= NetworkManager.Instance.PlayerState.GetData(PlayerStat.StatPoints);
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

            var softDef = totalVit * (100 + state.GetStat(CharacterStat.AddSoftDefPercent)) / 100;

            var changeSum = 0;
            for (var i = 0; i < 6; i++)
                changeSum += adjustValue[i] != 0 ? 1 : 0;

            ResetButton.interactable = changeSum != 0;
            ApplyButton.interactable = changeSum != 0;

            AttributeText[0].text = $"{state.GetStat(CharacterStat.Attack)} ~ {state.GetStat(CharacterStat.Attack2)}";
            AttributeText[1].text = $"{state.GetStat(CharacterStat.MagicAtkMin)} ~ {state.GetStat(CharacterStat.MagicAtkMax)}";
            AttributeText[2].text = $"{totalDex + state.Level + state.GetStat(CharacterStat.AddHit)}";
            AttributeText[3].text = $"{(1 + (totalLuk / 3) + state.GetStat(CharacterStat.AddCrit))}";
            AttributeText[4].text = $"{state.GetStat(CharacterStat.Def)} + {softDef}";
            AttributeText[5].text = $"{state.GetStat(CharacterStat.MDef)} + {totalInt}";
            AttributeText[6].text = $"{totalAgi + state.Level + state.GetStat(CharacterStat.AddFlee)} + {state.GetStat(CharacterStat.PerfectDodge)}";
            AttributeText[7].text = $"{(1 / state.AttackSpeed):F2}/sec";
            if (statPointsRequired == 0)
                AttributeText[8].text = $"{state.GetData(PlayerStat.StatPoints)}";
            else
                AttributeText[8].text = $"<color=blue>{state.GetData(PlayerStat.StatPoints) - statPointsRequired}";
        }
    }
}