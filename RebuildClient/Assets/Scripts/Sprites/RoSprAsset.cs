using UnityEngine;

namespace Sprites
{
    public class RoSprAsset : ScriptableObject
    {
        private int instanceID;
        public int InstanceID
        {
            get
            {
                if (instanceID == 0)
                    instanceID = GetInstanceID();
                return instanceID;
            }
        }
        private int hashCode;
        public int HashCode => InstanceID.GetHashCode();

        [SerializeField] private new string name;
        [SerializeField] private Texture2D sprite;
        [SerializeField] private Texture2D palette;
    }
}