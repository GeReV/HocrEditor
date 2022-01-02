using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace HocrEditor.Services
{
    public static class ProcessRunner
    {
        public static Task<string> Run(string filename, string? arguments) => Task.Run(async () =>
        {
            using Process p = new Process
            {
                StartInfo =
                {
                    FileName = filename,
                    Arguments = arguments ?? "",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                }
            };

            var sb = new StringBuilder();

            p.OutputDataReceived += (_, args) =>
            {
                if (args.Data != null)
                {
                    sb.AppendLine(args.Data);
                }
            };

            p.Start();
            p.BeginOutputReadLine();

            await p.WaitForExitAsync();

            return sb.ToString();
        });
    }
}
