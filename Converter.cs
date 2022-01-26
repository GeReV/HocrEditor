using System;

namespace HocrEditor;

public class Converter
{
    public static T? Convert<T>(object? value)
    {
        return (T?)Convert(value, typeof(T));
    }

    public static object? Convert(object? value, Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (value == null)
            {
                return null;
            }

            type = type.GetGenericArguments()[0];
        }

        if (type.IsEnum)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string)
            {
                return Enum.Parse(type, (string)value);
            }

            return Enum.ToObject(type, value);
        }

        return System.Convert.ChangeType(value, type);
    }

    public static T? ConvertValue<T>(object? input)
    {
        if (input == null)
        {
            return default;
        }

        var type = typeof(T);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsEnum)
        {
            if (input is string)
            {
                return (T)Enum.Parse(typeof(T), input.ToString()?.Trim() ?? string.Empty);
            }

            return (T)Enum.ToObject(type, input);
        }

        return (T)System.Convert.ChangeType(input, type);
    }


    public static T? SafeConvert<T>(object? value, T? defaultValue)
    {
        try
        {
            return Convert<T>(value);
        }
        catch
        {
            return defaultValue;
        }
    }
}
