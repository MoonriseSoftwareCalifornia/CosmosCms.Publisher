using Cosmos.Cms.Common.Data;
using Cosmos.Cms.Common.Data.Logic;
using Cosmos.Cms.Common.Models;
using Cosmos.Cms.Common.Services.Configurations;
using Cosmos.Cms.Publisher.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;

namespace Cosmos.Cms.Publisher.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ArticleLogic _articleLogic;
        private readonly IOptions<CosmosConfig> _options;

        public HomeController(ILogger<HomeController> logger, ArticleLogic articleLogic, IOptions<CosmosConfig> options)
        {
            _logger = logger;
            _articleLogic = articleLogic;
            _options = options;
        }

        public async Task<IActionResult> Index()
        {
            var article = await _articleLogic.GetByUrl(HttpContext.Request.Path, HttpContext.Request.Query["lang"]); // ?? await _articleLogic.GetByUrl(id, langCookie);

            // Article not found?
            // try getting a version not published.

            if (article == null)
            {
                //
                // Create your own not found page for a graceful page for users.
                //
                article = await _articleLogic.GetByUrl("/not_found", HttpContext.Request.Query["lang"]);

                HttpContext.Response.StatusCode = 404;

                if (article == null) return NotFound();
            }
            return View(article);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Gets the application validation for Microsoft
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        public IActionResult GetMicrosoftIdentityAssociation()
        {
            var model = new MicrosoftValidationObject();
            var appIds = _options.Value.MicrosoftAppId.Split(',');

            foreach (var id in appIds)
            {
                model.associatedApplications.Add(new AssociatedApplication() { applicationId = id });
            }

            var data = Newtonsoft.Json.JsonConvert.SerializeObject(model);

            return File(Encoding.UTF8.GetBytes(data), "application/json", fileDownloadName: "microsoft-identity-association.json");
        }


        /// <summary>
        /// Gets the children of a given page path.
        /// </summary>
        /// <param name="page">UrlPath</param>
        /// <param name="pageNo"></param>
        /// <param name="pageSize"></param>
        /// <param name="orderByPub"></param>
        /// <returns></returns>
        [EnableCors("AllCors")]
        public async Task<IActionResult> GetTOC(
            string page,
            bool? orderByPub,
            int? pageNo,
            int? pageSize)
        {
            var result = await _articleLogic.GetTOC(page, pageNo ?? 0, pageSize ?? 10, orderByPub ?? false);
            return Json(result);
        }

    }
}