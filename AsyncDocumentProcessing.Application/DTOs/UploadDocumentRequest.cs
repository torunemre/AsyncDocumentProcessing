using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.DTOs
{
    public class UploadDocumentRequest
    {
        public string DocumentType { get; set; } = string.Empty;

        public string BatchId { get; set; } = string.Empty;

        public string SourceSystem { get; set; } = string.Empty;
    }
}
