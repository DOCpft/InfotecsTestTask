using CmdScale.EntityFrameworkCore.TimescaleDB;
using InfotecsTestTask.DataAccess;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


namespace InfotecsTestTaskTests.IntegrationTests
{
    public class FilesControllerDockerIntegrationTests : IAsyncLifetime
    {
        private WebApplicationFactory<Program>? _factory;
        private HttpClient? _client;

        // Подключение к docker-compose timescaledb
        private readonly string _connectionString;

        public FilesControllerDockerIntegrationTests()
        {
            // Позволяет переопределить через переменную окружения при необходимости
            _connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                ?? "Host=localhost;Port=5433;Database=infotecsdb;Username=postgres;Password=password123";
        }

        public async Task InitializeAsync()
        {
            // Ждём готовности БД
            await WaitForPostgresAsync(_connectionString, TimeSpan.FromSeconds(60));

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Удаляем существующую регистрацию DbContext, если она есть
                        var descriptor = services.FirstOrDefault(d =>
                            d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                            d.ServiceType == typeof(AppDbContext));
                        if (descriptor != null) services.Remove(descriptor);

                        // Регистрируем тестовый DbContext с подключением к тестовой БД
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseNpgsql(_connectionString).UseTimescaleDb());
                    });
                });

            // Применяем миграции используя провайдер уже созданного тестового хоста
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            _client = _factory.CreateClient();
        }

        public async Task DisposeAsync()
        {
            _client?.Dispose();
            _factory?.Dispose();
            await Task.CompletedTask;
        }

        [Fact]
        public async Task UploadFile_EndToEnd_ReturnsSuccessAndStoresResults()
        {
            var csv = "2025-01-01T00-00-00.0000Z;1.5;10.0\n2025-01-01T00-01-00.0000Z;2.0;20.5\n";
            var bytes = Encoding.UTF8.GetBytes(csv);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", "data.csv");

            var response = await _client!.PostAsync("/api/Files/upload", content);

            var body = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"Upload failed. Status: {(int)response.StatusCode}. Body: {body}");

            // Ждём появление записи Results в БД (в течение 10s)
            await WaitForResultsInDb("data.csv", TimeSpan.FromSeconds(10));

            // Проверяем, что результат появился в БД
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM ""Results"" WHERE ""FileName"" = @p";
            cmd.Parameters.AddWithValue("p", "data.csv");
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }


        [Fact]
        public async Task GetResults_AfterUpload_ReturnsResult()
        {
            // уникальное имя файла, чтобы тесты не конфликтовали
            var fileName = $"data_{Guid.NewGuid():N}.csv";

            // подготовим CSV с 2 строками
            var rows = Enumerable.Range(0, 2)
                .Select(i => $"{DateTime.UtcNow.AddMinutes(-i).ToString("yyyy-MM-dd'T'HH-mm-ss.ffff'Z'")};{1.0 + i};{10.0 + i}")
                .ToArray();
            var csv = string.Join("\n", rows) + "\n";
            var bytes = Encoding.UTF8.GetBytes(csv);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", fileName);

            var uploadResponse = await _client!.PostAsync("/api/Files/upload", content);
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            Assert.True(uploadResponse.IsSuccessStatusCode, $"Upload failed. Status: {(int)uploadResponse.StatusCode}. Body: {uploadBody}");

            // Ждём появления результата в таблице Results
            await WaitForResultsInDb(fileName, TimeSpan.FromSeconds(10));

            // запрос результатов по фильтру FileName (передаём без расширения — контроллер использует Contains)
            var filter = Uri.EscapeDataString(Path.GetFileNameWithoutExtension(fileName));
            var response = await _client.GetAsync($"/api/Files/results?FileName={filter}");

            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"GetResults failed. Status: {(int)response.StatusCode}. Body: {responseBody}");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            Assert.True(root.ValueKind == JsonValueKind.Array, $"Expected JSON array but got: {root.ValueKind}. Body: {responseBody}");

            // должно быть хотя бы одна запись с нашим полным именем файла
            var found = root.EnumerateArray().Any(el =>
                el.TryGetProperty("fileName", out var p) && p.GetString() == fileName);

            Assert.True(found, $"Ожидается запись Result для загруженного файла {fileName}. Body: {responseBody}");
        }

        [Fact]
        public async Task GetLatestValues_AfterUpload_ReturnsLatestN()
        {
            var fileName = $"vals_{Guid.NewGuid():N}.csv";

            // создаём 5 строк с возрастающими датами
            var baseTime = DateTime.UtcNow;
            var rows = Enumerable.Range(0, 5)
                .Select(i => $"{baseTime.AddMinutes(-i).ToString("yyyy-MM-dd'T'HH-mm-ss.ffff'Z'")};{1.0 + i};{100.0 + i}")
                .ToArray();
            var csv = string.Join("\n", rows) + "\n";
            var bytes = Encoding.UTF8.GetBytes(csv);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
            content.Add(fileContent, "file", fileName);

            var uploadResponse = await _client!.PostAsync("/api/Files/upload", content);
            var uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            Assert.True(uploadResponse.IsSuccessStatusCode, $"Upload failed. Status: {(int)uploadResponse.StatusCode}. Body: {uploadBody}");

            // Ждём появления значений в таблице Values_
            await WaitForValuesInDb(fileName, expectedCount: 5, timeout: TimeSpan.FromSeconds(10));

            // запрос последних 3 значений
            var response = await _client.GetAsync($"/api/Files/{Uri.EscapeDataString(fileName)}/values/latest/3");

            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"GetLatestValues failed. Status: {(int)response.StatusCode}. Body: {responseBody}");

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            Assert.True(root.ValueKind == JsonValueKind.Array, $"Expected JSON array but got: {root.ValueKind}. Body: {responseBody}");
            var arr = root.EnumerateArray().ToArray();
            Assert.Equal(3, arr.Length);

            // проверяем порядок по убыванию даты
            var dates = arr.Select(el =>
            {
                if (el.TryGetProperty("date", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    return DateTime.Parse(d.GetString()!, null, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                return DateTime.MinValue;
            }).ToList();

            for (int i = 1; i < dates.Count; i++)
            {
                Assert.True(dates[i - 1] >= dates[i], "Ожидается порядок дат по убыванию");
            }
        }


        private static async Task WaitForPostgresAsync(string connectionString, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    await using var conn = new NpgsqlConnection(connectionString);
                    await conn.OpenAsync();
                    return;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
            throw new Exception("Postgres did not become ready within the timeout.");
        }

        private async Task WaitForResultsInDb(string fileName, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT COUNT(*) FROM ""Results"" WHERE ""FileName"" = @p";
                cmd.Parameters.AddWithValue("p", fileName);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count > 0) return;
                await Task.Delay(250);
            }
            throw new Exception($"Result for '{fileName}' did not appear in DB within {timeout.TotalSeconds}s");
        }

        private async Task WaitForValuesInDb(string fileName, int expectedCount, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT COUNT(*) FROM ""Values_"" WHERE ""FileName"" = @p";
                cmd.Parameters.AddWithValue("p", fileName);
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count >= expectedCount) return;
                await Task.Delay(250);
            }
            throw new Exception($"Values for '{fileName}' did not reach {expectedCount} rows in DB within {timeout.TotalSeconds}s");
        }
    }
}