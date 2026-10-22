using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Lancamentos.Application.Common;

/// <summary>
/// Posição da última linha entregue na listagem (ordem: data de competência, criação e id, decrescentes).
/// Opaco para o cliente (Base64Url); a página seguinte começa estritamente DEPOIS desta posição.
/// </summary>
public sealed record LancamentoCursor(DateOnly DataCompetencia, DateTimeOffset CriadoEm, Guid Id)
{
    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{DataCompetencia:yyyy-MM-dd}|{CriadoEm.UtcTicks}|{Id:N}")));

    public static bool TryDecode(string? value, out LancamentoCursor cursor)
    {
        cursor = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split('|');
            if (parts.Length != 3
                || !DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || ticks < 0 || ticks > DateTime.MaxValue.Ticks
                || !Guid.TryParseExact(parts[2], "N", out var id))
            {
                return false;
            }

            cursor = new LancamentoCursor(data, new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
