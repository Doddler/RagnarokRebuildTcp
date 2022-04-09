namespace RoRebuildServer.Database;

public interface IDbRequest
{
    public Task ExecuteAsync(RoContext dbContext);
}