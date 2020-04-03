using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;  
using System.Web; 
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
                ListObjectsV2Request request = new ListObjectsV2Request{
                    BucketName = s3config.bucketName,
                    MaxKeys = 10
                };
                ListObjectsV2Response response;

                response = await client.ListObjectsV2Async(request);
                foreach (S3Object entry in response.S3Objects)
                {
                    S3Obj tempObj = new S3Obj();
                    tempObj.keyName= entry.Key;
                    tempObj.imgUrl= s3config.url + entry.Key;

                    Objects.Add(tempObj);
                }
                
                viewModel.Objects = Objects;
                viewModel.VersioningStatus = await RetrieveBucketVersioningConfiguration(client);
            }

            
            return View(viewModel);
        }  
        public async Task<IActionResult> uploadObject (IFormFile file)
        {
            using (var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion))
            {
                using (var newMemoryStream = new MemoryStream())
                {
                    file.CopyTo(newMemoryStream);

                    var uploadRequest = new TransferUtilityUploadRequest
                    {
                        InputStream = newMemoryStream,
                        Key = file.FileName,
                        BucketName = s3config.bucketName,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    var fileTransferUtility = new TransferUtility(client);
                    await fileTransferUtility.UploadAsync(uploadRequest);
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
