using System;
using RebuildSharedData.Enum.EntityStats;

namespace RoRebuildServer.Data.Player;

public class ElementChart
{
    private int[][] chart;

    public ElementChart(int[][] newChart)
    {
        chart = newChart;
    }
    
    public int GetAttackModifier(AttackElement attack, CharacterElement defense)
    {
        return chart[(int)defense][(int)attack];
    }
}