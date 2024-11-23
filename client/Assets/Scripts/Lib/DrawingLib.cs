using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class DrawingLib
{
    public static void DrawCritterGizmo(this Surface surface, GridWorld grid, bool rightSidePointsAway)
    {
        DrawCritterGizmo(surface.GetStandingPosition(grid), surface.GetStandingAngle(), rightSidePointsAway);
    }

    public static void DrawCritterGizmo(Transform surfaceTransform)
    {
        DrawCritterGizmo(surfaceTransform.position, surfaceTransform.rotation);
    }

    public static void DrawCritterGizmo(Vector3 position, float angle, bool rightSidePointsAway)
    {
        Quaternion rotation = MathLib.GetRotationFromUpAngle(angle, rightSidePointsAway);
        DrawCritterGizmo(position, rotation);
    }

    public static void DrawCritterGizmoWithoutOrientation(Vector3 point, float angle)
    {
        DrawCritterGizmoWithoutOrientation(point, MathLib.Vector2FromAngle(angle));
    }
    public static void DrawCritterGizmoWithoutOrientation(Vector3 point, Vector3 upNormalized)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(point, point + upNormalized);
        Gizmos.DrawWireSphere(point + upNormalized, 0.3f);
    }

    public static void DrawCritterGizmo(Vector3 position, Quaternion rotation)
    {
        Vector3 rotatedForward = rotation * Vector3.forward;
        Vector3 rotatedUp = rotation * Vector3.up;
        Vector3 middle = position + rotatedUp * 0.5f;
        DrawCritterGizmoWithoutOrientation(position, rotatedUp);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(middle, middle + rotatedForward);
    }

    internal static void DrawCritterGizmoCluster(Vector3 position, float verticalAnchor = 0f)
    {
        Vector3[] upVectors = new Vector3[] { Vector3.up, Vector3.right, Vector3.left, Vector3.down };
        foreach (var up in upVectors)
        {
            Vector3 footPos = position - up * verticalAnchor;
            DrawCritterGizmoWithoutOrientation(footPos, up);
        }
    }
}