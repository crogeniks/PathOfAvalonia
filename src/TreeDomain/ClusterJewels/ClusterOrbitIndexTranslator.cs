namespace PathOfAvalonia.TreeDomain.ClusterJewels;

internal static class ClusterOrbitIndexTranslator
{
    public static int Translate(int sourceOrbitIndex, int sourceNodesPerOrbit, int destinationNodesPerOrbit)
    {
        if (sourceNodesPerOrbit == destinationNodesPerOrbit) return sourceOrbitIndex;
        if (sourceNodesPerOrbit == 12 && destinationNodesPerOrbit == 16)
        {
            return new[] { 0, 1, 3, 4, 5, 7, 8, 9, 11, 12, 13, 15 }[sourceOrbitIndex];
        }
        if (sourceNodesPerOrbit == 16 && destinationNodesPerOrbit == 12)
        {
            return new[] { 0, 1, 1, 2, 3, 4, 4, 5, 6, 7, 7, 8, 9, 10, 10, 11 }[sourceOrbitIndex];
        }
        if (sourceNodesPerOrbit == 6 && destinationNodesPerOrbit == 16)
        {
            return new[] { 0, 3, 5, 8, 11, 13 }[sourceOrbitIndex];
        }
        if (sourceNodesPerOrbit == 16 && destinationNodesPerOrbit == 6)
        {
            return new[] { 0, 0, 0, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 5, 5, 5 }[sourceOrbitIndex];
        }
        return (int)Math.Floor(sourceOrbitIndex * destinationNodesPerOrbit / (double)sourceNodesPerOrbit);
    }
}
