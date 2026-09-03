using AsyncDocumentProcessing.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace AsyncDocumentProcessing.Infrastructure.Queue
{
    public class DocumentQueue : IDocumentQueue
    {
        private readonly Channel<Guid> _channel;

        public DocumentQueue(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _channel = Channel.CreateBounded<Guid>(
                new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });
        }

        public async ValueTask EnqueueAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            await _channel.Writer.WriteAsync(
                documentId,
                cancellationToken);
        }

        public async ValueTask<Guid> DequeueAsync(
            CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(
                cancellationToken);
        }
    }
}
