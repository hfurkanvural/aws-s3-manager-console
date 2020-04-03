using System;
using Amazon;

namespace s3_example.Models
{
    public class S3Config
    {
        public string bucketName {get; set;}
        public RegionEndpoint bucketRegion {get; set;} = RegionEndpoint.EUCentral1;
        public string accesskey {get; set;}
        public string secretkey {get; set;}
        public string url {get; set;}
    }
}


