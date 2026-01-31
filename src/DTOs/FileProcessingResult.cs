namespace InfotecsTestTask.DTOs
{
    public class FileProcessingResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ProcessedData> ProcessedRows { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
