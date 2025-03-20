using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace RoRebuildServer.Database.Domain;

[Table("CartInventory")]
public class CartInventory
{
    [Key] public int Id { get; set; }
    public required byte[] StorageData { get; set; }
    public int UncompressedSize { get; set; }
    public int AccountId { get; set; }
    public RoUserAccount Account { get; set; } = null!; //auto populated via AccountId
}