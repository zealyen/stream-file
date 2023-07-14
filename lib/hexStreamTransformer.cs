using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;

public class HexStreamTransformer
{
    private PipeReader _pipeReader { get; set; }
    private string _lastLine = "";
    private string[] _lines = new string[0];
    private string[] _breaks = new string[] { "\r\n", "\r", "\n" };
    private long _sectionAddress = 0;

    private LineRange _lineRange = new LineRange();

    public ushort crcValue = 0xFFFF; // 用 ushort 因為最大值 0xFFFF，crc16 結果不會超過 0xFFFF

    private readonly ILogger<HexStreamTransformer> _logger;

    private readonly Partition[] _partitions;

    public HexStreamTransformer(Stream stream, Partition[] partitions, ILogger<HexStreamTransformer> logger)
    {
        _pipeReader = PipeReader.Create(stream);
        _logger = logger;
        _partitions = partitions;
    }

    public async Task pipeToAsync()
    {
        while (true)
        {
            ReadResult result = await _pipeReader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (result.IsCompleted)
            {
                break;
            }

            _lines = (_lastLine + Encoding.UTF8.GetString(buffer)).Split(_breaks, StringSplitOptions.None);
            _lastLine = _lines[_lines.Length - 1];
            _lines = _lines.Take(_lines.Length - 1).ToArray();

            // await targetStream.WriteAsync(buffer.ToArray());

            foreach (string line in _lines)
            {
                try
                {
                    if (line == "") continue;

                    byte[] lineBuffer = ByteConvert.convertHexStringToBytes(line.Substring(1));

                    switch (lineBuffer[3])
                    {
                        case 2:
                            _sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 4;
                            break;
                        case 4:
                            _sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 16;
                            break;

                        case 0:
                            _lineRange.startAddress = _sectionAddress + BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[1..3]);
                            _lineRange.endAddress = _lineRange.startAddress + lineBuffer[0];

                            foreach (Partition partition in _partitions)
                            {
                                if (partition.endAddress < _lineRange.startAddress || partition.startAddress >= _lineRange.endAddress) continue;

                                long validStartAddress = Math.Max(partition.startAddress, _lineRange.startAddress) - _lineRange.startAddress + 4;
                                long validEndAddress = Math.Min(partition.endAddress + 1, _lineRange.endAddress) - _lineRange.startAddress + 4;

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
                    // _logger.LogError(ex.Message);
                    throw ex;
                }
            }

            // Tell the PipeReader how much of the buffer we have consumed
            _pipeReader.AdvanceTo(buffer.End);
        }
    }
}