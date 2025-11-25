using api_prueba.Support;
using CommonTypes.Exceptions;
using CommonTypes.Log;
using CommonTypes.Settings.App;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using static CommonTypes.Language.LanguageSupport;
using System.Diagnostics;
using CommonTypes.Util.Text;
using CommonTypes.Responses;
using CommonTypes.Settings;
using CommonTypes.Settings.Keys.Items;

namespace api_prueba.Controllers
{
    
    public class PruebaController : ControllerBase
    {


        protected internal readonly AppSettings settings = Tools.Settings;
        protected internal readonly DBKeysSettings dbKeys = Tools.DBKeys;
        protected internal readonly LogWriter logger;
        private static readonly LanguageObject message_invalidToken = new LanguageObject("Invalid token", "Token inválido");
        private static readonly LanguageObject message_invalidOperation = new LanguageObject("Invalid operation", "Operacion inválida");
        private static readonly LanguageObject message_relogToken = new LanguageObject("Login required", "Login necessário");
        private static readonly LanguageObject message_expiredToken = new LanguageObject("Expired token", "Token Expirado");
        private static readonly LanguageObject message_notCompleteOperation = new LanguageObject("Operation not completed", "Operacion no completada");

        protected internal PruebaController(LogWriter logger) => this.logger = logger;
        protected internal Task<string> GetToken() => HttpContext.GetTokenAsync("access_token");

        protected internal ActionResult Default(bool expired, bool relog)
        {
            if (expired)
                return Expired();
            if (relog)
                return Relog();
            return InvalidToken();
        }

        protected internal ActionResult Success(object result = null, int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Success.", Tools.Settings.Log4Net.DetailedLog);
            return Ok(new GenericResponse<object>
            {
                Status = ReturnStatus.Success,
                Result = result,
            });
        }

        protected internal ActionResult InvalidOperation(object message = null, int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Invalid.", Tools.Settings.Log4Net.DetailedLog);
            return BadRequest(new GenericResponse<object>
            {
                Status = ReturnStatus.Error,
                Message = message ?? message_invalidOperation
            });
        }

        protected internal ActionResult Expired(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Expired.", Tools.Settings.Log4Net.DetailedLog);
            return Unauthorized(new GenericResponse<object>
            {
                Status = ReturnStatus.Expired,
                Message = message_expiredToken
            });
        }

        protected internal ActionResult InvalidToken(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Invalid.", Tools.Settings.Log4Net.DetailedLog);
            return StatusCode(403, new GenericResponse<object>
            {
                Status = ReturnStatus.Invalid,
                Message = message_invalidToken
            });
        }

        protected internal ActionResult Relog(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Relog.", Tools.Settings.Log4Net.DetailedLog);
            return StatusCode(403, new GenericResponse<object>
            {
                Status = ReturnStatus.Relog,
                Message = message_relogToken
            });
        }

        protected internal ActionResult Exception(Exception exc)
        {
            if (exc is MultilingualException mle)
                return InvalidOperation(mle.Message);
            else
            {
#if DEBUG
                Debug.Fail(exc.ToString());
#endif
                logger.LogError(exc);
                return StatusCode(500, new GenericResponse<object>
                {
                    Status = ReturnStatus.Error,
                    Message = message_systemError
                });
            }
        }
    }
}


