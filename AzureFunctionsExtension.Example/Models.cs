namespace AzureFunctionsExtension.Example;

using System.ComponentModel.DataAnnotations;

internal sealed class QueryResponse
{
    public int Result { get; set; }
}

internal sealed class BodyRequest
{
    public static BodyRequest Empty { get; } = new();

    [Required]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}

internal sealed class BodyResponse
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}
