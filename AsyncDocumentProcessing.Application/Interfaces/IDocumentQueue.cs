using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Interfaces
{
    public interface IDocumentQueue
    {
        ValueTask EnqueueAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);

        ValueTask<Guid> DequeueAsync(
            CancellationToken cancellationToken = default);
    }
}
