using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class CriticalDamageSprite : MonoBehaviour
{
    [FormerlySerializedAs("Time")] public float Progress;
    public float Strength;
    public float Dampening;
    public SpriteRenderer SpriteRenderer;
    
    private const float swapPoint = 0.06f;
    private bool isLeft;
    private bool isActive;
    
    public void Reset()
    {
        Progress = 0.04f;
        Strength = 0.24f;
        Dampening = 0.80f;
        isLeft = false;
        transform.localPosition = new Vector3(0f, transform.localPosition.y, 0f);
        isActive = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Strength < 0.02f)
            return;
        
        Progress -= Time.deltaTime;
        if(Progress < 0)
        {
            Strength *= Dampening;
            Progress = swapPoint;
            isLeft = !isLeft;
            isActive = true;
        }

        if (Strength < 0.02f)
            Strength = 0f;

        if(isActive)
            transform.localPosition = new Vector3(isLeft ? -Strength : Strength, transform.localPosition.y, 0);
    }
}
