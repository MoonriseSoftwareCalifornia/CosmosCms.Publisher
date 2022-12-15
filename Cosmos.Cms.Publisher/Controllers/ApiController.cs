using Cosmos.Cms.Common.Data;
using Cosmos.Cms.Common.Models;
using Jering.Javascript.NodeJS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Newtonsoft.Json;
using System.Data;

namespace Cosmos.Cms.Controllers
{

    /// <summary>
    /// API Controller
    /// </summary>
    [AllowAnonymous]
    [Authorize(Roles = "Reviewers, Administrators, Editors, Authors")]
    public sealed class ApiController : Controller
    {

        private readonly INodeJSService _nodeJSService;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ApiController> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeJSService"></param>
        /// <param name="dbContext"></param>
        /// <param name="logger"></param>
        public ApiController(INodeJSService nodeJSService, ApplicationDbContext dbContext, ILogger<ApiController> logger)
        {
            _nodeJSService = nodeJSService;
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// API End Point
        /// </summary>
        /// <param name="Id">EndPoint</param>
        /// <returns></returns>
        [HttpGet]
        [HttpPost]
        [ValidateAntiForgeryToken()]
        public async Task<IActionResult> Index(string Id)
        {
            string? result = null;
            try
            {
                var script = await _dbContext.NodeScripts.FirstOrDefaultAsync(f => f.EndPoint == Id);

                if (script == null)
                {
                    return NotFound();
                }

                var values = GetArgs(Request, script);

                // Send the module string to NodeJS where it's compiled, invoked and cached.
                if (string.IsNullOrEmpty(script.Code))
                {
                    if (values == null)
                    {
                        result = await _nodeJSService.InvokeFromFileAsync<string>($"{script.EndPoint}");
                    }
                    else
                    {
                        result = await _nodeJSService.InvokeFromFileAsync<string>($"{script.EndPoint}", args: values.Select(s => s.Value).ToArray());
                    }
                }
                else
                {
                    result = await _nodeJSService.InvokeFromStringAsync<string>(script.Code, script.Updated.ToString(), args: values);
                }

            }
            catch (Exception e)
            {
                _logger.LogError(e.Message, e);
                //throw new Exception("An error has occured.");
            }

            if (result == null)
            {
                return Ok();
            }

            return Json(result);
        }

        /// <summary>
        /// Gets arguments from a request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="script"></param>
        /// <returns></returns>
        private static ApiArgument[]? GetArgs(Microsoft.AspNetCore.Http.HttpRequest request, NodeScript script)
        {
            var inputVarDefs = script.InputVars.Select(s => new InputVarDefinition(s)).ToList();

            if (request.Method == "POST")
            {
                if (request.ContentType == null)
                {
                    var values = new List<ApiArgument>();

                    foreach (var item in inputVarDefs)
                    {
                        var value = (string?)request.Headers[item.Name];
                        value = string.IsNullOrEmpty(value) ? "" : value.Substring(0, item.MaxLength);

                        values.Add(new ApiArgument() { Key = item.Name, Value = value });
                    }

                    return values.ToArray();
                }

                if (request.Form != null)
                {
                    return request.Form.Where(a => script.InputVars.Contains(a.Key))
                   .Select(s => new ApiArgument()
                   {
                       Key = s.Key,
                       Value = s.Value
                   }).ToArray();
                }

                return null;
            }
            else if (request.Method == "GET")
            {
                return request.Query.Where(a => script.InputVars.Contains(a.Key))
                    .Select(s => new ApiArgument()
                    {
                        Key = s.Key,
                        Value = s.Value
                    }).ToArray();
            }
            return null;
        }

        /// <summary>
        /// Returns the Open API (Swagger) definition for this API
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Specification()
        {
            var scripts = await _dbContext.NodeScripts.Where(w => w.Published != null && w.Published <= DateTimeOffset.UtcNow).OrderBy(o => o.EndPoint).ToListAsync();

            var paths = new OpenApiPaths();

            foreach (var script in scripts)
            {
                var parameters = new List<OpenApiParameter>();

                foreach (var p in script.InputVars)
                {
                    parameters.Add(new OpenApiParameter()
                    {
                        Name = p,
                        Schema = new OpenApiSchema()
                        {
                            Type = "string"
                        },
                        In = ParameterLocation.Query
                    });
                }

                paths.Add($"/Index/{script.EndPoint}", new OpenApiPathItem()
                {
                    Operations = new Dictionary<OperationType, OpenApiOperation>
                    {
                        [OperationType.Post] = new OpenApiOperation
                        {
                            Description = script.Description,
                            Responses = new OpenApiResponses
                            {
                                ["200"] = new OpenApiResponse
                                {
                                    Description = "OK"
                                }
                            },
                            Parameters = parameters
                        }
                    }
                }); ;
            }

            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Version = "1.0.0",
                    Title = "Swagger Petstore (Simple)"
                },
                Servers = new List<OpenApiServer>
                            {
                                new OpenApiServer { Url = "/api" }
                            },
                Paths = paths,
            };


            using var outputString = new StringWriter();

            var writer = new OpenApiJsonWriter(outputString);
            document.SerializeAsV3(writer);

            var model = JsonConvert.DeserializeObject(outputString.ToString());

            return Json(model);
        }

    }

    /// <summary>
    /// Input variable definition
    /// </summary>
    public class InputVarDefinition
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="definition"></param>
        /// <example>InputVarDefinition("firstName:string:64")</example>
        public InputVarDefinition(string definition)
        {
            var parts = definition.Split(':');
            Name = parts[0];
            if (parts.Length > 1)
            {
                MaxLength = int.Parse(parts[1]);
            }
            else
            {
                MaxLength = 256;
            }
        }

        /// <summary>
        /// Input variable name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Maximum number of string characters
        /// </summary>
        public int MaxLength { get; set; } = 1024;
    }
}
