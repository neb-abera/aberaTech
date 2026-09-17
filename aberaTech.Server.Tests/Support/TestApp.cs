using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace aberaTech.Server.Tests.Support;

/// <summary>
/// The server as deployed — Production environment, a web root with a shell in
/// it — with whatever settings one test needs on top.
/// </summary>
/// <remarks>
/// Production rather than the factory's default of Development, because these
/// tests are about what a stranger on the internet meets: no development seed
/// data, no fitness sign-in bypass, no appsettings.Development.json pointing
/// at a loopback database.
/// </remarks>
public sealed class TestApp : IDisposable
{
    private readonly string _webRoot;

    public TestApp(
        IReadOnlyDictionary<string, string?> settings,
        Action<IServiceCollection>? services = null)
    {
        _webRoot = Directory.CreateTempSubdirectory("wwwroot-security").FullName;
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<html>home</html>");
        File.WriteAllText(Path.Combine(_webRoot, "spa.html"), "<html>shell</html>");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseWebRoot(_webRoot);
            builder.UseSetting("ConnectionStrings:Scheduling", "");
            builder.UseSetting("ConnectionStrings:Fitness", "");

            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureServices(collection =>
            {
                collection.AddTransient<IStartupFilter, RemoteAddressStartupFilter>();
                services?.Invoke(collection);
            });
        });
    }

    public WebApplicationFactory<Program> Factory { get; }

    /// <summary>A client that follows nothing and remembers nothing, like curl.</summary>
    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    public void Dispose()
    {
        Factory.Dispose();
        if (Directory.Exists(_webRoot)) Directory.Delete(_webRoot, recursive: true);
    }
}
