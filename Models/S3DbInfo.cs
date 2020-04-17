using System;
using Amazon.S3.Model;  
using System.Collections.Generic;

namespace s3_example.Models
{
    public class S3DbInfo
    {
        public string keyName {get; set;}
        public string extension {get; set;}
        public int size {get; set;}
        public string uploadDate{get; set;} 
        //public List<string> versions {get; set;}
        
    }
}

