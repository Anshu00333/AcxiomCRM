using System.Text;

namespace AcxiomCRM.Services
{
    public interface ICsvExportService
    {
        byte[] GenerateCsvBytes<T>(IEnumerable<string> headers, IEnumerable<T> rows, Func<T, IEnumerable<string?>> rowExtractor);
    }

    public class CsvExportService : ICsvExportService
    {
        public byte[] GenerateCsvBytes<T>(IEnumerable<string> headers, IEnumerable<T> rows, Func<T, IEnumerable<string?>> rowExtractor)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

            foreach (var item in rows)
            {
                var values = rowExtractor(item);
                sb.AppendLine(string.Join(",", values.Select(EscapeCsvField)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        private static string EscapeCsvField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return "\"\"";

            if (field.Contains("\"") || field.Contains(",") || field.Contains("\r") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }

            return $"\"{field}\"";
        }
    }
}
