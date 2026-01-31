using System.ComponentModel.DataAnnotations;

namespace InfotecsTestTask.DTOs
{
    public class ProcessedData
    {
        [Required]
        public DateTime Date { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double ExecutionTime { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public double Value { get; set; }
    }
}
