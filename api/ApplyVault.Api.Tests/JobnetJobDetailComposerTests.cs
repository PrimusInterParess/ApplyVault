using System.Net;
using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobDetailComposerTests
{
    [Fact]
    public async Task ComposeAsync_ReturnsFullDescription_ForNativeGuidListing()
    {
        var detail = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso A/S",
            "<p>Build <strong>APIs</strong></p><p>Ship reliable services across teams and mentor junior engineers.</p>",
            workInDenmark: true);

        var composer = CreateComposer(
            new JobnetRawDetail(
                JobnetDescriptionSource.NativeDetail,
                JobnetJobMapper.MapDetail("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", detail)));

        var response = await composer.ComposeAsync("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(JobnetDescriptionQualityValues.QualityFull, response.DescriptionQuality);
        Assert.Equal(JobnetDescriptionQualityValues.SourceNativeDetail, response.DescriptionSource);
        Assert.Contains("<p>", response.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposeAsync_ReturnsPreviewOnly_ForExternalListing()
    {
        var mapped = JobnetJobMapper.MapDetailFromSearch(
            "E10990623",
            JobnetTestData.CreateSearchJob(
                "E10990623",
                "Developer — Responsive",
                "RESPONSIVE A/S",
                JobnetJobDetailTestData.ScrapedExternalDescription,
                jobAdUrl: "https://responsive.dk/about-us/developer/"));

        var composer = CreateComposer(
            new JobnetRawDetail(JobnetDescriptionSource.SearchFallback, mapped));

        var response = await composer.ComposeAsync("E10990623", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(JobnetDescriptionQualityValues.QualityPreviewOnly, response.DescriptionQuality);
        Assert.Equal(JobnetDescriptionQualityValues.SourceSearchFallback, response.DescriptionSource);
        Assert.Null(response.Description);
        Assert.False(string.IsNullOrWhiteSpace(response.DescriptionExcerpt));
    }

    [Fact]
    public async Task ComposeAsync_ReturnsFullDescription_ForEuresImportedStructuredHtml()
    {
        var mapped = JobnetJobMapper.MapDetailFromSearch(
            "E11069412",
            JobnetTestData.CreateSearchJob(
                "E11069412",
                "Senior Backend Developer",
                "Nordea Bank Danmark",
                "<p><strong>We are hiring</strong></p><p>Build APIs with Java and Spring Boot across distributed systems and ship production features every week.</p>",
                jobAdUrl: "https://careers.nordea.com/job/example"));

        var composer = CreateComposer(
            new JobnetRawDetail(JobnetDescriptionSource.SearchFallback, mapped));

        var response = await composer.ComposeAsync("E11069412", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(JobnetDescriptionQualityValues.QualityFull, response.DescriptionQuality);
        Assert.Contains("<p>", response.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static JobnetJobDetailComposer CreateComposer(JobnetRawDetail rawDetail)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions
        {
            WorkInDenmarkOnly = true,
            BaseUrl = "https://jobnet.dk/bff"
        });
        var memoryCache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var payloadCache = new JobnetSearchPayloadCache(memoryCache, options);

        if (rawDetail.Source == JobnetDescriptionSource.SearchFallback)
        {
            payloadCache.SetAsync(
                ToSearchPayload(rawDetail.Mapped),
                CancellationToken.None).GetAwaiter().GetResult();
        }

        var handler = new StubHttpMessageHandler((request) =>
        {
            if (rawDetail.Source != JobnetDescriptionSource.NativeDetail)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var detail = JobnetTestData.CreateDetailJob(
                rawDetail.Mapped.Title ?? "Untitled",
                rawDetail.Mapped.Employer,
                rawDetail.Mapped.Description,
                workInDenmark: rawDetail.Mapped.WorkInDenmark,
                applicationUrl: rawDetail.Mapped.ApplicationUrl);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JobnetTestData.SerializeDetailResponse(detail),
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var fetcher = new JobnetJobDetailFetcher(
            new JobnetApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://jobnet.dk/bff") },
                options),
            payloadCache);

        return new JobnetJobDetailComposer(fetcher, new JobnetDescriptionQualityAssessor(), options);
    }

    private static JobnetSearchJobPayload ToSearchPayload(JobnetMappedJobDetail mapped) =>
        new()
        {
            JobAdId = mapped.Id,
            Title = mapped.Title,
            HiringOrgName = mapped.Employer,
            WorkPlaceAddress = mapped.Location,
            PublicationDate = mapped.PublicationDate,
            JobAdUrl = mapped.SourceUrl,
            Description = mapped.Description,
            Occupation = mapped.ContractType,
            WorkHourPartTime = mapped.WorkHours == "Part-time" ? true : mapped.WorkHours == "Full-time" ? false : null
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
