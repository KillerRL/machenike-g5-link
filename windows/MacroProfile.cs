namespace G5Link;

internal sealed record MacroCatalog(byte[] Keys,byte SlotMask,bool Extended,byte[] Capabilities);
internal sealed record MacroSlot(int Index,byte[] Data);

internal sealed partial class BleClient
{
    public async Task<MacroCatalog> ReadMacroCatalog()
    {
        var packet=await Exchange(Protocol.Build(0xB0,[0,1])); // z1.h.o -> b1.F(1) -> e.t(1).
        var caps=packet.AsSpan(4,packet.Length-5).ToArray();
        if(caps.Length<9)throw new IOException("Capacidades de macro incompletas.");
        byte[] support=await ReadBlob(0xB0,i=>[i,5],128);
        if(support.Length<2)throw new IOException("Lista de botões de macro indisponível.");
        var keys=new List<byte>();
        foreach(byte item in support.Skip(1))
        {
            if(item<128)keys.Add(item);
            else
            {
                if(keys.Count==0)throw new IOException("Lista macro comprimida inválida.");
                for(int n=1;n<(item&127);n++)keys.Add(checked((byte)(keys[^1]+1)));
            }
        }
        return new(keys.ToArray(),caps[5],(caps[8]&2)!=0,caps);
    }
    public async Task<MacroSlot> ReadMacro(int slot)
    {
        if(slot<0||slot>7)throw new ArgumentOutOfRangeException(nameof(slot));
        var data=await ReadBlob(0xB5,i=>[i,(byte)slot],255);
        // Keep the exact length-delimited bytes even when they do not match the legacy editor.
        // The device's existing macros must not be normalized or silently truncated.
        return new(slot,data);
    }
    public async Task SaveMacro(MacroCatalog catalog,MacroSlot expected,byte[] data,bool restoreTestEmpty=false)
    {
        if(catalog.Extended||(catalog.SlotMask&(1<<expected.Index))==0)throw new IOException("Slot/formato macro não suportado.");
        if(data.Length!=0&&(data.Length<2||(data.Length-2)%5!=0||data.Length>237))throw new ArgumentException("Formato macro inválido ou mais de 47 passos.");
        var before=await ReadMacro(expected.Index);
        if(!before.Data.SequenceEqual(expected.Data))throw new IOException("A macro mudou desde a leitura; leia novamente.");
        // Never overwrite an existing sequence whose format has not been decoded.
        if(expected.Data.Length!=0&&!MacroCodec.TryDecode(catalog,expected.Data,out _,out _,out _)&&!(restoreTestEmpty&&data.Length==0))
            throw new IOException("A macro existente tem formato desconhecido; gravação bloqueada.");
        byte[] payload=[(byte)data.Length,..data];
        for(int offset=0,index=0;offset<payload.Length;offset+=15,index++)
            await Exchange(Protocol.BuildMacroChunk(payload.Skip(offset).Take(15).ToArray(),index,expected.Index));
        if(!(await ReadMacro(expected.Index)).Data.SequenceEqual(data))throw new IOException("Macro não confirmada por releitura.");
    }
}
