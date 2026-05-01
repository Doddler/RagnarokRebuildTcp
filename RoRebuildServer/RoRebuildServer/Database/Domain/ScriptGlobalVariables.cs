using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RoRebuildServer.Database.Domain;

[Table("ScriptGlobalVar")]
public class ScriptGlobalVar
{
    [Key] [MaxLength(30)] public string VariableName { get; set; }
    public int IntValue { get; set; }
    [MaxLength(3000)] public string? StringValue { get; set; }
}