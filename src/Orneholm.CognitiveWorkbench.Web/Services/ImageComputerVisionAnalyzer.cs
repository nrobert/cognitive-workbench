using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Orneholm.CognitiveWorkbench.Web.Extensions;
using Orneholm.CognitiveWorkbench.Web.Models.ComputerVision;
using Orneholm.CognitiveWorkbench.Web.Models.Generic;
using ApiKeyServiceClientCredentials = Microsoft.Azure.CognitiveServices.Vision.ComputerVision.ApiKeyServiceClientCredentials;

namespace Orneholm.CognitiveWorkbench.Web.Services
{
    public class ImageComputerVisionAnalyzer
    {
        private static readonly List<VisualFeatureTypes?> AnalyzeVisualFeatureTypes = new List<VisualFeatureTypes?>
        {
            VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Faces,
            VisualFeatureTypes.Adult,
            VisualFeatureTypes.Categories,
            VisualFeatureTypes.Color,
            VisualFeatureTypes.Tags,
            VisualFeatureTypes.Description,
            VisualFeatureTypes.Objects,
            VisualFeatureTypes.Brands
        };

        private static readonly List<VisualFeatureTypes?> AnalyzeVisualFeatureLimitedTypes = new List<VisualFeatureTypes?>
        {
            VisualFeatureTypes.ImageType,
            VisualFeatureTypes.Faces,
            VisualFeatureTypes.Adult,
            VisualFeatureTypes.Categories,
            VisualFeatureTypes.Color,
            VisualFeatureTypes.Tags,
            VisualFeatureTypes.Description
        };

        private static readonly List<Details?> AnalyzeDetails = new List<Details?>
        {
            Details.Celebrities,
            Details.Landmarks
        };

        private readonly string _endpoint;
        private readonly string _subscriptionKey;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly int _computerVisionOperationIdLength = 36;
        private ComputerVisionClient _computerVisionClient;

        public ImageComputerVisionAnalyzer(string computerVisionSubscriptionKey, string computerVisionEndpoint, IHttpClientFactory httpClientFactory)
        {
            _endpoint = computerVisionEndpoint;
            _subscriptionKey = computerVisionSubscriptionKey;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ComputerVisionAnalyzeResponse> Analyze(string imageUrl, IFormFile file,
            AnalysisLanguage analysisLanguage, OcrLanguages ocrLanguage, ReadLanguage readLanguage)
        {
            // Setup
            _computerVisionClient = new ComputerVisionClient(new ApiKeyServiceClientCredentials(_subscriptionKey)) { Endpoint = _endpoint };

            // Computer vision
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                var imageAnalysis = ComputerVisionAnalyzeImageByUrl(imageUrl, analysisLanguage);
                var areaOfInterest = ComputerVisionGetAreaOfInterestByUrl(imageUrl);
                var read = ComputerVisionReadByUrl(imageUrl, readLanguage);
                var recognizedPrintedText = ComputerVisionRecognizedPrintedTextByUrl(imageUrl, ocrLanguage);

                // Combine
                await Task.WhenAll(imageAnalysis, areaOfInterest, read, recognizedPrintedText);

                return new ComputerVisionAnalyzeResponse
                {
                    ImageInfo = new ImageInfo
                    {
                        Src = imageUrl,
                        Description = imageAnalysis.Result.Description?.Captions?.FirstOrDefault()?.Text.ToSentence(),

                        Width = imageAnalysis.Result.Metadata.Width,
                        Height = imageAnalysis.Result.Metadata.Height
                    },

                    AnalyzeVisualFeatureTypes = AnalyzeVisualFeatureTypes,
                    AnalyzeDetails = AnalyzeDetails,

                    AnalysisResult = imageAnalysis.Result,
                    AreaOfInterestResult = areaOfInterest.Result,

                    OcrResult = recognizedPrintedText.Result,
                    ReadResult = read.Result
                };
            }
            else
            {
                using (var analyzeStream = new MemoryStream())
                using (var areaOfInterestStream = new MemoryStream())
                using (var readStream = new MemoryStream())
                using (var ocrStream = new MemoryStream())
                using (var outputStream = new MemoryStream())
                {
                    // Get initial value
                    await file.CopyToAsync(analyzeStream);
                    
                    // Duplicate for parallel access to the streams
                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    await analyzeStream.CopyToAsync(areaOfInterestStream);
                    
                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    await analyzeStream.CopyToAsync(readStream);

                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    await analyzeStream.CopyToAsync(ocrStream);

                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    await analyzeStream.CopyToAsync(outputStream);

                    // Reset the stream for consumption
                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    areaOfInterestStream.Seek(0, SeekOrigin.Begin);
                    readStream.Seek(0, SeekOrigin.Begin);
                    ocrStream.Seek(0, SeekOrigin.Begin);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    var imageAnalysis = ComputerVisionAnalyzeImageByStream(analyzeStream, analysisLanguage);
                    var areaOfInterest = ComputerVisionGetAreaOfInterestByStream(areaOfInterestStream);
                    var read = ComputerVisionReadByStream(readStream, readLanguage);
                    var recognizedPrintedText = ComputerVisionRecognizedPrintedTextByStream(ocrStream, ocrLanguage);

                    // Combine
                    await Task.WhenAll(imageAnalysis, areaOfInterest, read, recognizedPrintedText);

                    // Get image for display
                    var fileBytes = outputStream.ToArray();
                    var imageData = $"data:{file.ContentType};base64,{Convert.ToBase64String(fileBytes)}";

                    return new ComputerVisionAnalyzeResponse
                    {
                        ImageInfo = new ImageInfo
                        {
                            Src = imageData,
                            Description = imageAnalysis.Result.Description?.Captions?.FirstOrDefault()?.Text.ToSentence(),

                            Width = imageAnalysis.Result.Metadata.Width,
                            Height = imageAnalysis.Result.Metadata.Height
                        },

                        AnalyzeVisualFeatureTypes = AnalyzeVisualFeatureTypes,
                        AnalyzeDetails = AnalyzeDetails,

                        AnalysisResult = imageAnalysis.Result,
                        AreaOfInterestResult = areaOfInterest.Result,

                        OcrResult = recognizedPrintedText.Result,
                        ReadResult = read.Result
                    };
                    
                }
            }
        }

