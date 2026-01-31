namespace InfotecsTestTask.DTOs
{
    public class ResultFilterDto
    {
        public string FileName { get; set; }
        public DateTime? MinDateFrom { get; set; }
        public DateTime? MinDateTo { get; set; }
        public double? AverageValueFrom { get; set; }
        public double? AverageValueTo { get; set; }
        public double? AverageExecutionTimeFrom { get; set; }
        public double? AverageExecutionTimeTo {  get; set; }

    }
}
