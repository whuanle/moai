//using FastEndpoints;

//namespace MoAI.Storage.Endpoints;

///// <summary>
///// 兼容 S3 接口的文件上传端点.
///// </summary>
//[FastEndpoints.HttpPut($"{ApiPrefix.Prefix}/private/upload")]
//public class UploadPrivateFileEndpoint : Endpoint<AAA>
//{
//    public override async Task HandleAsync(AAA aaa, CancellationToken ct)
//    {
//        if (Files.Count > 0)
//        {
//            var file = Files[0];

//            await SendStreamAsync(
//                stream: file.OpenReadStream(),
//                fileName: "test.png",
//                fileLengthBytes: file.Length,
//                contentType: "image/png");

//            return;
//        }
//        var url = base.BaseURL;

//        await foreach (var section in FormFileSectionsAsync(ct))
//        {
//            if (section is not null)
//            {
//                using (var fs = System.IO.File.Create(section.FileName))
//                {
//                    await section.Section.Body.CopyToAsync(fs, 1024 * 64, ct);
//                }
//            }
//        }

//        await SendOkAsync("upload complete!");
//    }
//}
