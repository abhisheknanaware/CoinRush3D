using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    public Transform outerRing;
    public Transform innerRing;
    public Transform disc;
    public Light portalLight;
    public float pulseSpeed = 3.5f;

    Vector3 discScale;
    bool reached;

    void Start()
    {
        if (disc) discScale = disc.localScale;
    }

    void Update()
    {
        if (outerRing) outerRing.Rotate(0f, 0f, 60f * Time.deltaTime, Space.Self);
        if (innerRing) innerRing.Rotate(0f, 0f, -100f * Time.deltaTime, Space.Self);
        float p = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        if (disc) disc.localScale = discScale * (0.92f + 0.13f * p);
        if (portalLight) portalLight.intensity = 2f + 4f * p;
    }

    void OnTriggerEnter(Collider other)
    {
        if (reached || !other.GetComponent<PlayerController>() || !GameManager.Instance) return;
        if (GameManager.Instance.State != GameState.Playing) return;
        reached = true;
        GameManager.Instance.ReachPortal(transform.position);
    }
}
