using System.ComponentModel.DataAnnotations;

namespace InfotecsTestTask.Entities
{
    public class Values
    {
        //public long Id { get; set; }

        [Required]
        public string? FileName { get; set; }
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
