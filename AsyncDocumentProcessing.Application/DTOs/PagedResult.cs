using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.DTOs
{
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; init; } =
            Array.Empty<T>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int TotalPages =>
            PageSize == 0
                ? 0
                : (int)Math.Ceiling(
                    TotalCount / (double)PageSize);
    }
}
