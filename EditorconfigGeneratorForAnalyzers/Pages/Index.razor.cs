using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EditorconfigGeneratorForAnalyzers.Pages
{
    public partial class Index
    {
        private string _text;
        private string _error;
        private bool _includeTitle = true;
        private bool _includeHelpLink = true;

        private async Task OnInputFileChange(InputFileChangeEventArgs e)
        {
            _error = null;

            var sb = new StringBuilder();
            foreach (var file in e.GetMultipleFiles(maximumFileCount: 10))
            {
                if (!file.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var stream = file.OpenReadStream(maxAllowedSize: int.MaxValue);
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

                        if (_includeHelpLink || _includeTitle)
                        {
                            sb.Append(" # ");
                            if (_includeTitle)
                            {
                                sb.Append(diagnostic.Title);
                            }

                            if (_includeHelpLink)
                            {
                                sb.Append(" (").Append(diagnostic.HelpLinkUri).Append(')');
                            }
                        }

                        sb.AppendLine();
                    }
                }
                catch (Exception ex)
                {
                    _error += ex.ToString();
                }
            }

            _text = sb.ToString();
        }
    }
}
