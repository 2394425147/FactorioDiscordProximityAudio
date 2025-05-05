using System.Runtime.InteropServices;

namespace Client.Models;

[Serializable, StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public struct ClientPosition : IEquatable<ClientPosition>
{
    public double x;
    public double y;
    public int    surfaceIndex;

    public bool Equals(ClientPosition other)
    {
        return x.Equals(other.x) && y.Equals(other.y) && surfaceIndex == other.surfaceIndex;
    }

    public override bool Equals(object? obj)
    {
        return obj is ClientPosition other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(x, y, surfaceIndex);
    }

    public static bool operator ==(ClientPosition left, ClientPosition right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ClientPosition left, ClientPosition right)
    {
        return !(left == right);
    }
}
