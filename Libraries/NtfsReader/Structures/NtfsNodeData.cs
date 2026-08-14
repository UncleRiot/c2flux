namespace System.IO.Filesystem.Ntfs;

/// <summary>
/// Compact value representation of a named NTFS MFT node.
/// </summary>
public readonly struct NtfsNodeData
{
    public NtfsNodeData(
        uint nodeIndex,
        uint parentNodeIndex,
        Attributes attributes,
        string name,
        ulong size,
        ulong lastChangeTimeFileTimeUtc)
    {
        NodeIndex = nodeIndex;
        ParentNodeIndex = parentNodeIndex;
        Attributes = attributes;
        Name = name;
        Size = size;
        LastChangeTimeFileTimeUtc = lastChangeTimeFileTimeUtc;
    }

    public uint NodeIndex { get; }

    public uint ParentNodeIndex { get; }

    public Attributes Attributes { get; }

    public string Name { get; }

    public ulong Size { get; }

    public ulong LastChangeTimeFileTimeUtc { get; }
}
