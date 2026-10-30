using NJsonSchema;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Analytics.Infrastructure.Messaging;

public sealed class AsyncApiSchemas
{
    public const string AnalyticsEventsResourceName = "analytics-events.asyncapi.v1.yaml";

    private readonly Dictionary<string, JsonSchema> _schemas;

    private AsyncApiSchemas(Dictionary<string, JsonSchema> schemas)
    {
        _schemas = schemas;
    }

    // Der eigene Vertrag für analytics.events. Nachrichten der Bank liest dieser Dienst nicht mehr.
    public static AsyncApiSchemas FromEmbeddedContracts()
    {
        using Stream stream = typeof(AsyncApiSchemas).Assembly.GetManifestResourceStream(AnalyticsEventsResourceName)
            ?? throw new InvalidOperationException($"Die eingebettete Ressource '{AnalyticsEventsResourceName}' fehlt.");

        return Load(stream);
    }

    public static AsyncApiSchemas Load(Stream asyncApiYaml)
    {
        using StreamReader reader = new StreamReader(asyncApiYaml);

        IDeserializer yaml = new DeserializerBuilder().Build();
        object? graph = yaml.Deserialize(new StringReader(reader.ReadToEnd()));

        ISerializer json = new SerializerBuilder().JsonCompatible().Build();
        using JsonDocument document = JsonDocument.Parse(json.Serialize(graph));

        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        Dictionary<string, JsonSchema> loaded = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (JsonProperty schema in schemas.EnumerateObject())
        {
            loaded[schema.Name] = JsonSchema.FromJsonAsync(schema.Value.GetRawText()).GetAwaiter().GetResult();
        }

        return new AsyncApiSchemas(loaded);
    }

    public bool Knows(string messageType)
    {
        return _schemas.ContainsKey(messageType);
    }

    public IReadOnlyList<string> Validate(string messageType, string payload)
    {
        if (!_schemas.TryGetValue(messageType, out JsonSchema? schema))
        {
            return new[] { $"Der Kontrakt kennt den Nachrichtentyp '{messageType}' nicht." };
        }

        return schema.Validate(payload)
            .Select(error => $"{error.Path}: {error.Kind}")
            .ToList();
    }
}
