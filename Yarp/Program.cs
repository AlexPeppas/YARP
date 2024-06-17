namespace Yarp;

using System.Security.Cryptography.X509Certificates;

using Microsoft.AspNetCore.RateLimiting;

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
            .Services
                .AddSingleton<ILoadBalancingPolicy, ConsistentHashingPolicy>()
                .AddRateLimiter(limiterOpt =>
                {
                    limiterOpt.AddFixedWindowLimiter("myCustomPolicy", opt =>
                    {
                        /*
                         *  1. PermitLimit
                            Description: Specifies the maximum number of permits (requests) allowed within each time window.
                            Example: opt.PermitLimit = 4; means that up to 4 requests are allowed in each time window.

                            2. Window
                            Description: Defines the duration of the fixed window.
                            Example: opt.Window = TimeSpan.FromSeconds(12); means that each time window is 12 seconds long.

                            3. QueueProcessingOrder
                            Description: Determines the order in which requests are processed when they are queued due to exceeding the rate limit.
                            Options:
                            QueueProcessingOrder.OldestFirst: Processes the oldest request in the queue first.
                            QueueProcessingOrder.NewestFirst: Processes the newest request in the queue first.
                            Example: opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; means that when the rate limit is exceeded, the requests will be queued and processed in the order they arrived, oldest requests first.

                            4. QueueLimit
                            Description: Specifies the maximum number of requests that can be queued when the rate limit is exceeded.
                            Example: opt.QueueLimit = 2; means that up to 2 requests can be queued if the rate limit is reached. Additional requests will be rejected until the queue has space.
                         */
                        opt.PermitLimit = 4;
                        opt.Window = TimeSpan.FromSeconds(10);
                        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                        opt.QueueLimit = 2;
                    });

                    limiterOpt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    limiterOpt.OnRejected = async (context, cts) =>
                    {
                        cts.ThrowIfCancellationRequested();

                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                        logger.LogWarning($"Path {context.HttpContext.Request.Path} has been rate-limited");

                        await context.HttpContext.Response.WriteAsync("Rate limit exceeded, please try again later", cts);
                    };
                });

        var app  = builder.Build();

        app.Logger.LogInformation("Application launched");

        app.MapGet("/api", () => "Hello World from YARP");

        app.UseRateLimiter();

        app.MapReverseProxy();

        app.Run();
    }

    private static IReadOnlyList<RouteConfig> LoadRoutes()
    {
        var routes = new[]
        {
            new RouteConfig
            {
                RateLimiterPolicy = "myCustomPolicy",
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