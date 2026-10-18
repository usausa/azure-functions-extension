namespace AzureFunctionsExtension.Hosting;

using System.ComponentModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class GeneratedFunctionCatalog
{
    private static IGeneratedFunctionCatalog[] catalogs = [];

    internal static IReadOnlyList<IGeneratedFunctionCatalog> Catalogs => Volatile.Read(ref catalogs);

    public static void ConfigureStartup(IHostBuilder builder)
    {
        builder.ConfigureServices(static services => services.AddSingleton<IHostedService>(static sp => new ExtensionStartupValidator(sp)));
    }

    public static void Register(IGeneratedFunctionCatalog catalog)
    {
        while (true)
        {
            var current = Volatile.Read(ref catalogs);
            if (current.Any(x => x.GetType() == catalog.GetType()))
            {
                return;
            }

            if (Interlocked.CompareExchange(ref catalogs, [.. current, catalog], current) == current)
            {
                return;
            }
        }
    }
}
