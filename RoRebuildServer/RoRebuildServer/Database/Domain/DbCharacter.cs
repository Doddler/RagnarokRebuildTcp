using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using RebuildSharedData.Config;
using RebuildSharedData.Enum;

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
    public byte[]? SkillData { get; set; }
    public byte[]? NpcFlags { get; set; }
    public byte[]? ItemData { get; set; }
}