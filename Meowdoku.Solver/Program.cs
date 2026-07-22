using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Meowdoku.Solver;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        builder.Services.AddLocalization();

        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        });

        var host = builder.Build();

        host.SetLocaleFromQueryString();

        await host.RunAsync();
    }

    private static void SetLocaleFromQueryString(this WebAssemblyHost host)
    {
        var navigationManager = host.Services.GetRequiredService<NavigationManager>();
        var uri = new Uri(navigationManager.Uri);
        var query = uri.Query;
        if (query.StartsWith('?'))
            query = query[1..];
        var locale = query.Split("&").FirstOrDefault(x => x.StartsWith("locale="))?.Split('=').LastOrDefault();

        if (!string.IsNullOrWhiteSpace(locale))
        {
            try
            {
                CultureInfo.CurrentUICulture = new CultureInfo(locale);
                CultureInfo.CurrentCulture = new CultureInfo(locale);
            }
            catch
            {
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            }

        }
        else
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}