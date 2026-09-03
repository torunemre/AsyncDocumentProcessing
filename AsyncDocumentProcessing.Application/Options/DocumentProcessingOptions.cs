using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncDocumentProcessing.Application.Options
{
    public class DocumentProcessingOptions
    {
        public int MaxConcurrency { get; set; } = 3;

        public int MaxRetryCount { get; set; } = 3;
    }
}
