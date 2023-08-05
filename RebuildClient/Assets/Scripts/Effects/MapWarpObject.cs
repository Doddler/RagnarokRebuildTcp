using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    class MapWarpObject : MonoBehaviour
    {
        public void Awake()
        {
            EffectHandlers.MapWarpEffect.StartWarp(gameObject);
            //MapWarpEffect.StartWarp(gameObject);
        }
    }
}
