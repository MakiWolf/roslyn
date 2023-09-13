﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;

namespace Microsoft.CodeAnalysis.Copilot;

internal interface ICopilotConfigService : IWorkspaceService
{
    public Task<ImmutableDictionary<string, string>> GetCopilotConfigAsync(Project project, CancellationToken cancellationToken);
    public Task<ImmutableArray<string>?> TryGetCopilotConfigPromptAsync(string feature, Project project, CancellationToken cancellationToken);
    public Task<ImmutableArray<(string, ImmutableArray<string>)>> ParsePromptResponseAsync(ImmutableArray<string> response, string feature, Project project, CancellationToken cancellationToken);
}

[ExportWorkspaceService(typeof(ICopilotConfigService), ServiceLayer.Default), Shared]
internal sealed class DefaultCopilotConfigService : ICopilotConfigService
{
    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public DefaultCopilotConfigService()
    {
    }

    public Task<ImmutableArray<string>?> TryGetCopilotConfigPromptAsync(string feature, Project project, CancellationToken cancellationToken)
        => Task.FromResult<ImmutableArray<string>?>(null);

    public Task<ImmutableArray<(string, ImmutableArray<string>)>> ParsePromptResponseAsync(ImmutableArray<string> response, string feature, Project project, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task<ImmutableDictionary<string, string>> GetCopilotConfigAsync(Project project, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
