using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

public static class ProcessRunner
{
    public record ProcessResult(int ExitCode, string Output, string Error);

    public static async Task<ProcessResult> RunAsync(string fileName, string arguments, int timeoutMilliseconds = 30000)
    {
        using var process = new Process
        {
            StartInfo =
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, args) => outputBuilder.AppendLine(args.Data);
        process.ErrorDataReceived += (_, args) => errorBuilder.AppendLine(args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (await Task.WhenAny(process.WaitForExitAsync(), Task.Delay(timeoutMilliseconds)) != process.WaitForExitAsync())
        {
            process.Kill();
            throw new TimeoutException($"Process '{fileName} {arguments}' timed out after {timeoutMilliseconds / 1000} seconds.");
        }

        return new ProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}