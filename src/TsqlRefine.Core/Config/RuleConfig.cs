using System.Text.Json;
using System.Text.Json.Serialization;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Core.Config;

/// <summary>Kind of a typed rule option value.</summary>
public enum RuleOptionValueKind
{
    Flag,
    Number,
    Text
}

/// <summary>A typed option value kept inside the Core configuration layer.</summary>
public sealed record RuleOptionValue(
    RuleOptionValueKind Kind,
    bool BooleanValue = false,
    int Int32Value = 0,
    string? StringValue = null)
{
    public static RuleOptionValue FromBoolean(bool value) =>
        new(RuleOptionValueKind.Flag, BooleanValue: value);

    public static RuleOptionValue FromInt32(int value) =>
        new(RuleOptionValueKind.Number, Int32Value: value);

    public static RuleOptionValue FromString(string value) =>
        new(RuleOptionValueKind.Text, StringValue: value);
}

/// <summary>Severity and typed options configured for one rule.</summary>
[JsonConverter(typeof(RuleConfigJsonConverter))]
public sealed record RuleConfig(
    string Severity = "inherit",
    IReadOnlyDictionary<string, RuleOptionValue>? Options = null)
{
    public static implicit operator RuleConfig(string severity) => new(severity);
}

/// <summary>Exposes validated Core option values through the PluginSdk contract.</summary>
public sealed class RuleOptions(IReadOnlyDictionary<string, RuleOptionValue> values) : IRuleOptions
{
    public bool TryGetBoolean(string name, out bool value)
    {
        if (values.TryGetValue(name, out var configured) && configured.Kind == RuleOptionValueKind.Flag)
        {
            value = configured.BooleanValue;
            return true;
        }
        value = default;
        return false;
    }

    public bool TryGetInt32(string name, out int value)
    {
        if (values.TryGetValue(name, out var configured) && configured.Kind == RuleOptionValueKind.Number)
        {
            value = configured.Int32Value;
            return true;
        }
        value = default;
        return false;
    }

    public bool TryGetString(string name, out string? value)
    {
        if (values.TryGetValue(name, out var configured) && configured.Kind == RuleOptionValueKind.Text)
        {
            value = configured.StringValue;
            return true;
        }
        value = default;
        return false;
    }
}

internal sealed class RuleConfigJsonConverter : JsonConverter<RuleConfig>
{
    public override RuleConfig Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new RuleConfig(reader.GetString() ?? "inherit");
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Rule configuration must be a severity string or an object.");
        }

        var severity = "inherit";
        Dictionary<string, RuleOptionValue>? configuredOptions = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a rule configuration property.");
            }
            var propertyName = reader.GetString();
            reader.Read();
            switch (propertyName)
            {
                case "severity":
                    severity = reader.TokenType == JsonTokenType.String
                        ? reader.GetString() ?? "inherit"
                        : throw new JsonException("Rule severity must be a string.");
                    break;
                case "options":
                    configuredOptions = ReadOptions(ref reader);
                    break;
                default:
                    throw new JsonException($"Unknown rule configuration property '{propertyName}'.");
            }
        }
        return new RuleConfig(severity, configuredOptions);
    }

    public override void Write(Utf8JsonWriter writer, RuleConfig value, JsonSerializerOptions options)
    {
        if (value.Options is null or { Count: 0 })
        {
            writer.WriteStringValue(value.Severity);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("severity", value.Severity);
        writer.WritePropertyName("options");
        writer.WriteStartObject();
        foreach (var (name, option) in value.Options)
        {
            switch (option.Kind)
            {
                case RuleOptionValueKind.Flag:
                    writer.WriteBoolean(name, option.BooleanValue);
                    break;
                case RuleOptionValueKind.Number:
                    writer.WriteNumber(name, option.Int32Value);
                    break;
                case RuleOptionValueKind.Text:
                    writer.WriteString(name, option.StringValue);
                    break;
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static Dictionary<string, RuleOptionValue> ReadOptions(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Rule options must be an object.");
        }

        var result = new Dictionary<string, RuleOptionValue>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.TokenType == JsonTokenType.PropertyName
                ? reader.GetString()!
                : throw new JsonException("Expected a rule option name.");
            reader.Read();
            result[name] = reader.TokenType switch
            {
                JsonTokenType.True => RuleOptionValue.FromBoolean(true),
                JsonTokenType.False => RuleOptionValue.FromBoolean(false),
                JsonTokenType.Number when reader.TryGetInt32(out var number) => RuleOptionValue.FromInt32(number),
                JsonTokenType.String => RuleOptionValue.FromString(reader.GetString() ?? string.Empty),
                _ => throw new JsonException($"Rule option '{name}' must be a Boolean, 32-bit integer, or string.")
            };
        }
        return result;
    }
}
