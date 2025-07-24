using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TelegramInnBot.Models;

/// <summary>
/// Класс, инкапсулирующий функционал по поиску данных по ИНН.
/// </summary>
public static class InnService
{
    private static readonly HttpClient httpClient = new();
    private static string _apiToken;

    // Конструктор, собирающий нужный ключ из конфиг файла.
    static InnService()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("tokens.json")
            .Build();

        _apiToken = config["DaDataApiToken"];
    }

    /// <summary>
    /// Поиск данных в DaData по списку ИНН (с проверками).
    /// </summary>
    /// <param name="innList"> Список ИНН, которые нужно обработать. </param>
    /// <returns> Список структур данных о нужных компаниях. </returns>
    public static async Task<List<CompanyInfo>> GetCompaniesAsync(List<string> innList)
    {
        List<CompanyInfo> results = new();

        foreach (var inn in innList)
        {
            if (string.IsNullOrWhiteSpace(inn) || !inn.All(char.IsDigit))
            {
                results.Add(new CompanyInfo
                {
                    Name = $"Некорректный ИНН: {inn}",
                    Address = ""
                });
                continue;
            }

            try
            {
                var company = await QueryDadataAsync(inn);
                if (company != null)
                {
                    results.Add((CompanyInfo)company);
                }
                else
                {
                    results.Add(new CompanyInfo
                    {
                        Name = $"Компания с ИНН {inn} не найдена",
                        Address = ""
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new CompanyInfo
                {
                    Name = $"Ошибка при запросе ИНН {inn}",
                    Address = ex.Message
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Запрос-получение ответа от DaData через предоставленный ИНН.
    /// </summary>
    /// <param name="inn"> Предоставленный ИНН. </param>
    /// <returns> Структура нужных данных о компании. </returns>
    private static async Task<CompanyInfo?> QueryDadataAsync(string inn)
    {
        var requestUri = "https://suggestions.dadata.ru/suggestions/api/4_1/rs/findById/party";

        var requestJson = JsonSerializer.Serialize(new { query = inn });

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiToken);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var suggestions = document.RootElement.GetProperty("suggestions");

        if (suggestions.GetArrayLength() == 0)
            return null;

        var data = suggestions[0].GetProperty("data");
        var name = data.GetProperty("name").GetProperty("full_with_opf").GetString();
        var address = data.GetProperty("address").GetProperty("value").GetString();

        return new CompanyInfo
        {
            Name = name ?? $"Без названия ({inn})",
            Address = address ?? "Адрес не найден"
        };
    }
}