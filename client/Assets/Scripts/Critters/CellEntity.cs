using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class CellEntity : CellElement
{
    // Logic
    [SerializeField] private Vector2Int motion;
    public Vector2Int Motion
    {
        get { return motion; }
        set { motion = value; }
    }
    [SerializeField] private bool interpolateMotion;
    [SerializeField] private Vector2Int gravityVector = Vector2Int.down;
    [SerializeField] private Transform floorPointRoot;

    [Header("Behaviour")]
    [SerializeField] private bool shouldWalk = true;
    [SerializeField] private float maxWalkSlopeAngle = 60;

    private bool alive = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        standingSurface = Grid.GetSurface(cell, transform.up);
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
    }

    private Surface lastImpactedSurface = null;
    private Surface standingSurface = null;
    public bool Falling => standingSurface == null;
    private int pendingTimeStepCoroutines = 0;
    public override void HandleTimeStep()
    {
        Debug.Log($"Handling time step on entity {gameObject.name}.");
        pendingTimeStepCoroutines = 0;
        bool handledReservedMotion = HandleReservedMotion();
        if (!handledReservedMotion)
        {
            // TODO: check effectors in current position to add reserved motion
            handledReservedMotion = HandleReservedMotion();
        }
        
        // if no reserved motion exists: run regular motion logic
        if (!handledReservedMotion)
        {
            // First, check for whether the entity should fall.
            // assuming it is standing on something
            if (!Falling && shouldWalk)
            {
                // We'll try to follow a point in the middle of the entity forward by a distance equaling one unit
                Vector3 root = (transform.position + floorPointRoot.position) / 2;
                Vector2Int targetCell = Grid.WorldToGrid(root + transform.forward * Grid.GridScale);
                motion = targetCell - cell;
            }

            // Use motion logic to move with physical constraints.
            motion = grid.LimitMotion(motion, this.cell, transform.up, out lastImpactedSurface);
            Vector3 targetPosition;
            Quaternion targetRotation;
            bool shouldFlip = false;
            if (lastImpactedSurface != null)
            {
                Debug.Log($"Motion of {gameObject.name} blocked by surface {lastImpactedSurface}; set to {motion}");
                if (!Falling && Vector2.Angle(lastImpactedSurface.normal, standingSurface.normal) > maxWalkSlopeAngle)
                {
                    // effectively would slam into a wall. Try to turn around instead.
                    shouldFlip = true;
                }
                else
                {
                    // otherwise, try to latch onto the surface.
                    standingSurface = lastImpactedSurface;
                }
            }
            else
            {
                // Nothing was impacted, move in the direction of motion!
                // Then check if we would be able to stand somewhere
                standingSurface = Grid.GetSurface(cell + motion, transform.up);
            }
            // If we have a surface to stand on, simply try to get on it
            if (standingSurface != null)
            {
                Debug.Log($"{gameObject.name} standing on surface {standingSurface}");
                targetPosition = GetAdjustedStandingPosition(standingSurface);
                // If we didn't walk onto the surface, but rather fell onto it, check whether to correct rotation.
                if (lastImpactedSurface == standingSurface && lastImpactedSurface.HasFlag(Surface.Properties.IgnoreRotationOnLanding))
                {
                    targetRotation = GetRotationFromMotion(motion);
                }
                else
                {
                    targetRotation = GetRotationWithAngle(standingSurface.GetStandingRotation(), shouldFlip);
                }
            }
            else
            {
                targetPosition = transform.position + new Vector3(motion.x * grid.GridScale, motion.y * grid.GridScale, 0);
                targetRotation = GetRotationFromMotion(motion);
                
                // falling after reaching target!
                // After calculating the target position, apply gravity to velocity (motion) vector
                motion += gravityVector;
            }

            StartCoroutine(MotionCoroutine(targetPosition, targetRotation, 1));
        }

        AfterTimeStep();
    }

    public override void AfterTimeStep()
    {
        StartCoroutine(AfterTimeStepCoroutine());
    }

    public virtual IEnumerator AfterTimeStepCoroutine()
    {
        yield return new WaitUntil(() => pendingTimeStepCoroutines <= 0);
        cell = Grid.WorldToGrid(transform.position);
        if (lastImpactedSurface != null)
        {
            // handle falling logic
            if (Falling)
            {
                if (lastImpactedSurface.HasFlag(Surface.Properties.SuppressFall))
                {
                    // land safely.
                    motion = Vector2Int.zero;
                }
                else
                {
                    Die();
                }
            }
        }

        // handle death on Death surfaces
        if ((lastImpactedSurface != null && lastImpactedSurface.HasFlag(Surface.Properties.Fatal)) ||
            (standingSurface != null && standingSurface.HasFlag(Surface.Properties.Fatal)))
        {
            Die();
        }
        lastImpactedSurface = null;
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Check if we hit something that can kill us
        if (collision.collider.gameObject.CompareTag("KillCritter"))
        {
            Die();
        }
    }

    public bool HandleReservedMotion()
    {
        // TODO: Handle Reserved Motion
        return false;
    }

    public IEnumerator MotionCoroutine(Vector3 targetPosition, Quaternion targetRotation, float duration)
    {
        Debug.Log($"{gameObject.name} moving to {targetPosition} with rotation {targetRotation.eulerAngles} in {duration} seconds.");
        pendingTimeStepCoroutines++;
        snapping = false;
        if (interpolateMotion)
        {
            float t = 0;
            Vector3 initialPosition = transform.position;
            Quaternion initialRotation = transform.rotation;
            while (t < duration)
            {
                if (!alive) break;
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(initialPosition, targetPosition, t / duration);
                transform.rotation = Quaternion.Lerp(initialRotation, targetRotation, t / duration);
                yield return new WaitForEndOfFrame();
            }
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        if (alive)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        cell = grid.WorldToGrid(transform.position - snapOffset);
        pendingTimeStepCoroutines--;
    }

    public Quaternion GetRotationWithAngle(float angle, bool shouldFlip = false)
    {
        // get whether right side is pointing away or towards camera
        MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);

        return MathLib.GetRotationFromAngle(angle, rightSidePointsAway ^ shouldFlip);
    }

    public Quaternion GetFlippedRotation()
    {
        float angle = MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);
        
        return MathLib.GetRotationFromAngle(angle, !rightSidePointsAway);
    }

    public Vector3 GetAdjustedStandingPosition(Surface surf)
    {
        Vector3 feetPosition = surf.GetStandingPosition(grid);
        return GetAdjustedStandingPosition(feetPosition, surf.normal);
    }
    public Vector3 GetAdjustedStandingPosition(Vector3 feetPosition, Vector3 normal)
    {
        float feetHeight = (transform.position - floorPointRoot.position).magnitude;
        return feetPosition + normal * feetHeight;
    }

    public Quaternion GetRotationFromMotion(Vector2 motion)
    {
        MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);
        return GetRotationWithAngle(MathLib.AngleFromVector2(motion) + (rightSidePointsAway ? -90 : 90));
    }

    public void Die()
    {
        alive = false;
        throw new NotImplementedException();
    }

    public void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0.5f, 0, 1);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(Vector2)motion * Grid.GridScale);
    }
}
