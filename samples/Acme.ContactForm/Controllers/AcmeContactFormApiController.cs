using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Acme.ContactForm.ViewModels;

namespace Acme.ContactForm.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Acme.ContactForm")]
    public class AcmeContactFormApiController : AcmeContactFormApiControllerBase
    {
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

        public AcmeContactFormApiController(IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        {
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        }

        [HttpGet("ping")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string Ping() => "Pong";

        [HttpGet("whatsTheTimeMrWolf")]
        [ProducesResponseType(typeof(DateTime), 200)]
        public DateTime WhatsTheTimeMrWolf() => DateTime.Now;

        [HttpGet("whatsMyName")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        public string WhatsMyName()
        {
            // So we can see a long request in the dashboard with a spinning progress wheel
            Thread.Sleep(2000);

            IUser? currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            return currentUser?.Name ?? "I have no idea who you are";
        }

        [HttpGet("whoAmI")]
        [ProducesResponseType<WhoAmIResponseModel>(StatusCodes.Status200OK)]
        public ActionResult<WhoAmIResponseModel> WhoAmI()
        {
            IUser? currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            if (currentUser is null)
            {
                return Unauthorized();
            }

            return new WhoAmIResponseModel
            {
                Name = currentUser.Name,
                Email = currentUser.Email,
                Groups = currentUser.Groups
                    .Select(group => group.Name)
                    .OfType<string>()
                    .ToArray(),
            };
        }
    }
}
