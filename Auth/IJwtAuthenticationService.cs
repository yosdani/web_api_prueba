using CommonTypes.Settings.Keys;
using Datamodels.Models;
using static CommonTypes.Language.LanguageSupport;
using static CommonTypes.Request.Models.UserRequests;

namespace api_prueba.Auth
{
    public interface IJwtAuthenticationService
    {


        string Authenticate(string username, string password, out DateTime? expires, bool fullAuthentication = true);

        string RefreshToken(string token, string refreshCode, out DateTime? expires);

        string GetToken_GUID(out DateTime? expires);



    }
}