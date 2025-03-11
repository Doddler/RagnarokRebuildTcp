using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.ConfigWindow;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Network
{
    [ClientPacketHandler(PacketType.RemoveEntity)]
    public class PacketRemoveEntity : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var id = msg.ReadInt32();
            var reason = (CharacterRemovalReason)msg.ReadByte();

            if (!Network.EntityList.TryGetValue(id, out var controllable))
            {
                Debug.LogWarning("Trying to remove entity " + id + ", but it does not exist in scene!");
                return;
            }

            if (id == State.EntityId)
            {
                //Debug.Log("We're removing the player object! Hopefully the server knows what it's doing. We're just going to pretend we didn't see it.");
                //return;

                GameConfig.SaveConfig();
                Debug.LogWarning("Whoa! Trying to delete player object. Is that right...?");
                Camera.Target = null;
                Network.ClearGroundItemList();
            }

            Network.EntityList.Remove(id);

            if (Camera.SelectedTarget == controllable)
                Camera.ClearSelected();

            Network.EntityLastSeenTime[controllable.Id] = Time.timeSinceLevelLoad;

            switch (reason)
            {
                case CharacterRemovalReason.Dead when controllable.SpriteAnimator.Type != SpriteType.Player:
                    controllable.MonsterDie();
                    break;
                case CharacterRemovalReason.Dead:
                    controllable.FadeOutAndVanish(0.1f);
                    break;
                case CharacterRemovalReason.Disconnect:
                    ExitEffect.LaunchExitAtLocation(controllable.transform.localPosition);
                    controllable.AbortActiveWalk();
                    controllable.FadeOutAndVanish(0.3f);
                    break;
                case CharacterRemovalReason.Teleport:
                    controllable.AbortActiveWalk();
                    if (controllable.IsMainCharacter)
                        controllable.FadeOutAndVanish(0.1f);
                    else
                    {
                        TeleportEffect.LaunchTeleportAtLocation(controllable.transform.localPosition);
                        controllable.FadeOutAndVanish(0.3f);
                    }
                    break;
                case CharacterRemovalReason.Metamorphosis:
                    if (controllable.SpriteAnimator != null
                        && ClientDataLoader.Instance.TryGetMetamorphResult(controllable.SpriteAnimator.SpriteData.Name, out var result))
                    {
                        if (result.RemoveType == "FadeOut")
                            controllable.FadeOutAndVanish(2f);
                        else if (result.RemoveType == "Immediate")
                            controllable.FadeOutAndVanish(0.2f);
                        else
                            controllable.MonsterDie();
                    }
                    else
                    {
                        if (controllable.CharacterType == CharacterType.Monster)
                            controllable.MonsterDie();
                        else
                            controllable.FadeOutAndVanish(0.1f);
                    }
                    break;
                default:
                    controllable.FadeOutAndVanish(0.1f);
                    break;
            }
        }
    }
}