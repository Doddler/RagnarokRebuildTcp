using RebuildSharedData.Enum;

namespace Assets.Scripts.PlayerControl
{
    public interface IEntityActionHandler
    {
        void ChangeCharacterState(CharacterState state);
        
    }
}