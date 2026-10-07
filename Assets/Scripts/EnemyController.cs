using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public Vector3[] waypoints;
    public float speed = 3f;
    public float chaseMultiplier = 1.25f;
    public float detectRadius = 6f;
    public float leash = 9f;
    public float hitRadius = 1.15f;
    public float hoverHeight = 0.9f;
    public float retreatTime = 1.2f;
    public Transform body;
    public Transform spikes;
    public Renderer glow;

    int index = 1;
    float retreatUntil;
    bool chasing;
    Transform player;
    MaterialPropertyBlock block;

    public bool IsChasing => chasing;

    void Start()
    {
        player = GameManager.Instance ? GameManager.Instance.player.transform : null;
        block = new MaterialPropertyBlock();
        if (waypoints != null && waypoints.Length > 0)
            transform.position = new Vector3(waypoints[0].x, hoverHeight, waypoints[0].z);
        SetGlow(false);
    }

    void Update()
    {
        if (spikes) spikes.Rotate(0f, 150f * Time.deltaTime, 0f, Space.Self);
        if (body) body.localPosition = Vector3.up * Mathf.Sin(Time.time * 4f) * 0.12f;

        GameManager gm = GameManager.Instance;
        if (!gm || gm.State != GameState.Playing || waypoints == null || waypoints.Length == 0 || !player) return;

        Vector3 pos = transform.position;
        Vector3 toPlayer = player.position - pos;
        toPlayer.y = 0f;
        bool retreating = Time.time < retreatUntil;
        bool chase = !retreating && toPlayer.magnitude < detectRadius && NearestWaypointDistance(pos) < leash;
        if (chase != chasing)
        {
            chasing = chase;
            SetGlow(chase);
        }

        Vector3 target = retreating ? pos - toPlayer.normalized * 3f : chase ? player.position : waypoints[index];
        target.y = hoverHeight;
        Vector3 next = Vector3.MoveTowards(pos, target, speed * (chase ? chaseMultiplier : 1f) * Time.deltaTime);
        next.x = Mathf.Clamp(next.x, -23.5f, 23.5f);
        next.z = Mathf.Clamp(next.z, -23.5f, 23.5f);
        Vector3 dir = next - pos;
        if (dir.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        transform.position = next;

        if (!chase && Vector3.Distance(next, target) < 0.15f) index = (index + 1) % waypoints.Length;

        float vertical = Mathf.Abs(player.position.y + 0.9f - next.y);
        if (!retreating && toPlayer.magnitude < hitRadius && vertical < 1.3f && gm.DamagePlayer(next))
            retreatUntil = Time.time + retreatTime;
    }

    float NearestWaypointDistance(Vector3 pos)
    {
        float best = float.MaxValue;
        foreach (Vector3 w in waypoints)
            best = Mathf.Min(best, Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(w.x, w.z)));
        return best;
    }

    void SetGlow(bool on)
    {
        if (!glow) return;
        glow.GetPropertyBlock(block);
        block.SetColor("_BaseColor", new Color(1f, 0.15f, 0.3f, on ? 0.35f : 0.12f));
        glow.SetPropertyBlock(block);
    }
}
