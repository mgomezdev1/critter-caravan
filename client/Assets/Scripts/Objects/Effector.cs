using System;
using System.Collections.Generic;
using UnityEngine;

#nullable enable
public class EffectResult
{
    public bool executed = true;

    public EffectResult() { }
}

public interface IEffector
{
    public EffectorFlags Flags { get; } 

    public Vector2Int Cell { get; }

    bool HasAnyFlag(EffectorFlags flagWhitelist)
    {
        if (flagWhitelist == EffectorFlags.All) return true;

        return (flagWhitelist & Flags) > 0;
    }
    public EffectResult OnEntityEnter(CellEntity entity);
}

[Flags]
public enum EffectorFlags
{
    None = 0,
    StopFall = 1,
    BlockMovement = 2,
    RequireSurface = 4,
    All = 0x7fffffff
}

public enum MoveRotationType
{
    ForceRotation = 0,
    KeepOrientation = 1,
    KeepRotation = 2
}

public class Effector : CellBehaviour<CellElement>, IEffector
{
    [SerializeField] private EffectorFlags flags;
    [SerializeField] private bool registerToCell = true;

    [Header("Move Configuration: Filters")]
    [SerializeField] private Vector2 effectorForward;
    [Tooltip("Maximum angle between the entity's forward vector and the effectorForward vector for the effector to work, in degrees. If >180, the effector will always work.")]
    [SerializeField] private float maxForwardAngle;

    [Header("Move Configuration: Effects")]
    [SerializeField] private MoveRotationType rotationType = MoveRotationType.ForceRotation;
    [SerializeField] private List<Transform> effectorMoves = new();
    [SerializeField] private float verticalAnchor = 0f;
    [SerializeField] private MoveFlags moveFlags;
    [SerializeField] private float moveMaxSurfaceSnapAngle = 60f;
    [SerializeField] private int movePriority = 0;

    public EffectorFlags Flags => flags;

    public EffectResult OnEntityEnter(CellEntity entity)
    {
        Debug.Log($"Running effector {this}");
        if (flags.HasFlag(EffectorFlags.RequireSurface) && entity.StandingSurface == null)
        {
            Debug.Log("Effector aborted (standing surface required)");
            return new EffectResult() { executed = false };
        }
        Vector2 correctedForward = MathLib.RotateVector2(effectorForward, transform.rotation, false);
        float angle = Vector2.Angle(correctedForward, entity.transform.forward);
        if (angle > maxForwardAngle)
        {
            Debug.Log($"Effector aborted (forward angle mismatch)");
            return new EffectResult() { executed = false };
        }

        Move? firstMove = null;
        Move? lastMove = null;
        foreach (Transform t in effectorMoves)
        {
            Quaternion moveRotation = rotationType switch
            {
                MoveRotationType.ForceRotation => t.transform.rotation,
                MoveRotationType.KeepRotation => entity.transform.rotation,
                MoveRotationType.KeepOrientation => MathLib.MatchOrientation(t.transform.rotation, entity.transform.rotation),
                _ => throw new NotImplementedException()
            };
            Move move = new(t.transform.position, moveRotation, moveFlags, verticalAnchor, moveMaxSurfaceSnapAngle, movePriority);

            if (lastMove == null)
            {
                firstMove = move;
                lastMove = move;
            }
            else
            {
                lastMove.nextMove = move;
                lastMove = move;
            }
        }

        if (firstMove != null)
        {
            entity.QueueMove(firstMove);
        }

        Debug.Log($"Effector success");
        return new EffectResult() { executed = true };
    }

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        if (registerToCell)
        {
            World.RegisterEffector(this);
        }
    }

    private void OnDestroy()
    {
        World.DeregisterEffector(this);
    }

    const float ANGLE_GIZMO_LENGTH = 0.5f;
    const int ANGLE_GIZMO_SEGMENTS = 11;
    private void OnDrawGizmos()
    {
        Vector3 position = transform.position;
        if (registerToCell)
        {
            GridWorld grid = CellComponent.World;
            Vector3 center = grid.GetCellCenter(CellComponent.Cell);
            Gizmos.color = new Color(0.5f, 0, 1f);
            Gizmos.DrawWireCube(center, Vector3.one * (grid.GridScale * 0.9f));
        }

        if (maxForwardAngle < 180)
        {
            Gizmos.color = new Color(0.5f, 1, 0);
            float step = 2.0f / (ANGLE_GIZMO_SEGMENTS - 1);
            Vector2 correctedForward = MathLib.RotateVector2(effectorForward, transform.rotation, false);
            float baseAngle = MathLib.AngleFromVector2(correctedForward);
            for (float t = -1; t <= 1.001f; t += step)
            {
                float angleDelta = t * maxForwardAngle;
                Vector2 vec = MathLib.Vector2FromAngle(baseAngle + angleDelta);
                Gizmos.DrawLine(position, position + ((Vector3)vec * ANGLE_GIZMO_LENGTH));
            }
        }

        foreach (Transform t in effectorMoves)
        {
            if (rotationType == MoveRotationType.ForceRotation)
            {
                DrawingLib.DrawCritterGizmo(t.position - (t.rotation * Vector3.up * verticalAnchor), t.rotation);
            }
            else if (rotationType == MoveRotationType.KeepOrientation)
            {
                Vector3 relativeUp = t.rotation * Vector3.up;
                DrawingLib.DrawCritterGizmoWithoutOrientation(t.position - (relativeUp * verticalAnchor), relativeUp);
            }
            else if (rotationType == MoveRotationType.KeepRotation)
            {
                DrawingLib.DrawCritterGizmoCluster(t.position, verticalAnchor);
            }
            Gizmos.color = Color.white;
            Gizmos.DrawLine(position, t.position);
            position = t.position;
        }
    }

    public override string ToString()
    {
        return $"{gameObject.name} effector -> F={effectorForward}, MaxTheta={maxForwardAngle}, NumMoves={effectorMoves.Count}";
    }
}