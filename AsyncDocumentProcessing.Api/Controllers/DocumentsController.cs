using AsyncDocumentProcessing.Application.DTOs;
using AsyncDocumentProcessing.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using AsyncDocumentProcessing.Application.Options;

namespace AsyncDocumentProcessing.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IValidator<UploadDocumentRequest> _validator;
        private readonly DocumentProcessingOptions _processingOptions;

        public DocumentsController(
    IDocumentService documentService,
    IValidator<UploadDocumentRequest> validator,
    IOptions<DocumentProcessingOptions> processingOptions)
        {
            _documentService = documentService;
            _validator = validator;
            _processingOptions = processingOptions.Value;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UploadDocumentResponse>> Upload(
    [FromForm] UploadDocumentRequest request,
    IFormFile file,
    CancellationToken cancellationToken)
        {
            var validationResult = await _validator.ValidateAsync(
                request,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                return BadRequest(validationResult.Errors);
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest("Dosya zorunludur.");
            }

            var maxFileSize = _processingOptions.MaxFileSizeMb * 1024L * 1024L;

            if (file.Length > maxFileSize)
            {
                return BadRequest(
                    $"Dosya boyutu en fazla {_processingOptions.MaxFileSizeMb} MB olabilir.");
            }

            var extension = Path.GetExtension(file.FileName);

            if (string.IsNullOrWhiteSpace(extension) ||
                !_processingOptions.AllowedExtensions
                    .Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(
                    $"Desteklenmeyen dosya türü. İzin verilen türler: " +
                    $"{string.Join(", ", _processingOptions.AllowedExtensions)}");
            }

            await using var stream = file.OpenReadStream();

            var response = await _documentService.UploadAsync(
                request,
                stream,
                file.FileName,
                cancellationToken);

            return Accepted(response);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentResponse>> GetById(
    Guid id,
    CancellationToken cancellationToken)
        {
            var document = await _documentService.GetByIdAsync(
                id,
                cancellationToken);

            if (document is null)
            {
                return NotFound();
            }

            return Ok(document);
        }

        [HttpGet("batch/{batchId}")]
        public async Task<ActionResult<PagedResult<BatchDocumentResponse>>> GetByBatchId(
    string batchId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(batchId))
            {
                return BadRequest("BatchId zorunludur.");
            }

            if (batchId.Length > 100)
            {
                return BadRequest(
                    "BatchId en fazla 100 karakter olabilir.");
            }

            if (page < 1)
            {
                return BadRequest("Page 1 veya daha büyük olmalıdır.");
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest("PageSize 1 ile 100 arasında olmalıdır.");
            }

            var result = await _documentService.GetByBatchIdAsync(
                batchId,
                page,
                pageSize,
                cancellationToken);

            return Ok(result);
        }



    }
}
