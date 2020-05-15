using System;
using s3_example.Models;
using System.Collections.Generic;

namespace s3_example.Models
{
    public class S3ManagerViewModel
    {
        public string VersioningStatus { get; set; }

        public List<S3Obj> Objects {get; set;}
    }
}
