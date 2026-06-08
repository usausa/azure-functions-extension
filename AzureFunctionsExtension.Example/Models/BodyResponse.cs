namespace AzureFunctionsExtension.Example.Models;

internal sealed class BodyResponse
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}
