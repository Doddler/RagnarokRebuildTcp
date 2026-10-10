using UnityEngine;

namespace Assets.Scripts.UI.ClientDatabase
{
    [DisallowMultipleComponent]
    public sealed class ClientDatabasePooledObject : MonoBehaviour
    {
        public GameObject Template { get; private set; }

        public void Initialize(GameObject template)
        {
            Template = template;
        }
    }
}
