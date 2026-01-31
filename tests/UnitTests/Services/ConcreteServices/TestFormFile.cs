using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfotecsTestTaskTests.UnitTests.Services.ConcreteServices
{
    internal class TestFormFile : IFormFile
    {
        private readonly MemoryStream _stream;
        public TestFormFile(byte[] data, string fileName, string name = "file")
        {
            _stream = new MemoryStream(data);
            FileName = fileName;
            Name = name;
            Length = _stream.Length;
            Headers = new HeaderDictionary();
        }

        public string ContentType { get; set; } = "text/csv";
        public string ContentDisposition { get; set; } = string.Empty;
        public IHeaderDictionary Headers { get; }
        public long Length { get; }
        public string Name { get; }
        public string FileName { get; }

        public Stream OpenReadStream() => new MemoryStream(_stream.ToArray());

        public void CopyTo(Stream target) => OpenReadStream().CopyTo(target);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            OpenReadStream().CopyToAsync(target, cancellationToken);
    }
}
