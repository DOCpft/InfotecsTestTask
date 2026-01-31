using InfotecsTestTask.Abstract;

namespace InfotecsTestTask.Services.Configuration
{
    public class FileProcessingConfigurationService: IFileProcessingConfiguration
    {
        private readonly IConfiguration _config;

        public FileProcessingConfigurationService(IConfiguration config)
        {
            _config = config;
        }

        public string[] GetSupportedFormats()
        {
            return _config.GetSection("FileProcessing:SupportedFormats").Get<string[]>() ?? [".csv"];
        }
    }
}
