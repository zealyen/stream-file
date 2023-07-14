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
    public string Post([FromForm]IFormFile file)
    {
        _logger.LogInformation("upload called");
       
        var filePath = Path.GetTempFileName();

        using (var stream = System.IO.File.Create(filePath))
        {
            file.CopyTo(stream);
        }

        return $"got file: {file.FileName}";
    }
}