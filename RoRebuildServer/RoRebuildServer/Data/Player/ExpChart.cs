namespace RoRebuildServer.Data.Player;

public class ExpChart
{
    public required int[] ExpRequired;
    public required int[] JobExpRequired;

    public int RequiredJobExp(int jobId, int lvl)
    {
        if (lvl <= 0 || lvl >= 70)
            return -1;

        if (jobId == 0)
            return JobExpRequired[lvl];

        return JobExpRequired[JobExpRequired.Length / 2 + lvl];
    }
}