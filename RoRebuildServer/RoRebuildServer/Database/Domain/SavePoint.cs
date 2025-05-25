using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace RoRebuildServer.Database.Domain
{
    //owned classes means this will be flattened into any database object that uses it
    [Owned]
    public class DbSavePoint
    {
        [MaxLength(64)]
        public string? MapName { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Area { get; set; }
    }
}
