﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.AddPackage;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeFixes.Configuration.ConfigureSeverity;
using Microsoft.CodeAnalysis.CodeFixes.Suppression;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Copilot;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Packaging;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace Microsoft.CodeAnalysis.CodeAnalysisSuggestions;

[ExportCodeRefactoringProvider(LanguageNames.CSharp), Shared]
internal sealed class CodeAnalysisSuggestionsCodeRefactoringProvider
    : CodeRefactoringProvider
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public CodeAnalysisSuggestionsCodeRefactoringProvider()
    {
    }

    protected override CodeActionRequestPriority ComputeRequestPriority()
        => CodeActionRequestPriority.Low;

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var (document, span, cancellationToken) = context;
        var configService = document.Project.Solution.Services.GetRequiredService<ICopilotConfigService>();

        using var _ = ArrayBuilder<CodeAction>.GetInstance(out var actionsBuilder);

        var ruleConfigData = await configService.TryGetCodeAnalysisSuggestionsConfigDataAsync(document.Project, cancellationToken).ConfigureAwait(false);
        if (!ruleConfigData.IsEmpty)
        {
            var codeFixService = document.Project.Solution.Services.ExportProvider.GetExports<ICodeFixService>().FirstOrDefault().Value;
            var actions = await GetCodeAnalysisSuggestionActionsAsync(ruleConfigData, document, codeFixService, cancellationToken).ConfigureAwait(false);
            actionsBuilder.AddRange(actions);
        }

        var workspaceServices = document.Project.Solution.Services;
        var installerService = workspaceServices.GetService<IPackageInstallerService>();
        if (installerService is not null)
        {
            var packageConfigData = await configService.TryGetCodeAnalysisPackageSuggestionConfigDataAsync(document.Project, cancellationToken).ConfigureAwait(false);
            if (packageConfigData is not null)
            {
                actionsBuilder.Add(GetCodeAnalysisPackageSuggestionAction(packageConfigData, document, installerService));
            }
        }

        if (actionsBuilder.Count > 0)
        {
            context.RegisterRefactoring(
                CodeAction.Create(
                    FeaturesResources.Copilot_code_analysis_suggestions,
                    actionsBuilder.ToImmutable(),
                    isInlinable: false,
                    CodeActionPriority.Low),
                span);
        }
    }

    private static CodeAction GetCodeAnalysisPackageSuggestionAction(
        string packageName,
        Document document, IPackageInstallerService installerService)
    {
        return new InstallPackageParentCodeAction(installerService, source: null, packageName, includePrerelease: true, document);
    }

    private static async Task<ImmutableArray<CodeAction>> GetCodeAnalysisSuggestionActionsAsync(
        ImmutableArray<(string, ImmutableDictionary<string, ImmutableArray<DiagnosticData>>)> configData,
        Document document,
        ICodeFixService? codeFixService,
        CancellationToken cancellationToken)
    {
        var root = await document.GetRequiredSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var location = root.GetLocation();

        using var _1 = ArrayBuilder<CodeAction>.GetInstance(out var actionsBuilder);
        using var _2 = ArrayBuilder<CodeAction>.GetInstance(out var nestedActionsBuilder);
        using var _3 = ArrayBuilder<CodeAction>.GetInstance(out var nestedNestedActionsBuilder);
        foreach (var (category, diagnosticsById) in configData)
        {
            foreach (var kvp in diagnosticsById)
            {
                var id = kvp.Key;
                var diagnostics = kvp.Value;
                Debug.Assert(diagnostics.All(d => string.Equals(d.Category, category, StringComparison.OrdinalIgnoreCase)));

                var (diagnosticData, documentForFix) = GetPreferredDiagnosticAndDocument(diagnostics, document);
                var diagnostic = await diagnosticData.ToDiagnosticAsync(document.Project, cancellationToken).ConfigureAwait(false);
                if (SuppressionHelpers.IsNotConfigurableDiagnostic(diagnostic))
                    continue;

                if (documentForFix != null && codeFixService != null)
                {
                    var codeFixCollection = await codeFixService.GetDocumentFixAllForIdInSpanAsync(documentForFix, diagnostic.Location.SourceSpan, id, CodeActionOptions.DefaultProvider, cancellationToken).ConfigureAwait(false);
                    if (codeFixCollection != null)
                    {
                        nestedNestedActionsBuilder.AddRange(codeFixCollection.Fixes.Select(f => f.Action));
                    }
                    else
                    {
                        // This diagnostic has no code fix, or fix application failed.
                        // TODO: Can we add some code action such that it shows a preview of the diagnostic span with squiggle?
                    }
                }
                else
                {
                    // This is either a project diagnostic with no location OR a dummy diagnostic created from descriptor without background analysis.
                    // Append this document's span as it location so we can show configure severity code fix for it.
                    diagnostic = Diagnostic.Create(diagnostic.Descriptor, location);
                }

                var totalInInCurrentProject = diagnostics.Where(dd => dd.ProjectId == document.Project.Id).Count();
                var idCountText = totalInInCurrentProject > 0 ? $" (Found {totalInInCurrentProject} instances in current project)" : string.Empty;

                var nestedNestedAction = ConfigureSeverityLevelCodeFixProvider.CreateSeverityConfigurationCodeAction(diagnostic, document.Project);
                nestedNestedActionsBuilder.Add(nestedNestedAction);

                // TODO: Add actions to ignore all the rules here by adding them to .editorconfig and set to None or Silent.
                // Further, None could be used to filter out rules to suggest as they indicate user is aware of them and explicitly disabled them.

                var title = $"{diagnostic.Id}: {diagnostic.Descriptor.Title}{idCountText}";
                var nestedAction = CodeAction.Create(title, nestedNestedActionsBuilder.ToImmutableAndClear(), isInlinable: false);
                nestedActionsBuilder.Add(nestedAction);
            }

            if (nestedActionsBuilder.Count == 0)
                continue;

            // Add code action to Configure severity for the entire 'Category'
            var categoryConfigurationAction = ConfigureSeverityLevelCodeFixProvider.CreateBulkSeverityConfigurationCodeAction(category, document.Project);
            nestedActionsBuilder.Add(categoryConfigurationAction);

            var totalInThisCategoryInCurrentProject = diagnosticsById.SelectMany(ds => ds.Value).Where(dd => dd.ProjectId == document.Project.Id).Count();
            var categoryCountText = totalInThisCategoryInCurrentProject > 0 ? $" (Found {totalInThisCategoryInCurrentProject} instances in current project)" : string.Empty;
            var action = CodeAction.Create($"'{category}' improvements{categoryCountText}", nestedActionsBuilder.ToImmutableAndClear(), isInlinable: false);
            actionsBuilder.Add(action);
        }

        return actionsBuilder.ToImmutable();

        static (DiagnosticData, Document?) GetPreferredDiagnosticAndDocument(ImmutableArray<DiagnosticData> diagnostics, Document document)
        {
            (DiagnosticData diagnostic, DocumentId? documentId)? preferredDiagnosticAndDocumentId = null;
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.DocumentId == document.Id)
                {
                    return (diagnostic, document);
                }
                else if (!preferredDiagnosticAndDocumentId.HasValue &&
                    diagnostic.DocumentId?.ProjectId == document.Project.Id)
                {
                    preferredDiagnosticAndDocumentId = (diagnostic, diagnostic.DocumentId);
                }
            }

            if (preferredDiagnosticAndDocumentId.HasValue)
            {
                return (preferredDiagnosticAndDocumentId.Value.diagnostic,
                    document.Project.GetDocument(preferredDiagnosticAndDocumentId.Value.documentId));
            }

            return (diagnostics.First(), null);
        }
    }
}
