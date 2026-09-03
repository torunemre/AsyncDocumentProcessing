using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Application.DTOs;
using FluentValidation;


namespace AsyncDocumentProcessing.Application.Validators
{
    public class UploadDocumentRequestValidator
    : AbstractValidator<UploadDocumentRequest>
    {
        public UploadDocumentRequestValidator()
        {
            RuleFor(x => x.DocumentType)
                .NotEmpty()
                .WithMessage("DocumentType zorunludur.")
                .MaximumLength(100)
                .WithMessage("DocumentType en fazla 100 karakter olabilir.");

            RuleFor(x => x.BatchId)
                .NotEmpty()
                .WithMessage("BatchId zorunludur.")
                .MaximumLength(100)
                .WithMessage("BatchId en fazla 100 karakter olabilir.");

            RuleFor(x => x.SourceSystem)
                .NotEmpty()
                .WithMessage("SourceSystem zorunludur.")
                .MaximumLength(100)
                .WithMessage("SourceSystem en fazla 100 karakter olabilir.");
        }
    }
}
