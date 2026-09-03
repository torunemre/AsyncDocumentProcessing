using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IOcrService { Task<(string ExtractedText, int PageCount)> ProcessAsync(Stream fileStream, string fileExtension, CancellationToken cancellationToken = default); }
}
