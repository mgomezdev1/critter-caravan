using System;
using System.Collections;
using System.Collections.Generic;
using Utils;
using UnityEngine;
using Unity.VisualScripting;
using System.Drawing;
using System.Runtime.CompilerServices;

#nullable enable
#pragma warning disable CS8618
[RequireComponent(typeof(Animator))]
public class CellEntity : CellElement, IResettable
{
    // Position and Size
    [SerializeField] private Transform floorPointRoot;
    public Vector3 Center => (transform.position + floorPointRoot.position) / 2;
    public Vector3 FootPosition => floorPointRoot.position;
    public float Height => (transform.position - floorPointRoot.position).magnitude;

    [Header("Movement")]
    [SerializeField] private Surface.Flags intangibleWallFlags = Surface.Flags.Virtual;
    public Surface.Flags IntangibleWallFlags => intangibleWallFlags;
    [SerializeField] private float maxWalkSlopeAngle = 60;
    public float MaxWalkSlopeAngle => maxWalkSlopeAngle;
    [SerializeField] private int maxFallHeight = 1;
    public int MaxFallHeight => maxFallHeight;

    // Move logic
    private readonly PriorityQueue<Move, int> moveQueue = new();
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
    public bool Falling => FallHeight > 0;
    public int FallHeight { get; set; }

    [Header("Interaction")]
    [SerializeField] private float entityRadius = 0.4f;
    public float EntityRadius => entityRadius;

    public override Vector2Int Cell
    {
        get
        {
            return StandingSurface?.Cell ?? base.Cell;
        }
    }

    private Animator animator;
    public Animator Animator => animator;
    protected override void Awake()
    {
        base.Awake();
        animator = GetComponent<Animator>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Start()
    {
        base.Start();
        StandingSurface = World.GetSurface(cell, transform.up);
        World.RegisterEntity(this);
        AnimateSpawn();
    }

    // Update is called once per frame
    protected override void Update()
    {
        base.Update();
        EntityMove? move = CurrentMove;
        if (move != null && IsPlaying())
        {
            float moveTimer = Mathf.Clamp01(1 - World.TimeFragment);
            float moveSegment = Time.deltaTime * World.TimeSpeed / moveTimer;
            Vector3 oldPosition = transform.position;
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, move.movePosition, moveSegment),
                Quaternion.Lerp(transform.rotation, move.moveRotation, moveSegment)
            );
            AnimateVelocity((transform.position - oldPosition) / Time.deltaTime);
        }
    }

    private void OnDestroy()
    {
        World.DeregisterEntity(this);
    }

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

        FinishMoveInstantly(CurrentMove);
        if (alive && !goalReached)
        {
            CurrentMove = FetchMove();
            Debug.Log($"Executing move {CurrentMove}");
        }
        AfterTimeStep();
    }

    private void FinishMoveInstantly(EntityMove? move)
    {
        if (!alive || goalReached)
        {
            Destroy(gameObject);
        }

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
            FallHeight++;
        } 
        else
        {
            FallHeight = 0;
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

    public void HandleReset()
    {
        Destroy(gameObject);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsPlaying()
    {
        return alive && !goalReached;
    }

    /// <summary>
    /// Queues an EntityMove to be executed by the entity.
    /// </summary>
    /// <returns>The position of the queued move in the entity's internal queue.</returns>
    public int QueueMove(Move move)
    {
        Debug.Log($"Queuing {move} for {gameObject.name}");
        int idx = moveQueue.Count;
        moveQueue.Enqueue(move, move.priority);
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
        // Remove all effector moves that were not executed.
        moveQueue.Clear();

        return result;
    }

    [Obsolete("This method should not be used; obtain a quaternion using MathLib's GetRotation methods instead.")]
    public Quaternion GetRotationWithAngle(float angle, bool shouldFlip = false)
    {
        // get whether right side is pointing away or towards camera
        MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);

        Quaternion result = MathLib.GetRotationFromUpAngle(angle, rightSidePointsAway ^ shouldFlip);
        return result;
    }

    public Quaternion GetFlippedRotation()
    {
        float angle = MathLib.GetAngleFromRotation(transform.rotation, out bool rightSidePointsAway);
        
        return MathLib.GetRotationFromUpAngle(angle, !rightSidePointsAway);
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
        return MathLib.GetRotationFromUpAngle(MathLib.AngleFromVector2(motion) + (rightSidePointsAway ? -90 : 90), rightSidePointsAway);
    }

    protected bool alive = true;
    public void Die()
    {
        alive = false;
        AnimateDeath();
        Debug.Log($"{gameObject.name} died!");
    }

    protected bool goalReached = false;
    public void ReachGoal()
    {
        if (goalReached) { return; }
        goalReached = true;
        WorldManager.Instance.AddScore(new ColorScore(1, 0, Color));
        AnimateCampReached();
    }

    public void OnDrawGizmosSelected()
    {
        DrawingLib.DrawCritterGizmo(floorPointRoot);
        Gizmos.color = new UnityEngine.Color(1, 0.5f, 0.5f);
        Gizmos.DrawWireSphere(Center, EntityRadius);
    }

    /* ****************************** *
     *        ANIMATION LOGIC         *
     * ****************************** */
    public void AnimateVelocity(Vector3 velocity)
    {
        Quaternion worldToLocalRotation = Quaternion.Inverse(transform.rotation);
        Vector3 localVelocity = worldToLocalRotation * velocity;
        animator.SetFloat("v_rightward", localVelocity.x);
        animator.SetFloat("v_upward", localVelocity.y);
        animator.SetFloat("v_forward", localVelocity.z);
    }
    public void AnimateSpawn()
    {
        animator.SetTrigger("spawn");
    }
    public void AnimateDeath()
    {
        animator.SetTrigger("death");
    }
    public void AnimateCampReached()
    {
        animator.SetTrigger("camp");
    }
}
