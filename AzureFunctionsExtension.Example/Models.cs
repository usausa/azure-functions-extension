namespace AzureFunctionsExtension.Example;

using System.ComponentModel.DataAnnotations;

public class QueryResponse
{
    public int Result { get; set; }
}

public class BodyRequest
{
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = default!;

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}

public class BodyResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = default!;

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}
