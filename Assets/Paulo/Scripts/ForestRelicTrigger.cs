using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ForestRelicTrigger : MonoBehaviour
{
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private Renderer relicRenderer;
    [SerializeField] private RelicManager manager;
    [SerializeField] private float hideAfterSeconds = 2f;

    private bool activated;

    private void OnTriggerEnter(Collider other)
    {
        if (activated || !other.CompareTag("Player") || !manager || !manager.TryCollect(this))
            return;

        activated = true;
        particles.Play();
        StartCoroutine(HideRelic());
    }

    private IEnumerator HideRelic()
    {
        yield return new WaitForSeconds(hideAfterSeconds);
        relicRenderer.gameObject.SetActive(false);
    }
}
