using System;
using Xunit;
using Dapr.Workflows.Workflow;

namespace Dapr.Workflows.Tests
{
    public class CredentialsTests
    {
        [Fact]
        public void Credentials_ValuesPresent()
        {
            var credentials = new Credentials("name", "key");
            Assert.Equal("name", credentials.StorageAccountName);
            Assert.Equal("key", credentials.StorageAccountKey);
        }
    }
}
