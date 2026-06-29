using System;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

public class JsonStringEnumDescriptionConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string stringValue = reader.GetString();

        foreach (var field in typeof(TEnum).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description.Equals(stringValue, StringComparison.OrdinalIgnoreCase))
                {
                    return (TEnum)field.GetValue(null);
                }
            }
            if (field.Name.Equals(stringValue, StringComparison.OrdinalIgnoreCase))
            {
                return (TEnum)field.GetValue(null);
            }
        }

        throw new JsonException($"Không thể chuyển đổi '{stringValue}' thành enum {typeof(TEnum).Name}");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        FieldInfo fi = value.GetType().GetField(value.ToString());
        if (fi != null && Attribute.GetCustomAttribute(fi, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
        {
            writer.WriteStringValue(attribute.Description);
        }
        else
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}