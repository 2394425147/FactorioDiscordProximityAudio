namespace Client.Models;

public struct FactorioPosition : IEquatable<FactorioPosition>
{
    public double x;
    public double y;
    public int    surfaceIndex;

    public bool Equals(FactorioPosition other)
    {
        return x.Equals(other.x) && y.Equals(other.y) && surfaceIndex == other.surfaceIndex;
    }

    public override bool Equals(object? obj)
    {
        return obj is FactorioPosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, surfaceIndex);
    }
}
