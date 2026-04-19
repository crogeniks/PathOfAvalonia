namespace PathOfAvalonia.TreeDomain;

public abstract record Connector(int FromId, int ToId);

public sealed record LineConnector(
    int FromId, int ToId,
    double X1, double Y1, double X2, double Y2)
    : Connector(FromId, ToId);

public sealed record ArcConnector(
    int FromId, int ToId,
    double Cx, double Cy, double Radius,
    double StartAngle, double SweepAngle)
    : Connector(FromId, ToId);
