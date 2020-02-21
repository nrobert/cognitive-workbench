using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orneholm.CognitiveWorkbench.Web.Models;
using Orneholm.CognitiveWorkbench.Web.Services;

namespace Orneholm.CognitiveWorkbench.Web.Controllers
{
    [ApiController]
    [Route("/api")]
    public class ApiController : Controller
    {
        private readonly ILogger<ApiController> _logger;

        public ApiController(ILogger<ApiController> logger)
        {
            _logger = logger;
        }

        [HttpPost("vision/analyze")]
        public async Task<ActionResult<VisionAnalyzeResponse>> VisionAnalyze([FromBody]VisionAnalyzeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ComputerVisionSubscriptionKey))
            {
                throw new ArgumentException("Missing or invalid ComputerVisionSubscriptionKey", nameof(request.ComputerVisionSubscriptionKey));
            }

            if (string.IsNullOrWhiteSpace(request.ComputerVisionEndpoint))
            {
                throw new ArgumentException("Missing or invalid ComputerVisionEndpoint", nameof(request.ComputerVisionEndpoint));
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                throw new ArgumentException("Missing or invalid ImageUrl", nameof(request.ImageUrl));
            }

            var imageAnalyzer = new ImageAnalyzer(request.ComputerVisionSubscriptionKey, request.ComputerVisionEndpoint, request.FaceSubscriptionKey, request.FaceEndpoint);
            var analyzeResult = await imageAnalyzer.Analyze(request.ImageUrl, request.ImageAnalysisLanguage, request.ImageOcrLanguage);

            return analyzeResult;
        }
    }
}
