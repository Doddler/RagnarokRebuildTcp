namespace RoRebuildServer.Simulation.Util;

public struct GameTimer
{
    private byte timerGen = 0;
    private float markedTime = 0;

    public GameTimer()
    {
    }

    public float Remaining
    {
        get
        {
            CheckTimerGen();
            return Time.RollingTimerTime - markedTime;
        }
    }

    private void CheckTimerGen()
    {
        if (timerGen != Time.RollingTimerGen)
        {
            markedTime -= Time.RolloverTime;
            timerGen = Time.RollingTimerGen;
        }
    }

    public void Set()
    {
        timerGen = Time.RollingTimerGen;
        markedTime = Time.RollingTimerTime;
    }

    public void Set(float time)
    {
        timerGen = Time.RollingTimerGen;
        markedTime = Time.RollingTimerTime + time;
    }

    public void Reset()
    {
        timerGen = 0;
        markedTime = 0;
    }

    public void Add(float time)
    {
        markedTime += time;
    }

    public bool Elapsed()
    {
        CheckTimerGen();
        return markedTime > Time.RollingTimerTime;
    }

    public bool Elapsed(float time)
    {
        CheckTimerGen();
        return markedTime + time > Time.RollingTimerTime;
    }
}