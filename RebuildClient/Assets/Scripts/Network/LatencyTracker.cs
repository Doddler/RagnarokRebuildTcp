using UnityEngine;

namespace Assets.Scripts.Network
{
    //Fed by the pong replies to our keep-alive pings. The server answers from its zone tick, so up to a tick of its
    //own scheduling sits in the number as a floor.
    public class LatencyTracker
    {
        public const int NoSample = -1;

        private const float Smoothing = 0.3f; //weight of the newest sample in the running average

        private float current = NoSample;

        public int Current => current < 0 ? NoSample : Mathf.RoundToInt(current);

        public void AddSample(int roundTrip)
        {
            if (roundTrip < 0)
                return; //the pong outlived a reconnect, so it's timing against a ping we no longer track

            current = current < 0 ? roundTrip : current * (1f - Smoothing) + roundTrip * Smoothing;
        }

        public void Reset() => current = NoSample;
    }
}
