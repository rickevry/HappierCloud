using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using Microsoft.Rest;
using System.Net.Mail;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Azure.Management.Compute;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Management.Compute.Models;

namespace HappierCloud
{
    public class Client
    {

        dynamic _settings = null;

        public Client(dynamic props)
        {
            this._settings = props;
        }

        private string GetStringFromAnonymousType(object dataitem, string itemkey)
        {
            System.Type type = dataitem.GetType();
            object itemvalue = type.GetProperty(itemkey).GetValue(dataitem, null);
            if (itemvalue != null)
            {
                return itemvalue.ToString();
            }
            return "";
        }

        private static bool needToStartServer(TokenCredentials credential,string groupName,string vmName, string subscriptionId)
        {
            var computeManagementClient = new ComputeManagementClient(credential) { SubscriptionId = subscriptionId };

            var vm = computeManagementClient.VirtualMachines.Get(groupName, vmName);
            //log.Info($"ProvisioningState: {vm.ProvisioningState}");

            string output = Newtonsoft.Json.JsonConvert.SerializeObject(vm);
            //log.Info($"output: {output}");

            if (vm.ProvisioningState == "RUNNING")
            {
                return false;
            }

            return true;
        }

        public void StartVirtualMachine(string groupName,string vmName,string subscriptionId)
        {
            var token = GetAccessTokenAsync();
            var credential = new TokenCredentials(token.AccessToken);

            var computeManagementClient = new ComputeManagementClient(credential) { SubscriptionId = subscriptionId };
            OperationStatusResponse result = computeManagementClient.VirtualMachines.Start(groupName, vmName);
        }

        public void StoreFile(string containerName, string filename, string sourceFileName)
        {
            var connectionString = GetStringFromAnonymousType(this._settings, "connectionString");
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);
            blockBlob.UploadFromFile(sourceFileName);
        }

        public void StopVirtualMachine(string groupName, string vmName, string subscriptionId, int sleep)
        {
            var token = GetAccessTokenAsync();
            var credential = new TokenCredentials(token.AccessToken);

            if (sleep > 0)
            {
                System.Threading.Thread.Sleep(180000);
            }

            var computeManagementClient = new ComputeManagementClient(credential) { SubscriptionId = subscriptionId };
            computeManagementClient.VirtualMachines.Deallocate(groupName, vmName);
            System.Console.ReadLine();
        }


        private AuthenticationResult GetAccessTokenAsync()
        {
            var clientId = GetStringFromAnonymousType(this._settings, "clientId");
            var clientSecret = GetStringFromAnonymousType(this._settings, "clientSecret");
            var authority = GetStringFromAnonymousType(this._settings, "authority");
            var cc = new ClientCredential(clientId, clientSecret);

            var context = new AuthenticationContext(authority);
            var task = context.AcquireTokenAsync("https://management.azure.com/", cc);
            task.Wait();
            AuthenticationResult token = task.Result as AuthenticationResult;
            if (token == null)
            {
                throw new InvalidOperationException("Could not get the token");
            }
            return token;
        }

        public void QueueMessage(string msg, string queueName)
        {
            var connectionString = GetStringFromAnonymousType(this._settings, "connectionString");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue queue = queueClient.GetQueueReference(queueName);
            queue.CreateIfNotExists();
            CloudQueueMessage message = new CloudQueueMessage(msg);
            queue.AddMessage(message);
        }

        private byte[] LoadFile(string filename, string containerName)
        {
            var connectionString = GetStringFromAnonymousType(this._settings, "connectionString");
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(containerName);

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);
            if (blockBlob.Exists())
            {
                MemoryStream ms = new MemoryStream();
                blockBlob.DownloadToStream(ms);
                return ms.GetBuffer();
            }
            else
            {
                return null;
            }
        }

        public string CreateHashedFilename(string dataJson, string extension)
        {
            string filename = null;
            using (var sha = new System.Security.Cryptography.SHA256Managed())
            {
                byte[] textData = System.Text.Encoding.UTF8.GetBytes(dataJson);
                byte[] hash = sha.ComputeHash(textData);
                filename = Base32.Encode(hash) + extension;
            }
            return filename;
        }

        public bool BlobExists(string filename)
        {
            var connectionString = GetStringFromAnonymousType(this._settings, "connectionString");
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference("pdfcache");

            CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);
            if (blockBlob.Exists())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}


//private static string Encode(byte[] data, bool padOutput = false)
//{
//    Func<int, int> numberOfTrailingZeros = delegate (int i)
//    {
//        // HD, Figure 5-14
//        int y;
//        if (i == 0) return 32;
//        int n = 31;
//        y = i << 16; if (y != 0) { n = n - 16; i = y; }
//        y = i << 8; if (y != 0) { n = n - 8; i = y; }
//        y = i << 4; if (y != 0) { n = n - 4; i = y; }
//        y = i << 2; if (y != 0) { n = n - 2; i = y; }
//        return n - (int)((uint)(i << 1) >> 31);
//    };

//    char[] DIGITS = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();
//    int MASK = DIGITS.Length - 1;
//    int SHIFT = numberOfTrailingZeros(DIGITS.Length);
//    Dictionary<char, int> CHAR_MAP = new Dictionary<char, int>();
//    for (int i = 0; i < DIGITS.Length; i++) CHAR_MAP[DIGITS[i]] = i;

//    if (data.Length == 0)
//    {
//        return "";
//    }

//    // SHIFT is the number of bits per output character, so the length of the
//    // output is the length of the input multiplied by 8/SHIFT, rounded up.
//    if (data.Length >= (1 << 28))
//    {
//        // The computation below will fail, so don't do it.
//        throw new ArgumentOutOfRangeException("data");
//    }

//    int outputLength = (data.Length * 8 + SHIFT - 1) / SHIFT;
//    StringBuilder result = new StringBuilder(outputLength);

//    int buffer = data[0];
//    int next = 1;
//    int bitsLeft = 8;
//    while (bitsLeft > 0 || next < data.Length)
//    {
//        if (bitsLeft < SHIFT)
//        {
//            if (next < data.Length)
//            {
//                buffer <<= 8;
//                buffer |= (data[next++] & 0xff);
//                bitsLeft += 8;
//            }
//            else
//            {
//                int pad = SHIFT - bitsLeft;
//                buffer <<= pad;
//                bitsLeft += pad;
//            }
//        }
//        int index = MASK & (buffer >> (bitsLeft - SHIFT));
//        bitsLeft -= SHIFT;
//        result.Append(DIGITS[index]);
//    }
//    if (padOutput)
//    {
//        int padding = 8 - (result.Length % 8);
//        if (padding > 0) result.Append(new string('=', padding == 8 ? 0 : padding));
//    }
//    return result.ToString();
//}
