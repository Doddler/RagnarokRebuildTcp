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
            MapWarpEffect.StartWarp(gameObject);
        }
    }
}
