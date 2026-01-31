using InfotecsTestTask.Abstract;
using InfotecsTestTask.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfotecsTestTaskTests.UnitTests.Services.Factories.FakeProcessors
{
    internal class FakeProcessorCsv : IFileProcessingService
    {
        public bool CanProcess(string fileName) => fileName.EndsWith(".csv");

        public Task<FileProcessingResult> ProcessAsync(IFormFile file) =>
            Task.FromResult(new FileProcessingResult { Success = true });
    }
}
