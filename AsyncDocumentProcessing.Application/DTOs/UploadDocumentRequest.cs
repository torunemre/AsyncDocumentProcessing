using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.DTOs
{
    public class UploadDocumentRequest
    {
        public string? DocumentType { get; set; }
        public string? BatchId { get; set; }
        public string? SourceSystem { get; set; }
    }
}
