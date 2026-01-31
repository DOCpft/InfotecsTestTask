using InfotecsTestTask.Services.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace InfotecsTestTaskTests.UnitTests.Services.Configuration
{
    public class FileProcessingConfigurationServiceTests
    {
        [Fact]
        public void GetSupportedFormats_ReturnsConfiguredFormats()
        {
            var inMemory = new Dictionary<string, string?>
            {
                ["FileProcessing:SupportedFormats:0"] = ".csv",
                ["FileProcessing:SupportedFormats:1"] = ".txt"
            };
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemory)
                .Build();

            var svc = new FileProcessingConfigurationService(config);

            var formats = svc.GetSupportedFormats();

            Assert.NotNull(formats);
            Assert.Contains(".csv", formats);
            Assert.Contains(".txt", formats);
        }

        [Fact]
        public void GetSupportedFormats_ReturnsDefault_WhenMissing()
        {
            var config = new ConfigurationBuilder().Build();
            var svc = new FileProcessingConfigurationService(config);

            var formats = svc.GetSupportedFormats();

            Assert.Single(formats);
            Assert.Equal(".csv", formats[0]);
        }
    }
}
