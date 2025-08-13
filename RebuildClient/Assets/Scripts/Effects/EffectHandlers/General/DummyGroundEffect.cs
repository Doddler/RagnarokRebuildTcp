using TMPro;
using UnityEngine;

namespace Assets.Scripts.Effects.EffectHandlers.General
{
    [RoEffect("DummyGroundEffect")]
    public class DummyGroundEffect : IEffectHandler
    {
        public static Ragnarok3dEffect Create(GameObject target, string text)
        {
            var effect = RagnarokEffectPool.Get3dEffect(EffectType.DummyGroundEffect);
            effect.FollowTarget = target.gameObject;
            effect.SetDurationByTime(999999);
            effect.UpdateOnlyOnFrameChange = true;
            effect.DestroyOnTargetLost = true;

            var prefab = Resources.Load<GameObject>("TempGroundObject");
            var obj = GameObject.Instantiate(prefab, effect.transform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localScale = Vector3.one;

            var textObj = obj.GetComponent<TextMeshPro>();

            if (!string.IsNullOrWhiteSpace(text))
                textObj.text = text;
            // else
            //     textObj.text = "<color=#AAAAFF>Skill Object!!";
            
            effect.AttachChildObject(obj);

            return effect;
        }

        public bool Update(Ragnarok3dEffect effect, float pos, int step)
        {
            return effect.IsTimerActive;
        }
    }
}