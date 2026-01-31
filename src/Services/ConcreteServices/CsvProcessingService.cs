using CsvHelper;
using CsvHelper.Configuration;
using InfotecsTestTask.Abstract;
using InfotecsTestTask.DataAccess;
using InfotecsTestTask.DTOs;
using InfotecsTestTask.Entities;
using InfotecsTestTask.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace InfotecsTestTask.Services.ConcreteServices
{
    public class CsvProcessingService: IFileProcessingService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<CsvProcessingService> _logger;
        private readonly IFileProcessingConfiguration _config;
        private static readonly DateTime MIN_DATE = new DateTime(2000, 1, 1);
        private static readonly DateTime MAX_DATE = DateTime.UtcNow;
        public CsvProcessingService(
            AppDbContext context, 
            ILogger<CsvProcessingService> logger, 
            IFileProcessingConfiguration config)
        {
            _context = context;
            _logger = logger;
            _config = config;
        }
        public bool CanProcess(string fileName)
        {
            var extension = Path.GetExtension(fileName);

            var supportedFormats = _config.GetSupportedFormats();

            return supportedFormats.Any(f => f.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Выполняет обработку CSV файла
        /// </summary>
        public async Task<FileProcessingResult> ProcessAsync(IFormFile file)
        {
            var response = new FileProcessingResult();
            var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                var rows = await ParseAndValidateCsvAsync(file, response);
                if (!response.Success)
                {
                    await transaction.RollbackAsync();
                    return response;
                }


                await transaction.CommitAsync();
                response.Success = true;
                response.Message = "Файл успешно обработан";
                response.ProcessedRows = rows;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Внутренняя ошибка сервера";
                response.Errors.Add(ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Парсинг CSV файла со встроенной валидацией.
        /// Если какое-либо условие не выполнено - бросаем исключение
        /// </summary>
        private async Task<List<ProcessedData>> ParseAndValidateCsvAsync(IFormFile file, FileProcessingResult response)
        {
            var rows = new List<ProcessedData>();
            int lineNumber = 0;
            int validRowsCount = 0;

            try
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);

                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";",
                    HasHeaderRecord = false,
                    BadDataFound = context =>
                    {
                        // Если встретили некорректные данные - бросаем исключение
                        throw new CsvValidationException($"Строка {lineNumber + 1}: некорректные данные в строке");
                    },
                    MissingFieldFound = null,
                    TrimOptions = TrimOptions.Trim
                };

                using var csv = new CsvReader(reader, config);

                while (await csv.ReadAsync())
                {
                    lineNumber++;
                    validRowsCount++;

                    // Проверка количества полей (должно быть ровно 3)
                    if (csv.Parser.Count != 3)
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: не все поля заполнены. Ожидается 3 поля, получено {csv.Parser.Count}");
                    }

                    // Получение полей
                    var dateStr = csv.GetField(0);
                    var executionTimeStr = csv.GetField(1);
                    var valueStr = csv.GetField(2);

                    // Проверка на пустые значения
                    if (string.IsNullOrWhiteSpace(dateStr) ||
                        string.IsNullOrWhiteSpace(executionTimeStr) ||
                        string.IsNullOrWhiteSpace(valueStr))
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: одно или несколько полей пусты");
                    }

                    // 1. Валидация даты: парсинг и проверка диапазона
                    if (!DateTime.TryParseExact(dateStr.Trim(), "yyyy-MM-dd'T'HH-mm-ss.ffff'Z'",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: неверный формат даты. Ожидается: ГГГГ-ММ-ДДTчч-мм-сс.ммммZ, получено: {dateStr}");
                    }

                    // Проверка диапазона даты
                    if (date < MIN_DATE)
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: дата не может быть раньше {MIN_DATE:yyyy-MM-dd}");
                    }
                    if (date > MAX_DATE)
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: дата не может быть позже текущего времени");
                    }

                    // 2. Валидация времени выполнения: парсинг и проверка >= 0
                    if (!double.TryParse(executionTimeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var executionTime))
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: неверный формат времени выполнения: {executionTimeStr}");
                    }
                    if (executionTime < 0)
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: время выполнения не может быть меньше 0");
                    }

                    // 3. Валидация значения показателя: парсинг и проверка >= 0
                    if (!double.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: неверный формат значения показателя: {valueStr}");
                    }
                    if (value < 0)
                    {
                        throw new CsvValidationException(
                            $"Строка {lineNumber}: значение показателя не может быть меньше 0");
                    }

                    // Все проверки пройдены - добавляем строку
                    rows.Add(new ProcessedData
                    {
                        Date = date,
                        ExecutionTime = executionTime,
                        Value = value
                    });
                }

                // Проверка, что файл не пустой
                if (validRowsCount == 0)
                {
                    throw new CsvValidationException("Файл не содержит данных");
                }

                response.Success = true;
                return rows;
            }
            catch (CsvValidationException)
            {
                // Пробрасываем дальше наше кастомное исключение
                throw;
            }
            catch (Exception ex)
            {
                // Любая другая ошибка парсинга
                throw new CsvValidationException($"Ошибка чтения файла: {ex.Message}");
            }
        }



    }

}
