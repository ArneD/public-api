namespace Public.Api.Feeds
{
    using Be.Vlaanderen.Basisregisters.Api.ETag;
    using Be.Vlaanderen.Basisregisters.Api.Exceptions;
    using Autofac.Features.Indexed;
    using Infrastructure;
    using Marvin.Cache.Headers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.Net.Http.Headers;
    using Newtonsoft.Json.Converters;
    using RestSharp;
    using Swashbuckle.AspNetCore.Filters;
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using ParcelRegistry.Api.Legacy.Parcel.Responses;
    using Infrastructure.Configuration;
    using Microsoft.Extensions.Options;

    public partial class FeedController
    {
        /// <summary>
        /// Vraag een lijst met wijzigingen van percelen op in het Atom formaat.
        /// </summary>
        /// <param name="actionContextAccessor"></param>
        /// <param name="restClients"></param>
        /// <param name="responseOptions"></param>
        /// <param name="from">Optionele start id om van te beginnen.</param>
        /// <param name="offset">Optionele nulgebaseerde index van de eerste instantie die teruggegeven wordt.</param>
        /// <param name="limit">Optioneel maximaal aantal instanties dat teruggegeven wordt.</param>
        /// <param name="ifNoneMatch">Optionele If-None-Match header met ETag van een vorig verzoek.</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Als de opvraging van een lijst met percelen gelukt is.</response>
        /// <response code="304">Als de lijst niet gewijzigd is ten opzicht van uw verzoek.</response>
        /// <response code="400">Als uw verzoek foutieve data bevat.</response>
        /// <response code="500">Als er een interne fout is opgetreden.</response>
        [HttpGet("percelen")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status304NotModified)]
        [ProducesResponseType(typeof(BasicApiProblem), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicApiProblem), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseHeader(StatusCodes.Status200OK, "ETag", "string", "De ETag van de respons.")]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ParcelSyndicationResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status304NotModified, typeof(NotModifiedResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [HttpCacheExpiration(MaxAge = 12 * 60 * 60)] // Hours, Minutes, Second
        public async Task<IActionResult> GetParcels(
            [FromServices] IActionContextAccessor actionContextAccessor,
            [FromServices] IIndex<string, Lazy<IRestClient>> restClients,
            [FromServices] IOptions<AddressOptions> responseOptions,
            [FromQuery] long? from,
            [FromQuery] int? offset,
            [FromQuery] int? limit,
            [FromHeader(Name = HeaderNames.IfNoneMatch)] string ifNoneMatch,
            CancellationToken cancellationToken = default)
            => await GetParcels(
                null,
                actionContextAccessor,
                restClients,
                responseOptions,
                from,
                offset,
                limit,
                ifNoneMatch,
                cancellationToken);

        /// <summary>
        /// Vraag een lijst met wijzigingen van percelen op in het XML of Atom formaat.
        /// </summary>
        /// <param name="format">Gewenste formaat: percelen.xml of percelen.atom</param>
        /// <param name="actionContextAccessor"></param>
        /// <param name="restClients"></param>
        /// <param name="responseOptions"></param>
        /// <param name="from">Optionele start id om van te beginnen.</param>
        /// <param name="offset">Optionele nulgebaseerde index van de eerste instantie die teruggegeven wordt.</param>
        /// <param name="limit">Optioneel maximaal aantal instanties dat teruggegeven wordt.</param>
        /// <param name="ifNoneMatch">Optionele If-None-Match header met ETag van een vorig verzoek.</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Als de opvraging van een lijst met percelen gelukt is.</response>
        /// <response code="304">Als de lijst niet gewijzigd is ten opzicht van uw verzoek.</response>
        /// <response code="400">Als uw verzoek foutieve data bevat.</response>
        /// <response code="500">Als er een interne fout is opgetreden.</response>
        [HttpGet("percelen.{format}")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(void), StatusCodes.Status304NotModified)]
        [ProducesResponseType(typeof(BasicApiProblem), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BasicApiProblem), StatusCodes.Status500InternalServerError)]
        [SwaggerResponseHeader(StatusCodes.Status200OK, "ETag", "string", "De ETag van de respons.")]
        [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ParcelSyndicationResponseExamples))]
        [SwaggerResponseExample(StatusCodes.Status304NotModified, typeof(NotModifiedResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [SwaggerResponseExample(StatusCodes.Status500InternalServerError, typeof(InternalServerErrorResponseExamples), jsonConverter: typeof(StringEnumConverter))]
        [HttpCacheExpiration(MaxAge = 12 * 60 * 60)] // Hours, Minutes, Second
        public async Task<IActionResult> GetParcels(
            [FromRoute] string format,
            [FromServices] IActionContextAccessor actionContextAccessor,
            [FromServices] IIndex<string, Lazy<IRestClient>> restClients,
            [FromServices] IOptions<AddressOptions> responseOptions,
            [FromQuery] long? from,
            [FromQuery] int? offset,
            [FromQuery] int? limit,
            [FromHeader(Name = HeaderNames.IfNoneMatch)] string ifNoneMatch,
            CancellationToken cancellationToken = default)
        {
            format = !string.IsNullOrWhiteSpace(format)
                ? format
                : actionContextAccessor.ActionContext.GetValueFromHeader("format")
                  ?? actionContextAccessor.ActionContext.GetValueFromRouteData("format")
                  ?? actionContextAccessor.ActionContext.GetValueFromQueryString("format");

            var restClient = restClients["ParcelRegistry"].Value;

            from = from ?? 0;
            offset = offset ?? 0;
            limit = limit ?? DefaultLimit;

            void HandleBadRequest(HttpStatusCode statusCode)
            {
                switch (statusCode)
                {
                    case HttpStatusCode.NotAcceptable:
                        throw new ApiException("Ongeldig formaat.", StatusCodes.Status406NotAcceptable);

                    case HttpStatusCode.BadRequest:
                        throw new ApiException("Ongeldige vraag.", StatusCodes.Status400BadRequest);
                }
            }

            RestRequest BackendRequest() => CreateBackendSyndicationRequest(
                "percelen",
                from.Value,
                offset.Value,
                limit.Value);

            var value = await GetFromBackendAsync(
                format,
                restClient,
                BackendRequest,
                Request.GetTypedHeaders(),
                HandleBadRequest,
                cancellationToken);

            return BackendListResponseResult.Create(value, Request.Query, responseOptions.Value.Syndication.NextUri);
        }
    }
}