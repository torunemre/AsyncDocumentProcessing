using AsyncDocumentProcessing.Domain.Enums;
using System;


namespace AsyncDocumentProcessing.Application.DTOs
{
    public class DocumentResponse
    {
        public Guid Id { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string DocumentType { get; set; } = string.Empty;

        public string BatchId { get; set; } = string.Empty;

        public string SourceSystem { get; set; } = string.Empty;

        public DocumentStatus Status { get; set; }

        public int? PageCount { get; set; }

        public int? WordCount { get; set; }

        public string? Sha256Hash { get; set; }

        public string? ExtractedText { get; set; }

        public string? ErrorMessage { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ProcessingStartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
