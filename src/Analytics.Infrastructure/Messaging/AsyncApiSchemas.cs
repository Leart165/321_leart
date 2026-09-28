using NJsonSchema;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Analytics.Infrastructure.Messaging;

public sealed class AsyncApiSchemas
{
    public const string EventsResourceName = "events.asyncapi.v1.yaml";
    public const string PartnerResourceName = "partner.asyncapi.v1.yaml";

    private readonly Dictionary<string, JsonSchema> _schemas;

    private AsyncApiSchemas(Dictionary<string, JsonSchema> schemas)
    {
        _schemas = schemas;
    }

    // Beide Verträge der Bank, die dieser Dienst liest. Die Namen der Schemas überschneiden sich
    // nicht: TransactionCompleted intern, PartnerTransactionCompleted für Partner.
    public static AsyncApiSchemas FromEmbeddedContracts()
    {
        Dictionary<string, JsonSchema> schemas = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (string resource in new[] { EventsResourceName, PartnerResourceName })
        {
            using Stream stream = typeof(AsyncApiSchemas).Assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Die eingebettete Ressource '{resource}' fehlt.");

            foreach (KeyValuePair<string, JsonSchema> schema in Load(stream)._schemas)
            {
                schemas[schema.Key] = schema.Value;
            }
        }

        return new AsyncApiSchemas(schemas);
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
