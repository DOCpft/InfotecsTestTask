//using InfotecsTestTask.Abstract;
//using InfotecsTestTask.DataAccess;
//using InfotecsTestTask.DTOs;
//using InfotecsTestTask.Entities;
//using Microsoft.EntityFrameworkCore;
//using Npgsql;
//using System.Linq;

//namespace InfotecsTestTask.Services
//{
//    public class UnivarsalFileProcessingService
//    {
//        private readonly IFileProcessorFactory _fileProcessorFactory;
//        private readonly AppDbContext _context;
//        private readonly ILogger<UnivarsalFileProcessingService> _logger;

//        public UnivarsalFileProcessingService(
//            IFileProcessorFactory fileProcessorFactory,
//            AppDbContext context,
//            ILogger<UnivarsalFileProcessingService> logger)
//        {
//            _fileProcessorFactory = fileProcessorFactory;
//            _context = context;
//            _logger = logger;
//        }

//        public async Task<FileProcessingResult> ProcessFileAsync(IFormFile file)
//        {
//            var response = new FileProcessingResult();

//            try
//            {
//                var processor = _fileProcessorFactory.GetProcessor(file.FileName);

//                var result = await processor.ProcessAsync(file);

//                if (!result.Success)
//                {
//                    response.Errors = result.Errors;
//                    return response;
//                }

//                await RemoveExistingDataAsync(file.FileName);
//                await SaveValuesAsync(file.FileName, result.ProcessedRows);
//                await CalculateAndSaveResultsAsync(file.FileName, result.ProcessedRows);

//                response.Success = true;
//                response.ProcessedRows = result.ProcessedRows;
//            }
//            catch (NotSupportedException ex)
//            {
//                response.Errors.Add(ex.Message);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Ошибка обработки файла");
//                response.Errors.Add("Внутренняя ошибка сервера");
//            }

//            return response;
//        }

//        private async Task RemoveExistingDataAsync(string fileName)
//        {
//            var deleteValuesSql = @"DELETE FROM ""Values_"" WHERE ""FileName"" = @fileName";
//            await _context.Database.ExecuteSqlRawAsync(deleteValuesSql,
//                new NpgsqlParameter("@fileName", fileName));

//            var existingResults = await _context.Results.FirstOrDefaultAsync(r => r.FileName == fileName);
//            if (existingResults != null)
//            {
//                _context.Results.Remove(existingResults);
//                await _context.SaveChangesAsync();
//            }
//        }

//        private async Task SaveValuesAsync(string fileName, List<ProcessedData> rows)
//        {
//            if (!rows.Any()) return;

//            var sql = @"
//            INSERT INTO ""Values_"" (""FileName"", ""Date"", ""ExecutionTime"", ""Value"")
//            VALUES (@fileName, @date, @execTime, @value)";

//                foreach (var row in rows)
//                {
//                    await _context.Database.ExecuteSqlRawAsync(sql,
//                        new NpgsqlParameter("@fileName", fileName),
//                        new NpgsqlParameter("@date", row.Date),
//                        new NpgsqlParameter("@execTime", row.ExecutionTime),
//                        new NpgsqlParameter("@value", row.Value)
//                    );
//                }

//        }

//        private async Task CalculateAndSaveResultsAsync(string fileName, List<ProcessedData> rows)
//        {
//            var dates = rows.Select(r => r.Date).ToList();
//            var executionTimes = rows.Select(r => r.ExecutionTime).ToList();
//            var values = rows.Select(r => r.Value).ToList();

//            var results = new Result
//            {
//                FileName = fileName,
//                MinDate = dates.Min(),
//                MaxDate = dates.Max(),
//                DeltaTimeSeconds = (dates.Max() - dates.Min()).TotalSeconds,
//                AverageExecutionTime = executionTimes.Average(),
//                AverageValue = values.Average(),
//                MedianValue = CalculateMedian(values),
//                MaxValue = values.Max(),
//                MinValue = values.Min()
//            };

//            await _context.Results.AddAsync(results);
//            await _context.SaveChangesAsync();
//        }

//        private double CalculateMedian(List<double> values)
//        {
//            var sorted = values.OrderBy(v => v).ToList();

//            int count = sorted.Count;

//            if (count % 2 == 0) return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;

//            return sorted[count / 2];
//        }
//    }
//}
using InfotecsTestTask.Abstract;
using InfotecsTestTask.DataAccess;
using InfotecsTestTask.DTOs;
using InfotecsTestTask.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using System.Linq;

