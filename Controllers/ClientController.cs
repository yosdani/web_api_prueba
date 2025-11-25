using api_prueba.Auth;
using api_prueba.Support;
using CommonTypes.Log;
using CommonTypes.Request.General;
using CommonTypes.Util.Password;
using CommonTypes.Util.Text;
using Datamodels;
using Datamodels.Logic;
using Datamodels.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using static CommonTypes.Language.LanguageSupport;
using static CommonTypes.Request.Models.UserRequests;

namespace api_prueba.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientController : PruebaController
    {
        public ClientController(LogWriter logger, JwtAuthenticationService authService) : base(logger) { }

        

        [HttpGet("{isInactive}")]
        public async Task<ActionResult> GetClients(bool isInactive)
        {
            try
            {
                User owner = null;
                logger.LogInfo($"GetClient.{TextSupport.customSeparator}.", settings.Log4Net.DetailedLog);
                owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner == null)
                    owner = GeneralSupport.GetUserCommon(await GetToken(), true, out expired, out relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Agent, dbKeys.UserRoles.AgencyAdmin });

                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString()))
                        return Success(new ClientLogic(context).GetAll(isInactive));
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }


        [HttpPost()]
        public async Task<ActionResult> Create([FromBody] UserLang_Create request)
        {
            try
            {
                logger.LogInfo($"CreateClient.{TextSupport.customSeparator}Data: {request}.", settings.Log4Net.DetailedLog);
                if (!IsValidLang(request.Lang, out bool isEn))
                    return InvalidOperation(message_invalidLanguage);
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                       // if (new ClientLogic(context).CreateClient(request, out LanguageObject message ))
                            return Success(request);
                       // return InvalidOperation(message);
                    }
                }
                return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpGet("{search:alpha}")]
        public async Task<ActionResult> Search(string search)
        {
            try
            {
                logger.LogInfo($"Search.{TextSupport.customSeparator}Data: {search}.", settings.Log4Net.DetailedLog);
                
                User owner = GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, new int[] { dbKeys.UserRoles.Admin });
                if (owner is { })
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                      
                            return Success(new ClientLogic(context).Search(search));
                        return InvalidOperation(message_canceledOperation);
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

