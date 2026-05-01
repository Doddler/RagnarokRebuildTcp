using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoRebuildServer.Database.Domain
{
    [Table("Party")]
    public class DbParty
    {
        [Key] public int Id { get; set; }
        [MaxLength(64)] public required string PartyName { get; set; }
        public virtual ICollection<DbCharacter> Characters { get; set; } = null!; //ef core won't give us back a null collection so we can suppress the nullability warning
    }
}