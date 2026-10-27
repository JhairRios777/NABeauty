using System.Security.Cryptography;

namespace NABeauty.Core.Seguridad;

/// <summary>
/// PBKDF2-SHA256 (mismo formato que AnaMarcolPOS): "{iteraciones}.{salt}.{hash}".
/// 600,000 iteraciones = recomendación OWASP 2023+ para PBKDF2-SHA256.
/// Verificar() acepta hashes con otro número de iteraciones (los viejos siguen sirviendo).
/// </summary>
public static class PasswordHasher
{
    public const int Iteraciones = 600_000;
    private const int TamanoSalt = 16;
    private const int TamanoHash = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"{Iteraciones}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string password, string? almacenado)
    {
        var partes = (almacenado ?? "").Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iter) || iter < 1) return false;
        try
        {
            var salt = Convert.FromBase64String(partes[1]);
            var esperado = Convert.FromBase64String(partes[2]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool NecesitaRehash(string almacenado)
        => !int.TryParse(almacenado.Split('.')[0], out var iter) || iter < Iteraciones;

    /// <summary>Mínimo 10 caracteres con letras y números. Devuelve el error o null.</summary>
    public static string? ValidarFortaleza(string password)
    {
        if (password.Length < 10) return "La contraseña debe tener al menos 10 caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "La contraseña debe combinar letras y números.";
        return null;
    }

    /// <summary>Contraseña temporal de 12 caracteres sin ambiguos (0/O, 1/l).</summary>
    public static string Temporal()
    {
        const string letras = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ";
        const string numeros = "23456789";
        var c = new char[12];
        for (var i = 0; i < 9; i++) c[i] = letras[RandomNumberGenerator.GetInt32(letras.Length)];
        for (var i = 9; i < 12; i++) c[i] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];
        RandomNumberGenerator.Shuffle(c.AsSpan());
        return new string(c);
    }
}
