using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Meowdoku.Solver;

public class Program
{
    
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Pages.NotFound))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Pages.Solver))]
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");
        // builder.Services.AddLocalization();

        builder.Services.AddScoped(_ => new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        });

        var host = builder.Build();
        
        await host.RunAsync();
    }
}