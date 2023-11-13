// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Azure.Health.Data.Dicom.Cast.Fhir.Transactions.Contexts;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class EndpointTransactionHandler
{
    private readonly FhirClient _client;
    private readonly Uri _dicomServiceUri;
    private readonly string _endpointName;
    private readonly ILogger<EndpointTransactionHandler> _logger;

    public EndpointTransactionHandler(
        FhirClient client,
        IOptionsSnapshot<DicomWebClientOptions> options,
        ILogger<EndpointTransactionHandler> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DicomWebClientOptions dicomOptions = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _dicomServiceUri = dicomOptions.ServiceUri;
        _endpointName = $"Azure Dicom Service {dicomOptions.Workspace}/{dicomOptions.Service} WADO-RS Endpoint";
    }

    public async ValueTask<TransactionContext> ConfigureAsync(TransactionBuilder builder, DicomDataset dataset, CancellationToken cancellationToken)
    {
        if (builder is null)
            throw new ArgumentNullException(nameof(builder));

        if (dataset is null)
            throw new ArgumentNullException(nameof(dataset));

        // Whether we're updating and deleting DICOM SOP instances, ensure the endpoint is present in FHIR
        Endpoint? endpoint = await GetEndpointOrDefaultAsync(cancellationToken);
        if (endpoint is null)
        {
            endpoint = new()
            {
                Address = _dicomServiceUri.AbsoluteUri,
                ConnectionType = new List<CodeableConcept>
                {
                    // "dicom-wado-rs" is a well-defined Endpoint Connection Type code
                    new CodeableConcept("http://terminology.hl7.org/CodeSystem/endpoint-connection-type", "dicom-wado-rs")
                },
                Id = $"urn:uuid:{Guid.NewGuid()}",
                Name = _endpointName,
                Payload = new List<Endpoint.PayloadComponent>
                {
                    new Endpoint.PayloadComponent()
                    {
                        // "DICOM WADO-RS" is a well-defined Endpoint Connection Type display name
                        MimeType = new List<string> { "application/dicom" },
                        Type = new List<CodeableConcept> { new CodeableConcept(string.Empty, string.Empty, "DICOM WADO-RS") },
                    }
                },
                Status = Endpoint.EndpointStatus.Active,
            };

            builder = builder.Create(endpoint);
        }
        else if (!string.Equals(endpoint.Address, _dicomServiceUri.AbsoluteUri, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Exceptions.EndpointMismatch,
                    _dicomServiceUri,
                    _endpointName,
                    endpoint.Address));
        }

        return new TransactionContext(builder, endpoint);
    }

    private async ValueTask<Endpoint?> GetEndpointOrDefaultAsync(CancellationToken cancellationToken)
    {
        SearchParams searchParams = new SearchParams()
            .Add("name", _endpointName)
            .Add("connection-type", "http://terminology.hl7.org/CodeSystem/endpoint-connection-type|dicom-wado-rs")
            .LimitTo(1);

        Bundle? bundle = await _client.SearchAsync<Endpoint>(searchParams, cancellationToken);
        if (bundle is null)
            return null;

        return await bundle
            .GetEntriesAsync(_client)
            .Select(x => x.Resource)
            .Cast<Endpoint>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public class TransactionContext : Transactions.TransactionContext
    {
        public Endpoint Endpoint { get; }

        public TransactionContext(TransactionBuilder builder, Endpoint endpoint)
            : base(builder)
            => Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        public PatientTransactionContext WithPatient(Patient patient)
            => new(Builder, Endpoint, patient);
    }
}
