﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.CodeAnalysis.CodeFixes
{
    /// <summary>
    /// Use this attribute to declare a <see cref="CodeFixProvider"/> implementation so that it can be discovered by the host.
    /// </summary>
    [MetadataAttribute]
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ExportCodeFixProviderAttribute : ExportAttribute
    {
        private static readonly TextDocumentKind[] s_defaultDocumentKinds = new[] { TextDocumentKind.Document };

        /// <summary>
        /// Optional name of the <see cref="CodeFixProvider"/>.  
        /// </summary>
        [DisallowNull]
        public string? Name { get; set; }

        /// <summary>
        /// The source languages this provider can provide fixes for.  See <see cref="LanguageNames"/>.
        /// </summary>
        public string[] Languages { get; }

        /// <summary>
        /// The document kinds for which this provider can provide code fixes. See <see cref="TextDocumentKind"/>.
        /// By default, the provider supports code fixes only for source documents, <see cref="TextDocumentKind.Document"/>.
        /// </summary>
        public TextDocumentKind[] DocumentKinds { get; set; }

        /// <summary>
        /// The document extensions for which this provider can provide code fixes.
        /// Each extension string must include the leading period, for example, ".txt", ".xaml", ".editorconfig", etc.
        /// By default, this value is null and the document extension is not considered to determine applicability of code fixes.
        /// </summary>
        public string[]? DocumentExtensions { get; set; }

        /// <summary>
        /// Attribute constructor used to specify automatic application of a code fix provider.
        /// </summary>
        /// <param name="firstLanguage">One language to which the code fix provider applies.</param>
        /// <param name="additionalLanguages">Additional languages to which the code fix provider applies. See <see cref="LanguageNames"/>.</param>
        public ExportCodeFixProviderAttribute(
            string firstLanguage,
            params string[] additionalLanguages)
            : base(typeof(CodeFixProvider))
        {
            if (additionalLanguages == null)
            {
                throw new ArgumentNullException(nameof(additionalLanguages));
            }

            var languages = new string[additionalLanguages.Length + 1];
            languages[0] = firstLanguage ?? throw new ArgumentNullException(nameof(firstLanguage));
            for (var index = 0; index < additionalLanguages.Length; index++)
            {
                languages[index + 1] = additionalLanguages[index];
            }

            this.Languages = languages;
            this.DocumentKinds = s_defaultDocumentKinds;
            this.DocumentExtensions = null;
        }
    }
}
