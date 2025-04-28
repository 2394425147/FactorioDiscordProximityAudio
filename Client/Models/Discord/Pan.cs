namespace Client.Models.Discord;

public class Pan
{
    public float left  { get; set; }
    public float right { get; set; }

    public static Pan Calculate(FactorioPosition localPosition, FactorioPosition position)
    {
        if (localPosition.surfaceIndex != position.surfaceIndex)
            return new Pan
            {
                left  = 0.0f,
                right = 0.0f
            };

        const double falloffRadiusSqr = 100.0 * 100.0;
        const double earOffset        = 0.5;

        var leftEarDistanceSqr  = DistanceSqr(localPosition.x, localPosition.y, position.x - earOffset, position.y);
        var rightEarDistanceSqr = DistanceSqr(localPosition.x, localPosition.y, position.x + earOffset, position.y);

        var proximityLeft  = Proximity(leftEarDistanceSqr,  falloffRadiusSqr);
        var proximityRight = Proximity(rightEarDistanceSqr, falloffRadiusSqr);

        return new Pan
        {
            left  = (float)proximityLeft,
            right = (float)proximityRight
        };
    }

    private static double DistanceSqr(double x1, double y1, double x2, double y2)
    {
        return (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
    }

    private static double Proximity(double leftEarDistanceSqr, double falloffRadiusSqr)
    {
        if (leftEarDistanceSqr > falloffRadiusSqr)
            return 0.0;

        return 1 - (falloffRadiusSqr - leftEarDistanceSqr) / falloffRadiusSqr;
    }
}
