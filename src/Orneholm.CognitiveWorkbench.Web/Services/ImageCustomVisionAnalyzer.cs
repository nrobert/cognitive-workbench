using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction;
using Microsoft.Azure.CognitiveServices.Vision.CustomVision.Prediction.Models;
using Orneholm.CognitiveWorkbench.Web.Models.CustomVision;

namespace Orneholm.CognitiveWorkbench.Web.Services
{
    public class ImageCustomVisionAnalyzer
    {
        private readonly CustomVisionPredictionClient _customVisionPredictionClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public ImageCustomVisionAnalyzer(string customVisionPredictionKey, string customVisionEndpoint, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _customVisionPredictionClient = new CustomVisionPredictionClient(new ApiKeyServiceClientCredentials(customVisionPredictionKey))
            {
                Endpoint = customVisionEndpoint
            };
        }

        public async Task<CustomVisionResponse> AnalyzeAsync(string imageUrl, IFormFile file, 
            Guid projectId, string iterationPublishedName, CustomVisionProjectType projectType)
        {
            // Custom vision
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                var imageAnalysis = CustomVisionAnalyzeImageByUrlAsync(imageUrl, projectId, iterationPublishedName, projectType);
                var imageInfo = ImageInfoProcessor.GetImageInfoFromImageUrlAsync(imageUrl, _httpClientFactory);

                // Combine
                await Task.WhenAll(imageAnalysis, imageInfo);

                return new CustomVisionResponse
                {
                    ImageInfo = imageInfo.Result,
                    Predictions = imageAnalysis.Result.Predictions.ToList()
                };
            }
            else
            {
                using (var analyzeStream = new MemoryStream())
                using (var outputStream = new MemoryStream())
                {
                    // Get initial value
                    await file.CopyToAsync(analyzeStream);

                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    await analyzeStream.CopyToAsync(outputStream);

                    // Reset the stream for consumption
                    analyzeStream.Seek(0, SeekOrigin.Begin);
                    outputStream.Seek(0, SeekOrigin.Begin);

                    var imageAnalysis = await CustomVisionAnalyzeImageByStreamAsync(analyzeStream, projectId, iterationPublishedName, projectType);
                    var imageInfo = ImageInfoProcessor.GetImageInfoFromStream(outputStream, file.ContentType);

                    return new CustomVisionResponse
                    {
                        ImageInfo = imageInfo,
                        Predictions = imageAnalysis.Predictions.ToList()
                    };
                }
            }
        }

        private async Task<ImagePrediction> CustomVisionAnalyzeImageByUrlAsync(string imageUrl, Guid projectId, string publishedName, CustomVisionProjectType projectType)
        {
            ImagePrediction imagePrediction = null;

            switch (projectType)
            {
                case CustomVisionProjectType.Classification:
                    imagePrediction = await _customVisionPredictionClient.ClassifyImageUrlWithNoStoreAsync(
                        projectId: projectId,
                        publishedName: publishedName,
                        imageUrl: new ImageUrl(imageUrl));
                    break;
                case CustomVisionProjectType.Object_Detection:
                    imagePrediction = await _customVisionPredictionClient.DetectImageUrlWithNoStoreAsync(
                        projectId: projectId,
                        publishedName: publishedName,
                        imageUrl: new ImageUrl(imageUrl));
                    break;
            }

            return imagePrediction;
        }

        private async Task<ImagePrediction> CustomVisionAnalyzeImageByStreamAsync(Stream imageStream, Guid projectId, string publishedName, CustomVisionProjectType projectType)
        {
            ImagePrediction imagePrediction = null;

            switch (projectType)
            {
                case CustomVisionProjectType.Classification:
                    imagePrediction = await _customVisionPredictionClient.ClassifyImageWithNoStoreAsync(
                        projectId: projectId,
                        publishedName: publishedName,
                        imageData: imageStream);
                    break;
                case CustomVisionProjectType.Object_Detection:
                    imagePrediction = await _customVisionPredictionClient.DetectImageWithNoStoreAsync(
                        projectId: projectId,
                        publishedName: publishedName,
                        imageData: imageStream);
                    break;
            }

            return imagePrediction;
        }
    }
}
