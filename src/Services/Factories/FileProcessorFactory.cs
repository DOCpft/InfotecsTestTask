using InfotecsTestTask.Abstract;

namespace InfotecsTestTask.Services.Factories
{
    public class FileProcessorFactory : IFileProcessorFactory
    {
        private readonly IEnumerable<IFileProcessingService> _processors;
        public FileProcessorFactory(IEnumerable<IFileProcessingService> processors)
        {
            _processors = processors;
        }
        public IFileProcessingService GetProcessor(string fileName)
        {
            var processor = _processors.FirstOrDefault(p => p.CanProcess(fileName));

            if (processor == null)
                throw new NotSupportedException($"Формат файла {Path.GetExtension(fileName)} не поддерживается!");

            return processor;
        }
    }
}
