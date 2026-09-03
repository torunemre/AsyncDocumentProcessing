using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace AsyncDocumentProcessing.Tests
{
    public class IntegrationTest
        : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public IntegrationTest(
            WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Api_ShouldReturnSuccess()
        {
            // Act
            var response = await _client.GetAsync("/swagger/index.html");

            // Assert
            response.EnsureSuccessStatusCode();
        }
    }
}