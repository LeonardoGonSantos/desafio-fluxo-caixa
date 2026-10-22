using System.Data;
using Dapper;

namespace Lancamentos.External.Persistence;

public static class DapperConfiguration
{
    private static readonly object Gate = new();
    private static bool _configured;

    public static void Configure()
    {
        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            DefaultTypeMap.MatchNamesWithUnderscores = true;
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            _configured = true;
        }
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => throw new InvalidCastException($"Não é possível converter {value.GetType()} em DateOnly."),
        };
    }
}
