using System.Data;
using Microsoft.Data.SqlClient;

namespace Custom_keyboard.Repositories.SqlServer;

internal static class SqlRepositoryHelpers
{
    public static SqlParameter AddParameter(this SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = command.Parameters.Add(name, type);
        if (size != 0)
        {
            parameter.Size = size;
        }

        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    public static SqlParameter AddDecimalParameter(this SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 10;
        parameter.Scale = 2;
        parameter.Value = value;
        return parameter;
    }

    public static string GetStringValue(this SqlDataReader reader, string columnName)
    {
        return reader.GetString(reader.GetOrdinal(columnName));
    }

    public static string? GetNullableStringValue(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int GetIntValue(this SqlDataReader reader, string columnName)
    {
        return reader.GetInt32(reader.GetOrdinal(columnName));
    }

    public static int? GetNullableIntValue(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static decimal GetDecimalValue(this SqlDataReader reader, string columnName)
    {
        return reader.GetDecimal(reader.GetOrdinal(columnName));
    }

    public static bool GetBoolValue(this SqlDataReader reader, string columnName)
    {
        return reader.GetBoolean(reader.GetOrdinal(columnName));
    }

    public static bool? GetNullableBoolValue(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
    }

    public static DateTime GetDateTimeValue(this SqlDataReader reader, string columnName)
    {
        return reader.GetDateTime(reader.GetOrdinal(columnName));
    }

    public static DateTime? GetNullableDateTimeValue(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    public static TEnum GetEnumValue<TEnum>(this SqlDataReader reader, string columnName)
        where TEnum : struct, Enum
    {
        var value = reader.GetStringValue(columnName);
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var normalizedValue = value.Replace("_", string.Empty);
        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(
                name.Replace("_", string.Empty),
                normalizedValue,
                StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<TEnum>(name);
            }
        }

        return Enum.Parse<TEnum>(value, ignoreCase: true);
    }
}
