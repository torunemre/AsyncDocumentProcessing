using AsyncDocumentProcessing.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Domain.Enums;

namespace AsyncDocumentProcessing.Application.DTOs
{
    public class BatchDocumentResponse
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string DocumentType { get; set; } = string.Empty;

        public DocumentStatus Status { get; set; }

        public int? PageCount { get; set; }

        public int? WordCount { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
