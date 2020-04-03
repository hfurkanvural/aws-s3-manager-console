# AWS-S3-Manager-Console
This repository created for a Simple AWS S3 Bucket Console site. Uses AWS .NET SDK and .NET Core 3.1. You can run this program by cloning this repository and following the instructions below.

## Requirements
To run this program, you need .NET Core 3.1 in your environment. If you don't have, please see [here](https://dotnet.microsoft.com/download) to install.

Also you need to have an AWS account and should create an IAM user with `AmazonS3FullAccess` policy. Then, add following fields to `appsettings.json` with necessary credentials. Also, change the Region in the HomeController.cs if needed.

```json
  "S3Config": {
    "accesskey": "accessKey",
    "secretkey": "secretKey",
    "bucketName": "bucketname",
    "url": "domainurl"
  }
 ```


## Usage
Run following command to run project.
```bash
dotnet run
```
App will run on `localhost:5001`. 


![Welcome Page](https://github.com/hfurkanvural/aws-s3-manager-console/blob/master/indexScreen.png)

