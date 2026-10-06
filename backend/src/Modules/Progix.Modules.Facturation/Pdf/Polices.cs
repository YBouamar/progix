using System.Reflection;
using PdfSharp.Fonts;

namespace Progix.Modules.Facturation.Pdf;

/// <summary>Police embarquée (DejaVu Sans, licence libre) : le conteneur de l'API n'a aucune police système.</summary>
public sealed class Polices : IFontResolver
{
    public const string Famille = "DejaVu Sans";
    private static readonly object Verrou = new();

    public static void Installer()
    {
        lock (Verrou) { if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new Polices(); }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new($"DejaVuSans{(bold || italic ? "-" : "")}{(bold ? "Bold" : "")}{(italic ? "Oblique" : "")}");

    public byte[]? GetFont(string faceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var nom = asm.GetManifestResourceNames().First(n => n.EndsWith($".{faceName}.ttf", StringComparison.Ordinal));
        using var flux = asm.GetManifestResourceStream(nom)!;
        using var ms = new MemoryStream();
        flux.CopyTo(ms);
        return ms.ToArray();
    }
}
