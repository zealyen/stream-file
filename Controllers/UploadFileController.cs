using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
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
    public async Task<string> Post([FromForm]IFormFile file, [ModelBinder(BinderType = typeof(FormDataJsonBinder))]Partition[] partitions)
    {
        _logger.LogInformation("upload called");
       
        var stream = file.OpenReadStream();
        
        HexStreamTransformer hexStreamTransformer = new HexStreamTransformer(stream, partitions);

        FileStream targetStream = System.IO.File.Create("test.hex");

        await hexStreamTransformer.pipeToAsync(targetStream);

        return $"crc value: {hexStreamTransformer.crcValue}";
    }
}


