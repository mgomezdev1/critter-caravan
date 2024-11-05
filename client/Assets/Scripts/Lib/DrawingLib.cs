using UnityEngine;

public static class DrawingLib
{
    public static void DrawCritterGizmo(this Surface surface, GridWorld grid, bool rightSidePointsAway)
    {
        DrawCritterGizmo(surface.GetStandingPosition(grid), surface.GetStandingRotation(), rightSidePointsAway);
    }

    public static void DrawCritterGizmo(Transform surfaceTransform)
    {
        DrawCritterGizmo(surfaceTransform.position, surfaceTransform.rotation);
    }

    public static void DrawCritterGizmo(Vector3 position, float angle, bool rightSidePointsAway)
    {
        Quaternion rotation = MathLib.GetRotationFromAngle(angle, rightSidePointsAway);
        DrawCritterGizmo(position, rotation);
    }

    public static void DrawCritterGizmo(Vector3 position, Quaternion rotation)
    {
        Vector3 rotatedForward = rotation * Vector3.forward;
        Vector3 rotatedUp = rotation * Vector3.up;
        Vector3 middle = position + rotatedUp * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(middle, middle + rotatedForward);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(position, position + rotatedUp);
        Gizmos.DrawWireSphere(position + rotatedUp, 0.3f);
    }
}