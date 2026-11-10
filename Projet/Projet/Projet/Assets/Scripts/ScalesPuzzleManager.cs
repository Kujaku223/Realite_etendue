using UnityEngine;
using UnityEngine.Events;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField] WeighingScale[] scales;
    public UnityEvent onAllSolved;
    public UnityEvent onNoLongerSolved; // only fires if scales can reset

    bool allSolved;

    // Hook this to each scale's onSolved AND onUnsolved
    public void Recheck()
    {
        bool now = true;
        foreach (var s in scales)
            if (!s.IsSolved) { now = false; break; }

        if (now && !allSolved) { allSolved = true; onAllSolved?.Invoke(); }
        else if (!now && allSolved) { allSolved = false; onNoLongerSolved?.Invoke(); }
    }
}