using System;
using System.Net;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
// using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Assets.Scripts.Effects
{
    public class RagnarokPrimitive : MonoBehaviour
    {
        private MeshBuilder mb;
        private MeshRenderer mr;
        private MeshFilter mf;
        private Mesh mesh;
        private BillboardObject billboard;
        
        public Material Material;
        public UnityEngine.Object disposableComponent; 
        
        public Ragnarok3dEffect Effect;
        public System.Object PrimitiveData;
        public T GetPrimitiveData<T>() => (T)PrimitiveData;

        public PrimitiveType PrimitiveType;
        public IPrimitiveHandler PrimitiveHandler;
        public RagnarokEffectData.PrimitiveUpdateDelegate UpdateHandler;
        public RagnarokEffectData.PrimitiveRenderDelegate RenderHandler;
        public Func<RagnarokPrimitive, bool> EventTrigger;

        public EffectPart[] Parts;
        public EffectSegment[] Segments;
        public int PartsCount;
        public int SegmentCount;
        public int[] Flags = new int[4];

        public float DelayTime;
        public float Duration;
        public int FrameDuration;
        public float CurrentPos;
        public float PrevPos;
        public int Step;
        public bool IsStepFrame;
        public bool SkipClearingMeshBuilder = false;
        public bool SkipApplyingMeshFromBuilder = false;

        public float CurrentFrameTime => IsStepFrame ? 1 / 60f : 0f;

        public Vector3 Velocity;
        
        public bool IsActive = false;
        public bool IsDirty = false;
        public bool IsInit = false;
        public bool HasFiredEvent = false;

        #if DEBUG
        public string DebugString;
        #endif
        
        //these are temporary to be fed into the meshbuilder
        private Vector3[] verts = new Vector3[4];
        private Vector3[] normals = new Vector3[4];
        private Color32[] colors = new Color32[4];
        private Vector2[] uvs = new Vector2[4];
        private Vector3[] uv3s = new Vector3[4];
        private int[] tris;

        public Mesh GetMesh() => mesh;

        public void SetMesh(Mesh updatedMesh)
        {
            mesh = updatedMesh;
            mf.sharedMesh = updatedMesh;
        }

        public static RagnarokPrimitive Create()
        {
            var go = new GameObject("Primitive");
            var p = go.AddComponent<RagnarokPrimitive>();
            return p;
        }

        public void SetFrame(int frameNum)
        {
            Step = frameNum;
            CurrentPos = Step * (1 / 60f);
        }
        
        public void Reset()
        {
            Duration = 0;
            FrameDuration = 0;
            CurrentPos = 0;
            PrevPos = 0;
            Step = 0;
            DelayTime = 0;
            IsActive = false;
            IsDirty = false;
            IsInit = false;
            HasFiredEvent = false;
            Effect = null;
            if(PrimitiveData != null && PrimitiveData is IResettable)
                EffectPool.ReturnData(PrimitiveData, PrimitiveType);
            PrimitiveData = null;
            UpdateHandler = null;
            RenderHandler = null;
            PrimitiveHandler = null;
            Material = null;
            if (mr)
            {
                mr.material = null;
                mf.sharedMesh = null;
            }

            if(Parts != null)
                EffectPool.ReturnParts(Parts);
            if(Segments != null)
                EffectPool.ReturnSegments(Segments);
            if(mesh != null)
                EffectPool.ReturnMesh(mesh);
            if(mb != null)
                EffectPool.ReturnMeshBuilder(mb);
            if(disposableComponent)
                Destroy(disposableComponent);
            disposableComponent = null;
            Parts = null;
            Segments = null;
            mesh = null;
            mb = null;
            PartsCount = 0;
            SegmentCount = 0;
            if (billboard != null)
                billboard.Style = BillboardStyle.None;
            Velocity = Vector3.zero;
            EventTrigger = null;
            SkipApplyingMeshFromBuilder = false;
            SkipClearingMeshBuilder = false;
            for (var i = 0; i < 4; i++)
                Flags[i] = 0;
            #if DEBUG
            DebugString = "";
            #endif
        }

        public void SetBillboardMode(BillboardStyle style)
        {
            if (billboard == null)
                billboard = gameObject.AddComponent<BillboardObject>();
            billboard.Style = style;
        }

        public void SetBillboardAxis(Vector3 axis) => billboard.Axis = axis;
        public void SetBillboardSubRotation(Quaternion subRotation) => billboard.SubRotation = subRotation;

        public void EndPrimitive()
        {
            IsActive = false;
            mr.enabled = false;
        }

        public void CreateParts(int count)
        {
            Parts = EffectPool.BorrowParts(count);    
            PartsCount = count;
        }

        public void CreateSegments(int count)
        {
            Segments = EffectPool.BorrowSegments(count);
            SegmentCount = count;
        }

        public void SetDisposableComponent(UnityEngine.Object component)
        {
            disposableComponent = component;
        }

        public void Prepare(Ragnarok3dEffect effect, PrimitiveType type, Material material, float duration)
        {
            Effect = effect;
            PrimitiveType = type;
            Duration = duration;
            FrameDuration = Mathf.FloorToInt(duration * 60f);
            PartsCount = 0;

            if (PrimitiveHandler != null)
            {
                UpdateHandler = PrimitiveHandler.GetDefaultUpdateHandler();
                RenderHandler = PrimitiveHandler.GetDefaultRenderHandler();
            }

            if (mb == null)
                mb = EffectPool.BorrowMeshBuilder();
            if (mesh == null)
                mesh = EffectPool.BorrowMesh();
            if (mr == null)
            {
                mr = gameObject.AddComponent<MeshRenderer>();
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }

            if (mf == null)
                mf = gameObject.AddComponent<MeshFilter>();

            Material = material;
            mr.material = Material;
            mr.enabled = true;
            mf.sharedMesh = mesh;


            PrimitiveData = EffectPool.BorrowData(PrimitiveType); //RagnarokEffectData.NewPrimitiveData(PrimitiveType);

            IsActive = true;
            IsDirty = true;
        }

        public void AddTriangle(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 uv1,
            Vector3 uv2, Vector3 uv3, Color32 c, float scale = 1)
        {

            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;

            verts[0] = vert1 * scale;
            verts[1] = vert2 * scale;
            verts[2] = vert3 * scale;
            verts[3] = Vector3.zero;

            uvs[0] = uv1;
            uvs[1] = uv2;
            uvs[2] = uv3;
            uvs[3] = Vector2.zero;

            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;

            if (tris == null)
                tris = new[] { 2, 1, 0 };
            
            mb.AddFullTriangle(verts, normals, uvs, colors, tris );
        }
        
        public void AddTexturedRectangleQuad(Vector3 offset, float width, float height, Color32 c)
        {
            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;
            
            verts[0] = new Vector3(-width,0, height);
            verts[1] = new Vector3(width, 0,height);
            verts[2] = new Vector3(-width, 0,-height);
            verts[3] = new Vector3(width, 0,-height);
            
            uvs[0] = new Vector2(0, 1);
            uvs[1] = new Vector2(1, 1);
            uvs[2] = new Vector2(0, 0);
            uvs[3] = new Vector2(1, 0);
            
            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;
            
            mb.AddQuad(verts, normals, uvs, colors);
        }
        
        public void AddTexturedBillboardQuad(Vector3 offset, float width, float height, Color32 c)
        {
            var worldPos = transform.position + offset;
            
            var lookAt = worldPos - CameraFollower.Instance.transform.position;
            var rotation = Quaternion.LookRotation(lookAt, Vector3.up);
            
            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;
            
            verts[0] = rotation * new Vector3(-width,0, height) + offset;
            verts[1] = rotation * new Vector3(width, 0,height) + offset;
            verts[2] = rotation * new Vector3(-width, 0,-height) + offset;
            verts[3] = rotation * new Vector3(width, 0,-height) + offset;
            
            uvs[0] = new Vector2(0, 1);
            uvs[1] = new Vector2(1, 1);
            uvs[2] = new Vector2(0, 0);
            uvs[3] = new Vector2(1, 0);
            
            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;
            
            mb.AddQuad(verts, normals, uvs, colors);
        }


        public void AddTexturedBillboardSprite(Sprite sprite, Vector3 offset, float width, float height, Color32 c)
        {
            var rotation = CameraFollower.Instance.transform.rotation * Quaternion.Inverse(transform.rotation);

            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;

            verts[0] = rotation * new Vector3(-width, height) + offset;
            verts[1] = rotation * new Vector3(width, height) + offset;
            verts[2] = rotation * new Vector3(-width, -height) + offset;
            verts[3] = rotation * new Vector3(width, -height) + offset;

            var spriteUVs = sprite.uv;

            uvs[0] = spriteUVs[0];
            uvs[1] = spriteUVs[1];
            uvs[2] = spriteUVs[2];
            uvs[3] = spriteUVs[3];

            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;

            mb.AddQuad(verts, normals, uvs, colors);
        }

        public void AddTexturedBillboardSpriteWithAngle(Sprite sprite, Vector3 offset, float width, float height, float angle, Color32 c)
            {
                var rotation = CameraFollower.Instance.transform.rotation * Quaternion.Inverse(transform.rotation);
            
                colors[0] = c;
                colors[1] = c;
                colors[2] = c;
                colors[3] = c;

                angle *= Mathf.Deg2Rad;
                    
                verts[0] = rotation * (Vector3)VectorHelper.Rotate(new Vector2(-width, height), angle) + offset;
                verts[1] = rotation * (Vector3)VectorHelper.Rotate(new Vector2(width, height), angle) + offset;
                verts[2] = rotation * (Vector3)VectorHelper.Rotate(new Vector2(-width,-height), angle) + offset;
                verts[3] = rotation * (Vector3)VectorHelper.Rotate(new Vector2(width, -height), angle) + offset;
            
                var spriteUVs = sprite.uv;
            
                uvs[0] = spriteUVs[0];
                uvs[1] = spriteUVs[1];
                uvs[2] = spriteUVs[2];
                uvs[3] = spriteUVs[3];
            
                //completely unused really
                normals[0] = Vector3.up;
                normals[1] = Vector3.up;
                normals[2] = Vector3.up;
                normals[3] = Vector3.up;
            
                mb.AddQuad(verts, normals, uvs, colors);
            }
        
        
        public void AddTextured2DQuad(Vector3 offset, float width, float height, Color32 c)
        {
            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;
            
            verts[0] = new Vector3(-width, height) + offset;
            verts[1] = new Vector3(width, height) + offset;
            verts[2] = new Vector3(-width, -height) + offset;
            verts[3] = new Vector3(width, -height) + offset;

            uvs[0] = new Vector2(0, 1);
            uvs[1] = new Vector2(1, 1);
            uvs[2] = new Vector2(0, 0);
            uvs[3] = new Vector2(1, 0);
            
            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;
            
            mb.AddQuad(verts, normals, uvs, colors);
        }
        
        public void AddTexturedSpriteQuad(Sprite sprite, Vector3 offset, float width, float height, Color32 c)
        {
            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;
            
            verts[0] = new Vector3(-width, height) + offset;
            verts[1] = new Vector3(width, height) + offset;
            verts[2] = new Vector3(-width, -height) + offset;
            verts[3] = new Vector3(width, -height) + offset;

            var spriteUVs = sprite.uv;
            var rect = sprite.textureRect;
            
            uvs[0] = spriteUVs[0];
            uvs[1] = spriteUVs[1];
            uvs[2] = spriteUVs[2];
            uvs[3] = spriteUVs[3];
            
            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;
            
            mb.AddQuad(verts, normals, uvs, colors);
        }
        
        public void AddTexturedPerspectiveQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, Vector3 uv1,
            Vector3 uv2, Vector3 uv3, Vector3 uv4, Color32 c, float scale = 1)
        {
            //we get the uvs for a vertical slice for the position pos out of the number of parts used

            colors[3] = c;
            colors[2] = c;
            colors[1] = c;
            colors[0] = c;

            verts[3] = vert1 * scale;
            verts[2] = vert2 * scale;
            verts[1] = vert3 * scale;
            verts[0] = vert4 * scale;

            uv3s[3] = uv1;
            uv3s[2] = uv2;
            uv3s[1] = uv3;
            uv3s[0] = uv4;

            //completely unused really
            normals[3] = Vector3.up;
            normals[2] = Vector3.up;
            normals[1] = Vector3.up;
            normals[0] = Vector3.up;

            //Debug.Log(uv1 + " + " + uv2);

            mb.AddPerspectiveQuad(verts, normals, uv3s, colors);
        }

        
        public void AddTexturedSliceQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, int pos,
            int sliceCount, Color32 c, float scale = 1)
        {
            var uv1 = (pos - 1) / (float)sliceCount;
            var uv2 = pos / (float)sliceCount;

            AddTexturedQuad(vert1, vert2, vert3, vert4, new Vector2(uv1, 1), new Vector2(uv2, 1), new Vector2(uv1, 0),
                new Vector2(uv2, 0), c, scale);
        }


        public void AddTexturedQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, Color c, float scale = 1)
        {
            AddTexturedQuad(vert1, vert2, vert3, vert4, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0),
                new Vector2(1, 0), c, scale);
        }

        public void AddTexturedQuad(Vector3 vert1, Vector3 vert2, Vector3 vert3, Vector3 vert4, Vector2 uv1,
            Vector2 uv2, Vector2 uv3, Vector2 uv4, Color32 c, float scale = 1)
        {
            //we get the uvs for a vertical slice for the position pos out of the number of parts used

            colors[0] = c;
            colors[1] = c;
            colors[2] = c;
            colors[3] = c;

            verts[0] = vert1 * scale;
            verts[1] = vert2 * scale;
            verts[2] = vert3 * scale;
            verts[3] = vert4 * scale;

            uvs[0] = uv1;
            uvs[1] = uv2;
            uvs[2] = uv3;
            uvs[3] = uv4;

            //completely unused really
            normals[0] = Vector3.up;
            normals[1] = Vector3.up;
            normals[2] = Vector3.up;
            normals[3] = Vector3.up;

            //Debug.Log(uv1 + " + " + uv2);

            mb.AddQuad(verts, normals, uvs, colors);
        }
        
        public bool UpdatePrimitive()
        {
            if (!IsActive)
                return false;
            
            if(!IsInit && PrimitiveHandler != null)
                PrimitiveHandler.Init(this);
            IsInit = true;

            DelayTime -= Time.deltaTime;
            if (DelayTime > 0)
                return IsActive;

            PrevPos = CurrentPos;
            CurrentPos += Time.deltaTime;
            if (Mathf.RoundToInt(CurrentPos / (1 / 60f)) > Step)
            {
                Step++; //only advance once per frame.
                IsStepFrame = true;
            }
            else
                IsStepFrame = false;

            if (Step > int.MaxValue / 2)
                Step = 0; //safety in case they are on screen for a very, very long time

            if (UpdateHandler != null)
                UpdateHandler(this);
            
            if (!IsActive)
                mr.enabled = false;
            
            transform.position += Velocity * Time.deltaTime;

            if (EventTrigger != null && !HasFiredEvent)
            {
                if (EventTrigger(this))
                {
                    HasFiredEvent = true;
                    Effect.EffectHandler.OnEvent(Effect, this);
                }
            }

            if(Duration > 0)
                return IsActive && CurrentPos < Duration;
            
            return IsActive;
        }

        public void RenderPrimitive()
        {
            mr.enabled = IsActive;
            if (!IsActive)
                return;
            
            if(!SkipClearingMeshBuilder)
                mb.Clear();
            
            if (RenderHandler != null)
            {
                RenderHandler(this, mb);
                if (SkipApplyingMeshFromBuilder)
                    return;
                
                if (mb.HasMesh())
                {
                    var m = mb.ApplyToMesh(mesh);
                    // Debug.Log(m.colors32[0]);
                    mf.sharedMesh = m;
                }
                else
                    mr.enabled = false;
            }
        }
        
    }
}