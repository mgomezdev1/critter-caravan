using System;
using System.Collections.Generic;
using UnityEngine;

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
    All = 0x7fffffff
}

[RequireComponent(typeof(CellElement))]
public class Effector : MonoBehaviour, IEffector
{
    [SerializeField] private EffectorFlags flags;
    [SerializeField] private bool registerToCell = true;

    [Header("Move Configuration: Filters")]
    [SerializeField] private Vector2 effectorForward;
    [Tooltip("Maximum angle between the entity's forward vector and the effectorForward vector for the effector to work, in degrees. If >180, the effector will always work.")]
    [SerializeField] private float maxForwardAngle;

    [Header("Move Configuration: Effects")]
    [SerializeField] private bool keepMoveRotation = false;
    [SerializeField] private List<Transform> effectorMoves = new();
    [SerializeField] private float verticalAnchor = 0f;
    [SerializeField] private MoveFlags moveFlags;
    [SerializeField] private float moveMaxSurfaceSnapAngle = 60f;

    private CellElement cellElement;
    private CellElement CellElement { 
        get { 
            if (cellElement == null) cellElement = GetComponent<CellElement>();
            return cellElement;
        }
    }

    public Vector2Int Cell => CellElement.Cell;
    public EffectorFlags Flags => flags;

    public EffectResult OnEntityEnter(CellEntity entity)
    {
        Debug.Log($"Running effector {this}");
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
            Move move;
            if (keepMoveRotation)
            {
                move = new(t.transform.position, entity.transform.rotation, moveFlags, verticalAnchor, moveMaxSurfaceSnapAngle);
            } 
            else
            {
                move = new(t, moveFlags, verticalAnchor, moveMaxSurfaceSnapAngle);
            }

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

    private void Start()
    {
        cellElement = GetComponent<CellElement>();
        if (registerToCell)
        {
            cellElement.World.RegisterEffector(this);
        }
    }

    private void OnDestroy()
    {
        CellElement.World.DeregisterEffector(this);
    }

    const float ANGLE_GIZMO_LENGTH = 0.5f;
    const int ANGLE_GIZMO_SEGMENTS = 11;
    private void OnDrawGizmos()
    {
        Vector3 position = transform.position;
        if (registerToCell)
        {
            CellElement cellElem = CellElement;
            GridWorld grid = cellElem.World;
            Vector3 center = grid.GetCellCenter(cellElem.Cell);
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
            DrawingLib.DrawCritterGizmo(t.position - (t.rotation * Vector3.up * verticalAnchor), t.rotation);
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