using System.Collections.Generic;
using Assets.Scripts.Objects;
using UnityEngine;

public class ParticleCollisionSoundSource : MonoBehaviour
{
    private ParticleSystem part;
    private List<ParticleCollisionEvent> collisionEvents;
    public List<string> Sounds;
    
    // Start is called before the first frame update
    void Start()
    {
        part = GetComponent<ParticleSystem>();
        collisionEvents = new List<ParticleCollisionEvent>();
        if(Sounds == null)
            Destroy(this);
    }

    void OnParticleCollision(GameObject other)
    {
        
        int numCollisionEvents = part.GetCollisionEvents(other, collisionEvents);

        Rigidbody rb = other.GetComponent<Rigidbody>();
        int i = 0;

        while (i < numCollisionEvents)
        {
            Vector3 pos = collisionEvents[i].intersection;
            AudioManager.Instance.OneShotSoundEffect(-1, Sounds[Random.Range(0, Sounds.Count)], pos );

            i++;
        }
    }
}
