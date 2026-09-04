
using AsyncDocumentProcessing.Application.Interfaces;
using Docnet.Core;
using Docnet.Core.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using System.Runtime.Versioning;

namespace AsyncDocumentProcessing.Infrastructure.OCR
{

    [SupportedOSPlatform("windows")]
    public class TesseractOcrService : IOcrService
    {
        private readonly string _tessDataPath;

        public TesseractOcrService()
        {
            _tessDataPath = Path.Combine(
                AppContext.BaseDirectory,
                "tessdata");
        }

        public async Task<(string ExtractedText, int PageCount)> ProcessAsync(
            Stream fileStream,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            if (fileStream is null)
            {
                throw new ArgumentNullException(nameof(fileStream));
            }

            if (!fileStream.CanSeek)
            {
                throw new InvalidOperationException(
                    "OCR işlemi için dosya stream'i seekable olmalıdır.");
            }

            if (!Directory.Exists(_tessDataPath))
            {
                throw new DirectoryNotFoundException(
                    $"Tesseract tessdata klasörü bulunamadı: {_tessDataPath}");
            }

            var normalizedExtension = fileExtension?
    .Trim()
    .ToLowerInvariant() ?? string.Empty;

            var supportedExtensions = new[]
            {
                ".pdf",
                ".jpg",
                ".jpeg",
                ".png"
            };

            if (!Array.Exists(
                    supportedExtensions,
                    extension => extension == fileExtension))
            {
                throw new NotSupportedException(
                    $"Desteklenmeyen dosya formatı: {fileExtension}. " +
                    "Desteklenen formatlar: PDF, JPG, JPEG, PNG.");
            }

            using var memoryStream = new MemoryStream();

            await fileStream.CopyToAsync(
                memoryStream,
                cancellationToken);

            var fileBytes = memoryStream.ToArray();

            cancellationToken.ThrowIfCancellationRequested();

            using var engine = new TesseractEngine(
                _tessDataPath,
                "tur+eng",
                EngineMode.Default);

            if (fileExtension == ".pdf")
            {
                return ProcessPdf(
                    fileBytes,
                    engine,
                    cancellationToken);
            }

            return ProcessImage(
                fileBytes,
                engine,
                cancellationToken);
        }

        private static (string ExtractedText, int PageCount) ProcessPdf(
            byte[] pdfBytes,
            TesseractEngine engine,
            CancellationToken cancellationToken)
        {
            using var library = DocLib.Instance;

            using var docReader = library.GetDocReader(
                pdfBytes,
                new PageDimensions(1654, 2339));

            var pageCount = docReader.GetPageCount();

            var extractedText = new List<string>();

            for (var pageIndex = 0;
                 pageIndex < pageCount;
                 pageIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var pageReader =
                    docReader.GetPageReader(pageIndex);

                var rawBytes = pageReader.GetImage();

                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();

                using var bitmap = new Bitmap(
                    width,
                    height,
                    PixelFormat.Format32bppArgb);

                var bitmapData = bitmap.LockBits(
                    new Rectangle(
                        0,
                        0,
                        width,
                        height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        rawBytes,
                        0,
                        bitmapData.Scan0,
                        rawBytes.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                using var pix =
                    PixConverter.ToPix(bitmap);

                using var page =
                    engine.Process(pix);

                var text = page.GetText();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    extractedText.Add(
                        text.Trim());
                }
            }

            return (
                string.Join(
                    Environment.NewLine +
                    Environment.NewLine,
                    extractedText),
                pageCount);
        }

        private static (string ExtractedText, int PageCount) ProcessImage(
            byte[] imageBytes,
            TesseractEngine engine,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var imageStream =
                new MemoryStream(imageBytes);

            using var bitmap =
                new Bitmap(imageStream);

            using var pix =
                PixConverter.ToPix(bitmap);

            using var page =
                engine.Process(pix);

            var text = page.GetText();

            return (
                text?.Trim() ?? string.Empty,
                1);
        }
    }
}

