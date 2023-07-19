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
    private uint _sectionAddress = 0;
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

    /*
    * 只有呼叫這個 method 才會開始讀取 stream，並且把讀到的資料流進 targetStream
    * 最終 _pipeReader stream 會讀完(流完)，此 stream 會變空，並且關閉
    */
    public async Task pipeToAsync(Stream targetStream)
    {
        ReadResult result;
        ReadOnlySequence<byte> buffer = new ReadOnlySequence<byte>();
        while (true)
        {
            result = await _pipeReader.ReadAsync();
            buffer = result.Buffer;

            // 把 buffer 轉成 string，蒐集每一行 hex string
            // 把上次剩下的最後一行加上這次讀到的 buffer，再用 \r\n \r \n 切開
            _lines = (_lastLine + Encoding.UTF8.GetString(buffer)).Split(_breaks, StringSplitOptions.None);

            // 類似 flush 的概念，檢查 buffer 是否有剩下的資料，如果沒有就把最後一行清空
            if(buffer.Length == 0)
            {
                _lastLine = "";
            }
            else {
                // 最後一行可能不完整，先留著
                _lastLine = _lines[_lines.Length - 1];
                // 把最後一行拿掉，留到下一次讀取時再處理
                _lines = _lines.Take(_lines.Length - 1).ToArray();
            }

            //原封不動把這次讀到的 buffer 流進 targetStream
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
                            _sectionAddress = (uint)BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 4; // 位移 4 bit
                            break;
                        case 4:
                            _sectionAddress = (uint)BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[4..6]) << 16; // 位移 16 bit = 2 byte
                            break;

                        case 0:
                            _lineRange.startAddress = _sectionAddress + BinaryPrimitives.ReadUInt16BigEndian(lineBuffer[1..3]);
                            _lineRange.endAddress = _lineRange.startAddress + lineBuffer[0];

                            foreach (Partition partition in _partitions)
                            {
                                // 不在 partition 範圍內，跳過
                                if (partition.endAddress < _lineRange.startAddress || partition.startAddress >= _lineRange.endAddress) continue;

                                uint validStartAddress = Math.Max(partition.startAddress, _lineRange.startAddress) - _lineRange.startAddress + 4;
                                // +1 是因為 endAddress 是包含的，但是我們要的是不包含的，ex: 0x0000 ~ 0x0003，我們要的是 0x0000 ~ 0x0002
                                uint validEndAddress = Math.Min(partition.endAddress + 1, _lineRange.endAddress) - _lineRange.startAddress + 4;

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

            // 檢查是否已經讀完
            if (result.IsCompleted)
            {
                break;
            }

            // 紀錄讀取進度，下一個 ReadAsync 會從這裡開始讀
            // block by block 讀取，不會有 memory 不足的問題
            _pipeReader.AdvanceTo(buffer.End);
        }

        // 確認讀取完畢，並且把 stream 關閉，這樣最後一個 buffer 才會被釋放，寫入 targetStream
        await _pipeReader.CompleteAsync();
        // 關閉 targetStream
        targetStream.Close();
    }
}