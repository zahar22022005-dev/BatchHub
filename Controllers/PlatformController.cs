using Microsoft.AspNetCore.Mvc;
using BatchHub.Models;
using System.Text.Json;

namespace BatchHub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlatformController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformController> _logger;

    // Адреса API (из переменных окружения на Amvera)
    private readonly string _myApiUrl;
    private readonly string _partnerApiUrl;

    public PlatformController(HttpClient httpClient, ILogger<PlatformController> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Читаем адреса из переменных окружения
        _myApiUrl = Environment.GetEnvironmentVariable("MY_API_URL") ?? "https://batchapi-24h4r.amvera.io";
        _partnerApiUrl = Environment.GetEnvironmentVariable("PARTNER_API_URL") ?? "https://raw.githubusercontent.com/Zahar22022005-dev/partner-api/main";
    }

    /// <summary>
    /// Проверка доступности обоих API
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> HealthCheck()
    {
        var results = new
        {
            MyApi = await CheckApiHealth($"{_myApiUrl}/api/Batch"),
            PartnerApi = await CheckApiHealth($"{_partnerApiUrl}/demand.json")
        };
        return Ok(results);
    }

    private async Task<bool> CheckApiHealth(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Обработка заявки: получение коэффициента спроса → корректировка → создание партии
    /// </summary>
    [HttpPost("process")]
    public async Task<IActionResult> ProcessRequest([FromBody] BatchRequest request)
    {
        // 1. Валидация входных данных
        if (string.IsNullOrWhiteSpace(request.ProductName))
            return BadRequest(new { error = "Название товара обязательно" });

        if (request.Quantity <= 0)
            return BadRequest(new { error = "Количество должно быть больше 0" });

        if (request.Price <= 0)
            return BadRequest(new { error = "Цена должна быть больше 0" });

        // 2. Получение коэффициента спроса от API партнёра (GitHub JSON)
        double demandFactor = 1.0;
        try
        {
            demandFactor = await GetDemandFactor(request.ProductName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось получить коэффициент спроса");
            return StatusCode(503, new { error = "Сервис коэффициента спроса недоступен" });
        }

        // 3. Корректировка количества
        int adjustedQuantity = (int)Math.Round(request.Quantity * demandFactor);
        if (adjustedQuantity < 1) adjustedQuantity = 1;

        // 4. Отправка в моё API
        var batchData = new
        {
            productName = request.ProductName,
            quantity = adjustedQuantity,
            price = request.Price
        };

        try
        {
            var json = JsonSerializer.Serialize(batchData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_myApiUrl}/api/Batch", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return StatusCode(500, new { error = $"Ошибка создания партии: {error}" });
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var createdBatch = JsonSerializer.Deserialize<object>(responseJson);

            // 5. Формирование ответа
            var result = new BatchResponse
            {
                Id = 0,
                ProductName = request.ProductName,
                OriginalQuantity = request.Quantity,
                AdjustedQuantity = adjustedQuantity,
                Price = request.Price,
                DemandFactor = demandFactor
            };

            return Ok(new 
            { 
                message = "Партия успешно создана с учётом коэффициента спроса",
                batch = createdBatch,
                adjustment = result
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при вызове моего API");
            return StatusCode(500, new { error = "Сервис создания партий недоступен" });
        }
    }

    private async Task<double> GetDemandFactor(string productName)
    {
        // Запрос к статическому JSON-файлу на GitHub
        var response = await _httpClient.GetAsync($"{_partnerApiUrl}/demand.json");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DemandResponse>(json);
        
        // Можно добавить логику разных коэффициентов для разных товаров
        var productDemand = new Dictionary<string, double>
        {
            { "хлеб", 1.2 },
            { "молоко", 1.1 },
            { "масло", 1.3 },
            { "сахар", 0.9 }
        };
        
        var key = productName.ToLower();
        if (productDemand.ContainsKey(key))
        {
            return productDemand[key] * (result?.DemandFactor ?? 1.0);
        }
        
        return result?.DemandFactor ?? 1.0;
    }

    private class DemandResponse
    {
        public double DemandFactor { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}