using InfotecsTestTask.DataAccess;
using InfotecsTestTask.Services.ConcreteServices;
using InfotecsTestTask.Services.Configuration;
using InfotecsTestTaskTests.UnitTests.Services.ConcreteServices;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;
using Xunit;

namespace InfotecsTestTaskTests.UnitTests.Services
{
    public class CsvProcessingServiceTests : IDisposable
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;

        public CsvProcessingServiceTests()
        {
            _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var ctx = new AppDbContext(_options);
            ctx.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection?.Dispose();
        }

        private IFormFile CreateFormFile(string content, string fileName)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return new TestFormFile(bytes, fileName);
        }

        [Fact]
        public async Task ProcessAsync_ValidCsv_ReturnsSuccessAndRows()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["FileProcessing:SupportedFormats:0"] = ".csv"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            await using var context = new AppDbContext(_options);
            var logger = NullLogger<CsvProcessingService>.Instance;
            var svc = new CsvProcessingService(context, logger, new FileProcessingConfigurationService(config));

            // CSV строка: Date;ExecutionTime;Value
            var csv = "2025-01-01T00-00-00.0000Z;1.5;10.0\n2025-01-01T00-01-00.0000Z;2.0;20.5\n";
            var formFile = CreateFormFile(csv, "data.csv");

            var result = await svc.ProcessAsync(formFile);

            Assert.True(result.Success);
            Assert.Equal(2, result.ProcessedRows.Count);
            Assert.Equal(1.5, result.ProcessedRows[0].ExecutionTime);
            Assert.Equal(10.0, result.ProcessedRows[0].Value);
        }

        [Fact]
        public async Task ProcessAsync_InvalidDate_ReturnsError()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["FileProcessing:SupportedFormats:0"] = ".csv"
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
            await using var context = new AppDbContext(_options);
            var logger = NullLogger<CsvProcessingService>.Instance;
            var svc = new CsvProcessingService(context, logger, new FileProcessingConfigurationService(config));

            var csv = "invalid-date;1.5;10.0\n";
            var formFile = CreateFormFile(csv, "data.csv");

            var result = await svc.ProcessAsync(formFile);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
        }
    }
}
