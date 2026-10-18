using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PharmaAI.Application.Sync.Eta;
using PharmaAI.Application.Tests.TestSupport;
using PharmaAI.Infrastructure.Sync;

namespace PharmaAI.Application.Tests.Sync;

public class EtaHttpClientTests
{
    private static readonly EtaSettings Settings = new()
    {
        Environment = EtaEnvironment.PreProduction,
        ClientId = "client",
        ClientSecret = "secret",
        PosSerial = "POS-001",
        PosOsVersion = "windows",
        PosModelFramework = "1",
        PresharedKey = "key",
    };

    private const string Receipt = """{"header":{"uuid":"aaa","receiptNumber":"1","unitPrice":45}}""";

    private sealed class FakeEta : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        public Queue<HttpResponseMessage> SubmitResponses { get; } = new();

        public HttpStatusCode TokenStatus { get; set; } = HttpStatusCode.OK;

        public Exception? Throw { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Requests.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            if (request.RequestUri!.AbsolutePath == "/connect/token")
            {
                return TokenStatus == HttpStatusCode.OK
                    ? Json(HttpStatusCode.OK, """{"access_token":"tok","token_type":"Bearer","expires_in":3600}""")
                    : Json(TokenStatus, """{"error":"invalid_presharedkey"}""");
            }

            return SubmitResponses.Count > 0
                ? SubmitResponses.Dequeue()
                : Json(HttpStatusCode.Accepted, """{"submissionUUID":"sub","acceptedDocuments":[{"uuid":"aaa","longId":"x","receiptNumber":"1"}],"rejectedDocuments":[]}""");
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (EtaHttpClient Client, FakeEta Server, FixedTimeProvider Clock) Create()
    {
        var server = new FakeEta();
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 18, 9, 0, 0, TimeSpan.Zero));
        return (new EtaHttpClient(new HttpClient(server), new EtaTokenCache(), clock, NullLogger<EtaHttpClient>.Instance), server, clock);
    }

    [Fact]
    public async Task Logs_in_as_the_pos_then_submits_the_receipts_byte_for_byte()
    {
        var (client, server, _) = Create();

        var result = await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.Null(result.BatchError);
        Assert.Contains("aaa", result.Accepted);

        var (login, form) = server.Requests[0];
        Assert.Equal("https://id.preprod.eta.gov.eg/connect/token", login.RequestUri!.ToString());
        Assert.Equal("grant_type=client_credentials&client_id=client&client_secret=secret", form);
        Assert.Equal(
            ["POS-001", "windows", "1", "key"],
            new[] { "posserial", "pososversion", "posmodelframework", "presharedkey" }.Select(h => login.Headers.GetValues(h).Single()));

        var (submit, body) = server.Requests[1];
        Assert.Equal("https://api.preprod.invoicing.eta.gov.eg/api/v1/receiptsubmissions", submit.RequestUri!.ToString());
        Assert.Equal("Bearer tok", submit.Headers.Authorization!.ToString());
        Assert.Equal($"{{\"receipts\":[{Receipt}]}}", body);
    }

    [Fact]
    public async Task Reuses_the_token_until_shortly_before_it_expires()
    {
        var (client, server, clock) = Create();

        await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);
        clock.Now = clock.Now.AddMinutes(50);
        await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);
        clock.Now = clock.Now.AddMinutes(6);
        await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.Equal(2, server.Requests.Count(r => r.Request.RequestUri!.AbsolutePath == "/connect/token"));
    }

    [Fact]
    public async Task Reports_each_rejected_receipt_with_eta_s_reasons()
    {
        var (client, server, _) = Create();
        server.SubmitResponses.Enqueue(Json(HttpStatusCode.Accepted, """
            {"submissionUUID":"sub","acceptedDocuments":[],"rejectedDocuments":[{"receiptNumber":"1","uuid":"aaa","error":{"message":"Validation Error","target":"aaa","details":[{"code":"CF313","message":"Issuance date time value is out of the range","target":"DatetimeIssued"}]}}]}
            """));

        var result = await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.Equal("Validation Error; CF313 Issuance date time value is out of the range (DatetimeIssued)", result.Rejected["aaa"]);
    }

    [Fact]
    public async Task A_refused_login_fails_the_whole_batch_with_the_reason()
    {
        var (client, server, _) = Create();
        server.TokenStatus = HttpStatusCode.BadRequest;

        var result = await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.Equal("ETA login failed (400): invalid_presharedkey", result.BatchError);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task An_expired_token_is_dropped_so_the_next_attempt_logs_in_again()
    {
        var (client, server, _) = Create();
        server.SubmitResponses.Enqueue(Json(HttpStatusCode.Unauthorized, ""));

        Assert.NotNull((await client.SubmitAsync(Settings, [Receipt], CancellationToken.None)).BatchError);
        Assert.Null((await client.SubmitAsync(Settings, [Receipt], CancellationToken.None)).BatchError);

        Assert.Equal(2, server.Requests.Count(r => r.Request.RequestUri!.AbsolutePath == "/connect/token"));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Server_errors_fail_the_whole_batch(HttpStatusCode status)
    {
        var (client, server, _) = Create();
        server.SubmitResponses.Enqueue(Json(status, """{"error":{"message":"BadStructure"}}"""));

        var result = await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.StartsWith($"ETA returned {(int)status}", result.BatchError);
    }

    [Fact]
    public async Task Being_offline_fails_the_whole_batch()
    {
        var (client, server, _) = Create();
        server.Throw = new HttpRequestException("No such host is known.");

        var result = await client.SubmitAsync(Settings, [Receipt], CancellationToken.None);

        Assert.Equal("Could not reach ETA: No such host is known.", result.BatchError);
    }

    [Fact]
    public void Production_uses_the_production_hosts()
    {
        Assert.Equal("https://id.eta.gov.eg/", EtaEndpoints.Identity(EtaEnvironment.Production).ToString());
        Assert.Equal("https://api.invoicing.eta.gov.eg/", EtaEndpoints.Api(EtaEnvironment.Production).ToString());
    }
}
