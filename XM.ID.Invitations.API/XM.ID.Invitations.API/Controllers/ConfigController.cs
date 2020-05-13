﻿
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using XM.ID.Invitations.Net;

namespace Invitations.Controllers
{
    [ApiController]
    [Route("api/config")]
    public class ConfigController : ControllerBase
    {
        private readonly ILogger<ConfigController> Logger;
        private IConfiguration Config;
        public readonly AuthTokenValidation AuthTokenValidation;
        public readonly ConfigService ConfigService;

        public ConfigController(ILogger<ConfigController> logger, IConfiguration config, AuthTokenValidation authTokenValidation, ConfigService configService)
        {
            Logger = logger;
            Config = config;
            AuthTokenValidation = authTokenValidation;
            ConfigService = configService;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] SPALoginRequest request)
        {
            ACMLoginResponse response = await ConfigService.Login(request);
            if (response.IsSuccessful == true)
                return StatusCode(200, response);
            else
                return StatusCode(401, response);
        }

        [HttpGet]
        [Route("dispatch")]
        public async Task<IActionResult> GetDispatches([FromHeader(Name = "Authorization")] string authToken)
        {
            ACMGenericResult<DispatchesAndQueueDetails> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.GetDispatches(authToken);
            }
            if (response.StatusCode == 400)
                return StatusCode(400, "Settings/Dispatches received from WXM were Null/Empty");
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpGet]
        [Route("dispatch/{dispatchId}")]
        public async Task<IActionResult> GetDispatchChannel([FromHeader(Name = "Authorization")] string authToken, string dispatchId)
        {
            ACMGenericResult<DispatchChannel> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.GetDispatchChannel(dispatchId);
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpPost]
        [Route("dispatch")]
        public async Task<IActionResult> UpdateDispatchChannel([FromHeader(Name = "Authorization")] string authToken, [FromBody] DispatchChannel dispatchChannel)
        {
            ACMGenericResult<DispatchChannel> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.AddOrUpdateDispatchChannel(dispatchChannel);
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpGet]
        [Route("vendor/{vendorName}")]
        public async Task<IActionResult> GetVendor([FromHeader(Name = "Authorization")] string authToken, string vendorName)
        {
            ACMGenericResult<Vendor> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.GetVendor(vendorName);
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpPost]
        [Route("vendor")]
        public async Task<IActionResult> AddOrUpdateVendor([FromHeader(Name = "Authorization")] string authToken, [FromBody] Vendor vendor)
        {
            ACMGenericResult<Vendor> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.AddOrUpdateVendor(vendor);
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpGet]
        [Route("extendedproperties")]
        public async Task<IActionResult> GetExtendedProperties([FromHeader(Name = "Authorization")] string authToken)
        {
            ACMGenericResult<Dictionary<string, string>> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.GetExtendedProperties();
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpPost]
        [Route("extendedproperties")]
        public async Task<IActionResult> UpdateExtendedProperties([FromHeader(Name = "Authorization")] string authToken, [FromBody] Dictionary<string, string> extendedProperties)
        {
            ACMGenericResult<Dictionary<string, string>> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.UpdateExtendedProperties(extendedProperties);
            }
            return StatusCode(response.StatusCode, response.Value);
        }

        [HttpDelete]
        [Route("")]
        public async Task<IActionResult> DeleteConfig([FromHeader(Name = "Authorization")] string authToken)
        {
            ACMGenericResult<string> response;
            if (!(await AuthTokenValidation.ValidateBearerToken(authToken)))
            {
                return Unauthorized(SharedSettings.AuthorizationDenied);
            }
            else
            {
                response = await ConfigService.DeleteAccountConfiguration();
            }
            return StatusCode(response.StatusCode, response.Value);
        }
    }
}
