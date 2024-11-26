using api_prueba.Controllers;
using api_prueba.Support;
using CommonTypes.Log;
using Datamodels.Utils;
using System.Net;

namespace api_prueba.Middleware
{
    public class IPSecurityMiddleware : PruebaController
    {
        private readonly RequestDelegate _next;

        public IPSecurityMiddleware(RequestDelegate next, LogWriter logger) : base(logger) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            IPAddress ipAddress = context.Connection.RemoteIpAddress;
            if (!Tools.ApiFirewall.WhiteList().Any(r => r.Contains(ipAddress)) || Tools.ApiFirewall.BlackList().Any(r => r.Contains(ipAddress)))
            {
                logger.LogError($"Request from Remote IP address: {ipAddress} is forbidden.");
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                return;
            }
            await _next.Invoke(context);
        }
    }

    public class DataSecurityMiddleware : PruebaController
    {
        private readonly RequestDelegate _next;

        public DataSecurityMiddleware(RequestDelegate next, LogWriter logger) : base(logger) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            if (context._ValidForMiddleware() && context.Request.Path.StartsWithSegments($"/{Tools.dataPathReplacement}", StringComparison.OrdinalIgnoreCase))
                try
                {
                    string redirection = $"/{Tools.dataPath}/{Encryption.Decrypt_2(Encryption.FromURLFix(context.Request.Path.Value.Substring(Tools.dataPathReplacement.Length + 2)))}";
                    context.Request.Path = redirection;
                }
                catch
                {
                    logger.LogError($"Request to: {context?.Request?.Path.Value} is forbidden.");
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return;
                }
            await _next(context);
        }
    }
}




