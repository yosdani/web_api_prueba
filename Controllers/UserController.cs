using api_prueba.Auth;
using api_prueba.Security;
using api_prueba.Support;
using CommonTypes.Log;
using CommonTypes.Request.General;
using CommonTypes.Util.MailService;
using CommonTypes.Util.Password;
using CommonTypes.Util.Text;
using Datamodels;
using Datamodels.Logic;
using Datamodels.Models;
using Datamodels.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using static CommonTypes.Language.LanguageSupport;
using static CommonTypes.Request.Models.UserRequests;

namespace api_prueba.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : PruebaController
    {
        private static readonly LanguageObject message_wrongCredentials = new LanguageObject("Wrong credentials", "Credenciais erradas");
        private static readonly LanguageObject message_userActivation = new LanguageObject("SEAMind user activation", "Ativação do utilizador do SEAMInd");
        private static readonly LanguageObject message_passRecovery = new LanguageObject("SEAMind password recover attempt", "Tentativa de recuperação de senha do SEAMind");
        private static Dictionary<string, Tuple<string, byte[]>> registrationFooter = new Dictionary<string, Tuple<string, byte[]>>() { { "footer", new Tuple<string, byte[]>("image/png", Tools.Resource_RegistrationFooter) } };
        private readonly JwtAuthenticationService _authService;

        public UserController(LogWriter logger, JwtAuthenticationService authService) : base(logger) => _authService = authService;
       
        [HttpPost("get_users")]
        public async Task<ActionResult> GetUsers([FromBody] User_Get request)
        {
            try
            {
                logger.LogInfo($"GetUsers.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString()))
                        return Success(new UserLogic(context).GetUsers(request, dbKeys.MasterUser));
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPost("create_user")]
        public async Task<ActionResult> CreateUser([FromBody] UserLang_Create request)
        {
            try
            {
                logger.LogInfo($"CreateUser.{TextSupport.customSeparator}Data: {request}.", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        if (new UserLogic(context).CreateUser(request, new PasswordGenerator(8, 8).Generate(), dbKeys.MasterUser, out LanguageObject message) != null)
                            return await TryRecoverPass(request.Email, isEn);
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

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] UserLang_Register request)
        {
            try
            {
                logger.LogInfo($"Register.{TextSupport.customSeparator}Data: {request}", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        User user = new UserLogic(context).Register(request, dbKeys.MasterUser, dbKeys.GeneralStatus.Inactive, dbKeys.UserRoles, out LanguageObject message);
                        if (user != null)
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

        private ActionResult SendActivationEmail(User user, bool isEn)
        {
            EmailHandler.Send(settings.EMails.Sender, new Dictionary<string, bool>() { { user.Email, true } }, isEn ? message_userActivation.En : message_userActivation.Pt, (isEn ? Tools.Resource_Registration_En : Tools.Resource_Registration_Pt).Replace("%name%", $"{user.Name} {user.Surname}").Replace("http://link.link/", $"{settings.DataAccess.FrontURL.TrimEnd(TextSupport.simpleUriSeparator)}/register/{Encryption.ToURLFix(Encryption.Encrypt_DoubleBound_1(user.Email))}"), MailService.MessageType.html, registrationFooter);
            return Success();
        }

        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] User_Authenticate request)
        {
            try
            {
                logger.LogInfo($"Authenticate.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                {
                    Tuple<string, User> result = _authService.Authenticate(request, dbKeys.MasterUser, out DateTime? expires, new int[] { dbKeys.GeneralStatus.Active }, out LanguageObject message);
                    if (result != null)
                        return Success(new
                        {
                            User = GeneralSupport.Convert(result.Item2),
                            Token = result.Item1,
                            ExpiresUtc = expires
                        });
                    return InvalidOperation(message ?? message_wrongCredentials);
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPost("get_user_login_info")]
        public async Task<IActionResult> GetUserLoginInfo([FromBody] EmailRequest request)
        {
            try
            {
                logger.LogInfo($"GetUserLoginInfo.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    bool isAdmin = owner.RoleId == dbKeys.UserRoles.Admin;
                    if ( owner.Email.ToLower().Trim() == request.Email.ToLower().Trim())
                        using (Context context = new Context(await Tools.ConnectionString()))
                        {
                            User user = new UserLogic(context).GetUser(request.Email, dbKeys.MasterUser, isAdmin ? null : new int[] { dbKeys.GeneralStatus.Active });
                            if (user != null)
                                return Success(GeneralSupport.Convert(user));
                        }
                    return InvalidOperation();
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPost("resend_email")]
        public async Task<ActionResult> ResendEmail([FromBody] EmailLangRequest request)
        {
            try
            {
                logger.LogInfo($"ResendEmail.{TextSupport.customSeparator}To: {request}", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                    using (Context context = new Context(await Tools.ConnectionString()))
                    {
                        User user = new UserLogic(context).GetUser(request.Email, dbKeys.MasterUser, new int[] { dbKeys.GeneralStatus.Inactive });
                        if (user != null)
                            return SendActivationEmail(user, isEn);
                        return InvalidOperation();
                    }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPatch("confirm_email")]
        public async Task<ActionResult> ConfirmEmail([FromBody] TextRequest_Mandatory request)
        {
            try
            {
                logger.LogInfo($"ConfirmEmail.{TextSupport.customSeparator}For: {request}", settings.Log4Net.DetailedLog);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                {
                    string email = Encryption.Decript_DoubleBound_1(Encryption.FromURLFix(request.Text));
                    logger.LogInfo($"{TextSupport.levelSeparator}For: {email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        User user = new UserLogic(context).ChangeStatus(email, dbKeys.MasterUser, dbKeys.GeneralStatus.Active, out LanguageObject message, new int[] { dbKeys.GeneralStatus.Inactive });
                        if (user != null)
                        {
                            string utoken = _authService.GetToken_Email(user.Email, out DateTime? expires, user.RoleId);
                            if (!string.IsNullOrWhiteSpace(utoken))
                                return Success(new
                                {
                                    User = GeneralSupport.Convert(user),
                                    Token = utoken,
                                    ExpiresUtc = expires
                                });
                        }
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

        [HttpPost("get_user_data")]
        public async Task<ActionResult> GetUserData([FromBody] EmailRequest request)
        {
            try
            {
                logger.LogInfo($"GetUserData.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString()))
                    {
                        object result = new UserLogic(context).GetUserData(request.Email);
                        if (result != null)
                            return Success(result);
                    }
                    return InvalidOperation();
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPost("contact")]
        public async Task<ActionResult> Contact([FromBody] User_Contact request)
        {
            try
            {
                logger.LogInfo("Contact.", settings.Log4Net.DetailedLog);
                if (GeneralSupport.IsValidToken(await GetToken(), out _, out bool expired, out bool relog, out _))
                {
                    User owner = GeneralSupport.GetUser(await GetToken(), true, out _, out _, new int[] { dbKeys.GeneralStatus.Active });
                    string email = owner?.Email ?? request.Email, name = owner == null ? request.Name : $"{owner.Name} {owner.Surname}";
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {email}.", settings.Log4Net.DetailedLog);
                    EmailHandler.Send(settings.EMails.Sender, settings.EMails.Receivers, $"SEAMInd - Contacto do utilizador: {name}", Tools.Resource_Contact.Replace("%name%", name).Replace("%email%", email).Replace("%phone%", request.Phone).Replace("%type of contact%", request.ContactType).Replace("%subject%", request.Subject).Replace("%message%", request.Message), MailService.MessageType.html);
                    return Success();
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPost("try_recover_pass")]
        public async Task<ActionResult> TryRecoverPass([FromBody] EmailLangRequest request)
        {
            try
            {
                logger.LogInfo($"TryRecoverPass.{TextSupport.customSeparator}From: {request.Email}", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                    return await TryRecoverPass(request.Email, isEn);
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        private async Task<ActionResult> TryRecoverPass(string email, bool isEn)
        {
            using (Context context = new Context(await Tools.ConnectionString(), false))
            {
                string pin = new PasswordGenerator(5, 5, 0, 0, 5, 0).Generate();
                User user = new UserLogic(context).SetToken(email, Encryption.Encrypt_1(pin), dbKeys.MasterUser, new int[] { dbKeys.GeneralStatus.Active });
                if (user != null)
                {
                    string token = _authService.GetToken_Email(email, pin, out _, user.RoleId);
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        EmailHandler.Send(settings.EMails.Sender, new Dictionary<string, bool>() { { user.Email, true } }, isEn ? message_passRecovery.En : message_passRecovery.Pt, (isEn ? Tools.Resource_Recovery_En : Tools.Resource_Recovery_Pt).Replace("%name%", $"{user.Name} {user.Surname}").Replace("%pin%", pin).Replace("http://link.link/", $"{settings.DataAccess.FrontURL.TrimEnd(TextSupport.simpleUriSeparator)}/recover/{JwtAuthenticationService.ToURLFix(token)}"), MailService.MessageType.html, registrationFooter);
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
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                {
                    string token = JwtAuthenticationService.FromURLFix(request.Text);
                    Tuple<string, JwtSecurityToken> data = GeneralSupport.GetTokenData(token);
                    if (data != null)
                    {
                        User owner = GeneralSupport.GetUser(token, false, out _, out _, new int[] { dbKeys.GeneralStatus.Active });
                        if (owner != null)
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

        [HttpPatch("do_recover_pass")]
        public async Task<ActionResult> DoRecoverPass([FromBody] UserChangePasswordPIN request)
        {
            try
            {
                logger.LogInfo($"DoRecoverPass.{TextSupport.customSeparator}From: {request}.", settings.Log4Net.DetailedLog);
                if (GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type) && type == SecTokenType.Public)
                {
                    string token = JwtAuthenticationService.FromURLFix(request.Text);
                    Tuple<string, JwtSecurityToken> data = GeneralSupport.GetTokenData(token);
                    if (data != null)
                    {
                        Tuple<string, string> split = GeneralSupport.SplitTokenData(data.Item1);
                        if (split.Item2 == request.PIN)
                        {
                            User owner = GeneralSupport.GetUser(token, false, out _, out _, new int[] { dbKeys.GeneralStatus.Active });
                            if (owner != null)
                            {
                                logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                                if (owner.Token != null && Encryption.Decrypt_1(owner.Token) == split.Item2)
                                {
                                    string pass = request.Password;
                                    if (!string.IsNullOrWhiteSpace(pass))
                                        using (Context context = new Context(await Tools.ConnectionString(), false))
                                        {
                                            User user = new UserLogic(context).ChangePassword(owner.Email, pass, dbKeys.MasterUser, new int[] { dbKeys.GeneralStatus.Active });
                                            if (user != null)
                                            {
                                                //EmailHandler.Send(settings.EMails.Sender, new string[] { owner.Email }, "SEAMind password recover confirm", EmailSupport.GenerateForPassword($"{user.Name} {user.Surname}", pass));
                                                string utoken = _authService.GetToken_Email(user.Email, out DateTime? expires, user.RoleId);
                                                if (!string.IsNullOrWhiteSpace(utoken))
                                                    return Success(new
                                                    {
                                                        User = GeneralSupport.Convert(user),
                                                        Token = utoken,
                                                        ExpiresUtc = expires
                                                    });
                                            }
                                        }
                                }
                            }
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
                if (owner != null)
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

        [HttpPut("update_user_basic")]
        public async Task<ActionResult> UpdateUserBasic([FromBody] UserCore request)
        {
            try
            {
                logger.LogInfo($"UpdateUserBasic.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active });
                if (owner != null && owner.Email.ToLower().Trim() == request.Email.ToLower().Trim())
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        if (new UserLogic(context).UpdateUser(request, dbKeys.MasterUser, out LanguageObject message, null))
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

        [HttpPut("update_user")]
        public async Task<ActionResult> UpdateUser([FromBody] User_Update request)
        {
            try
            {
                logger.LogInfo($"UpdateUser.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    if (owner.Password == Encryption.Encrypt_1(request.AdminPassword))
                        using (Context context = new Context(await Tools.ConnectionString(), false))
                        {
                            if (new UserLogic(context).UpdateUser(request, dbKeys.MasterUser, out LanguageObject message, null))
                                return Success();
                            return InvalidOperation(message);
                        }
                    return InvalidOperation();
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpPatch("close_account")]
        public async Task<ActionResult> CloseAccount([FromBody] UserCloseAccount request)
        {
            try
            {
                logger.LogInfo($"CloseAccount.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                if (!request.IsValid(out LanguageObject message))
                    return InvalidOperation(message);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    if (owner.Email.ToLower().Trim() == request.Email.ToLower().Trim() && owner.Password == Encryption.Encrypt_1(request.Password))
                        using (Context context = new Context(await Tools.ConnectionString(), false))
                        {
                            if (new UserLogic(context).ChangeStatus(request.Email, dbKeys.MasterUser, dbKeys.GeneralStatus.Deleted, out message) != null)
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

        [HttpPatch("activate_user")]
        public async Task<ActionResult> ActivateUser([FromBody] EmailsRequest_Mandatory request) => await ChangeStatus(request, "ActivateUser", dbKeys.GeneralStatus.Active);

        [HttpPatch("deactivate_user")]
        public async Task<ActionResult> DisableUser([FromBody] EmailsRequest_Mandatory request) => await ChangeStatus(request, "ActivateUser", dbKeys.GeneralStatus.Inactive);

        [HttpPatch("delete_user")]
        public async Task<ActionResult> DeleteUser([FromBody] EmailsRequest_Mandatory request) => await ChangeStatus(request, "DeleteUser", dbKeys.GeneralStatus.Deleted);

        private async Task<ActionResult> ChangeStatus(EmailsRequest_Mandatory request, string label, int newStatus)
        {
            try
            {
                logger.LogInfo($"{label}.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner != null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    if (owner.Password == Encryption.Encrypt_1(request.Password))
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
    }
}
