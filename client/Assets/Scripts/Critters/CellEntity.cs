using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
public class CellEntity : CellElement
{
    // Logic
    [SerializeField] private bool interpolateMotion;
    [SerializeField] private Transform floorPointRoot;
    public Vector3 Center => (transform.position + floorPointRoot.position) / 2;
    public Vector3 FootPosition => floorPointRoot.position;
    public float Height => (transform.position - floorPointRoot.position).magnitude;
    [SerializeField] private Surface.Flags intangibleWallFlags = Surface.Flags.Virtual;
    public Surface.Flags IntangibleWallFlags => intangibleWallFlags;
    [SerializeField] private float maxWalkSlopeAngle = 60;
    public float MaxWalkSlopeAngle => maxWalkSlopeAngle;
    [SerializeField] private int maxFallHeight = 1;
    public int MaxFallHeight => maxFallHeight;

    private readonly Queue<Move> moveQueue = new();
    protected EntityMove? currentMove = null;
    public EntityMove? CurrentMove { 
        get { return currentMove; } 
        private set { 
            currentMove = value; 
            adjustedMovePositionDirty = true;
            StandingSurface = value?.surface;
        } 
    }
    public Surface? StandingSurface { get; private set; }
    [SerializeField] protected int fallHeight = 0;
    public bool Falling => fallHeight > 0;
    public int FallHeight => fallHeight;
    float moveTimer = 0f;

    public override Vector2Int Cell
    {
        get
        {
            return StandingSurface?.Cell ?? base.Cell;
        }
    }

    protected override void Awake()
    {
        base.Awake();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        StandingSurface = World.GetSurface(cell, transform.up);
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
        if (CurrentMove != null && moveTimer > 0f)
        {
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, CurrentMove.movePosition, Time.deltaTime / moveTimer),
                Quaternion.Lerp(transform.rotation, CurrentMove.moveRotation, Time.deltaTime / moveTimer)
            );
            moveTimer -= Time.deltaTime;
        }
    }

    private int pendingTimeStepCoroutines = 0;
    private Vector3 adjustedMovePosition = Vector3.zero;
    private bool adjustedMovePositionDirty;
    public Vector3 AdjustedMovePosition {
        get
        {
            if (adjustedMovePositionDirty && CurrentMove != null)
            {
                adjustedMovePositionDirty = false;
                adjustedMovePosition = GetAdjustedStandingPosition(CurrentMove.movePosition, CurrentMove.moveRotation * Vector3.up);
            }
            return adjustedMovePosition;
        }
    }

    public override void HandleTimeStep()
    {
        Debug.Log($"Handling time step on entity {gameObject.name}.");
        pendingTimeStepCoroutines = 0;

        FinishMoveInstantly(CurrentMove);
        if (alive)
        {
            CurrentMove = FetchMove();
            Debug.Log($"Executing move {CurrentMove}");
            moveTimer = 1f;
        }
        AfterTimeStep();
    }

    private void FinishMoveInstantly(EntityMove? move)
    {
        if (move == null)
        {
            return;
        }

        transform.SetPositionAndRotation(move.movePosition, move.moveRotation);
        foreach (var effector in move.GetEffectors())
        {
            effector.OnEntityEnter(this);
        }
        
        if (move.flags.HasFlag(MoveFlags.Fatal))
        {
            Die();
        }

        cell = move.GetCell();
        if (move.surface == null)
        {
            fallHeight++;
        } 
        else
        {
            fallHeight = 0;
        }
    }

    public override void AfterTimeStep()
    {
        // StartCoroutine(AfterTimeStepCoroutine());
    }

    public virtual IEnumerator AfterTimeStepCoroutine()
    {
        // yield return new WaitUntil(() => pendingTimeStepCoroutines <= 0);
        yield break;
    }

    public void OnCollisionEnter(Collision collision)
    {
        // Check if we hit something that can kill us
        if (collision.collider.gameObject.CompareTag("KillCritter"))
        {
            Die();
        }
    }

    /// <summary>
    /// Queues an EntityMove to be executed by the entity.
    /// </summary>
    /// <returns>The position of the queued move in the entity's internal queue.</returns>
    public int QueueMove(Move move)
    {
        Debug.Log($"Queuing {move} for {gameObject.name}");
        int idx = moveQueue.Count;
        moveQueue.Enqueue(move);
        return idx;
    }

    private EntityMove FetchMove()
    {
        EntityMove? result = null;
        
        while (result == null || result.Reached())
        {
            if (CurrentMove?.nextMove != null)
            {
                result = CurrentMove.nextMove.Process(this);
                continue;
            }

            if (moveQueue.Count > 0)
            {
                result = moveQueue.Dequeue().Process(this);
                continue;
            }

            result = GetComponent<EntityBrain>().GetNextMove(this);
            break;
        }

        return result;
    }

    [Obsolete("This method should not be used; obtain a quaternion using MathLib's GetRotation methods instead.")]
    public Quaternion GetRotationWithAngle(float angle, bool shouldFlip = false)
    {
        // get whether right side is pointing away or towards camera
        MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);

        Quaternion result = MathLib.GetRotationFromAngle(angle, rightSidePointsAway ^ shouldFlip);
        return result;
    }

    public Quaternion GetFlippedRotation()
    {
        float angle = MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);
        
        return MathLib.GetRotationFromAngle(angle, !rightSidePointsAway);
    }

    public Vector3 GetAdjustedStandingPosition(Surface surf)
    {
        Vector3 feetPosition = surf.GetStandingPosition(world);
        return GetAdjustedStandingPosition(feetPosition, surf.normal);
    }
    public Vector3 GetAdjustedStandingPosition(Vector3 feetPosition, Vector3 normal)
    {
        return feetPosition + normal * Height;
    }

    public Quaternion GetRotationFromMotion(Vector2 motion)
    {
        bool rightSidePointsAway = MathLib.RightSidePointsAway(transform.right);
        return MathLib.GetRotationFromAngle(MathLib.AngleFromVector2(motion) + (rightSidePointsAway ? -90 : 90), rightSidePointsAway);
    }

    protected bool alive = true;
    public void Die()
    {
        alive = false;
        Debug.Log($"{gameObject.name} died!");
    }

    public void OnDrawGizmosSelected()
    {
        DrawingLib.DrawCritterGizmo(floorPointRoot);
    }
}
