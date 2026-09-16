namespace MetroCIM.Models;

public readonly record struct ResolvedAppearance(byte R, byte G, byte B, byte A)
{
    public static ResolvedAppearance Default { get; } = new(180, 180, 180, 255);

    public bool IsNearBlack => R < 16 && G < 16 && B < 16;

    public System.Numerics.Vector4 ToBaseColor() =>
        new(R / 255f, G / 255f, B / 255f, A / 255f);

    public static ResolvedAppearance FromRgb(byte r, byte g, byte b, int transparencyPercent = 0)
    {
        int t = transparencyPercent < 0 ? 0 : transparencyPercent > 100 ? 100 : transparencyPercent;
        byte a = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(255d * (100 - t) / 100d)));
        return new ResolvedAppearance(r, g, b, a);
    }
}
