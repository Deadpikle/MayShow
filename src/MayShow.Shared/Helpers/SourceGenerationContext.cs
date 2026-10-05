using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MayShow.Models;

namespace MayShow.Helpers;

[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(ReportFile))]
[JsonSerializable(typeof(PDFReport))]
[JsonSerializable(typeof(ObservableCollection<ReportFile>))]
internal partial class SourceGenerationContext : JsonSerializerContext { }