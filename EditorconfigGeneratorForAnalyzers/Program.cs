using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EditorconfigGeneratorForAnalyzers
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("No dll provided");
                return;
            }

            var sb = new StringBuilder();
            foreach (var file in args)
            {
                try
                {
                    using var stream = File.OpenRead(file);
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();

                    var assembly = Assembly.Load(bytes);
                    var diagnosticAnalyzers = assembly.GetExportedTypes()
                            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type))
                            .ToList();

                    sb.AppendLine($"# {assembly.GetName().Name} {assembly.GetName().Version}");
                    sb.AppendLine();
                    foreach (var diagnostic in diagnosticAnalyzers.SelectMany(diagnosticAnalyzer => diagnosticAnalyzer.SupportedDiagnostics).OrderBy(diag => diag.Id))
                    {
                        var severity = diagnostic.IsEnabledByDefault ?
                                            diagnostic.DefaultSeverity switch
                                            {
                                                DiagnosticSeverity.Hidden => "silent    ",
                                                DiagnosticSeverity.Info => "suggestion",
                                                DiagnosticSeverity.Warning => "warning   ",
                                                DiagnosticSeverity.Error => "error     ",
                                                _ => throw new Exception($"{diagnostic.DefaultSeverity} not supported"),
                                            }
                                            : "none      ";

                        sb.Append("dotnet_diagnostic.").Append(diagnostic.Id).Append(".severity = ").Append(severity);
                        sb.Append(" # ");
                        sb.Append(diagnostic.Title);
                        sb.Append(" (").Append(diagnostic.HelpLinkUri).Append(')');
                    }

                    sb.AppendLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            Console.WriteLine(sb.ToString());
        }
    }
}
