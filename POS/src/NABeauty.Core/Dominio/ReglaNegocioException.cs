namespace NABeauty.Core.Dominio;

/// <summary>
/// Error esperado por una regla de negocio (saldo insuficiente, hito ya marcado…).
/// La UI lo muestra tal cual al usuario; cualquier otra excepción es un bug.
/// </summary>
public sealed class ReglaNegocioException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);
