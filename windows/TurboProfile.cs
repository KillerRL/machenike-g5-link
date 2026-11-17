namespace G5Link;

internal sealed record TurboProfile(byte[] Keys,byte[] AutoKeys,byte[] Data);

internal sealed partial class BleClient
{
    public async Task<TurboProfile> ReadTurbo()
    {
        // HostActivity.L / b1.J(index,4) -> e.y(index,4), then I -> e.w/B4.
        byte[] support=await ReadBlob(0xB0,i=>[i,4],128);
        int position=0;
        byte[] ReadList()
        {
            if(position>=support.Length)throw new IOException("Lista turbo incompleta.");
            int count=support[position++];if(count>24)throw new IOException("Turbo com mais de 24 botões não suportado.");
            var keys=new List<byte>();
            while(keys.Count<count)
            {
                if(position>=support.Length)throw new IOException("Lista turbo truncada.");
                byte item=support[position++];
                if(item<128)keys.Add(item);
                else
                {
                    if(keys.Count==0||(item&127)<2)throw new IOException("Intervalo turbo inválido.");
                    for(int n=1;n<(item&127);n++)keys.Add(checked((byte)(keys[^1]+1)));
                }
                if(keys.Count>count)throw new IOException("Intervalo turbo excede a lista.");
            }
            if(keys.Distinct().Count()!=count)throw new IOException("Lista turbo com códigos duplicados.");
            return keys.ToArray();
        }
        byte[] manual=ReadList(),auto=ReadList();
        if(position!=support.Length||auto.Any(k=>!manual.Contains(k)))throw new IOException("Lista de suporte turbo não reconhecida.");
        byte[] data=await ReadBlob(0xB4,i=>[i],13);
        if(data.Length!=13)throw new IOException("Layout turbo não suportado.");
        return new(manual,auto,data);
    }
    public async Task SaveTurbo(TurboProfile expected,byte[] data)
    {
        if(data.Length!=13||data[0]==0)throw new ArgumentException("Configuração turbo inválida.");
        var before=await ReadTurbo();
        if(!before.Keys.SequenceEqual(expected.Keys)||!before.AutoKeys.SequenceEqual(expected.AutoKeys)||!before.Data.SequenceEqual(expected.Data))
            throw new IOException("Turbo mudou desde a leitura; leia novamente.");
        await WriteBlob(0x34,data); // HostActivity.S9 -> b1.y1 -> e.p0.
        if(!(await ReadTurbo()).Data.SequenceEqual(data))throw new IOException("Turbo não confirmado por releitura.");
    }
}
