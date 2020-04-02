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
        public class S3Config
        {
            public string bucketName {get; set;}
            public RegionEndpoint bucketRegion {get; set;} = RegionEndpoint.EUCentral1;
            public string accesskey {get; set;}
            public string secretkey {get; set;}
        }
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
            ListObjectsV2Request request = new ListObjectsV2Request{
                    BucketName = s3config.bucketName,
                    MaxKeys = 10
                };
            ListObjectsV2Response response;
            
            var client = new AmazonS3Client(s3config.accesskey, s3config.secretkey, s3config.bucketRegion);

            response = await client.ListObjectsV2Async(request);
            List<string> images =  new List<string>();
            foreach (S3Object entry in response.S3Objects)
            {
                images.Add("https://hfv-itu-aws-course.s3.eu-central-1.amazonaws.com/"+entry.Key);
            }
            return View(images);  
        }  
        public async Task<IActionResult> uploadFile (IFormFile file)
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


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
