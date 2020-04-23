using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Transfer;
using Net.Codecrete.QrCodeGenerator;
using QRCoder;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Petzfinder.QRGenerator
{
    public class Function
    {
        private const string bucketName = "petzfinderqr";
        // Specify your bucket region (an example region is shown).
        private static readonly RegionEndpoint bucketRegion = RegionEndpoint.USWest2;
        private static IAmazonS3 s3Client;

        /// <summary>
        /// A simple function that takes a string and does a ToUpper
        /// </summary>
        /// <param name="input"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(string input, ILambdaContext context)
        {

            s3Client = new AmazonS3Client(bucketRegion);
            var fileTransferUtility = new TransferUtility(s3Client);

            var dbClient = new AmazonDynamoDBClient();
            var request = new ScanRequest
            {
                TableName = "Tag",
                ProjectionExpression = "tagId",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    { ":catg", new AttributeValue { BOOL = false } }
                },
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    { "#printed", "printed" }
                },
                FilterExpression = "#printed = :catg"
            };

            var response = await dbClient.ScanAsync(request);

            foreach (var item in response.Items)
            {
                var value = new AttributeValue();
                item.TryGetValue("tagId", out value);
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode($"http://petzfinder/tags/{ value.S }", QRCodeGenerator.ECCLevel.Q); ;
                QRCode qrCode = new QRCode(qrCodeData);
                Bitmap bitmap = qrCode.GetGraphic(20);
                //var stream = new MemoryStream();
                //var qr = QrCode.EncodeText($"http://petzfinder/tags/{ value.S }", QrCode.Ecc.Medium);
                //using (var bitmap = qr.ToBitmap(4, 10))
                //{
                //    bitmap.Save("qr-code.png", ImageFormat.Png);
                //    stream = BitmapToStream(bitmap);
                //}
                try
                {
                    var stream = BitmapToStream(bitmap);
                    await fileTransferUtility.UploadAsync(stream,
                                               bucketName, value.S);
                }
                catch (AmazonS3Exception e)
                {
                    Console.WriteLine("Error encountered on server. Message:'{0}' when writing an object", e.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unknown encountered on server. Message:'{0}' when writing an object", e.Message);
                }
            }


        }

        private static MemoryStream BitmapToStream(Image img)
        {
            using (var stream = new MemoryStream())
            {
                img.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream;
            }
        }
    }
}
