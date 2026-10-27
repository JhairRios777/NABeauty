using System.Globalization;
using System.Text;

namespace NABeauty.Core.Dominio;

/// <summary>
/// "Labial Mate Rojo Pasión" → "labial-mate-rojo-pasion". Mismo resultado que la tienda en línea:
/// la URL del producto en la web sale de aquí.
/// </summary>
public static class Slug
{
    public static string Crear(string texto, int max = 150)
    {
        var sinAcentos = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sinAcentos.Append(c);

        var sb = new StringBuilder();
        var guion = false;
        foreach (var c in sinAcentos.ToString().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(c); guion = false; }
            else if (!guion && sb.Length > 0) { sb.Append('-'); guion = true; }
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > max) slug = slug[..max].TrimEnd('-');
        return slug.Length == 0 ? "item" : slug;
    }
}
