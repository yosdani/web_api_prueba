using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using api_prueba.Security;
using Datamodels.Models;
using static CommonTypes.Language.LanguageSupport;
using Datamodels;
using Datamodels.Logic;
using CommonTypes.Util.RegExp;
using Datamodels.Extentions;

namespace api_prueba.Support
{
    public class GeneralSupport
    {
        private const string pattersSeparator = "-";
        internal static readonly Regex emailNumRegex = new Regex($"^{RegexSupport.emailRegex._Trim()}{pattersSeparator}{RegexSupport.positiveIntegerRegex._Trim()}$", RegexOptions.IgnoreCase);
        internal static readonly Regex guidNumRegex = new Regex($"^{RegexSupport.guidRegex._Trim()}{pattersSeparator}{RegexSupport.positiveIntegerRegex._Trim()}$", RegexOptions.IgnoreCase);
        internal static object Convert(User user) => new
        {
            email = user.Email,
            name = user.Name,
            lastname = user.Surname,
            
            role = user.Role == null ? null : new
            {
                id = user.RoleId,
                name = new LanguageObject(user.Role.NameEn, user.Role.NameEs)
            }
        };

        internal static Tuple<string, JwtSecurityToken> GetTokenData(string token)
        {
            try
            {
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                JwtSecurityToken jst = tokenHandler.ReadJwtToken(token);
                Claim claim = jst.Claims.FirstOrDefault(c => c.Type == Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Email);
                if (claim?.Value != null)
                    return new Tuple<string, JwtSecurityToken>(claim.Value, jst);
                return null;
            } 
            catch
            {
                return null;
            }
        }

        internal static User GetUser(string token, bool withRoles, out bool expired, out bool relog, IEnumerable<int> userStatus = null, IEnumerable<int> userRoles = null)
        {
            if (_IsValidToken(token, out Tuple<string, JwtSecurityToken> tk, out expired, out SecTokenType type) && type == SecTokenType.Email)
            {
                User user = _GetUser(SplitTokenData(tk.Item1).Item1, withRoles, userStatus, userRoles);
                if (user != null)
                {
                    if (!(relog = user.RoleId != GetUserRoleId(tk.Item2)))
                        return user;
                    return null;
                }
            }
            relog = false;
            return null;
        }

        private static User _GetUser(string email, bool withRoles, IEnumerable<int> userStatus = null, IEnumerable<int> userRoles = null)
        {
            using (Context context = new Context(Tools.ConnectionString().Result))
            {
                User user = withRoles ? new UserLogic(context).GetUser(email, Tools.DBKeys.MasterUser, userStatus, userRoles) : new UserLogic(context).GetUser(email, Tools.DBKeys.MasterUser, userStatus);
                if (user?.Id != Tools.DBKeys.MasterUser.Id)
                    return user;
                return null;
            }
        }

        internal static string RefreshTokenKey()
        {
            byte[] bytearray = new byte[64];
            using (RandomNumberGenerator mg = RandomNumberGenerator.Create())
            {
                mg.GetBytes(bytearray);
                return System.Convert.ToBase64String(bytearray);
            }
        }

        internal static bool IsValidToken(string token, out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out SecTokenType type, bool skipExpiration = false)
        {
            if (_IsValidToken(token, out tk, out expired, out type, skipExpiration))
            {
                if (type == SecTokenType.Public)
                {
                    relog = false;
                    return true;
                }
                return ExistsUser(tk, out relog);
            }
            return relog = false;
        }

        private static bool _IsValidToken(string token, out Tuple<string, JwtSecurityToken> tk, out bool expired, out SecTokenType type, bool skipExpiration = false)
        {
            expired = false;
            tk = null;
            type = SecTokenType.None;
            if (!string.IsNullOrWhiteSpace(token) && (tk = GetTokenData(token)) != null && !string.IsNullOrWhiteSpace(tk.Item1))
            {
                if (emailNumRegex.IsMatch(tk.Item1))
                    type = SecTokenType.Email;
                else if (guidNumRegex.IsMatch(tk.Item1))
                    type = SecTokenType.Public;
                expired = tk.Item2.ValidTo <= DateTime.UtcNow;
            }
            return (skipExpiration || !expired) && type != SecTokenType.None;
        }

        private static bool ExistsUser(Tuple<string, JwtSecurityToken> tk, out bool relog)
        {
            using (Context context = new Context(Tools.ConnectionString().Result))
            {
                Tuple<int, int> user = new UserLogic(context).GetUserIdRoleId(SplitTokenData(tk.Item1).Item1, Tools.DBKeys.MasterUser);
                if (user != null && user.Item1 != Tools.DBKeys.MasterUser.Id)
                    return !(relog = user.Item2 != GetUserRoleId(tk.Item2));
                return relog = false;
            }
        }

        internal static int? GetUserRoleId(JwtSecurityToken token)
        {
            Claim claim = token.Claims.FirstOrDefault(c => c.Type == "role");
            if (claim?.Value != null && int.TryParse(claim.Value, out int roleId))
                return roleId;
            return null;
        }

        internal static Tuple<string, string> SplitTokenData(string tokenData) => new Tuple<string, string>(tokenData.Substring(0, tokenData.LastIndexOf("-")), tokenData.Substring(tokenData.LastIndexOf("-") + 1));

        internal static string JoinTokenData(string data, string key) => $"{data}{pattersSeparator}{key}";
    }
}
