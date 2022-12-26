using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RebuildSharedData.Enum;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Sprites
{
    [ExecuteInEditMode]
    internal class RoSpriteRendererUI : MaskableGraphic, IRoSpriteRenderer
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

        public void SetAction(int action) => ActionId = action;
        public void SetColor(Color color) => Color = color;
        public void SetDirection(Direction direction) => Direction = direction;
        public void SetFrame(int frame) => CurrentFrame = frame;
        public void SetSprite(RoSpriteData sprite) => SpriteData = sprite;
        public void SetOffset(float offset) => SpriteOffset = offset;

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

            if (!isInitialized)
                return;
            
            var mesh = GetMeshForFrame();
            
            for(var i = 0; i < mesh.vertices.Length; i++)
                vh.AddVert(mesh.vertices[i] * rectTransform.sizeDelta, mesh.colors32[i], mesh.uv[i]);

            for(var i = 0; i < mesh.triangles.Length; i+=3)
                vh.AddTriangle(mesh.triangles[i], mesh.triangles[i+1], mesh.triangles[i+2]);

            SpriteData.Atlas.filterMode = FilterMode.Bilinear;

            Debug.Log(rectTransform.sizeDelta);

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
                throw new Exception("Attempting to double initialize UI sprite renderer!");
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

            Debug.Log("Initializing RoSpriteRendererUI!");
            
            meshCache = SpriteMeshCache.GetMeshCacheForSprite(SpriteData.Name);

            isInitialized = true;
        }

        public void Update()
        {
            if(!isInitialized && SpriteData != null)
                Initialize();
        }
    }
}
