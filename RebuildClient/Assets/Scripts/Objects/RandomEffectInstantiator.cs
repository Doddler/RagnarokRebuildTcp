using System.Collections.Generic;
using Assets.Scripts.Objects;
using UnityEngine;

namespace Objects
{
    public class RandomEffectInstantiator : MonoBehaviour
    {
        public List<GameObject> PrefabList;

        public bool IsLoop;
        public bool UseZTest;
        public bool RandomStart;
        public float LoopDelay;

        public void Awake()
        {
            if (PrefabList != null)
            {
                var rnd = Random.Range(0, PrefabList.Count);
                var go = Instantiate(PrefabList[rnd]);
                go.transform.position = transform.position;
                go.transform.rotation = transform.rotation;
                
                
                var renderer = go.GetComponent<RoEffectRenderer>();
                if (renderer != null)
                {
                    renderer.IsLoop = IsLoop;
                    renderer.UseZTest = UseZTest;
                    renderer.RandomStart = RandomStart;
                    renderer.LoopDelay = LoopDelay;
                    renderer.Initialize(renderer.Anim);
                }
            }
        }
    }
}