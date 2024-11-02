using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class MathLib
{
    public static int Clamp01(int n)
    {
        return Clamp(n, 0, 1);
    }
    public static int ClampUnit(int n)
    {
        return Clamp(n, -1, 1);
    }
    public static int Clamp(int n, int min, int max)
    {
        if (n < min) return min;
        if (n > max) return max;
        return n;
    }

    public static Vector2Int GetVectorStep(Vector2Int direction) {
        return new Vector2Int(
            ClampUnit(direction.x),
            ClampUnit(direction.y)
        );
    }
    public static Vector3Int GetVectorStep(Vector3Int direction)
    {
        return new Vector3Int(
            ClampUnit(direction.x),
            ClampUnit(direction.y),
            ClampUnit(direction.z)
        );
    }

    public static Vector2Int GetOrthoStep(Vector2Int direction, Vector2 up)
    {
        int deltaX = direction.x;
        int deltaY = direction.y;
        if (deltaX == 0)
        {
            return new Vector2Int(0, ClampUnit(deltaY));
        }
        else if (deltaY == 0)
        {
            return new Vector2Int(ClampUnit(deltaX), 0);
        }

        // movement in two axes, complex; we must use the "up" vector
        // we'll assume that if one must move across two axes, they must move up first, to avoid going through a ramp.
        Vector2 relativeForwardUp = (up.normalized + ((Vector2)direction).normalized) / 2;
        // then we'll return whatever is the closest snap to this
        return RoundUnitVector(relativeForwardUp);
    }

    public static float BisectorAngle(float a, float b)
    {
        // we first normalize the two angles into the 0 - 360 degree interval
        float simpleAvg = (a + b) / 2;
        return GetClosestAngle(a, simpleAvg, simpleAvg + 180);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetClosestAngle(float to, float a, float b)
    {
        float distA = GetAngleDistance(to, a);
        float distB = GetAngleDistance(to, b);
        return distA < distB ? a : b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetAngleDistance(float from, float to)
    {
        float delta = Mathf.Abs(from - to) % 360;
        return Mathf.Min(delta, 360 - delta);
    }

    /// <summary>
    /// Converts a vector2 into the counter-clockwise angle it would form with a vector pointing right.
    /// </summary>
    /// <param name="vector">The direction vector to convert into an angle. Should have a positive magnitude.</param>
    /// <returns>The amplitude of the angle, in degrees</returns>
    public static float AngleFromVector2(Vector2 vector)
    {
        return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
    }
    /// <summary>
    /// Calculates the vector with which another vector pointing right would form a counter-clockwise angle equaling the provided angle.
    /// </summary>
    /// <param name="angle">The target amplitude of the angle, in degrees</param>
    /// <returns>A unit vector satisfying the angle constraint.</returns>
    public static Vector2 Vector2FromAngle(float angle)
    {
        angle *= Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    /// <summary>
    /// Rounds a unit vector to align to one of four cardinal directions
    /// </summary>
    /// <param name="direction">The unit vector to approximate</param>
    /// <returns>One of four cardinal unit vectors; up, left, down, right</returns>
    public static Vector2Int RoundUnitVector(Vector2 direction)
    {
        if (Mathf.Abs(direction.y) > Mathf.Abs(direction.x))
        {
            return direction.y > 0 ? Vector2Int.up : Vector2Int.down;
        } 
        else
        {
            return direction.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
    }

    /// <summary>
    /// Rounds each component of a vector individually
    /// </summary>
    /// <param name="vector">The vector to round</param>
    /// <returns>The rounded integer vector</returns>
    public static Vector2Int RoundVector(Vector2 vector)
    {
        return new Vector2Int(
            Mathf.RoundToInt(vector.x),
            Mathf.RoundToInt(vector.y)
        );
    }

    public static Vector2 RotateVector2(Vector2 vector, float angle)
    {
        return Vector2FromAngle(AngleFromVector2(vector) + angle) * vector.magnitude;
    }

    /// <summary>
    /// This calculates a quaternion to rotate an entity standing up to one looking in the specified angle from the right vector, on the XY plane.
    /// </summary>
    /// <param name="angle">The counter-clockwise angle to form with the right vector in the XY plane.</param>
    /// <param name="rightSidePointsAway">Whether the right-hand side of the entity should point towards the camera (negative Z) or away (positive Z)</param>
    /// <returns>A valid quaternion rotation matching the specifications above.</returns>
    public static Quaternion GetRotationFromAngle(float angle, bool rightSidePointsAway = false)
    {
        if (rightSidePointsAway)
        {
            // "up" is a rotation of 0, again.
            return Quaternion.Euler(angle - 90, 270, 0);
        }
        else
        {
            // "up" is a rotation of 0. So we shift the angular space by 90 degrees.
            // also we need to change the interpretation from clockwise to counter-clockwise.
            return Quaternion.Euler(-angle + 90, 90, 0);
        }
    }

    /// <summary>
    /// Calculates the counter-clockwise angle in the XY plane formed by the forward vector of an entity rotated by rotation and the right vector.
    /// </summary>
    /// <param name="rotation">The rotation of the entity.</param>
    /// <param name="rightSidePointsAway">Whether the specified rotation has the right-hand side of the entity pointing towards the camera (negative Z) or away (positive Z)</param>
    /// <returns>The amplitude of the angle specified above, in degrees.</returns>
    public static float GetAngleFromRotation(Quaternion rotation, out bool rightSidePointsAway)
    {
        Vector3 angles = rotation.eulerAngles;
        // looking left when standing upright (its right points towards the background, thus x rotation is counter-clockwise)
        if (angles.y > 180 || angles.y <= 0)
        {
            rightSidePointsAway = true;
            return angles.x + 90;
        }
        // looking right when standing upright (its right points towards the camera, thus x rotation is clockwise)
        else
        {
            rightSidePointsAway = false;
            return -angles.x + 90;
        }
    }
}
