using api_prueba.Auth;
using api_prueba.Controllers;
using api_prueba.Models;
using CommonTypes.Log;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static CommonTypes.Language.LanguageSupport;
using static Dapper.SqlMapper;

namespace api_stocktaking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebServiceController : PruebaController
    {
        private static readonly LanguageObject message_wrongCredentials = new LanguageObject("Wrong credentials", "Credenciais erradas");
        private static readonly LanguageObject message_userActivation = new LanguageObject("Stocktaking user activation", "Ativação do utilizador do SEAMInd");
        private static readonly LanguageObject message_passRecovery = new LanguageObject("Stocktaking password recover attempt", "Tentativa de recuperação de senha do SEAMind");
        private readonly IJwtAuthenticationService _authService;
        public WebServiceController(LogWriter logger, JwtAuthenticationService authService) : base(logger) => _authService = authService;
     
        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody] AuthInfo user)
        {
            try
            {
                                var request = JsonSerializer.Serialize(user);
                logger.LogInfo($"Request to authenticate. {Environment.NewLine}Request: {request}", settings.Log4Net.DetailedLog);
                var fullAuthentication = user.fullAuthentication.HasValue ? user.fullAuthentication.Value : true;
                var token = _authService.Authenticate(user.Username, user.Password, out DateTime? expires, fullAuthentication);

                if (token == null)
                {
                    return Unauthorized();
                }

                return Success(new
                {
                    Token = token,
                    ExpiresUtc = expires
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex);

                return Exception(ex);
            }
        }

        [HttpGet("get_publictoken")]
        public IActionResult GetPublicToken()
        {
            try
            {
                logger.LogInfo($"GetPublicToken.", settings.Log4Net.DetailedLog);
                return Success(new
                {
                    Token = _authService.GetToken_GUID(out DateTime? expire),
                    ExpiresUtc = expire
                });
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }
        [AllowAnonymous]
        [HttpGet]
        public object Get()
        {
            var responseObject = new
            {
                Status = "Running",

            };
            logger.LogInfo($"APIStatus: {responseObject.Status}", settings.Log4Net.DetailedLog);
            return responseObject;
        }
     
    }
}
