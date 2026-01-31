using InfotecsTestTask.DataAccess;
using InfotecsTestTask.DTOs;
using InfotecsTestTask.Entities;
using InfotecsTestTask.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;


namespace InfotecsTestTask.Controllers
{
    /// <summary>
    /// Контроллер предназначен для обработки запросов над файлом:
    ///     1. POST для отправки файла на сервер и осуществление его обработки, после чего добавляются/обновляются записи в таблицах БД - Values, Results
    ///     2. GET для получения записей из таблицы Results, соответствующих указанному в запросе фильтру
    ///     3. GET для получения последних 10 записей из таблицы Values, отсортированных по начальному времени запуска Date, по имени файла
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController: ControllerBase
    {
        private readonly UnivarsalFileProcessingService _processingService;
        private readonly AppDbContext _context;
        private readonly ILogger<FilesController> _logger;

        public FilesController(
            UnivarsalFileProcessingService processingService,
            AppDbContext context,
            ILogger<FilesController> logger)
        {
            _processingService = processingService;
            _context = context;
            _logger = logger;
        }


        /// <summary>
        /// 1. Загрузка и обработка CSV файла
        /// </summary>
        /// <param name="file">CSV файл в формате: Date;ExecutionTime;Value</param>
        /// <returns>Результат обработки файла</returns>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [SwaggerOperation(
            Summary = "Обрабатывает отправленный файл",
            Description = "Возвращает результат обработки файла." +
            "Если файл не был обработан (не прошла валидация, некорректный формат) - возвращает ошибку 400"
            )]
        [SwaggerResponse(200, "Файл успешно прошел обработку", typeof(FileProcessingResult))]
        [SwaggerResponse(400, "Файл не был обработан")]
        public async Task<ActionResult<FileProcessingResult>> UploadFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Файл не предоставлен, или оказался пустым");
            }

            var result = await _processingService.ProcessFileAsync(file);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);

        }


        /// <summary>
        /// Получает список записей из таблицы Result, удовлетворяющих заданным фильтрам
        /// </summary>
        /// <param name="filter">Параметры фильтрации</param>
        /// <returns>Список результатов обработки файлов</returns>
        [HttpGet("results")]
        [SwaggerOperation(
            Summary = "Получает результат вычисления данных, которые были вычислены после загрузки файла.",
            Description = "Возвращает список вычисленных значений согласно используемым фильтрам. \n" +
            "Если фильтры были заданы в некорректном формате, - ошибка 400")]
        [SwaggerResponse(200, "Фильтры применены и необходимые значения получены.", typeof(IEnumerable<Result>))]
        [SwaggerResponse(400, "Фильтры не применились, значения не получены.")]
        public async Task<ActionResult<IEnumerable<Result>>> GetResults([FromQuery] ResultFilterDto filter)
        {
            try
            {
                var validationResult = ValidateFilter(filter);

                if (!validationResult.IsValid)
                {
                    return BadRequest(new {errors = validationResult.Errors});
                }

                var query = _context.Results.AsQueryable();

                query = ApplyFilters(query, filter);

                query = query.OrderByDescending(r => r.CreatedAt);

                var results = await query.ToListAsync();

                _logger.LogInformation("Получено {Count} строк", results.Count);

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении результата");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Внутренняя ошибка сервера." });
            }
        }


        /// <summary>
        /// 3. Получение последних n значений по имени файла
        /// </summary>
        /// <param name="fileName">Имя файла</param>
        /// <returns>Последние n записей из таблицы Values</returns>
        [HttpGet("{fileName}/values/latest/{n?}")]
        [SwaggerOperation(
            Summary = "Получает последние n значений загруженных из файла (но перед вычислением)",
            Description = "Возвращает список из n запрошенных значений. \n" +
            "Если имя файла не указано или записи для такого файла не найдены, - ошибка 400 \n" +
            "Если n не указано, то возвращаются последние 10 записей. Максимум можно вернуть n = 100 записей")]
        [SwaggerResponse(200, "Имя файла корректное, записи получены", typeof(IEnumerable<Values>))]
        [SwaggerResponse(400, "Имя файла некорректно - значения не получены.")]
        public async Task<ActionResult<IEnumerable<Values>>> GetLatestValues(string fileName, int n = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest("Имя файла не указано");
                }

                var fileExists = await _context.Results.AnyAsync(r => r.FileName == fileName);

                if (!fileExists)
                {
                    return BadRequest("Запись с таким именем файла не найдена!");
                }

                if (n <= 0) n = 10;
                if (n > 100) n = 100;
                var latestValues = await _context.Values_
                    .Where(v => v.FileName == fileName)
                    .OrderByDescending(v => v.Date)
                    .Take(n)
                    .ToListAsync();

                _logger.LogInformation("Получено {Count} последних значений для файла {FileName}", latestValues.Count, fileName);
                return Ok(latestValues);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении последних значений для файла {FileName}", fileName);
                return BadRequest("Внутренняя ошибка сервера");
            }
        }


        /// <summary>
        /// Применение фильтров к запросу
        /// </summary>
        private IQueryable<Result> ApplyFilters(IQueryable<Result> query, ResultFilterDto filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.FileName))
            {
                query = query.Where(r => r.FileName.Contains(filter.FileName));
            }

            if (filter.MinDateFrom.HasValue)
            {
                query = query.Where(r => r.MinDate >= filter.MinDateFrom.Value);
            }

            if (filter.MinDateTo.HasValue)
            {
                query = query.Where(r => r.MinDate <= filter.MinDateTo.Value);
            }

            if (filter.AverageValueFrom.HasValue)
            {
                query = query.Where(r => r.AverageValue >= filter.AverageValueFrom.Value);
            }

            if (filter.AverageValueTo.HasValue)
            {
                query = query.Where(r => r.AverageValue <= filter.AverageValueTo.Value);
            }

            if (filter.AverageExecutionTimeFrom.HasValue)
            {
                query = query.Where(r => r.AverageExecutionTime >= filter.AverageExecutionTimeFrom.Value);
            }

            if (filter.AverageExecutionTimeTo.HasValue)
            {
                query = query.Where(r => r.AverageExecutionTime <= filter.AverageExecutionTimeTo.Value);
            }

            return query;
        }


        /// <summary>
        /// Валидация параметров фильтра
        /// </summary>
        private (bool IsValid, List<string> Errors) ValidateFilter(ResultFilterDto filter)
        {
            var errors = new List<string>();

            // Проверка корректности диапазонов дат
            if (filter.MinDateFrom.HasValue && filter.MinDateTo.HasValue)
            {
                if (filter.MinDateFrom > filter.MinDateTo)
                {
                    errors.Add("Начальная дата не может быть позже конечной даты");
                }
            }

            // Проверка корректности диапазонов значений
            if (filter.AverageValueFrom.HasValue && filter.AverageValueTo.HasValue)
            {
                if (filter.AverageValueFrom > filter.AverageValueTo)
                {
                    errors.Add("Минимальное среднее значение не может быть больше максимального");
                }
            }

            // Проверка корректности диапазонов времени выполнения
            if (filter.AverageExecutionTimeFrom.HasValue && filter.AverageExecutionTimeTo.HasValue)
            {
                if (filter.AverageExecutionTimeFrom > filter.AverageExecutionTimeTo)
                {
                    errors.Add("Минимальное среднее время выполнения не может быть больше максимального");
                }
            }

            return (!errors.Any(), errors);
        }
    }
}
