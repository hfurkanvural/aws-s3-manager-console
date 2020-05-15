using System;
using Amazon.S3.Model;  
using System.Collections.Generic;

namespace s3_example.Models
{
    public class S3Obj
    {
        public string keyName {get; set;}
        public string imgUrl {get; set;}
        public long size {get; set;}
        public DateTime uploadDate {get; set;}
        public List<S3ObjectVersion> versions {get; set;}
    }
}


