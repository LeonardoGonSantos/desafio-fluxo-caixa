namespace Lancamentos.Application.Common;

/// <param name="NextCursor">Cursor opaco da próxima página; nulo quando não há mais itens.</param>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
