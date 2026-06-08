namespace AzureFunctionsExtension.Example.Models;

using System.ComponentModel.DataAnnotations;

internal sealed class BodyRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = default!;

    public bool Flag { get; set; }

    public DateTime DateTime { get; set; }
}
