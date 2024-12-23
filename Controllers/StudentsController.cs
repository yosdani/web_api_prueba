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
    public class StudentsController : PruebaController
    {
        public StudentsController(LogWriter logger, JwtAuthenticationService authService) : base(logger) { }



        [HttpPost("get_upload_txt")]
        public async Task<ActionResult> GetUploadTxt([FromBody] string request)
        {
            try
            {
                logger.LogInfo($"Upload txt.{TextSupport.customSeparator}From: {request}.", settings.Log4Net.DetailedLog);
                User owner = null;
                if (owner == null)
                {
                    logger.LogInfo($"{TextSupport.customSeparator}Read: {request} file", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString()))
                    {
                        StudentLogic a = new(context);
                        a.ReadTxt(request);
                        return Success(request);
                    }
                }
                return Default(true, true);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }
        [HttpGet("get_student")]
        public async Task<ActionResult> GetStudent()
        {
            try
            {
                logger.LogInfo($"GetStudent.", settings.Log4Net.DetailedLog);
              //  if (!GeneralSupport.IsValidToken(await GetToken(), out Tuple<string, JwtSecurityToken> tk, out bool expired, out bool relog, out _))
                    using (Context context = new Context(await Tools.ConnectionString()))
                        return Success(new StudentLogic(context).GetStudents());
              //  return Default(expired, relog);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }
        [HttpPut("update_student")]
        public async Task<ActionResult> UpdateStudent([FromBody] Student_Update request)
        {
            try
            {
                logger.LogInfo($"UpdateStudent.{TextSupport.customSeparator}Data: {request}.", settings.Log4Net.DetailedLog);
                User owner = null;// GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, adminEditorRoles);
                if (owner == null)
                {
                    //logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                    {
                        if (new StudentLogic(context).UpdateStudent(request, out LanguageObject message))
                            return Success();
                        return InvalidOperation(message);
                    }
                }
                return Default(true, true);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }

        [HttpDelete("delete_student")]
        public async Task<ActionResult> DeleteStudents([FromBody] IdsRequest_Mandatory request)
        {
            try
            {
                logger.LogInfo($"DeleteStudent.{TextSupport.customSeparator}For: {request}.", settings.Log4Net.DetailedLog);
                User owner = null;//GeneralSupport.GetUser(await GetToken(), true, out bool expired, out bool relog, new int[] { dbKeys.GeneralStatus.Active }, adminEditorRoles);
                if (owner == null)
                {
                    //logger.LogInfo($"{TextSupport.customSeparator}Owner: {owner.Email}.", settings.Log4Net.DetailedLog);
                    using (Context context = new Context(await Tools.ConnectionString(), false))
                        if (new StudentLogic(context).DeleteStudent(request.Ids))
                            return Success();
                    return InvalidOperation();
                }
                return Default(true, true);
            }
            catch (Exception exc)
            {
                return Exception(exc);
            }
        }
    }

}

