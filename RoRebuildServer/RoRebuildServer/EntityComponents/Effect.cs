using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.EntityComponents;

[EntityComponent(EntityType.Effect)]
public class Effect : IEntityAutoReset
{
    public Entity Owner { get; set; } = Entity.Null;
    public AreaOfEffect? AreaOfEffect { get; set; }
 
    public void Reset()
    {
        AreaOfEffect = null;
    }
}