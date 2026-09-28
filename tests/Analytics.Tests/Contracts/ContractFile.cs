using System.Text.Json;
using YamlDotNet.Serialization;

namespace Analytics.Tests.Contracts;

// Die Tests lesen den Kontrakt, statt ihn abzuschreiben: nur so belegen sie, dass der Code zu
// dem passt, was mit der Bank vereinbart ist.
public static class ContractFile
{
    public static JsonDocument Load(string relativePath)
    {
        string full = Path.Combine(RepositoryRoot(), "contracts", relativePath);
        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                $"Der Kontrakt fehlt: {full}. Kopie aktualisieren mit: cp -R ../m321-main/contracts/. contracts/");
        }

        IDeserializer yaml = new DeserializerBuilder().Build();
        object? graph = yaml.Deserialize(new StringReader(File.ReadAllText(full)));

        ISerializer json = new SerializerBuilder().JsonCompatible().Build();
        return JsonDocument.Parse(json.Serialize(graph));
    }

    public static string RepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Analytics.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Das Wurzelverzeichnis des Repos wurde nicht gefunden.");
    }
}
