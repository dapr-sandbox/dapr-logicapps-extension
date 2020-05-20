

using System.Collections;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Dapr.Workflows.Workflow;
using Microsoft.WindowsAzure.ResourceStack.Common.Services;
using System.Diagnostics;

namespace Dapr.Workflows.Configuration
{
    public class FlowConfigurationManager : AzureConfigurationManager
    {
        /// <summary>Gets or sets the XML application settings.</summary>
        private XDocument FlowAppSettings { get; set; }
        private List<string> settings = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Flow.WebJobs.Extensions.Initialization.FlowConfigurationManager" /> class.
        /// </summary>
        public FlowConfigurationManager()
        {
            PopulateSettings();

            var hostFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
            this.FlowAppSettings = XDocument.Parse(File.ReadAllText(hostFile));
        }

        private void PopulateSettings()
        {
            settings.Add("CloudStorageAccount.Workflows.BillingDataStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Workflows.RegionalDataStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Workflows.HydrationDataStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Workflows.PlatformArtifactsContentStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Workflows.ScaleUnitsDataStorage.CU00.ConnectionString");
            settings.Add("CloudStorageAccount.Workflows.PairedRegion.RegionalDataStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Flow.FunctionAppsRuntimeStorage.ConnectionString");
            settings.Add("CloudStorageAccount.Flow.FunctionAppsSecretStorage.ConnectionString");
        }

        public void SetCredentials(Credentials credentials)
        {
            string connectionString = $"DefaultEndpointsProtocol=https;AccountName={credentials.StorageAccountName};AccountKey={credentials.StorageAccountKey};EndpointSuffix=core.windows.net";

            settings.ForEach(s => {
                var node = this.FlowAppSettings.Root.Elements().FirstOrDefault().Elements().FirstOrDefault(d => d.Attribute("key").Value == s);
                node.Attribute("value").Value = connectionString;
            });
        }

        /// <summary>Gets configuration settings.</summary>
        /// <param name="settingName">The setting name.</param>
        protected override string GetConfigurationSettings(string settingName)
        {
            return this.XPathSelectAttributeOrDefault(this.FlowAppSettings.Document, "/configuration/appSettings/add[@key='" + settingName + "']/@value", (XmlNamespaceManager)null)?.Value;
        }

        /// <summary>Select the XML attribute if exists.</summary>
        /// <param name="document">The document.</param>
        /// <param name="xPath">The XML path.</param>
        /// <param name="namespaceManager">The namespace manager.</param>
        private XAttribute XPathSelectAttributeOrDefault(
          XDocument document,
          string xPath,
          XmlNamespaceManager namespaceManager = null)
        {
            return ((IEnumerable)document.Document.XPathEvaluate(xPath, (IXmlNamespaceResolver)namespaceManager)).Cast<XAttribute>().SingleOrDefault<XAttribute>();
        }
    }
}