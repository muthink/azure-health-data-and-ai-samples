﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal class FhirDelegatingHandler : DelegatingHandler
{
    private readonly FhirTokenCredential _tokenCredential;

    public FhirDelegatingHandler(FhirTokenCredential tokenCredential)
        => _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // In-memory caching is available and enabled by default for most TokenCredential types
        AccessToken token = _tokenCredential.GetToken(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return base.SendAsync(request, cancellationToken);
    }
}
