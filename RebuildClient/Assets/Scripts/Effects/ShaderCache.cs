using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    public class ShaderCache : MonoBehaviour
    {
        private static ShaderCache instance;
        public static ShaderCache Instance
        {
            get
            {
                if (instance == null)
                    instance = GameObject.FindObjectOfType<ShaderCache>();

                return instance;
            }
        }

        public Shader SpriteShader;
        public Shader SpriteShaderNoZWrite;
        public Shader SpritePerspectiveShader;
        public Shader AlphaBlendParticleShader;
        public Shader InvAlphaShader;
        public Shader AdditiveShader;
        public Shader WaterShader;
        public Shader PerspectiveAlphaShader;

        public bool BillboardSprites = true;
    }
}
