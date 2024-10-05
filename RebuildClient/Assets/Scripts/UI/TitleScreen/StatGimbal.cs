using Assets.Scripts.Utility;
using UnityEngine;
using UnityEngine.UI;


namespace Assets.Scripts.UI.TitleScreen
{
    //yes I know technically it's not a gimbal. Well, maybe if you consider it rotating in 6 different angles?
    [ExecuteInEditMode]
    public class StatGimbal : Graphic
    {
        public int[] StatValues = new int[6];
        public Texture2D Texture;

        private CanvasRenderer cr;
        private Vector3[] verts = new Vector3[3];
        private Vector2[] uvs = new Vector2[3];

        public void Awake()
        {
            Refresh();
            cr = GetComponent<CanvasRenderer>();
            material = Canvas.GetDefaultCanvasMaterial();
        }

        public void SetActive(bool isActive)
        {
            Refresh();
        }

        public void Refresh()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            SetAllDirty();
        }

        protected override void UpdateMaterial()
        {
            if (cr == null)
                cr = GetComponent<CanvasRenderer>();
            
            base.UpdateMaterial();
            
            if(Texture != null)
                cr.SetTexture(Texture);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            uvs[0] = new Vector2(0, 0);
            uvs[2] = new Vector2(0.5f, 1f);
            uvs[1] = new Vector2(1, 0);
            
            for (var i = 0; i < 6; i++)
            {
                var curAngle = i * 60;
                var nextAngle = (i + 1) * 60;

                var stat = i;
                var nextStat = i + 1;
                if (nextStat >= 6) nextStat = 0;

                verts[0] = new Vector2(Mathf.Sin(curAngle * Mathf.Deg2Rad), Mathf.Cos(curAngle * Mathf.Deg2Rad)) * (StatValues[stat] / 9f);
                verts[1] = new Vector2(Mathf.Sin(nextAngle * Mathf.Deg2Rad), Mathf.Cos(nextAngle * Mathf.Deg2Rad)) * (StatValues[nextStat] / 9f);
                verts[2] = new Vector2(0, 0);

                for (var j = 0; j < 3; j++)
                    vh.AddVert((verts[j] * rectTransform.sizeDelta / 2f), color, uvs[j]);
            }

            for (var i = 0; i < 3 * 6; i += 3)
                vh.AddTriangle(i, i + 1, i + 2);
        }

        protected override void OnValidate()
        {
            SetVerticesDirty();
            SetMaterialDirty();
        }
    }
}