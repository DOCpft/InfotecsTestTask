namespace InfotecsTestTask.Entities
{
    public class Result
    {
        public long Id { get; set; }

        public string? FileName { get; set; }

        public DateTime MinDate { get; set; }
        public DateTime MaxDate { get; set; }
        public double DeltaTimeSeconds { get; set; }
        public double AverageExecutionTime { get; set; }
        public double AverageValue { get; set; }
        public double MedianValue { get; set; }
        public double MaxValue { get; set; }
        public double MinValue { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
