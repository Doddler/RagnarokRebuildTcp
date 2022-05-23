using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assets.Scripts.Utility;
using UnityEngine;

namespace Assets.Scripts.Effects
{
    class PrimitiveCylinderEffect : PrimitiveBaseEffect
    {
        private Vector3[] verts = new Vector3[4];
        private Vector3[] normals = new Vector3[4];
        private Color[] colors = new Color[4];
        private Vector2[] uvs = new Vector2[4];

        public static PrimitiveCylinderEffect LaunchEffect(GameObject go, Material mat, int partCount, float duration)
        {
            var prim = go.AddComponent<PrimitiveCylinderEffect>();
            prim.Init(partCount, mat);
            prim.Duration = duration;

            return prim;
        }
        
        //used in main elemental casting
        public void Update3DCasting()
        {
            var mid = (EffectPart.PartCount - 1) / 2;
            var m2 = 90 / mid;

            for (var ec = 0; ec < 4; ec++)
            {
                //if (Mathf.Approximately(size, 3f) && ec < 3)
                //    ec = 3;

                var p = Parts[ec];

                if (p.Active)
                {
                    if (ec < 3)
                        p.Angle += (ec + 3) * 60 * Time.deltaTime;
                    else
                        p.Angle += 0.2f * 60 * Time.deltaTime;

                    if (p.Angle > 360)
                        p.Angle -= 360;

                    if (Step > Duration * 60 - 40)
                    {
                        if (ec < 3)
                            p.Alpha -= 5 * 60 * Time.deltaTime;
                        else
                            p.Alpha -= 3 * 60 * Time.deltaTime;
                    }
                }
                else if (Step < 20)
                {
                    if (p.Alpha2 != 1)
                    {
                        if (ec == 3)
                            p.Alpha = Mathf.Clamp(p.Alpha + 8 * 60 * Time.deltaTime, 0, 60);
                        else
                            p.Alpha = Mathf.Clamp(p.Alpha + 10 * 60 * Time.deltaTime, 0, 180);
                    }
                }

                for (var i = 0; i < EffectPart.PartCount; i++)
                {
                    if (p.Flags[i] == 0)
                    {
                        var sinLimit = 90 + ((i - mid) * m2);

                        if (Step <= 90)
                            p.Heights[i] = p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad) * Mathf.Sin(CurrentPos * 60 * Mathf.Deg2Rad);

                        if (p.Heights[i] > p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad))
                        {
                            p.Heights[i] = p.MaxHeight * Mathf.Sin(sinLimit * Mathf.Deg2Rad);
                            p.Flags[i] = 1;
                        }

                        if (p.Heights[i] < 0)
                            p.Heights[i] = 0;
                    }
                }
            }
        }

        //used for warp portal effect
        public void Update3DCasting4()
        {
            var mid = (EffectPart.PartCount - 1) / 2;
            var m2 = 90 / mid;

            for (var ec = 0; ec < 4; ec++)
            {
                var p = Parts[ec];

                if (!p.Active)
                    continue;

                p.Distance -= 0.05f * 60 * Time.deltaTime;
                if (p.Distance <= 0)
                {
                    p.Distance += 10f;
                    p.Alpha = 0f;
                }

                p.RiseAngle = (90f - p.Distance * 9f);
                p.MaxHeight = p.Distance;

                if (Step > Duration * 60 + 40)
                {
                    p.Alpha -= 3 * 60 * Time.deltaTime;
                    if (p.Alpha < 0)
                        p.Alpha = 0;
                }
                else if (p.Alpha < 70)
                {
                    p.Alpha += 1 * 60 * Time.deltaTime;
                }


                for (var i = 0; i < EffectPart.PartCount; i++)
                {
                    if (p.Flags[i] == 0)
                        p.Heights[i] = p.MaxHeight;
                }
            }
        }


        public void Render3DCasting()
        {
            mb.Clear();

            for (var ec = 0; ec < 4; ec++)
            {
                var p = Parts[ec];
                if (!p.Active)
                    continue;
                
                if (p.Alpha < 0)
                    p.Alpha = 0;

                var baseAngle = p.CoverAngle / (EffectPart.PartCount - 1);
                var pos = 0;

                //Debug.Log(baseAngle);

                var bottomLast = Vector3.zero;
                var topLast = Vector3.zero;

                var color = new Color(1f, 1f, 1f, p.Alpha / 255f);
                
                for (var i = 0f; i <= p.CoverAngle; i += baseAngle)
                {
                    var angle = i + p.Angle;
                    if (angle > 360)
                        angle -= 360;

                    if (p.CoverAngle == 360)
                    {
                        if (pos == EffectPart.PartCount - 1)
                            angle = p.Angle;
                    }

                    var c1 = Mathf.Cos(angle * Mathf.Deg2Rad);
                    var s1 = Mathf.Sin(angle * Mathf.Deg2Rad);

                    var bottom = new Vector3(c1 * p.Distance, 0f, s1 * p.Distance);

                    //if (ec == 3)
                    //    bottom.y = -2f;

                    var rx = Mathf.Cos(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos];
                    var ry = Mathf.Sin(p.RiseAngle * Mathf.Deg2Rad) * p.Heights[pos];

                    var rz = s1 * rx;
                    rx = c1 * rx;

                    var top = new Vector3(bottom.x + rx, ry, bottom.z + rz);

                    if (pos > 0)
                    {
                        AddTexturedSliceQuad(topLast, top, bottomLast, bottom, pos, EffectPart.PartCount, color, 0.1f);
                    }

                    pos++;
                    if (pos >= EffectPart.PartCount)
                        pos = EffectPart.PartCount - 1;

                    bottomLast = bottom;
                    topLast = top;
                }
            }

            mf.sharedMesh = mb.ApplyToMesh(mesh);
        }
        
    }
}
