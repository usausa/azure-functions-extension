namespace AzureFunctionsExtension.Mvc;

using System.Text.Json;

public class JsonOptions
{
    public JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
