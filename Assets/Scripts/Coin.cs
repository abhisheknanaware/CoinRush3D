using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    public Transform visual;
    public float spinSpeed = 180f;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2.5f;

    Vector3 basePosition;
    float phase;
    bool collected;

    void Start()
    {
        basePosition = visual.localPosition;
        phase = Random.value * Mathf.PI * 2f;
    }

    void Update()
    {
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        visual.localPosition = basePosition + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || !other.GetComponent<PlayerController>() || !GameManager.Instance) return;
        if (GameManager.Instance.State != GameState.Playing) return;
        collected = true;
        GameManager.Instance.CollectCoin(this);
    }
}
