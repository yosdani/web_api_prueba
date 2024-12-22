using CommonTypes.Settings.Keys;
using Datamodels.Models;
using static CommonTypes.Language.LanguageSupport;
using static CommonTypes.Request.Models.UserRequests;

namespace api_prueba.Auth
{
    public interface IJwtAuthenticationService
    {
       
            Tuple<string, User> Authenticate(User_Authenticate aur, out DateTime? expires, IEnumerable<int> generalStatus, out LanguageObject message);

            string GetToken_Email(string email, out DateTime? expires, int roleId);

            string GetToken_GUID(out DateTime? expires);

            string RefreshToken(string token, string refreshCode, out DateTime? expires);
        }
    }
