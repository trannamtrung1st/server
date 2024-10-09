namespace IO.Swagger.Lib.V3.Services;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AasxServerStandardBib.Models;

public class NotificationService
{
    private readonly HttpClient _ahiClient;

    public NotificationService()
    {
        _ahiClient = new HttpClient();
        _ahiClient.BaseAddress = new Uri("http://localhost:6001");
        SetAccessToken().Wait();
    }

    public async Task NotifyAssetChanged(Guid assetId)
    {
        var message = new NotificationMessage
        {
            AssetId = assetId.ToString(),
            Type = "asset"
        };

        async Task<HttpResponseMessage> Send()
        {
            var resp = await _ahiClient.PostAsJsonAsync("/ntf/notifications/asset/notify", message, options: new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return resp;
        }

        HttpResponseMessage resp = null;
        try
        {
            resp = await Send();
            resp.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException) when (resp?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            await SetAccessToken();
            resp = await Send();
            resp.EnsureSuccessStatusCode();
        }
    }

    private async Task SetAccessToken()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:6001/connect/token");
        var collection = new List<KeyValuePair<string, string>>();
        collection.Add(new("grant_type", "client_credentials"));
        collection.Add(new("client_id", "postman-sa"));
        collection.Add(new("client_secret", "CU8cEU3yJ4hCEd6QAB"));
        var content = new FormUrlEncodedContent(collection);
        request.Content = content;
        var response = await _ahiClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        _ahiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
    }

    class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
    }
}