using UnityEngine;

public class dummyCombat : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private playerCombat player;
    [SerializeField] private Renderer view;

    private double impactDsp = -1;

    private void OnEnable()
    {
        clock.OnBeat += HandleBeat;
    }
    private void OnDisable()
    {
        clock.OnBeat -= HandleBeat;
    }

    private void HandleBeat(int beat)
    {
        if(beat % 4 == 2)
        {
            impactDsp = clock.BeatToDspTime(beat + 1);
            view.material.color = Color.yellow;
        }
    }

    void Update()
    {
        if (impactDsp < 0 || AudioSettings.dspTime < impactDsp) return;

        player.ReceiveAttack(impactDsp);
        impactDsp = -1;
        view.material.color = Color.white;
    }
}
