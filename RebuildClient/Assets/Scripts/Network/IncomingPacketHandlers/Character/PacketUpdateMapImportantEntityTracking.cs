using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using Assets.Scripts.PlayerControl;
using Assets.Scripts.UI.Hud;
using RebuildSharedData.Enum;
using RebuildSharedData.Networking;
using UnityEngine;

namespace Assets.Scripts.Network.IncomingPacketHandlers.Character
{
    [ClientPacketHandler(PacketType.UpdateMapImportantEntityTracking)]
    public class PacketUpdateMapImportantEntityTracking : ClientPacketHandlerBase
    {
        public override void ReceivePacket(ClientInboundMessage msg)
        {
            var count = msg.ReadInt16();
            for (var i = 0; i < count; i++)
            {
                var id = msg.ReadInt32();
                var pos = msg.ReadPosition();
                var type = (CharacterDisplayType)msg.ReadByte();

                if (id == NetworkManager.Instance.PlayerId)
                    continue;

                if (pos.x > 0 && pos.y > 0)
                {
                    if (type != CharacterDisplayType.Effect)
                        MinimapController.Instance.SetEntityPosition(id, type, pos);
                    else
                    {
                        var name = msg.ReadString();
                        if (Network.MapEffects.ContainsKey(name))
                            return; //already added
                        var go = GameObject.Instantiate(Resources.Load<GameObject>(name));
                        if (go == null)
                            Debug.LogError($"Could not load mapimportantentity effect by name of {name}");
                        else
                            NetworkManager.Instance.MapEffects[name] = go;
                    }
                }
                else
                {
                    if(type != CharacterDisplayType.Effect)
                        MinimapController.Instance.RemoveEntity(id);
                    else
                    {
                        var name = msg.ReadString();
                        if (NetworkManager.Instance.MapEffects.Remove(name, out var go))
                            GameObject.Destroy(go);
                    }
                }
            }
        }
    }
}