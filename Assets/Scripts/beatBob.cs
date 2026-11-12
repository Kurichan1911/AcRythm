using UnityEngine;

public class beatBob : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private float bobDepth = 0.1f;
    private Vector3 basePos;

    private void Awake()
    {
        basePos = transform.localPosition;
    }

    void Update()
    {
        if (clock == null || !clock.isPlaying) return;

        double beat = clock.currentBeat;
        if (beat < 0) return;

        //1 for head of beat, into 0 for next beat
        float t = 1f - (float)(beat - System.Math.Floor(beat));

        //dive on head of beat, get back to next beat
        transform.localPosition = basePos + Vector3.down * bobDepth * t * t;
    }
}
