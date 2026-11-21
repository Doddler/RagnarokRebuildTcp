using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RebuildSharedData.Config;

namespace RoRebuildServer.Database.Domain
{
    [Table("Party")]
    public class DbParty
    {
        [Key] public int Id { get; set; }
        [MaxLength(SharedConfig.MaxPartyName)] public required string PartyName { get; set; }
        public virtual ICollection<DbCharacter> Characters { get; set; } = null!; //ef core won't give us back a null collection so we can suppress the nullability warning
    }
}