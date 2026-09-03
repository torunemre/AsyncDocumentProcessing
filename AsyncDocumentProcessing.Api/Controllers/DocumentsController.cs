using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using AsyncDocumentProcessing.Application.DTOs;
using AsyncDocumentProcessing.Application.Interfaces;

namespace AsyncDocumentProcessing.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _documentService;

        public DocumentsController(IDocumentService documentService)
        {
            _documentService = documentService;
        }


        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UploadDocumentResponse>> Upload(
    [FromForm] UploadDocumentRequest request,
    IFormFile file,
    CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest("Dosya zorunludur.");
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
