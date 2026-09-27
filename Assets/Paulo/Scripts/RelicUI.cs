using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class RelicUI : MonoBehaviour
{
    [SerializeField] private RelicManager manager;
    [SerializeField] private Text counter;
    [SerializeField] private Text message;

    private Coroutine messageTimer;

    private void OnEnable()
    {
        manager.ProgressChanged += ShowProgress;
        manager.CollectionCompleted += ShowCompletion;
        counter.text = $"Reliquias: {manager.CollectedCount}/{manager.TotalRelics}";
        message.text = string.Empty;
    }

    private void OnDisable()
    {
        manager.ProgressChanged -= ShowProgress;
        manager.CollectionCompleted -= ShowCompletion;
    }

    private void ShowProgress(int count, int total)
    {
        counter.text = $"Reliquias: {count}/{total}";
        if (messageTimer != null)
            StopCoroutine(messageTimer);
        message.text = $"Reliquia encontrada: {count}/{total}";
        messageTimer = StartCoroutine(ClearMessage());
    }

    private void ShowCompletion()
    {
        if (messageTimer != null)
            StopCoroutine(messageTimer);
        message.text = $"Colección completa: {manager.CollectedCount}/{manager.TotalRelics}";
    }

    private IEnumerator ClearMessage()
    {
        yield return new WaitForSeconds(2.5f);
        message.text = string.Empty;
        messageTimer = null;
    }
}
