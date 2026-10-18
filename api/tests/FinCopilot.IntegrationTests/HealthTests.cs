using System.Net;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_reports_healthy_when_database_is_reachable()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
