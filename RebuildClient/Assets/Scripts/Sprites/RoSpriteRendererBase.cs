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
    public abstract class RoSpriteRendererBase : MonoBehaviour
    {
        public int ActionId;
        public int CurrentFrame;
        public Color Color;
        public Direction Direction;
        public float SpriteOffset;
        public RoSpriteData SpriteData;
        
        public void SetAction(int action) => ActionId = action;
        public void SetColor(Color color) => Color = color;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetFrame(int frame) => CurrentFrame = frame;
        public void SetSprite(RoSpriteData sprite) => SpriteData = sprite;
        public void SetOffset(float offset) => SpriteOffset = offset;

        public virtual void Rebuild() { throw new NotImplementedException(); }
        public virtual void Initialize(bool makeCollider = false) { }
    }
}
