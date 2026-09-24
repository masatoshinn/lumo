namespace Lumo.Engine.Core;

/// <summary>
/// Helper for degree/radian conversions and common math operations.
/// </summary>
public static class MathHelper
{
    public const float Deg2Rad = MathF.PI / 180.0f;
    public const float Rad2Deg = 180.0f / MathF.PI;

    public static float DegreesToRadians(float degrees) => degrees * Deg2Rad;
    public static float RadiansToDegrees(float radians) => radians * Rad2Deg;
}
