namespace InfotecsTestTask.Exceptions
{
    /// <summary>
    /// Кастомное исключение для ошибок валидации CSV
    /// </summary>
    public class CsvValidationException : Exception
    {
        public CsvValidationException(string message) : base(message) { }
    }
}
