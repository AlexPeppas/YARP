
namespace Proxy
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            app.MapGet("/", (int? a, int? b) => $"Hello World from App1 : {(a??0)+ (b??0)}");

            app.Run();
        }
    }
}
