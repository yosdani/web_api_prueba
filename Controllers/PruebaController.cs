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

namespace api_prueba.Controllers
{
    public abstract class PruebaController : ControllerBase
    {
        private static readonly LanguageObject message_invalidToken = new LanguageObject("Invalid token", "Token inválido");
        private static readonly LanguageObject message_relogToken = new LanguageObject("Login required", "Login necessário");
        private static readonly LanguageObject message_expiredToken = new LanguageObject("Expired token", "O token expirou");
        private static readonly LanguageObject message_invalidOperation = new LanguageObject("Invalid operation", "Operação inválida");
        protected internal readonly AppSettings settings = Tools.Settings;
        protected internal readonly DBKeysSettings dbKeys = Tools.DBKeys;
        protected internal static readonly int[] adminEditorRoles = new int[] { Tools.DBKeys.UserRoles.Admin, Tools.DBKeys.UserRoles.Invitado }, adminEditorPartnerRoles = new List<int>(adminEditorRoles) { Tools.DBKeys.UserRoles.Public }.ToArray();
        protected internal readonly LogWriter logger;

        protected internal PruebaController(LogWriter logger) => this.logger = logger;

        protected internal string WebRootURL => $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        protected internal string DataURL => $"{WebRootURL}/{Tools.dataPathReplacement}";

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
                Status = ReturnStatus.Success.ToString(),
                Result = result
            });
        }

        protected internal ActionResult InvalidOperation(object message = null, int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Invalid.", Tools.Settings.Log4Net.DetailedLog);
            return BadRequest(new GenericResponse<object>
            {
                Status = ReturnStatus.Error.ToString(),
                Message = message ?? message_invalidOperation
            });
        }

        protected internal ActionResult Expired(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Expired.", Tools.Settings.Log4Net.DetailedLog);
            return Unauthorized(new GenericResponse<object>
            {
                Status = ReturnStatus.Expired.ToString(),
                Message = message_expiredToken
            });
        }

        protected internal ActionResult InvalidToken(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Invalid.", Tools.Settings.Log4Net.DetailedLog);
            return StatusCode(403, new GenericResponse<object>
            {
                Status = ReturnStatus.Invalid.ToString(),
                Message = message_invalidToken
            });
        }

        protected internal ActionResult Relog(int level = 1)
        {
            logger.LogInfo($"{string.Concat(Enumerable.Repeat(TextSupport.levelSeparator, level))}Relog.", Tools.Settings.Log4Net.DetailedLog);
            return StatusCode(403, new GenericResponse<object>
            {
                Status = ReturnStatus.Relog.ToString(),
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
                    Status = ReturnStatus.Error.ToString(),
                    Message = message_systemError.ToString()
                });
            }
        }

        protected internal bool IsAdminOrEditor(int roleId) => adminEditorRoles.Any(r => r == roleId);


        private enum ReturnStatus
        {
            Success,
            Error,
            Expired,
            Invalid,
            Relog
        }
    }
}
