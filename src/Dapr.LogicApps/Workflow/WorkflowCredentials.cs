// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.LogicApps.Workflow
{
    public class Credentials
    {
        public string StorageAccountKey { get; private set; }
        public string StorageAccountName { get; private set; }

        public Credentials(string storageAccountName, string storageAccountKey)
        {
            StorageAccountKey = storageAccountKey;
            StorageAccountName = storageAccountName;
        }
    }
}