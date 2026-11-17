namespace G5Link;

internal sealed record ControlProfile(byte[] SupportedKeys, byte[] Mapping, byte[] Sticks);

internal sealed partial class BleClient
{
    async Task<byte[]> ReadBlob(byte command, Func<byte,byte[]> arguments, int maxSize)
    {
        var packet=await Exchange(Protocol.Build(command,arguments(0)));
        var payload=packet.AsSpan(4,packet.Length-5).ToArray();
        if (payload.Length==0 || payload[0]>maxSize) throw new IOException("Comprimento de configuração inválido.");
        int size=payload[0]; var data=payload.Skip(1).ToList();
        for (byte index=1;data.Count<size;index++)
        {
            if (index>16) throw new IOException("Fragmentação não suportada.");
            packet=await Exchange(Protocol.Build(command,arguments(index)));
            data.AddRange(packet.AsSpan(4,packet.Length-5).ToArray());
        }
        if (data.Count!=size) throw new IOException("Configuração com comprimento inconsistente.");
        return data.ToArray();
    }
    public async Task<ControlProfile> ReadControls()
    {
        // HostActivity.Y(): first length-delimited data byte is metadata, remaining bytes use range encoding.
        byte[] supported=await ReadBlob(0xB0,i=>[i,3],128);
        if (supported.Length<2) throw new IOException("Lista de botões não disponível.");
        var keys=new List<byte>();
        foreach (byte value in supported.Skip(1))
        {
            if (value<128) keys.Add(value);
            else
            {
                if (keys.Count==0) throw new IOException("Lista comprimida de botões inválida.");
                for (int n=1;n<(value&127);n++) keys.Add(checked((byte)(keys[^1]+1)));
            }
        }
        if (keys.Count==0 || keys.Count>64 || keys.Distinct().Count()!=keys.Count) throw new IOException("Lista de botões não suportada.");
        byte[] mapping=await ReadBlob(0xB6,i=>[i],64);
        if (mapping.Length==0) mapping=keys.ToArray();
        else
        {
            if (mapping.Length!=keys.Count) throw new IOException("Mapeamento incompatível com lista de botões.");
            for(int i=0;i<mapping.Length;i++) if(mapping[i]==0) mapping[i]=keys[i];
        }
        byte[] sticks=await ReadBlob(0xB1,i=>[i],14);
        if(sticks.Length!=14) throw new IOException("Layout dos analógicos não suportado.");
        return new(keys.ToArray(),mapping,sticks);
    }
    async Task WriteBlob(byte command, byte[] data)
    {
        byte[] payload=[(byte)data.Length,..data];
        for(int offset=0,index=0;offset<payload.Length;offset+=15,index++)
            await Exchange(Protocol.BuildChunk(command,payload.Skip(offset).Take(15).ToArray(),index));
    }
    public async Task SaveMapping(ControlProfile expected, byte[] mapping)
    {
        if(mapping.Length!=expected.SupportedKeys.Length || mapping.Any(k=>!expected.SupportedKeys.Contains(k)))
            throw new ArgumentException("Mapeamento contém botão não suportado.");
        var before=await ReadControls();
        if(!before.SupportedKeys.SequenceEqual(expected.SupportedKeys) || !before.Mapping.SequenceEqual(expected.Mapping))
            throw new IOException("O mapeamento mudou desde a leitura. Leia novamente antes de aplicar.");
        byte[] data=mapping.Select((k,i)=>(byte)(k==expected.SupportedKeys[i]?0:k)).ToArray();
        // util.b1.w1 / util.e.k0 (0x36). Empty length restores default identity mapping.
        if(data.All(k=>k==0)) await Exchange(Protocol.BuildChunk(0x36,[0],0));
        else await WriteBlob(0x36,data);
        var after=await ReadControls();
        if(!after.Mapping.SequenceEqual(mapping)) throw new IOException("Mapeamento não confirmado por releitura.");
    }
    public async Task SaveSticks(ControlProfile expected, byte[] data)
    {
        if(data.Length!=14) throw new ArgumentException("Layout de analógicos inválido.");
        var before=await ReadControls();
        if(!before.Sticks.SequenceEqual(expected.Sticks)) throw new IOException("Os analógicos mudaram desde a leitura. Leia novamente.");
        await WriteBlob(0x31,data); // HostActivity.R9 -> b1.D1 -> e.o0.
        var after=await ReadControls();
        if(!after.Sticks.SequenceEqual(data)) throw new IOException("Analógicos não confirmados por releitura.");
    }
}
