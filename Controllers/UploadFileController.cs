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
       
        PipeReader pipeReader = PipeReader.Create(file.OpenReadStream());
        LineRange lineRange = new LineRange();

        string lastLine = "";
        string[] lines = new string[0];
        string[] breaks = new string[] { "\r\n", "\r", "\n" };
        long sectionAddress = 0;
        ushort crcValue = 0xFFFF; // 用 ushort 因為最大值 0xFFFF，crc16 結果不會超過 0xFFFF

        while(true) {
            ReadResult result = await pipeReader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (result.IsCompleted) {
                break;
            }
            
            lines = (lastLine + Encoding.UTF8.GetString(buffer)).Split(breaks, StringSplitOptions.None);

            lastLine = lines[lines.Length - 1];
            lines = lines.Take(lines.Length - 1).ToArray();

            foreach (var line in lines) {
                try
                {
                    if(line == "") continue; 

                    byte[] lineBuffer = ByteConvert.convertHexStringToBytes(line.Substring(1));
                    switch (lineBuffer[3])
                    {
                        case 2:
                            sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 4;
                            break;
                        case 4:
                            sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 16;
                            break;
                        
                        case 0:
                            lineRange.startAddress = sectionAddress + BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[1..3]);
                            lineRange.endAddress = lineRange.startAddress + lineBuffer[0];

                            foreach (Partition partition in partitions)
                            {
                                if(partition.endAddress < lineRange.startAddress || partition.startAddress >= lineRange.endAddress) continue;

                                long validStartAddress = Math.Max(partition.startAddress, lineRange.startAddress) - lineRange.startAddress + 4;
                                long validEndAddress = Math.Min(partition.endAddress + 1, lineRange.endAddress) - lineRange.startAddress + 4;

                                byte[] validData = lineBuffer[(int)validStartAddress..(int)validEndAddress];
                                string hex = Convert.ToHexString(validData);
                                
                                crcValue = Crc16Modbus.ComputeChecksum(validData, crcValue);

                                break;
                            }

                            break;
                    }
                }
                catch (System.Exception ex)
                {
                    throw ex;
                }
                
            }

            // Tell the PipeReader how much of the buffer we have consumed
            pipeReader.AdvanceTo(buffer.End);
        } 

        return $"got file: {crcValue}";
    }
}


