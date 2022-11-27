using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RebuildSharedData.Config;

namespace RoRebuildServer.Database.Domain;

[Table("Character")]
public class DbCharacter
{
    public Guid Id { get; set; }
    [MaxLength(SharedConfig.MaxPlayerName, ErrorMessage = "Player name cannot exceed 32 characters in length")]
    public string Name { get; set; } = null!;
    public string? Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public DbSavePoint? SavePoint { get; set; }
    public byte[]? Data { get; set; }
}