﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Xaml;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.LanguageServer.Xaml;

[ExportCSharpVisualBasicLspServiceFactory(typeof(OnInitializedService)), Shared]
internal class OnInitializedServiceFactory : ILspServiceFactory
{
    private readonly ICapabilityRegistrationsProvider _registrationsProvider;

    [ImportingConstructor]
    [Obsolete(StringConstants.ImportingConstructorMessage, error: true)]
    public OnInitializedServiceFactory(ICapabilityRegistrationsProvider registrationsProvider)
    {
        _registrationsProvider = registrationsProvider;
    }

    public ILspService CreateILspService(LspServices lspServices, WellKnownLspServerKinds serverKind)
    {
        return new OnInitializedService(_registrationsProvider);
    }

    private class OnInitializedService : ILspService, IOnInitialized
    {
        private readonly ICapabilityRegistrationsProvider _registrationsProvider;

        public OnInitializedService(ICapabilityRegistrationsProvider registrationsProvider)
        {
            _registrationsProvider = registrationsProvider;
        }

        public async Task OnInitializedAsync(ClientCapabilities clientCapabilities, RequestContext context, CancellationToken cancellationToken)
        {
            var clientLanguageServerManager = context.GetRequiredLspService<IClientLanguageServerManager>();
            var registrations = _registrationsProvider.GetRegistrations(clientCapabilities);

            await RegisterCapabilityAsync(clientLanguageServerManager, registrations, cancellationToken).ConfigureAwait(false);

            // Call LSP method client/registerCapability
            ValueTask RegisterCapabilityAsync(IClientLanguageServerManager clientLanguageServerManager, ImmutableArray<Registration> registrations, CancellationToken cancellationToken)
            {
                var registrationParams = new RegistrationParams()
                {
                    Registrations = registrations.ToArray()
                };

                return clientLanguageServerManager.SendRequestAsync("client/registerCapability", registrationParams, cancellationToken);
            }
        }
    }
}
