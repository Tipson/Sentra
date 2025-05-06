// EmbeddingClient.cs — отправка текста на AI-сервер и получение эмбеддинга

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sentra.Config;

namespace Sentra.Application.Embedding;

public class EmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly ILogger<EmbeddingClient>? _logger;

    /// <summary>
    /// Конструктор клиента эмбеддингов
    /// </summary>
    /// <param name="endpoint">Адрес AI-сервера. Если null — берётся из AppConfig</param>
    /// <param name="logger">Логгер. Необязателен</param>
    public EmbeddingClient(string? endpoint = null, ILogger<EmbeddingClient>? logger = null)
    {
        _httpClient = new HttpClient();
        _endpoint = endpoint ?? AppConfig.AiServerUrl;
        _logger = logger;
    }

    /// <summary>
    /// Получить эмбеддинг (вектор) для заданного текста
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        try
        {
            var request = new { text = text };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("AI-сервер вернул ошибку: {StatusCode}", response.StatusCode);
                return Array.Empty<float>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<EmbeddingResponse>(json);
            return result?.Vector ?? Array.Empty<float>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при получении эмбеддинга");
            return Array.Empty<float>();
        }
    }

    private class EmbeddingResponse
    {
        [JsonPropertyName("vector")]
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}