        private async Task<ImageAnalysis> ComputerVisionAnalyzeImageByUrl(string imageUrl, AnalysisLanguage analysisLanguage)
        {
            var visualFeatures = AnalyzeVisualFeatureTypes;

            // API does not support some visual features if analysis is not in English
            if (!AnalysisLanguage.en.Equals(analysisLanguage))
            {
                visualFeatures = AnalyzeVisualFeatureLimitedTypes;
            }

            return await _computerVisionClient.AnalyzeImageAsync(
                url: imageUrl,
                visualFeatures: visualFeatures,
                details: AnalyzeDetails,
                language: analysisLanguage.ToString()
            );
        }

        private async Task<ImageAnalysis> ComputerVisionAnalyzeImageByStream(Stream imageStream, AnalysisLanguage analysisLanguage)
        {
            var visualFeatures = AnalyzeVisualFeatureTypes;

            // API does not support some visual features if analysis is not in English
            if (!AnalysisLanguage.en.Equals(analysisLanguage))
            {
                visualFeatures = AnalyzeVisualFeatureLimitedTypes;
            }

            return await _computerVisionClient.AnalyzeImageInStreamAsync(
                image: imageStream,
                visualFeatures: visualFeatures,
                details: AnalyzeDetails,
                language: analysisLanguage.ToString()
            );
        }

        private Task<AreaOfInterestResult> ComputerVisionGetAreaOfInterestByUrl(string imageUrl)
        {
            return _computerVisionClient.GetAreaOfInterestAsync(imageUrl);
        }

        private Task<AreaOfInterestResult> ComputerVisionGetAreaOfInterestByStream(Stream imageStream)
        {
            return _computerVisionClient.GetAreaOfInterestInStreamAsync(imageStream);
        }

        private async Task<ReadOperationResult> ComputerVisionReadByUrl(string imageUrl, ReadLanguage readLanguage)
        {
            var readRequestHeaders = await _computerVisionClient.ReadAsync(imageUrl, language: readLanguage.ToString());
            return await GetReadOperationResultAsync(readRequestHeaders.OperationLocation);
        }

        private async Task<ReadOperationResult> ComputerVisionReadByStream(Stream imageStream, ReadLanguage readLanguage)
        {
            var readRequestHeaders = await _computerVisionClient.ReadInStreamAsync(imageStream, language: readLanguage.ToString());
            return await GetReadOperationResultAsync(readRequestHeaders.OperationLocation);
        }

        private async Task<ReadOperationResult> GetReadOperationResultAsync(string operationLocation)
        {
            var operationId = new Guid(operationLocation.Substring(operationLocation.Length - _computerVisionOperationIdLength));
            var result = await _computerVisionClient.GetReadResultAsync(operationId);

            // Wait for the operation to complete
            int i = 1;
            int waitDurationInMs = 500;
            var maxWaitTimeInMs = 30000;
            int maxTries = maxWaitTimeInMs / waitDurationInMs;

            while ((result.Status == OperationStatusCodes.Running || result.Status == OperationStatusCodes.NotStarted)
                && i <= maxTries)
            {
                Console.WriteLine($"Computer Vision Read - Server status: {0}, try {1}, waiting {2} milliseconds...", result.Status, i, waitDurationInMs);
                await Task.Delay(waitDurationInMs);

                result = await _computerVisionClient.GetReadResultAsync(operationId);
                i++;
            }

            return result;
        }
    
        private async Task<OcrResult> ComputerVisionRecognizedPrintedTextByUrl(string imageUrl, OcrLanguages ocrLanguage)
        {
            return await _computerVisionClient.RecognizePrintedTextAsync(true, imageUrl, ocrLanguage);
        }

        private async Task<OcrResult> ComputerVisionRecognizedPrintedTextByStream(Stream imageStream, OcrLanguages ocrLanguage)
        {
            return await _computerVisionClient.RecognizePrintedTextInStreamAsync(true, imageStream, ocrLanguage);
        }
    }
}
