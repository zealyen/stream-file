using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;

public class HexStreamTransformer
{
    private PipeReader _pipeReader { get; set; }
    private string _lastLine = ""; // 上次讀取時剩下的最後一行
    private string[] _lines = new string[0];
    private string[] _breaks = new string[] { "\r\n", "\r", "\n" };
    private long _sectionAddress = 0;
    private LineRange _lineRange = new LineRange();
    public ushort crcValue = 0xFFFF; // 用 ushort 因為 crc16 結果不會超過 0xFFFF
    private readonly Partition[] _partitions;

    /*
    * partitions 是 hex file 合法區間的內容
    */
    public HexStreamTransformer(Stream stream, Partition[] partitions)
    {
        _pipeReader = PipeReader.Create(stream);
        _partitions = partitions;
    }

    public async Task pipeToAsync(Stream targetStream)
    {
        while (true)
        {
            ReadResult result = await _pipeReader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            // 檢查是否已經讀完
            if (result.IsCompleted)
            {
                break;
            }

            // 把 buffer 轉成 string，蒐集每一行 hex string
            // 把上次剩下的最後一行加上這次讀到的 buffer，再用 \r\n \r \n 切開
            _lines = (_lastLine + Encoding.UTF8.GetString(buffer)).Split(_breaks, StringSplitOptions.None);
            // 最後一行可能不完整，先留著
            _lastLine = _lines[_lines.Length - 1];
            // 把最後一行拿掉，留到下一次讀取時再處理
            _lines = _lines.Take(_lines.Length - 1).ToArray();

            await targetStream.WriteAsync(buffer.ToArray());

            foreach (string line in _lines)
            {
                /**
                * section 處理
                * 02 0000 04 0001 F9
                * 02 這一行的 [dd...] 有多少 byte
                *    0000 這一行的 [addr...] 是多少
                *         04 這一行的 [type...] 是多少, 00 代表資料, 02 & 04 代表一個 section 的開頭
                *            0001 這一行的 [data...] 是多少, 這一行的資料是 0001
                *                F9 這一行的 [crc...] 是多少
                */

                /**
                * 一般資料處理
                * 20 4000 00 0070002011440100794401007944010079440100794401007944010000000000 04
                * 20 這一行的 [dd...] 有多少 byte
                *    4000 這一行的 [addr...] 是多少
                *         00 這一行的 [type...] 是多少, 00 代表資料, 04 代表一個 section 的開頭
                *            0070002011440100794401007944010079440100794401007944010000000000 這一行的 [data...] 是多少
                *                04 這一行的 [crc...] 是多少
                */
                try
                {
                    if (line == "") continue;

                    byte[] lineBuffer = ByteConvert.convertHexStringToBytes(line.Substring(1));

                    switch (lineBuffer[3])
                    {
                        case 2:
                            _sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 4; // 位移 4 bit
                            break;
                        case 4:
                            _sectionAddress = BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 16; // 位移 16 bit = 2 byte
                            break;

                        case 0:
                            _lineRange.startAddress = _sectionAddress + BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[1..3]);
                            _lineRange.endAddress = _lineRange.startAddress + lineBuffer[0];

                            foreach (Partition partition in _partitions)
                            {
                                // 不在 partition 範圍內，跳過
                                if (partition.endAddress < _lineRange.startAddress || partition.startAddress >= _lineRange.endAddress) continue;

                                long validStartAddress = Math.Max(partition.startAddress, _lineRange.startAddress) - _lineRange.startAddress + 4;
                                long validEndAddress = Math.Min(partition.endAddress + 1, _lineRange.endAddress) - _lineRange.startAddress + 4;

                                byte[] validData = lineBuffer[(int)validStartAddress..(int)validEndAddress];
                                string hex = Convert.ToHexString(validData);

                                // 計算 crc，這裡可以更換不同的 crc 算法
                                // crcValue 是上一次的結果，不斷的累加
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

            // 紀錄讀取進度，下一個 ReadAsync 會從這裡開始讀
            // block by block 讀取，不會有 memory 不足的問題
            _pipeReader.AdvanceTo(buffer.End);
        }
    }
}