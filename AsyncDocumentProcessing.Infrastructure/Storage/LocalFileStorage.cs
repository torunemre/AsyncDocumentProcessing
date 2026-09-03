using AsyncDocumentProcessing.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Infrastructure.Storage
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly string _rootPath;

        public LocalFileStorage(string rootPath)
        {
            _rootPath = rootPath;

            Directory.CreateDirectory(_rootPath);
        }

        public async Task<string> SaveAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var uniqueFileName =
                $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";

            var filePath = Path.Combine(
                _rootPath,
                uniqueFileName);

            await using var outputStream =
                new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

            await fileStream.CopyToAsync(
                outputStream,
                cancellationToken);

            return filePath;
        }

        public Task<Stream> OpenReadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            Stream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            return Task.FromResult(stream);
        }
    }
}
