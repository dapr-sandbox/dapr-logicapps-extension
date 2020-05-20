using System;
using Xunit;
using Dapr.Workflows.Configuration;

namespace Dapr.Workflows.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void PopulateSettings_NoException()
        {
            new FlowConfigurationManager();
        }
    }
}
