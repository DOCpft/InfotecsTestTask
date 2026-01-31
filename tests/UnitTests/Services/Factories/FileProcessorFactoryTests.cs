using InfotecsTestTask.Abstract;
using InfotecsTestTask.Services.Factories;
using InfotecsTestTask.DTOs;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using InfotecsTestTaskTests.UnitTests.Services.Factories.FakeProcessors;

namespace InfotecsTestTaskTests.UnitTests.Factories
{

    public class FileProcessorFactoryTests
    {
        [Fact]
        public void GetProcessor_ReturnsCorrectProcessor()
        {
            var processors = new IFileProcessingService[]
            {
                new FakeProcessorTxt(),
                new FakeProcessorCsv()
            };

            var factory = new FileProcessorFactory(processors);

            var processor = factory.GetProcessor("data.csv");

            Assert.NotNull(processor);
            Assert.IsType<FakeProcessorCsv>(processor);
        }

        [Fact]
        public void GetProcessor_Throws_WhenNoProcessor()
        {
            var processors = new IFileProcessingService[] { new FakeProcessorTxt() };
            var factory = new FileProcessorFactory(processors);

            Assert.Throws<NotSupportedException>(() => factory.GetProcessor("data.unknown"));
        }
    }
}