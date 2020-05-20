using System;
using Xunit;
using Dapr.Workflows.Workflow;
using System.Collections.Generic;

namespace Dapr.Workflows.Tests
{
    public class WorkflowRunnerTests
    {
        [Fact]
        public void Constructor_WorkflowConfigExists_ReturnsTrue()
        {
            var workflow = new WorkflowConfig("a", null);
            var runner = new DaprWorkflowExecutor(new List<WorkflowConfig>() { workflow}, null);
            var exists = runner.WorkflowExists("a");

            Assert.True(exists);
        }

        [Fact]
        public void Constructor_WorkflowConfigDoesntExist_RetursFalse()
        {
            var workflow = new WorkflowConfig("a", null);
            var runner = new DaprWorkflowExecutor(new List<WorkflowConfig>() { workflow}, null);
            var exists = runner.WorkflowExists("b");

            Assert.False(exists);
        }
    }
}
