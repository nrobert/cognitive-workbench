using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orneholm.CognitiveWorkbench.Web.Models.ComputerVision;
using Orneholm.CognitiveWorkbench.Web.Models.CustomVision;
using Orneholm.CognitiveWorkbench.Web.Models.Face;
using Orneholm.CognitiveWorkbench.Web.Services;

namespace Orneholm.CognitiveWorkbench.Web.Controllers
{
    public class VisionController : Controller
    {
        private readonly ILogger<VisionController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TelemetryClient _telemetryClient;

        private List<string> _allowedFileContentType = new List<string> { "image/jpeg", "image/png", "image/gif", "image/bmp" };

        public VisionController(ILogger<VisionController> logger, IHttpClientFactory httpClientFactory, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _telemetryClient = telemetryClient;
        }

        [HttpGet("/vision/computer-vision")]
        public IActionResult ComputerVision()
        {
            return View(ComputerVisionViewModel.NotAnalyzed());
        }

        [HttpPost("/vision/computer-vision")]
        public async Task<ActionResult<ComputerVisionViewModel>> ComputerVision([FromForm]ComputerVisionAnalyzeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ComputerVisionSubscriptionKey))
            {
                throw new ArgumentException("Missing or invalid ComputerVisionSubscriptionKey", nameof(request.ComputerVisionSubscriptionKey));
            }

            if (string.IsNullOrWhiteSpace(request.ComputerVisionEndpoint))
            {
                throw new ArgumentException("Missing or invalid ComputerVisionEndpoint", nameof(request.ComputerVisionEndpoint));
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl) && (request.File == null || !_allowedFileContentType.Contains(request.File.ContentType)))
            {
                throw new ArgumentException("Missing or invalid ImageUrl / no file provided", nameof(request.ImageUrl));
            }

            Track("Vision_ComputerVision");

            var imageAnalyzer = new ImageComputerVisionAnalyzer(request.ComputerVisionSubscriptionKey, request.ComputerVisionEndpoint, _httpClientFactory);
            var analyzeResult = await imageAnalyzer.Analyze(request.ImageUrl, request.File, request.ImageAnalysisLanguage, request.ImageOcrLanguage, request.ImageReadLanguage);

            return View(ComputerVisionViewModel.Analyzed(request, analyzeResult));
        }

        [HttpGet("/vision/custom-vision")]
        public IActionResult CustomVision()
        {
            return View(CustomVisionViewModel.NotAnalyzed());
        }

        [HttpPost("/vision/custom-vision")]
        public async Task<ActionResult<CustomVisionViewModel>> CustomVision([FromForm]CustomVisionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CustomVisionPredictionKey))
            {
                throw new ArgumentException("Missing or invalid CustomVisionPredictionKey", nameof(request.CustomVisionPredictionKey));
            }

            if (string.IsNullOrWhiteSpace(request.CustomVisionEndpoint))
            {
                throw new ArgumentException("Missing or invalid CustomVisionEndpoint", nameof(request.CustomVisionEndpoint));
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                throw new ArgumentException("Missing or invalid ImageUrl", nameof(request.ImageUrl));
            }

            if (request.ProjectId == null || Guid.Empty.Equals(request.ProjectId))
            {
                throw new ArgumentException("Missing or invalid ProjectId", nameof(request.ProjectId));
            }

            if (string.IsNullOrWhiteSpace(request.IterationPublishedName))
            {
                throw new ArgumentException("Missing or invalid IterationPublishedName", nameof(request.IterationPublishedName));
            }

            Track("Vision_CustomVision");

            var imageAnalyzer = new ImageCustomVisionAnalyzer(request.CustomVisionPredictionKey, request.CustomVisionEndpoint, _httpClientFactory);
            var analyzeResult = await imageAnalyzer.Analyze(request.ImageUrl, request.ProjectId, request.IterationPublishedName, request.ProjectType);

            return View(CustomVisionViewModel.Analyzed(request, analyzeResult));
        }

        [HttpGet("/vision/face")]
        public IActionResult Face()
        {
            return View(FaceViewModel.NotAnalyzed());
        }

        [HttpPost("/vision/face")]
        public async Task<ActionResult<FaceViewModel>> Face([FromForm]FaceAnalyzeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FaceSubscriptionKey))
            {
                throw new ArgumentException("Missing or invalid FaceSubscriptionKey", nameof(request.FaceSubscriptionKey));
            }

            if (string.IsNullOrWhiteSpace(request.FaceEndpoint))
            {
                throw new ArgumentException("Missing or invalid FaceEndpoint", nameof(request.FaceEndpoint));
            }

            if (string.IsNullOrWhiteSpace(request.ImageUrl))
            {
                throw new ArgumentException("Missing or invalid ImageUrl", nameof(request.ImageUrl));
            }

            if (request.EnableIdentification && string.IsNullOrWhiteSpace(request.IdentificationGroupId))
            {
                throw new ArgumentException("Missing or invalid IdentificationGroupId", nameof(request.IdentificationGroupId));
            }

            Track("Vision_Face");

            var imageAnalyzer = new ImageFaceAnalyzer(request.FaceSubscriptionKey, request.FaceEndpoint, _httpClientFactory);
            var analyzeResult = await imageAnalyzer.Analyze(request.ImageUrl, request.DetectionModel, request.EnableIdentification, request.RecognitionModel, request.IdentificationGroupType, request.IdentificationGroupId);

            return View(FaceViewModel.Analyzed(request, analyzeResult));
        }

        private void Track(string type)
        {
            _telemetryClient.TrackEvent("AnalyzeImage", new Dictionary<string, string>()
            {
                { "cwb_type", type }
            });
        }
    }
}
