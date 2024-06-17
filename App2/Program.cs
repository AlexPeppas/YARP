
using Microsoft.AspNetCore.Mvc;

namespace App2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            app.MapGet("/", (
                int? a,
                int? b,
                [FromHeader(Name = "App2SugarHeader")] string? app2SugarHeader) =>
            {
                return $"Hello World from App2 : {(a ?? 0) * (b ?? 0)}, Signature : {app2SugarHeader}";
            });

            app.Run();
        }
    }
}
