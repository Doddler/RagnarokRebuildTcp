﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoRebuildServer.Database.Domain;

[Table("StorageInventory")]
public class StorageInventory
{
    [Key] public int Id { get; set; }
    public byte[] StorageData { get; set; }
    public int UncompressedSize { get; set; }
    public Guid CharacterId { get; set; }
    public DbCharacter Character { get; set; }
}