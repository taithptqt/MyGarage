using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;

var root=Directory.GetCurrentDirectory();
var source=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(root,"design-reference/ui-reverse/mirror-cache"));
var config=new ConfigurationBuilder().SetBasePath(Path.Combine(root,"backend/src/MyGarage.Api"))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json",optional:true).AddEnvironmentVariables().Build();
var connection=config["AzureStorage:ConnectionString"];
if(string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("AzureStorage connection is not configured.");
var container=new BlobContainerClient(connection,config["AzureStorage:ContainerName"]??"uploads");
await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
var records=new List<(string Url,string Type,string File,long Bytes,string Key)>();
foreach(var file in Directory.EnumerateFiles(source,"*.json"))
{
    using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(file));
    var node=doc.RootElement;
    if(!node.TryGetProperty("type",out var typeNode)||!node.TryGetProperty("url",out var urlNode))continue;
    var type=typeNode.GetString()!.Split(';')[0];
    if(!(type.StartsWith("image/")||type.StartsWith("video/")||type.StartsWith("font/")||type=="application/font-woff"))continue;
    var data=Path.Combine(source,Path.GetFileNameWithoutExtension(file));
    if(File.Exists(data))records.Add((urlNode.GetString()!,type,data,new FileInfo(data).Length,Path.GetFileName(data)));
}
var publicRoot=Path.Combine(root,"Frontend/public");
foreach(var data in Directory.EnumerateFiles(publicRoot,"*",SearchOption.AllDirectories))
{
    var type=Path.GetExtension(data).ToLowerInvariant() switch {".jpg" or ".jpeg"=>"image/jpeg",".png"=>"image/png",".mp4"=>"video/mp4",".webp"=>"image/webp",".svg"=>"image/svg+xml",_=>null};
    if(type==null)continue;
    await using var file=File.OpenRead(data);
    var hash=Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(file)).ToLowerInvariant();
    records.Add(("/"+Path.GetRelativePath(publicRoot,data).Replace('\\','/'),type,data,file.Length,hash));
}
Console.WriteLine($"Uploading {records.Count} media/font resources ({records.Sum(x=>x.Bytes)/1024/1024} MiB). Existing blobs are reused.");
var manifest=new ConcurrentDictionary<string,string>();
var checkpoint=Path.Combine(root,".dev-runtime/ui-assets-checkpoint.json");
Directory.CreateDirectory(Path.GetDirectoryName(checkpoint)!);
if(File.Exists(checkpoint))foreach(var item in JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(checkpoint))!)manifest[item.Key]=item.Value;
var finished=0;
await Parallel.ForEachAsync(records,new ParallelOptions{MaxDegreeOfParallelism=6},async(item,ct)=>
{
    var extension=item.Type switch {"image/jpeg"=>".jpg","image/png"=>".png","image/webp"=>".webp","image/svg+xml"=>".svg","video/mp4"=>".mp4","font/ttf"=>".ttf","font/otf"=>".otf","application/font-woff"=>".woff",_=>""};
    var blob=container.GetBlobClient("site-ui-reverse/"+item.Key+extension);
    if(!manifest.TryGetValue(item.Url,out var previousUrl)||previousUrl!=blob.Uri.ToString())
    {
        if(!await blob.ExistsAsync(ct))
        {
            await using var stream=File.OpenRead(item.File);
            await blob.UploadAsync(stream,new BlobUploadOptions {HttpHeaders=new BlobHttpHeaders{ContentType=item.Type,CacheControl="public,max-age=31536000,immutable"},TransferOptions=new StorageTransferOptions{MaximumConcurrency=2,MaximumTransferSize=8*1024*1024}},ct);
        }
        manifest[item.Url]=blob.Uri.ToString();
    }
    var count=Interlocked.Increment(ref finished);
    if(count%100==0)Console.WriteLine($"Media progress: {count}/{records.Count}");
});
await File.WriteAllTextAsync(checkpoint,JsonSerializer.Serialize(manifest));
var output=Path.Combine(root,"Frontend/src/shared/brand/media-manifest.json");
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await File.WriteAllTextAsync(output,JsonSerializer.Serialize(manifest.OrderBy(x=>x.Key).ToDictionary(),new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Uploaded/verified {manifest.Count} resources. Public URL manifest written.");
