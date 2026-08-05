using System.Text;
using System.IO;

namespace ApplyVault.Api.Infrastructure;

public interface IInterviewPrepDebugFileTraceLogger
{
    void Log(Guid sessionId, string line);
}

public sealed class InterviewPrepDebugFileTraceLogger : IInterviewPrepDebugFileTraceLogger
{
    private readonly string _baseDir;
    private readonly object _gate = new();

    public InterviewPrepDebugFileTraceLogger()
    {
        // Writes into the server working folder so you can find it next to the app.
        _baseDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "interview-prep-traces");
    }

    public void Log(Guid sessionId, string line)
    {
        try
        {
            Directory.CreateDirectory(_baseDir);
            var filePath = Path.Combine(_baseDir, $"{sessionId}.txt");

            var safe = line.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            var stamped = $"{DateTimeOffset.UtcNow:O} {safe}";

            lock (_gate)
            {
                File.AppendAllText(filePath, stamped + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Never break interview prep flow because of debug logging.
        }
    }
}

