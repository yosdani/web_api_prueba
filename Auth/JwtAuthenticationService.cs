using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using api_prueba.Support;
using static CommonTypes.Request.Models.UserRequests;
using static CommonTypes.Language.LanguageSupport;
using CommonTypes.Util.RegExp;
using CommonTypes.Util;
using Datamodels.Models;
using Datamodels;
using Datamodels.Logic;
using CommonTypes.Settings.Keys;

namespace api_prueba.Auth
{
    public class JwtAuthenticationService : IJwtAuthenticationService
    {
        private readonly string _key;
        private readonly Random _random;
        private const char a1 = '.', v1 = '$';

        public JwtAuthenticationService(string key)
        {
            _key = key;
            _random = new Random();
        }

        public Tuple<string, User> Authenticate(User_Authenticate aur, out DateTime? expires, IEnumerable<int> generalStatus, out LanguageObject message)
        {
            using (Context context = new Context(Tools.ConnectionString().Result))
            {
                expires = null;
                User user = new UserLogic(context).Authenticate(aur, out message, generalStatus);
               
                if (user == null)
                  return null;
                return new Tuple<string, User>(GetToken_Email(aur.Email, out expires, user.RoleId), user);
            }
        }

        private string GetToken(string data, out DateTime? expires, double extendHours, int? roleId)
        {
            if (!string.IsNullOrEmpty(data))
            {
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                byte[] tokenKey = Encoding.ASCII.GetBytes(_key);
                expires = DateTime.UtcNow.AddHours(extendHours);
                List<Claim> claims = new List<Claim>() { new Claim(ClaimTypes.Email, data) };
                if (roleId != null)
                    claims.Add(new Claim(ClaimTypes.Role, roleId.Value.ToString()));
                SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = expires,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tokenKey), SecurityAlgorithms.HmacSha256Signature)
                };
                SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
                return tokenHandler.WriteToken(token);
            }
            expires = null;
            return null;
        }

        public string RefreshToken(string token, string refreshCode, out DateTime? expires)
        {
            if (!string.IsNullOrWhiteSpace(refreshCode))
            {
                Tuple<string, JwtSecurityToken> tk = GeneralSupport.GetTokenData(token);
                if (tk != null)
                    return GetToken(tk.Item1, out expires, Tools.Settings.Timers.TokenRefreshTimeHours, GeneralSupport.GetUserRoleId(tk.Item2));
            }
            expires = null;
            return string.Empty;
        }

        public string GetToken_Email(string email, out DateTime? expires, int roleId) => GetToken_Email(email, _random.Next().ToString(), out expires, roleId);

        public string GetToken_Email(string email, string key, out DateTime? expires, int roleId)
        {
            if (EmailSupport.ValidateEmail(email) && RegexSupport.positiveIntegerRegex.IsMatch(key))
                return GetToken(GeneralSupport.JoinTokenData(email, key), out expires, Tools.Settings.Timers.TokenLifetimeHours, roleId);
            expires = null;
            return null;
        }

        public string GetToken_GUID(out DateTime? expires) => GetToken_GUID(Guid.NewGuid().ToString(), out expires);

        public string GetToken_GUID(string guid, out DateTime? expires) => GetToken_GUID(guid, _random.Next().ToString(), out expires);

        public string GetToken_GUID(string guid, string key, out DateTime? expires)
        {
            if (guid != null && RegexSupport.guidRegex.IsMatch(guid) && RegexSupport.positiveIntegerRegex.IsMatch(key))
                return GetToken(GeneralSupport.JoinTokenData(guid, key), out expires, Tools.Settings.Timers.TokenLifetimeHours, null);
            expires = null;
            return null;
        }

        public static string ToURLFix(string text) => text.Replace(a1, v1);

        public static string FromURLFix(string text) => text.Replace(v1, a1);
    }
}
