namespace Decay.Renderer
{
    /// <summary>
    /// Small numeric helpers kept in-house so the engine compiles unchanged
    /// for .NET Framework 4.8 (Math.Clamp does not exist there).
    /// </summary>
    internal static class Num
    {
        internal static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }

        internal static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
