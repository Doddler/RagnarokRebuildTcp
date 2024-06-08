using System;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts
{

    [Serializable]
    public class RoLayer
    {
        public Vector2 Position;
        public int Index;
        public bool IsMirror;
        public Vector2 Scale;
        public Color Color;
        public int Angle;
        public int Type;
        public int Width;
        public int Height;
    }

    [Serializable]
    public class RoPos
    {
        public Vector2 Position;
        public int Unknown1;
        public int Unknown2;
    }

    [Serializable]
    public class RoFrame
    {
        public RoLayer[] Layers;
        public RoPos[] Pos;
        public int Sound;
        public bool IsAttackFrame;
    }

    [Serializable]
    public class RoAction
    {
        public int Delay;
        public RoFrame[] Frames;
    }

    public enum FacingDirection
    {
        South,
        SouthWest,
        West,
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast,
    }

    public enum SpriteState
    {
        Idle,
        Walking,
        Standby,
        Sit,
        Dead
    }

    public enum SpriteType
    {
        Player,
        Head,
        Headgear,
        Monster,
        Monster2,
        Npc,
        ActionNpc,
        Pet
    }

    public enum SpriteMotion
    {
        Idle,
        Walk,
        Sit,
        PickUp,
        Attack1,
        Attack2,
        Attack3,
        Standby,
        Hit,
        Freeze1,
        Freeze2,
        Dead,
        Casting,
        Special,
        Performance1,
        Performance2,
        Performance3,
    }

    public static class RoAnimationHelper
    {
        private static float AngleDir(Vector2 targetDir, Vector2 up)
        {
            var dir = targetDir - up;
            var angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            if (angle < 0)
                angle += 360;
            return angle;
        }

        public static Vector2 FacingDirectionToVector(Direction facing)
        {
            switch (facing)
            {
                case Direction.South: return new Vector2(0, -1);
                case Direction.SouthWest: return new Vector2(-1, -1);
                case Direction.West: return new Vector2(-1, 0);
                case Direction.NorthWest: return new Vector2(-1, 1);
                case Direction.North: return new Vector2(0, 1);
                case Direction.NorthEast: return new Vector2(1, 1);
                case Direction.East: return new Vector2(1, 0);
                case Direction.SouthEast: return new Vector2(1, -1);
            }

            return Vector2.zero;
        }
        
        
        public static float FacingDirectionToRotation(Direction facing)
        {
            switch (facing)
            {
                case Direction.South: return 180f;
                case Direction.SouthWest: return 225f;
                case Direction.West: return 270f;
                case Direction.NorthWest: return 315f;
                case Direction.North: return 0f;
                case Direction.NorthEast: return 45f;
                case Direction.East: return 90f;
                case Direction.SouthEast: return 135f;
            }

            return 0;
        }
        
        public static Direction GetFacingForAngle(float angle)
        {
            if (angle > 360)
                angle -= 360;
            if (angle < 0)
                angle += 360;

            if (angle > 360f - 22.5f) return Direction.North;
            if (angle > 315f - 22.5f) return Direction.NorthWest;
            if (angle > 270f - 22.5f) return Direction.West;
            if (angle > 225f - 22.5f) return Direction.SouthWest;
            if (angle > 180f - 22.5f) return Direction.South;
            if (angle > 135f - 22.5f) return Direction.SouthEast;
            if (angle > 90f - 22.5f) return Direction.East;
            if (angle > 45f - 22.5f) return Direction.NorthEast;
            return Direction.North;
        }

        public static int GetSpriteIndexForAngle(Direction facing, float cameraRotation)
        {
            cameraRotation += 45f * (int)facing + (45f / 2f);
            if (cameraRotation > 360)
                cameraRotation -= 360;
            if (cameraRotation < 0)
                cameraRotation += 360;

            var index = Mathf.FloorToInt(cameraRotation / 45f);

            //Debug.Log($"a: {angle} i: {index}");


            return Mathf.Clamp(index, 0, 7); ;
        }

        public static int GetFourDirectionSpriteIndexForAngle(Direction facing, float cameraRotation)
        {
	        cameraRotation += 45f * (int) facing; // + (45f / 2f);
	        if (cameraRotation > 360)
		        cameraRotation -= 360;
	        if (cameraRotation < 0)
		        cameraRotation += 360;

	        var index = Mathf.FloorToInt(cameraRotation / 45f);

	        if (index > 7)
		        index = 0;
            
	        return Mathf.Clamp(index, 0, 7); ;
        }

        public static int GetSpriteIndexForAngle(FacingDirection facing, Vector3 position, Vector3 cameraPosition)
        {
            var targetDir = new Vector2(position.x, position.z) - new Vector2(cameraPosition.x, cameraPosition.z);
            var angle = -AngleDir(targetDir, Vector2.down);

            angle += 45f * (int) facing + (45f / 2f);
            if (angle > 360)
                angle -= 360;
            if (angle < 0)
                angle += 360;

            var index = Mathf.FloorToInt(angle / 45f);

            //Debug.Log($"a: {angle} i: {index}");
            

            return index;
        }
        
        public static bool IsFourDirectionAnimation(SpriteType type, SpriteMotion motion)
        {
	        if (type != SpriteType.Player)
		        return true;

	        switch (motion)
	        {
                case SpriteMotion.Idle:
                case SpriteMotion.Sit:
                case SpriteMotion.Walk:
	                return false;
	        }

	        return true;
        }

        public static int GetMotionIdForSprite(SpriteType type, SpriteMotion motion)
        {
            if (motion == SpriteMotion.Idle)
                return 0;

            if (type == SpriteType.ActionNpc)
            {
                switch (motion)
                {
                    case SpriteMotion.Walk: return 1 * 8;
                    case SpriteMotion.Hit: return 2 * 8;
                    case SpriteMotion.Attack1: return 3 * 8;
                }
            }

            if (type == SpriteType.Monster2)
            {
                if (motion == SpriteMotion.Attack2)
                    return 5 * 8;
            }

            if (type == SpriteType.Monster || type == SpriteType.Monster2 || type == SpriteType.Pet)
            {
                switch (motion)
                {
                    case SpriteMotion.Walk: return 1 * 8;
                    case SpriteMotion.Attack1: return 2 * 8;
                    case SpriteMotion.Attack2: return 2 * 8;
                    case SpriteMotion.Attack3: return 2 * 8;
                    case SpriteMotion.Hit: return 3 * 8;
                    case SpriteMotion.Dead: return 4 * 8;
                }
            }
            
            if (type == SpriteType.Pet)
            {
                switch (motion)
                {
                    case SpriteMotion.Special: return 5 * 8;
                    case SpriteMotion.Performance1: return 6 * 8;
                    case SpriteMotion.Performance2: return 7 * 8;
                    case SpriteMotion.Performance3: return 8 * 8;
                }
            }

            if (type == SpriteType.Player)
            {
                switch (motion)
                {
                    case SpriteMotion.Walk: return 1 * 8;
                    case SpriteMotion.Sit: return 2 * 8;
                    case SpriteMotion.PickUp: return 3 * 8;
                    case SpriteMotion.Standby: return 4 * 8;
                    case SpriteMotion.Attack1: return 11 * 8;
                    case SpriteMotion.Hit: return 6 * 8;
                    case SpriteMotion.Freeze1: return 7 * 8;
                    case SpriteMotion.Dead: return 8 * 8;
                    case SpriteMotion.Freeze2: return 9 * 8;
                    case SpriteMotion.Attack2: return 10 * 8;
                    case SpriteMotion.Attack3: return 11 * 8;
                    case SpriteMotion.Casting: return 12 * 8;
                }
            }

            return -1;
        }

        public static SpriteMotion GetMotionForState(SpriteState state)
        {
            switch (state)
            {
                case SpriteState.Idle:
                    return SpriteMotion.Idle;
                case SpriteState.Standby:
                    return SpriteMotion.Standby;
                case SpriteState.Walking:
                    return SpriteMotion.Walk;
                case SpriteState.Dead:
                    return SpriteMotion.Dead;
            }

            return SpriteMotion.Idle;
        }

        public static bool IsLoopingMotion(SpriteMotion motion)
        {
            switch (motion)
            {
                case SpriteMotion.Idle:
                case SpriteMotion.Sit:
                case SpriteMotion.Walk:
                case SpriteMotion.Casting:
                case SpriteMotion.Freeze1:
                case SpriteMotion.Freeze2:
                case SpriteMotion.Dead:
                    return true;
                case SpriteMotion.Attack1:
                case SpriteMotion.Attack2:
                case SpriteMotion.Attack3:
                case SpriteMotion.Hit:
                case SpriteMotion.PickUp:
                case SpriteMotion.Special:
                case SpriteMotion.Performance1:
                case SpriteMotion.Performance2:
                case SpriteMotion.Performance3:
                    return false;
            }

            return false;
        }

        public static bool Is8Direction(SpriteType type, SpriteMotion motion)
        {
            switch (type)
            {
                case SpriteType.Player:
                case SpriteType.Head:
                case SpriteType.Headgear:
                case SpriteType.Npc:
                {
                    switch (motion)
                    {
                        case SpriteMotion.Idle:
                        case SpriteMotion.Sit:
                        case SpriteMotion.Walk:
                            return true;
                    }                    
                }
                break;
            }
            
            return false;
        }
        
    }

    public class RoSpriteData : ScriptableObject
    {
        public string Name;
        public SpriteType Type;
        public RoAction[] Actions;
        public Sprite[] Sprites;
        public Vector2Int[] SpriteSizes;
        public Texture2D Atlas;
        public int Size;
        public float AverageWidth;
        public AudioClip[] Sounds;
        public int AttackFrameTime;
    }
}
