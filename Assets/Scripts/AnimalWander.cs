using UnityEngine;
using Controller;

public class AnimalWander : MonoBehaviour
{
    public float wanderRadius = 10f;
    public float waitTime = 3f;
    public bool runSometimes = false;

    private CreatureMover mover;
    private Vector3 targetPoint;
    private float waitTimer;
    private bool waiting;

    void Start()
    {
        mover = GetComponent<CreatureMover>();
        PickNewTarget();
    }

    void Update()
    {
        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                PickNewTarget();
            }
            return;
        }

        Vector3 direction = targetPoint - transform.position;
        direction.y = 0;

        if (direction.magnitude < 1f)
        {
            waiting = true;
            waitTimer = Random.Range(1f, waitTime);
            mover.SetInput(Vector2.zero, transform.forward, false, false);
            return;
        }

        Vector3 forward = direction.normalized;

        // axis.y = forward movement
        Vector2 axis = new Vector2(0, 1);

        bool shouldRun = runSometimes && Random.value > 0.7f;

        mover.SetInput(axis, targetPoint, shouldRun, false);
    }

    void PickNewTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * wanderRadius;
        targetPoint = new Vector3(
            transform.position.x + randomCircle.x,
            transform.position.y,
            transform.position.z + randomCircle.y
        );
    }


}