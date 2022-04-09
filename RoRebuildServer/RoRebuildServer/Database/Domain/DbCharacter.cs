using System.ComponentModel.DataAnnotations.Schema;

namespace RoRebuildServer.Database.Domain;

[Table("Character")]
public class DbCharacter
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public byte[]? Data { get; set; }
}