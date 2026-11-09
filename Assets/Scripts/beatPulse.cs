using UnityEngine;

public class beatPulse : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private float pulseScale = 1.4f;
    [SerializeField] private bool accentFirstBeat = true;

    private Vector3 baseScale;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    void Start()
    {
        
    }
    
    void Update()
    {
        if (clock == null || !clock.isPlaying) return;

        double beat = clock.currentBeat;
        if(beat < 0)
        {
            transform.localScale = baseScale;
            return;
        }

        float t = 1f - (float)(beat - System.Math.Floor(beat));

        float scale = pulseScale;
        if (accentFirstBeat && (long)System.Math.Floor(beat) % 4 == 0)
            scale = pulseScale * 1.2f;

        transform.localScale = baseScale * Mathf.Lerp(1f, scale, t * t);
    }
}
