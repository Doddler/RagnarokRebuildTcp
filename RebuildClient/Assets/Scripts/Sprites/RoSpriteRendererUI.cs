using System;
using System.Collections.Generic;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sprites
{
    [ExecuteInEditMode]
    public class RoSpriteRendererUI : MaskableGraphic, IRoSpriteRenderer
    {
        public int ActionId;
        public int CurrentFrame;
        public Color Color = Color.white;
        public Direction Direction;
        public float SpriteOffset;
        public RoSpriteData SpriteData;

        public Vector2 OffsetPosition;
        
        private Dictionary<int, Mesh> meshCache;
        
        private bool isInitialized;
        private bool isEnabled;

        public void SetAction(int action, bool is8Direction) => ActionId = action;
        public void SetColor(Color color) => Color = color;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetAngle(float angle) => Direction = RoAnimationHelper.GetFacingForAngle(angle);

        public void SetFrame(int frame) => CurrentFrame = frame;
        public void SetSprite(RoSpriteData sprite) => SpriteData = sprite;
        public void SetOffset(float offset) => SpriteOffset = offset;
        public RoFrame GetActiveRendererFrame()
        {
            var actions = SpriteData.Actions[ActionId + (int)Direction];
            var frame = CurrentFrame;

            if (frame >= actions.Frames.Length)
                frame = actions.Frames.Length - 1;

            return actions.Frames[frame];
        }
        
        public bool UpdateRenderer()
        {
            return false;
        }

        public void SetActive(bool isActive)
        {
            isEnabled = isActive;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void SetOverrideMaterial(Material mat)
        {
            throw new NotImplementedException();
        }

        public void SetLightProbeAnchor(GameObject anchor)
        {
            throw new NotImplementedException();
        }

        public override Texture mainTexture => SpriteData.Atlas;
        
        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!isInitialized || !isEnabled)
                return;
            
            var mesh = GetMeshForFrame();

            if (mesh == null)
                return; // this happens when leaving play mode so we won't report on it. Hopefully won't bite us.
            
            for(var i = 0; i < mesh.vertices.Length; i++)
                vh.AddVert(((Vector2)mesh.vertices[i] + OffsetPosition / 50f) * rectTransform.sizeDelta, mesh.colors32[i], mesh.uv[i]);

            for(var i = 0; i < mesh.triangles.Length; i+=3)
                vh.AddTriangle(mesh.triangles[i], mesh.triangles[i+1], mesh.triangles[i+2]);

            SpriteData.Atlas.filterMode = FilterMode.Bilinear;

            //Debug.Log(rectTransform.sizeDelta);

            //Debug.Log($"We're populating a mesh with {vh.currentVertCount} verts {vh.currentIndexCount} tris");
        }

        public void Rebuild()
        {
            if (!VerifyStatus()) return;

            SetVerticesDirty();
            SetMaterialDirty();
        }

        public bool VerifyStatus(bool verifyInitialized = true)
        {
            if (!isInitialized && verifyInitialized)
                throw new Exception("UI sprite renderer is not initialized! (but it should be)");
            if (gameObject == null)
                throw new Exception("Attempting to initialize UI sprite renderer while it's gameobject is invalid!");
            if (SpriteData == null)
                throw new Exception("Attempting to initialize UI sprite renderer without sprite data set!");

            return true;
        }
        
        public Mesh GetMeshForFrame()
        {
            //just a simple hash
            var id = ((ActionId + (int)Direction) << 16) + CurrentFrame;
            
            if (meshCache == null)
                meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);

            if (meshCache.TryGetValue(id, out var mesh))
                return mesh;

            var newMesh = SpriteMeshBuilder.BuildSpriteMesh(SpriteData, ActionId, (int)Direction, CurrentFrame);

            meshCache.Add(id, newMesh);

            return newMesh;
        }

        public void Initialize(bool makeCollider = false)
        {
            if (!VerifyStatus(false)) return;

            //Debug.Log("Initializing RoSpriteRendererUI!");
            
            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);

            isInitialized = true;
            isEnabled = true;


            SetVerticesDirty();
            SetMaterialDirty();
        }

        public void Update()
        {
            if(!isInitialized && SpriteData != null)
                Initialize();
        }
    }
}
