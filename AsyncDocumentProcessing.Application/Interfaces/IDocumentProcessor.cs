using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IDocumentProcessor
    {
        Task ProcessAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);
    }
}
