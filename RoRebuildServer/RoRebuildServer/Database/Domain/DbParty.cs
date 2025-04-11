using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoRebuildServer.Database.Domain
{
    [Table("Party")]
    public class DbParty
    {
        [Key] public int Id { get; set; }
        public string PartyName { get; set; }
        public virtual ICollection<DbCharacter> Characters { get; set; }
    }
}
