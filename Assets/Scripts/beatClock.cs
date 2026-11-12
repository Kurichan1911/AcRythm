using UnityEngine;
using System;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Runtime.ExceptionServices;
public class beatClock : MonoBehaviour
{
    [Header("Play Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Song Information")]
    [SerializeField] private double bpm = 120;
    [SerializeField] private double firstBeatOffset = 0.0;

    [Header("Play Setting")]
    [SerializeField] private double startDelay = 0.5;
    [SerializeField] private bool playOnStart = true;

    public event Action<int> OnBeat;
    public double Bpm => bpm;
    public double secPerBeat => 60.0 / bpm;
    public bool isPlaying { get; private set; }

    private double startDspTime;
    private int lastBeat = -1;

    public double songTime => AudioSettings.dspTime - startDspTime - firstBeatOffset;

    public double currentBeat => songTime / secPerBeat;

    public double BeatToDspTime(double beat)
    {
        return startDspTime + firstBeatOffset + beat * secPerBeat;
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        startDspTime = AudioSettings.dspTime + startDelay;
        audioSource.loop = true;
        audioSource.PlayScheduled(startDspTime);
        lastBeat = -1;
        isPlaying = true;
    }

    public void Stop()
    {
        audioSource.Stop();
        isPlaying = false;
    }

    void Update()
    {
        //Debug.Log(currentBeat);
        if (!isPlaying) return;

        int beat = (int)Math.Floor(currentBeat);
        while(lastBeat < beat)
        {
            lastBeat++;
            if (lastBeat >= 0) OnBeat?.Invoke(lastBeat);
        }
    }
}
