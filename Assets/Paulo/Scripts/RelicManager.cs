using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RelicManager : MonoBehaviour
{
    [SerializeField] private int totalRelics = 10;
    [SerializeField] private ParticleSystem completionEffect;

    private readonly HashSet<ForestRelicTrigger> collected = new HashSet<ForestRelicTrigger>();

    public int CollectedCount => collected.Count;
    public int TotalRelics => totalRelics;
    public bool IsComplete => collected.Count == totalRelics;

    public event Action<int, int> ProgressChanged;
    public event Action CollectionCompleted;

    public bool TryCollect(ForestRelicTrigger relic)
    {
        if (!relic || IsComplete || !collected.Add(relic))
            return false;

        ProgressChanged?.Invoke(CollectedCount, totalRelics);
        if (IsComplete)
        {
            completionEffect.transform.position = relic.transform.position + Vector3.up;
            completionEffect.Play();
            CollectionCompleted?.Invoke();
        }

        return true;
    }
}
