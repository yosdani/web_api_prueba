using api_prueba.Middleware;
using Microsoft.VisualBasic;

namespace api_prueba.Support
{
    internal static class Extensions
    {
        internal static IApplicationBuilder _UseIPSecurity(this IApplicationBuilder builder) => builder.UseMiddleware<IPSecurityMiddleware>();

        internal static IApplicationBuilder _UseDataSecurity(this IApplicationBuilder builder) => builder.UseMiddleware<DataSecurityMiddleware>();


        internal static bool _ValidForMiddleware(this HttpContext context) => context?.Request?.Path.HasValue == true && !string.IsNullOrWhiteSpace(context.Request.Path.Value);


    }
}