

using System.Collections;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.WindowsAzure.ResourceStack.Common.Services;

namespace Dapr.LogicApps.Configuration
{
    internal class FlowConfigurationManager : AzureConfigurationManager
    {
        /// <summary>Gets or sets the XML application settings.</summary>
        private XDocument FlowAppSettings { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Flow.WebJobs.Extensions.Initialization.FlowConfigurationManager" /> class.
        /// </summary>
         public FlowConfigurationManager()
        {
            var hostFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
            this.FlowAppSettings = XDocument.Parse(File.ReadAllText(hostFile));
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