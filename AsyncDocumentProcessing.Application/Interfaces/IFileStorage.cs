using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default);

        Task<Stream> OpenReadAsync(
            string filePath,
            CancellationToken cancellationToken = default);
    }
}