namespace InfotecsTestTask.Services
{
    public class UnivarsalFileProcessingService
    {
        private readonly IFileProcessorFactory _fileProcessorFactory;
        private readonly AppDbContext _context;
        private readonly ILogger<UnivarsalFileProcessingService> _logger;

        public UnivarsalFileProcessingService(
            IFileProcessorFactory fileProcessorFactory,
            AppDbContext context,
            ILogger<UnivarsalFileProcessingService> logger)
        {
            _fileProcessorFactory = fileProcessorFactory;
            _context = context;
            _logger = logger;
        }

        public async Task<FileProcessingResult> ProcessFileAsync(IFormFile file)
        {
            var response = new FileProcessingResult();

            try
            {
                var processor = _fileProcessorFactory.GetProcessor(file.FileName);

                var result = await processor.ProcessAsync(file);

                if (!result.Success)
                {
                    response.Errors = result.Errors;
                    return response;
                }

                // Накладываем advisory lock по имени файла, чтобы избежать гонок при параллельных загрузках одного fileName.
                await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Получаем соединение и открываем его (если нужно) — импорт и SQL должны выполняться на одном соединении/транзакции.
                    var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    // Берём advisory lock в рамках транзакции (блокировка автоматически отпустится при завершении транзакции)
                    // Используем hashtext(fileName) для получения int64-ключа
                    var lockSql = @"SELECT pg_advisory_xact_lock(hashtext(@p0)::bigint)";
                    await _context.Database.ExecuteSqlRawAsync(lockSql, file.FileName);

                    await RemoveExistingDataAsync(file.FileName);
                    await SaveValuesAsync(file.FileName, result.ProcessedRows, conn);
                    await CalculateAndSaveResultsAsync(file.FileName, result.ProcessedRows);

                    await dbTransaction.CommitAsync();
                }
                catch
                {
                    await dbTransaction.RollbackAsync();
                    throw;
                }

                response.Success = true;
                response.ProcessedRows = result.ProcessedRows;
            }
            catch (NotSupportedException ex)
            {
                response.Errors.Add(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки файла");
                response.Errors.Add("Внутренняя ошибка сервера");
            }

            return response;
        }

        private async Task RemoveExistingDataAsync(string fileName)
        {
            var sql = @"
                DELETE FROM ""Values_"" WHERE ""FileName"" = @fileName;
                DELETE FROM ""Results"" WHERE ""FileName"" = @fileName;
                ";
            await _context.Database.ExecuteSqlRawAsync(sql,
                new NpgsqlParameter("@fileName", fileName));
        }

        private async Task SaveValuesAsync(string fileName, List<ProcessedData> rows, NpgsqlConnection conn)
        {
            if (!rows.Any()) return;

            var copyCommand = @"COPY ""Values_"" (""FileName"", ""Date"", ""ExecutionTime"", ""Value"") FROM STDIN (FORMAT BINARY)";

            using var writer = await conn.BeginBinaryImportAsync(copyCommand);
            foreach (var row in rows)
            {
                writer.StartRow();
                writer.Write(fileName, NpgsqlDbType.Text);
                writer.Write(row.Date, NpgsqlDbType.TimestampTz);
                writer.Write(row.ExecutionTime, NpgsqlDbType.Double);
                writer.Write(row.Value, NpgsqlDbType.Double);
            }
            writer.Complete();
        }

        private async Task CalculateAndSaveResultsAsync(string fileName, List<ProcessedData> rows)
        {
            var dates = rows.Select(r => r.Date).ToList();
            var executionTimes = rows.Select(r => r.ExecutionTime).ToList();
            var values = rows.Select(r => r.Value).ToList();

            var results = new Result
            {
                FileName = fileName,
                MinDate = dates.Min(),
                MaxDate = dates.Max(),
                DeltaTimeSeconds = (dates.Max() - dates.Min()).TotalSeconds,
                AverageExecutionTime = executionTimes.Any() ? executionTimes.Average() : 0,
                AverageValue = values.Any() ? values.Average() : 0,
                MedianValue = values.Any() ? CalculateMedian(values) : 0,
                MaxValue = values.Any() ? values.Max() : 0,
                MinValue = values.Any() ? values.Min() : 0
            };

            await _context.Results.AddAsync(results);
            await _context.SaveChangesAsync();
        }

        private double CalculateMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();

            int count = sorted.Count;

            if (count % 2 == 0) return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;

            return sorted[count / 2];
        }
    }
}