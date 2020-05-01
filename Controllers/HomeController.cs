using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Amazon;  
using Amazon.Runtime;  
using Amazon.S3;  
using Amazon.S3.Model;  
using Amazon.S3.Transfer; 
using Microsoft.Extensions.Configuration;
using s3_example.Models;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text;

namespace s3_example.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration config;
        private S3Config s3config;
        public string url = "https://t5lqxq8wja.execute-api.eu-central-1.amazonaws.com/test"; 
        
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

            
            return View(viewModel);
        }  
        public async Task<IActionResult> uploadObject (IFormFile file, string tableName, bool encryptionEnabled = false)
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
                    var objects = await GetS3Objects(client);
                    S3Obj obj = objects.FindLast(element => element.keyName == file.FileName);
                
                    
                    using (var httpClient = new HttpClient())
                    {
                        using (var response = await httpClient.GetAsync(url+"?tableName="+tableName+"&objectKey="+file.FileName))
                        {
                            string apiResponse = await response.Content.ReadAsStringAsync();
                            dynamic resp = JObject.Parse(apiResponse);
                            dynamic payload = new System.Dynamic.ExpandoObject();
                            payload.s3Obj = new System.Dynamic.ExpandoObject();
                            payload.s3Obj.key = obj.keyName;
                            payload.s3Obj.imgUrl = obj.imgUrl;
                            payload.s3Obj.size = obj.size;
                            payload.s3Obj.extension = Path.GetExtension(obj.keyName);
                            payload.s3Obj.uploadDate = obj.uploadDate.ToString();
                            payload.s3Obj.versions = new List<string>();
                            foreach(var version in obj.versions)
                            {
                                payload.s3Obj.versions.Add(version.VersionId);
                            }
                            payload.table_name = tableName;

                            string strPayload = JsonConvert.SerializeObject(payload);
                           
                            if (resp.statusCode == 200)
                            {
                                HttpResponseMessage result = await httpClient.PutAsync(new Uri(url), new StringContent(strPayload, Encoding.UTF8, "application/json"));
                            }
                            else
                            {
                                HttpResponseMessage result = await httpClient.PostAsync(new Uri(url), new StringContent(strPayload, Encoding.UTF8, "application/json"));
                            }
                               
                        }
                        
                    }
                }
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
                await client.DeleteObjectAsync(deleteObjectRequest);
                using (var httpClient = new HttpClient())
                {
                    using (var response = await httpClient.GetAsync(url+"/gettables?objectKey="+id))
                    {
                        string apiResponse = await response.Content.ReadAsStringAsync();
                        dynamic resp = JObject.Parse(apiResponse);
                        if(resp.body.Count > 0)
                        {
                            foreach (string name in resp.body)
                            {
                                var result = await httpClient.DeleteAsync(url+"?tableName="+name+"&objectKey="+id);
                                Console.WriteLine(result);
                            }
                        }
                    }
                }
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
