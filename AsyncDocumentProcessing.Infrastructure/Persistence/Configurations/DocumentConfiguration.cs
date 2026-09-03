using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncDocumentProcessing.Domain.Entities;

namespace AsyncDocumentProcessing.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration
    : IEntityTypeConfiguration<Document>
{
    public void Configure(
        EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.DocumentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.BatchId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SourceSystem)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Sha256Hash)
            .HasMaxLength(64);

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.BatchId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => new
        {
            x.BatchId,
            x.Status
        });
    }
}
