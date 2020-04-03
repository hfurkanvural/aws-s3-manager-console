using System;
using Amazon.S3.Model;  
using System.Collections.Generic;

namespace s3_example.Models
{
    public class S3Obj
    {
        public string keyName {get; set;}
        public string imgUrl {get; set;}
        public List<S3ObjectVersion> versions {get; set;}
    }
}


