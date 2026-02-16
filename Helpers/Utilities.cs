
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReceiptPDFBuilders.Helpers;

class Utilities
{
    public static JsonSerializerOptions GetSerializerOptions()
    {
        var opts = new JsonSerializerOptions 
        { 
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return opts;
    }
}