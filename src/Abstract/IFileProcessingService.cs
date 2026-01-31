using InfotecsTestTask.DTOs;

namespace InfotecsTestTask.Abstract
{
    /// <summary>
    /// Определяет контракт для создаваемого обработчика
    /// </summary>
    public interface IFileProcessingService
    {
        /// <summary>
        /// Выполняет обработку файла в зависимости от типа процессора, путем вызова ProcessAsync в нужном классе.
        /// </summary>
        /// <param name="file"></param>
        /// <returns>Объект типа FileProcessingResult, содержащий результат обработки файла</returns>
        Task<FileProcessingResult> ProcessAsync(IFormFile file);

        /// <summary>
        /// Проверяет возможность обработки файла с данным расширением.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>true, если в списке обработчиков, есть такой, который умеет работать с расширением файла fileName</returns>
        bool CanProcess(string fileName);
    }
}
