using System.Text;

namespace Sentra.Infrastructure.Crawling;

public class RawTextExtractor
{
    public string TryExtract(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch
        {
            try
            {
                return File.ReadAllText(filePath, Encoding.GetEncoding(1251));
            }
            catch
            {
                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    var sb = new StringBuilder();
                    foreach (var b in bytes)
                    {
                        char c = (char)b;
                        if (!char.IsControl(c) || c == '\n' || c == '\r')
                            sb.Append(c);
                    }
                    return sb.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}