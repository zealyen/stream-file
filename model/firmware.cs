public class LineRange {
    public uint startAddress { get; set;}
    public uint endAddress { get; set;}
}

public class Partition {
    // 用 uint 是因為 unit32 的最大值是 0xFFFFFFFF
    public uint startAddress { get; set;}
    public uint endAddress { get; set;}
}
