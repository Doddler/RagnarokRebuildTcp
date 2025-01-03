using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RebuildSharedData.Config;
using RebuildSharedData.Enum;

namespace RoRebuildServer.Database.Domain;

[Table("Character")]
public class DbCharacter
{
    [Key] public Guid Id { get; set; }
    [MaxLength(SharedConfig.MaxPlayerName, ErrorMessage = "Player name cannot exceed 32 characters in length")]
    public string Name { get; set; } = null!;
    [MaxLength(32)]
    public string? Map { get; set; }
    public int CharacterSlot { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public DbSavePoint? SavePoint { get; set; }
    public byte[]? CharacterSummary { get; set; }
    public byte[]? Data { get; set; }
    public byte[]? SkillData { get; set; }
    public byte[]? NpcFlags { get; set; }
    public int NpcFlagsLength { get; set; }
    public byte[]? ItemData { get; set; }
    public StorageInventory? KafraStorage { get; set; }
    public int ItemDataLength { get; set; }
    public RoUserAccount Account { get; set; } = null!;
    public int AccountId { get; set; }
    public int VersionFormat { get; set; }
    public static int CurrentVersion = 1;
}