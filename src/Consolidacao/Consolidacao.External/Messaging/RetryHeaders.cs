using System.Globalization;
using System.Text;

namespace Consolidacao.External.Messaging;

/// <summary>Contagem de tentativas de processamento, carregada num header da própria mensagem.</summary>
public static class RetryHeaders
{
    public const string RetryCount = "x-retry-count";

    /// <summary>Quantas vezes a mensagem já foi reprocessada (0 se for a primeira entrega).</summary>
    public static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryCount, out var value))
        {
            return 0;
        }

        return value switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture, out var n) => n,
            string text when int.TryParse(text, CultureInfo.InvariantCulture, out var n) => n,
            _ => 0,
        };
    }
}
