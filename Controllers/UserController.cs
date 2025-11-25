
using CommonTypes.Util.Text;
using Datamodels.Logic;
using Datamodels.Models;
using Datamodels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static CommonTypes.Language.LanguageSupport;
using static CommonTypes.Request.Models.UserRequests;
using System.IdentityModel.Tokens.Jwt;
using api_prueba.Auth;
using CommonTypes.Log;
using CommonTypes.Util.MailService;
using Datamodels.Utils;
using CommonTypes.Util.Password;
using CommonTypes.Request.General;
using Microsoft.AspNetCore.Authorization;
using api_prueba.Support;
using api_prueba.Controllers;
using api_prueba.Security;

namespace api_stocktaking.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : PruebaController
    {
        private readonly JwtAuthenticationService _authService;
        private static readonly LanguageObject message_wrongCredentials = new LanguageObject("Wrong credentials", "Credenciais erradas");
        private static readonly LanguageObject message_userActivation = new LanguageObject(" user activation", "Usuario Activado");
        private static readonly LanguageObject message_passRecovery = new LanguageObject(" password recover attempt", "Contraseña Recuperada");
        public UserController(LogWriter logger, JwtAuthenticationService authService) : base(logger) => _authService = authService;
       
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] UserLang_Register request)
        {
            try
            {
                logger.LogInfo($"Register.{TextSupport.customSeparator}Data: {request}", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                //The public token is still to be defined, that's why the !
                if (!GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out api_prueba.Support.SecTokenType type))
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        User user = new UserLogic(context).Register(request, dbKeys.MasterUser, dbKeys.GeneralStatus.Inactive, dbKeys.UserRoles, out LanguageObject message);
                        if (user is { })
                            return SendActivationEmail(user, isEn);
                        return InvalidOperation(message);
                    }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpGet]
        public async Task<ActionResult> GetUsers()
        {
            try
            {
                logger.LogInfo($"GetUsers.{TextSupport.customSeparator}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString()))
                        return Success(new UserLogic(context).GetAllUser());
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }
        [HttpPost()]
        public async Task<ActionResult> CreateUser([FromBody] UserLang_Create request)
        {
            try
            {
                logger.LogInfo($"CreateUser.{TextSupport.customSeparator}Data: {request}.", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        if (new UserLogic(context).CreateUser(request, new PasswordGenerator(8, 8).Generate(), dbKeys.MasterUser, out LanguageObject message) != null)
                            return Success(request);
                        return InvalidOperation(message);
                    }
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }



        [HttpDelete("{email}")]
        public async Task<ActionResult> Delete(string email)
        {
            logger.LogInfo($"DeleteUser.{TextSupport.customSeparator}Data: {email}.", settings.Log4Net.DetailedLog);

            User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
            if (owner is { })
            {
                using (Context context = new Context(await Tools.ConnectionString(), false))
                {

                    EmailsRequest_Mandatory obj = new EmailsRequest_Mandatory
                    {
                        Email = email
                    };
                    return Ok(DeleteUser(obj));
                }
            }
            return InvalidOperation();
        }
        private async Task<ActionResult> DeleteUser(EmailsRequest_Mandatory request) => await ChangeStatus(request, "DeleteUser", dbKeys.GeneralStatus.Deleted);
        private async Task<ActionResult> ChangeStatus(EmailsRequest_Mandatory request, string label, int newStatus)
        {
            try
            {
                logger.LogInfo($"{label}.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    if (owner.Password.Equals(Encryption.Encrypt_1(request.Password)))
                        using (Context context = new Context(await Tools.ConnectionString(), false))
                        {
                            if (new UserLogic(context).ChangeStatus(request.Emails, dbKeys.MasterUser, newStatus, out LanguageObject message))
                                return Success();
                            return InvalidOperation(message);
                        }
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }


        private ActionResult SendActivationEmail(User user, bool isEn)
        {
            EmailHandler.Send(settings.EMails.Sender, new Dictionary<string, bool>() { { user.Email, true } }, isEn ? message_userActivation.En : message_userActivation.Es, (isEn ? Tools.Resource_Registration_En : Tools.Resource_Registration_Pt).Replace("%name%", $"{user.Name} {user.Surname}").Replace("http://link.link/", $"{settings.DataAccess.FrontURL.TrimEnd(TextSupport.simpleUriSeparator)}/register/{Encryption.ToURLFix(Encryption.Encrypt_DoubleBound_1(user.Email))}"), MailService.MessageType.html);
            return Success();
        }
        private async Task<ActionResult> TryRecoverPass(string email, bool isEn)
        {
            using (Context context = new Context(await Tools.ConnectionString(), false))
            {
                string pin = new PasswordGenerator(5, 5, 0, 0, 5, 0).Generate();
                User user = new UserLogic(context).SetToken(email, Encryption.Encrypt_1(pin), dbKeys.MasterUser, new int[] { dbKeys.GeneralStatus.Active });
                if (user is { })
                {
                    string token = _authService.GetToken_Email(email, pin, out _, user.RoleId);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        EmailHandler.Send(settings.EMails.Sender, new Dictionary<string, bool>() { { user.Email, true } }, isEn ? message_passRecovery.En : message_passRecovery.Es, (isEn ? Tools.Resource_Recovery_En : Tools.Resource_Recovery_Pt).Replace("%name%", $"{user.Name} {user.Surname}").Replace("%pin%", pin).Replace("http://link.link/", $"{settings.DataAccess.FrontURL.TrimEnd(TextSupport.simpleUriSeparator)}/recover/{JwtAuthenticationService.ToURLFix(token)}"), MailService.MessageType.html);
                        return Success();
                    }
                }
                return InvalidOperation();
            }
        }

        [HttpPost("can_recover_pass")]
        public async Task<ActionResult> CanRecoverPass([FromBody] TextRequest_Mandatory request)
        {
            try
            {
                logger.LogInfo($"CanRecoverPass.{TextSupport.customSeparator}From: {request}.", settings.Log4Net.DetailedLog);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out api_prueba.Support.SecTokenType type) && type == api_prueba.Support.SecTokenType.Public)
                {
                    string token = JwtAuthenticationService.FromURLFix(request.Text);
                    Tuple<string, JwtSecurityToken> data = GeneralSupport.GetTokenData(token);
                    if (data is { })
                    {
                        User owner = GeneralSupport.GetUser(token, false, out _, out _, new int[] { dbKeys.GeneralStatus.Active });
                        if (owner is { })
                        {
                            logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                            Tuple<string, string> split = GeneralSupport.SplitTokenData(data.Item1);
                            if (owner.Token != null && Encryption.Decrypt_1(owner.Token) == split.Item2)
                                return Success();
                        }
                        return InvalidOperation();
                    }
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPatch("change_password")]
        public async Task<ActionResult> ChangePassword([FromBody] UserChangePassword request)
        {
            try
            {
                logger.LogInfo($"ChangePassword.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    if (owner.Email.ToLower().Trim() == request.Email.ToLower().Trim() && Encryption.Encrypt_1(request.OldPassword) == owner.Password)
                        using (Context context = new Context(await Tools.ConnectionString(), false))
                            if (new UserLogic(context).ChangePassword(owner.Email, request.Password, dbKeys.MasterUser) != null)
                                return Success();
                    return InvalidOperation();
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

       

        

    }
}
