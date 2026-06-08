namespace AzureFunctionsExtension.Example.Services;

internal interface IGreetingService
{
    string Greet(string name);
}

internal sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
