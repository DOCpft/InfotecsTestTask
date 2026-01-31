namespace InfotecsTestTask.Abstract
{
    public interface IFileProcessorFactory
    {
        /// <summary>
        /// Получает обработчик, согласно расширению файла fileName
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        IFileProcessingService GetProcessor(string fileName);
    }
}
