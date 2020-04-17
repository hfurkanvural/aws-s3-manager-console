using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Amazon;  
using Amazon.Runtime;  
using Amazon.S3;  
using Amazon.S3.Model;  
using Amazon.S3.Transfer; 
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.DynamoDBv2.DocumentModel;
using Microsoft.Extensions.Configuration;
using s3_example.Models;

namespace s3_example.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration config;
        private S3Config s3config;
        
        public HomeController(IConfiguration configuration)
        {
            config = configuration;
            s3config = config.GetSection("S3Config").Get<S3Config>();
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]  
        public async Task<IActionResult> s3Manager()  
        {  

            List<S3Obj> Objects =  new List<S3Obj>();
            S3ManagerViewModel viewModel = new S3ManagerViewModel();

            using (var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion))
            {
                await GetS3ObjectswithVersions(client);
                viewModel.Objects = await GetS3Objects(client);
                viewModel.VersioningStatus = await RetrieveBucketVersioningConfiguration(client);
            }
            AmazonDynamoDBClient dbclient = new AmazonDynamoDBClient(s3config.accesskey, s3config.secretkey, s3config.bucketRegion);
            List<string> tableNames = await GetTableNames(dbclient);
            
            foreach(var name in tableNames)
            {
                viewModel.tableName = name;
                try
                {
                    viewModel = await LoadTable(dbclient, viewModel);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("Exception Found for {0}! {1}", name, ex);
                    viewModel.tableName = null;
                    ViewBag.Message = "No table for S3 bucket found!";
                }
                
            }


            return View(viewModel);
        }  
        public async Task<IActionResult> uploadObject (IFormFile file,  S3ManagerViewModel viewModel, bool encryptionEnabled = false)
        {
            
            using (var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion))
            {
                
                using (var newMemoryStream = new MemoryStream())
                {
                    file.OpenReadStream().CopyTo(newMemoryStream);
                    PutObjectRequest putObj = new PutObjectRequest()
                    {
                        BucketName = s3config.bucketName,
                        Key = file.FileName,
                        ContentType = file.ContentType,
                        InputStream = newMemoryStream,
                        CannedACL = S3CannedACL.PublicRead
                    };
                    if(encryptionEnabled)
                        putObj.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;
                        
                    await client.PutObjectAsync(putObj);
                }
                if(viewModel.Objects == null)
                {} 
            }
            return RedirectToAction(nameof(s3Manager));
        }

        public async Task<IActionResult> deleteObject(string id)
        {

            using (var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion))
            {
                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = s3config.bucketName,
                    Key = id
                };

                Console.WriteLine("Deleting an object");
                await client.DeleteObjectAsync(deleteObjectRequest);
            }

            return RedirectToAction(nameof(s3Manager));
        }

        public async Task<IActionResult> enableVersioning()
        {
            var status = VersionStatus.Enabled;

            using (var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion))
            {
                var currentStatus = await RetrieveBucketVersioningConfiguration(client);
                if(currentStatus == "Enabled")
                    status = VersionStatus.Suspended;
                    
                PutBucketVersioningRequest request = new PutBucketVersioningRequest
                {
                    BucketName = s3config.bucketName,
                    VersioningConfig = new S3BucketVersioningConfig 
                    {
                        Status = status
                    }
                };
                

                PutBucketVersioningResponse response = await client.PutBucketVersioningAsync(request);
            }
            
            return RedirectToAction(nameof(s3Manager));
        }


        public async Task<string> RetrieveBucketVersioningConfiguration(AmazonS3Client client)
        {
            GetBucketVersioningRequest request = new GetBucketVersioningRequest
            {
                BucketName = s3config.bucketName
            };
            GetBucketVersioningResponse response = await client.GetBucketVersioningAsync(request);
           
            return response.VersioningConfig.Status;    
        }

        public async Task<List<S3Obj>> GetS3Objects(AmazonS3Client client)
        {
            List<S3Obj> Objects =  new List<S3Obj>();
            ListObjectsV2Request request = new ListObjectsV2Request{
                    BucketName = s3config.bucketName,
                    MaxKeys = 100
                };
            ListObjectsV2Response response = await client.ListObjectsV2Async(request);

            List<S3ObjectVersion> versions = await GetS3ObjectswithVersions(client);

            foreach (S3Object entry in response.S3Objects)
            {
                S3Obj tempObj = new S3Obj();
                tempObj.keyName= entry.Key;
                tempObj.imgUrl= s3config.url + entry.Key;
                tempObj.size = entry.Size;
                tempObj.uploadDate = entry.LastModified;
                tempObj.versions = versions.FindAll(element => element.Key.Equals(entry.Key));
                Objects.Add(tempObj);
            }
            return Objects;
        }

        public async Task<List<S3ObjectVersion>> GetS3ObjectswithVersions(AmazonS3Client client)
        {
            ListVersionsRequest request = new ListVersionsRequest()
            {
                BucketName = s3config.bucketName,
                MaxKeys = 100
            };
            ListVersionsResponse response = await client.ListVersionsAsync(request); 
            return response.Versions;
        }
        public async Task<IActionResult> CreateTable(string tableName)
        {
            Console.Write("\n\n\n\n\n***************");
            Console.Write(tableName);
            Console.Write("\n\n\n\n\n***************");
            AmazonDynamoDBClient client =  new AmazonDynamoDBClient(s3config.accesskey, s3config.secretkey, s3config.bucketRegion);
            //string tableName = "TestTable";

            var response = await client.CreateTableAsync(new CreateTableRequest
            {
                TableName = tableName,
                AttributeDefinitions = new List<AttributeDefinition>()
                              {
                                  new AttributeDefinition
                                  {
                                      AttributeName = "ObjectKey",
                                      AttributeType = "S"
                                  }
                              },
                KeySchema = new List<KeySchemaElement>()
                              {
                                  new KeySchemaElement
                                  {
                                      AttributeName = "ObjectKey",
                                      KeyType = "HASH"
                                  }
                              },
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = 5,
                    WriteCapacityUnits = 5
                }
            });
            var tableDescription = response.TableDescription;

            string status = tableDescription.TableStatus;

            Console.WriteLine(tableName + " - " + status);


                    var res = await client.DescribeTableAsync(new DescribeTableRequest
                    {
                        TableName = tableName
                    });
                    Console.WriteLine("Table name: {0}, status: {1}", res.Table.TableName,
                              res.Table.TableStatus);
                    status = res.Table.TableStatus;
      
            WaitUntilTableReady(client, tableName);

            return RedirectToAction(nameof(s3Manager));
        }

        public async Task<IActionResult> DeleteTable(string id)
        {

            AmazonDynamoDBClient client =  new AmazonDynamoDBClient(s3config.accesskey, s3config.secretkey, s3config.bucketRegion);
            DeleteTableRequest request = new DeleteTableRequest
            {
                TableName = id
            };

            var response = await client.DeleteTableAsync(request);
            
            var tableDescription = response.TableDescription;

            string status = tableDescription.TableStatus;

            Console.WriteLine(id + " - " + status);

            var res = await client.DescribeTableAsync(new DescribeTableRequest
            {
                TableName = id
            });
            Console.WriteLine("Table name: {0}, status: {1}", res.Table.TableName,
                      res.Table.TableStatus);
            status = res.Table.TableStatus;
      
            WaitUntilTableReady(client, id);

            return RedirectToAction(nameof(s3Manager));
        }
        
        public async Task<List<string>> GetTableNames(AmazonDynamoDBClient client)
        {

            Console.WriteLine("\n*** listing tables ***");
            string lastTableNameEvaluated = null;

                var request = new ListTablesRequest
                {
                    Limit = 2,
                    ExclusiveStartTableName = lastTableNameEvaluated
                };

                var response = await client.ListTablesAsync(request);
                foreach (string name in response.TableNames)
                    Console.WriteLine(name);

                if (response.TableNames.Count() > 0)
                    lastTableNameEvaluated = response.TableNames[0];
            return response.TableNames;
        }
        public async Task<S3ManagerViewModel> LoadTable(AmazonDynamoDBClient client, S3ManagerViewModel viewModel)
        {
        
            var table = Table.LoadTable(client, viewModel.tableName);
            
            try{
                foreach (var item in viewModel.Objects)
                {
                    Document document = await table.GetItemAsync(item.keyName);
                    if (document == null)
                    {
                        Console.WriteLine("Error: product " + item.keyName + " does not exist");
                        S3Obj obj = viewModel.Objects.Find(element => element.keyName == item.keyName);
                        document = await uploadItem(table, obj);
                    }

                    PrintDocument(document);
                    
                    S3DbInfo dbinfo = new S3DbInfo();
                    dbinfo.keyName = document["ObjectKey"];
                    dbinfo.imgUrl = document["ImgUrl"];
                    dbinfo.extension = document["Extension"];
                    dbinfo.size = document["Size"].AsInt();
                    dbinfo.uploadDate = document["UploadDate"].AsDateTime();
                    dbinfo.versions = document["Versions"].AsListOfString();

                    item.Data = dbinfo;
                }
            }
            catch(Exception ex)
            {
                throw(ex);
            }

            
            return viewModel;
        }

        public async Task<Document> uploadItem(Table table, S3Obj obj)
        {
            Document document = null;
            Console.WriteLine("\n*** Executing uploadItem() ***");
            try{
                List<string> vers = new List<string>();
                
                foreach(var version in obj.versions)
                {
                    vers.Add(version.VersionId);
                }

                var newobj = new Document();
                newobj["ObjectKey"] = obj.keyName;
                newobj["ImgUrl"] = obj.imgUrl;
                newobj["Extension"] = Path.GetExtension(obj.keyName);
                newobj["Size"] = obj.size;
                newobj["UploadDate"] = obj.uploadDate;
                newobj["Versions"] = vers;
                
    
                await table.PutItemAsync(newobj);
                document = await table.GetItemAsync(obj.keyName);
            }
            catch(Exception ex)
            {
                throw(ex);
            }
            return document;
        }
        private async void WaitUntilTableReady(AmazonDynamoDBClient client, string tableName)
        {
            string status = null;
            // Let us wait until table is created. Call DescribeTable.
            do
            {
                System.Threading.Thread.Sleep(5000); // Wait 5 seconds.
                try
                {
                    var res =await client.DescribeTableAsync(new DescribeTableRequest
                    {
                        TableName = tableName
                    });

                    Console.WriteLine("Table name: {0}, status: {1}",
                              res.Table.TableName,
                              res.Table.TableStatus);
                    status = res.Table.TableStatus;
                }
                catch (ResourceNotFoundException)
                {
                    // DescribeTable is eventually consistent. So you might
                    // get resource not found. So we handle the potential exception.
                }
            } while (status != "ACTIVE");
        }


        private static void PrintDocument(Document document)
        {
            //   count++;
            Console.WriteLine();
            foreach (var attribute in document.GetAttributeNames())
            {
                string stringValue = null;
                var value = document[attribute];
                if (value is Primitive)
                    stringValue = value.AsPrimitive().Value.ToString();
                else if (value is PrimitiveList)
                    stringValue = string.Join(",", (from primitive
                                    in value.AsPrimitiveList().Entries
                                                    select primitive.Value).ToArray());
                Console.WriteLine("{0} - {1}", attribute, stringValue);
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        
    }
}
