namespace Yarp;

using System.Security.Cryptography.X509Certificates;

using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Transforms;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.ConfigureKestrel(options =>
        {
            using X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, "localhost", validOnly: true);

            if (certificates is not null && certificates.Any())
            {
                options.ListenAnyIP(7160, listenOptions =>
                {
                    listenOptions.UseHttps(certificates.First());
                });

                /*options.ConfigureHttpsDefaults(httpsOptions =>
                {
                    httpsOptions.ServerCertificate = certificates.First();
                });*/
            }
        });

        builder.Services
            .AddReverseProxy()
            //.LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .LoadFromMemory(LoadRoutes(), LoadClusers())
            .AddTransforms(builderCtx =>
            {
                builderCtx.AddRequestHeader("App2SugarHeader", "Sugar", append: true);
            })
            .Services.AddSingleton<ILoadBalancingPolicy, ConsistentHashingPolicy>();

        var app  = builder.Build();

        app.Logger.LogInformation("Application launched");

        app.MapGet("/api", () => "Hello World from YARP");

        app.MapReverseProxy();

        app.Run();
    }

    private static IReadOnlyList<RouteConfig> LoadRoutes()
    {
        var routes = new[]
        {
            new RouteConfig
            {
                RouteId = "route1",
                ClusterId = "cluster1",
                Match = new RouteMatch
                {
                    Path = "/{**catch-all}"
                }
            }
        };

        return routes;
    }

    private static IReadOnlyList<ClusterConfig> LoadClusers()
    {
        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = "cluster1",
                LoadBalancingPolicy = "ConsistentHashing",
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    { "destination1", new DestinationConfig() { Address = "https://localhost:7215/" } },
                    { "destination2", new DestinationConfig() { Address = "https://localhost:7207" } }
                }
            }
        };

        return clusters;
    }
}