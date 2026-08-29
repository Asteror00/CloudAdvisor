using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudAdvisor.Models.Domain;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CloudAdvisor.Services
{
    public class RoslynAnalysisService : IRoslynAnalysisService
    {
        private readonly ILogger<RoslynAnalysisService> _logger;

        public RoslynAnalysisService(ILogger<RoslynAnalysisService> logger)
        {
            _logger = logger;
        }

        public async Task<List<ExtractedFeature>> AnalyzeProjectFilesAsync(List<string> filePaths, Guid sessionId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting Roslyn analysis on {Count} files for session {SessionId}", filePaths.Count, sessionId);

            var extractedFeatures = new List<ExtractedFeature>();
            if (filePaths == null || filePaths.Count == 0)
            {
                return extractedFeatures;
            }

            try
            {
                var syntaxTrees = new List<(string FilePath, SyntaxTree Tree)>();

                // 1. Parse all files into syntax trees
                foreach (var path in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(path)) continue;

                    string content = await File.ReadAllTextAsync(path, cancellationToken);
                    var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: cancellationToken);
                    syntaxTrees.Add((path, tree));
                }

                // 2. Build ad-hoc compilation for semantic checking
                var references = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                    .Select(a => MetadataReference.CreateFromFile(a.Location))
                    .Cast<MetadataReference>()
                    .ToList();

                var compilation = CSharpCompilation.Create("AnalysisCompilation")
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddReferences(references)
                    .AddSyntaxTrees(syntaxTrees.Select(t => t.Tree));

                // 3. Walk each syntax tree
                foreach (var (path, tree) in syntaxTrees)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var semanticModel = compilation.GetSemanticModel(tree);
                    var walker = new ArchitectureWalker(path, semanticModel, sessionId);
                    walker.Visit(tree.GetRoot(cancellationToken));
                    extractedFeatures.AddRange(walker.Features);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing Roslyn code analysis for session {SessionId}", sessionId);
                throw;
            }

            _logger.LogInformation("Roslyn analysis completed. Extracted {Count} features.", extractedFeatures.Count);
            return extractedFeatures;
        }

        private class ArchitectureWalker : CSharpSyntaxWalker
        {
            private readonly string _filePath;
            private readonly SemanticModel _semanticModel;
            private readonly Guid _sessionId;
            private readonly string _relativeFilePath;

            public List<ExtractedFeature> Features { get; } = new List<ExtractedFeature>();

            public ArchitectureWalker(string filePath, SemanticModel semanticModel, Guid sessionId)
            {
                _filePath = filePath;
                _semanticModel = semanticModel;
                _sessionId = sessionId;
                
                // Get a clean relative path
                int idx = filePath.IndexOf("src", StringComparison.OrdinalIgnoreCase);
                _relativeFilePath = idx >= 0 ? filePath.Substring(idx) : Path.GetFileName(filePath);
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                var classSymbol = _semanticModel.GetDeclaredSymbol(node) as INamedTypeSymbol;
                string className = node.Identifier.Text;
                int lineNumber = node.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                if (classSymbol != null)
                {
                    // A. CONTROLLERS
                    bool inheritsControllerBase = InheritsFromOrImplements(classSymbol, "Microsoft.AspNetCore.Mvc.ControllerBase");
                    bool inheritsController = InheritsFromOrImplements(classSymbol, "Microsoft.AspNetCore.Mvc.Controller");
                    
                    if (inheritsControllerBase || inheritsController)
                    {
                        var routePrefix = "";
                        var routeAttr = node.AttributeLists
                            .SelectMany(al => al.Attributes)
                            .FirstOrDefault(a => a.Name.ToString().Contains("Route"));
                        if (routeAttr != null && routeAttr.ArgumentList != null && routeAttr.ArgumentList.Arguments.Count > 0)
                        {
                            routePrefix = routeAttr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
                        }

                        // Count public methods with HTTP attributes
                        int actionCount = 0;
                        var methodDeclarations = node.Members.OfType<MethodDeclarationSyntax>();
                        foreach (var method in methodDeclarations)
                        {
                            if (method.Modifiers.Any(m => m.Text == "public"))
                            {
                                var hasHttpAttr = method.AttributeLists
                                    .SelectMany(al => al.Attributes)
                                    .Any(a => a.Name.ToString().StartsWith("Http"));
                                if (hasHttpAttr)
                                {
                                    actionCount++;
                                }
                            }
                        }

                        var details = new
                        {
                            Inherits = inheritsController ? "Controller" : "ControllerBase",
                            RoutePrefix = routePrefix,
                            ActionCount = actionCount
                        };

                        Features.Add(new ExtractedFeature
                        {
                            SessionId = _sessionId,
                            FeatureType = FeatureType.Controller,
                            FeatureName = className,
                            FilePath = _relativeFilePath,
                            LineNumber = lineNumber,
                            Details = JsonSerializer.Serialize(details)
                        });
                    }

                    // B. DATABASE CONTEXTS (DbContext)
                    if (InheritsFromOrImplements(classSymbol, "Microsoft.EntityFrameworkCore.DbContext") || className.EndsWith("DbContext"))
                    {
                        // Extract all DbSet<T> properties
                        var dbSets = new List<string>();
                        var properties = node.Members.OfType<PropertyDeclarationSyntax>();
                        foreach (var prop in properties)
                        {
                            var propTypeStr = prop.Type.ToString();
                            if (propTypeStr.StartsWith("DbSet<") || propTypeStr.Contains(".DbSet<"))
                            {
                                dbSets.Add(prop.Identifier.Text);
                            }
                        }

                        // Check for OnModelCreating override
                        bool overridesOnModelCreating = node.Members.OfType<MethodDeclarationSyntax>()
                            .Any(m => m.Identifier.Text == "OnModelCreating" && m.Modifiers.Any(mod => mod.Text == "override"));

                        var details = new
                        {
                            DbSets = dbSets,
                            DbSetCount = dbSets.Count,
                            OverridesOnModelCreating = overridesOnModelCreating
                        };

                        Features.Add(new ExtractedFeature
                        {
                            SessionId = _sessionId,
                            FeatureType = FeatureType.DbContext,
                            FeatureName = className,
                            FilePath = _relativeFilePath,
                            LineNumber = lineNumber,
                            Details = JsonSerializer.Serialize(details)
                        });
                    }

                    // C. AUTHENTICATION HANDLERS
                    bool isAuthHandler = InheritsFromOrImplements(classSymbol, "Microsoft.AspNetCore.Authorization.IAuthorizationHandler") ||
                                         InheritsFromOrImplements(classSymbol, "Microsoft.AspNetCore.Authentication.IAuthenticationHandler");
                    if (isAuthHandler)
                    {
                        var details = new { Role = "AuthHandler" };
                        Features.Add(new ExtractedFeature
                        {
                            SessionId = _sessionId,
                            FeatureType = FeatureType.AuthMiddleware,
                            FeatureName = className,
                            FilePath = _relativeFilePath,
                            LineNumber = lineNumber,
                            Details = JsonSerializer.Serialize(details)
                        });
                    }

                    // E. BACKGROUND SERVICES
                    bool isBackgroundService = InheritsFromOrImplements(classSymbol, "Microsoft.Extensions.Hosting.BackgroundService") ||
                                               InheritsFromOrImplements(classSymbol, "Microsoft.Extensions.Hosting.IHostedService") ||
                                               className.EndsWith("BackgroundService");
                    if (isBackgroundService)
                    {
                        var details = new
                        {
                            ServiceType = InheritsFromOrImplements(classSymbol, "Microsoft.Extensions.Hosting.BackgroundService") ? "BackgroundService" : "IHostedService",
                            SchedulePattern = "Continuous/Async"
                        };

                        Features.Add(new ExtractedFeature
                        {
                            SessionId = _sessionId,
                            FeatureType = FeatureType.BackgroundService,
                            FeatureName = className,
                            FilePath = _relativeFilePath,
                            LineNumber = lineNumber,
                            Details = JsonSerializer.Serialize(details)
                        });
                    }
                }

                // Check class-level attributes for auth
                var classAuth = node.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => a.Name.ToString() == "Authorize" || a.Name.ToString().StartsWith("Authorize("));
                if (classAuth != null)
                {
                    var details = new
                    {
                        Scope = "Class",
                        AttributeText = classAuth.ToString()
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.AuthMiddleware,
                        FeatureName = $"{className} [Authorize]",
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                base.VisitClassDeclaration(node);
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                int lineNumber = node.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                string methodName = node.Identifier.Text;

                // F. API ENDPOINTS & HTTP METHODS
                var httpAttr = node.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => a.Name.ToString().StartsWith("Http"));

                if (httpAttr != null)
                {
                    string httpMethod = httpAttr.Name.ToString().Replace("Http", "").ToUpper();
                    string route = "";
                    if (httpAttr.ArgumentList != null && httpAttr.ArgumentList.Arguments.Count > 0)
                    {
                        route = httpAttr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
                    }

                    // Check if it accepts complex body parameters
                    bool acceptsComplexBody = node.ParameterList.Parameters
                        .Any(p => p.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == "FromBody") || 
                                  (!p.Type.ToString().StartsWith("string") && !p.Type.ToString().StartsWith("int") && !p.Type.ToString().StartsWith("Guid")));

                    var details = new
                    {
                        HttpMethod = httpMethod,
                        Route = route,
                        AcceptsComplexBody = acceptsComplexBody
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.ApiEndpoint,
                        FeatureName = methodName,
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                // D. FILE HANDLING (IFormFile parameter check)
                bool hasFileParam = node.ParameterList.Parameters
                    .Any(p => p.Type.ToString().Contains("IFormFile"));
                if (hasFileParam)
                {
                    var details = new
                    {
                        Method = methodName,
                        Type = "IFormFile Upload"
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.FileHandling,
                        FeatureName = $"{methodName} (IFormFile)",
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                // Check method-level Auth attribute
                var methodAuth = node.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(a => a.Name.ToString() == "Authorize" || a.Name.ToString().StartsWith("Authorize("));
                if (methodAuth != null)
                {
                    var details = new
                    {
                        Scope = "Method",
                        AttributeText = methodAuth.ToString()
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.AuthMiddleware,
                        FeatureName = $"{methodName} [Authorize]",
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                base.VisitMethodDeclaration(node);
            }

            public override void VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                string expr = node.Expression.ToString();
                int lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                // C. AUTH MIDDLEWARE CALLS
                if (expr.EndsWith("UseAuthentication") || expr.EndsWith("UseAuthorization") || expr.Contains("AddJwtBearer"))
                {
                    var details = new
                    {
                        MethodCalled = expr,
                        Context = "Configuration Pipeline"
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.AuthMiddleware,
                        FeatureName = expr.Split('.').Last(),
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                // D. DIRECT SYSTEM.IO FILE HANDLING CALLS
                if (expr.StartsWith("File.") || expr.StartsWith("System.IO.File.") || 
                    expr.StartsWith("Directory.") || expr.StartsWith("System.IO.Directory.") ||
                    expr.StartsWith("Stream.") || expr.StartsWith("System.IO.Stream."))
                {
                    var details = new
                    {
                        IOAction = expr,
                        ClassCalled = expr.Split('.').First()
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.FileHandling,
                        FeatureName = expr,
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                // E. HANGFIRE BACKGROUND JOB
                if (expr.Contains("BackgroundJob") || expr.Contains("RecurringJob"))
                {
                    var details = new
                    {
                        JobLib = "Hangfire",
                        Call = expr
                    };

                    Features.Add(new ExtractedFeature
                    {
                        SessionId = _sessionId,
                        FeatureType = FeatureType.BackgroundService,
                        FeatureName = expr.Split('.').Last(),
                        FilePath = _relativeFilePath,
                        LineNumber = lineNumber,
                        Details = JsonSerializer.Serialize(details)
                    });
                }

                base.VisitInvocationExpression(node);
            }

            private bool InheritsFromOrImplements(INamedTypeSymbol symbol, string targetTypeName)
            {
                if (symbol == null) return false;

                // Check base classes
                var current = symbol.BaseType;
                while (current != null)
                {
                    if (current.ToDisplayString() == targetTypeName || current.Name == targetTypeName)
                        return true;
                    current = current.BaseType;
                }

                // Check interfaces
                foreach (var iface in symbol.AllInterfaces)
                {
                    if (iface.ToDisplayString() == targetTypeName || iface.Name == targetTypeName)
                        return true;
                }

                return false;
            }
        }
    }
}
