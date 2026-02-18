using System.Text.Json.Serialization;
using ReceiptPDFBuilder.Models;

namespace ReceiptPDFBuilder.Helpers;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(ReportFile))]
[JsonSerializable(typeof(PDFReport))]
internal partial class SourceGenerationContext : JsonSerializerContext { }