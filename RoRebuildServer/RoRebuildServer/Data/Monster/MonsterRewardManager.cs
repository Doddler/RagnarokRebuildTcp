namespace RoRebuildServer.Data.Monster;
using EntityComponents;

public static class MonsterRewardManager
{
    public delegate void DistributeExperienceEvent(Monster monster, Player player, ref int baseExp, ref int jobExp);

    public delegate void OnKillMonster(Monster monster);

    private static event DistributeExperienceEvent OnDistributeExperienceEvent;
    private static event OnKillMonster OnKillMonsterEvent;

    public static void RegisterKillMonsterEvent(OnKillMonster e)
    {
        OnKillMonsterEvent -= e;
        OnKillMonsterEvent += e;
    }

    public static void RegisterDistributeExperienceEvent(DistributeExperienceEvent e)
    {
        OnDistributeExperienceEvent -= e;
        OnDistributeExperienceEvent += e;
    }

    public static void TriggerOnDistributeExperienceEvent(Monster monster, Player player, ref int baseExp, ref int jobExp)
    {
        OnDistributeExperienceEvent?.Invoke(monster, player, ref baseExp, ref jobExp);
    }

    public static void TriggerOnKillMonsterEvent(Monster monster)
    {
        OnKillMonsterEvent?.Invoke(monster);
    }
}
