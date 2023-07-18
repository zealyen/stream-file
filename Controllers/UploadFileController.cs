using Microsoft.AspNetCore.Mvc;

namespace stream_file.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadFileController : ControllerBase
{

    private readonly ILogger<UploadFileController> _logger;

    public UploadFileController(ILogger<UploadFileController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IEnumerable<string> Get()
    {
        _logger.LogInformation("Get called");
        return new string[] { "value1", "value2" };
    }

    [HttpPost]
    public async Task<string> Post([FromForm] IFormFile file, [ModelBinder(BinderType = typeof(FormDataJsonBinder))] Partition[]? partitions)
    {
        try
        {
            Stream stream = file.OpenReadStream();

            // 如果沒有指定 partitions，就用預設的
            // 0xFFFFFFFF 是 32-bit 的最大值，因為 0xFFFFFFFF 會被當成結束的標記，要多扣掉每一行的最大 byte 數 0xFF
            partitions ??= new Partition[] { new Partition() { startAddress = 0x0, endAddress = 0xFFFFFEFF } };

            HexStreamTransformer hexStreamTransformer = new HexStreamTransformer(stream, partitions);

            string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "upload-files");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            string filePath = Path.Combine(uploadsFolder, $"{DateTime.Now.ToString("yyyyMMddHHmmss")}.hex");

            // 這個 stream 是用來存 hex file 的
            FileStream targetStream = System.IO.File.Create(filePath);

            await hexStreamTransformer.pipeToAsync(targetStream);

            return $"crc value: {hexStreamTransformer.crcValue}";
        }
        catch (System.Exception ex)
        {
            throw ex;
        }
    }
}


