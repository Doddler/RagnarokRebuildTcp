using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.Enum;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;

namespace Assets.Scripts.Sprites
{
    public interface IRoSpriteRenderer
    {
        void SetAction(int action);
        void SetColor(Color color);
        void SetDirection(Direction direction);
        void SetFrame(int frame);
        void SetSprite(RoSpriteData sprite);
        void SetOffset(float offset);


        void Rebuild();
        void Initialize(bool makeCollider = false);
    }
}
