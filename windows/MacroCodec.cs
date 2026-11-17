namespace G5Link;

internal sealed record MacroStep(uint State,int Milliseconds);

internal static class MacroCodec
{
    public static Dictionary<byte,int> Positions(MacroCatalog catalog)
    {
        int offset=0;var result=new Dictionary<byte,int>();
        foreach(byte key in catalog.Keys)
        {
            if(result.ContainsKey(key))throw new IOException("Lista macro duplicada.");
            result[key]=offset;offset+=key is 18 or 19?4:1;
        }
        if(offset>24)throw new IOException("Estado macro excede 24 bits.");
        return result;
    }
    public static uint Neutral(MacroCatalog catalog)
    {
        uint result=0;foreach(var pair in Positions(catalog))if(pair.Key is 18 or 19)result|=8u<<pair.Value;return result;
    }
    public static uint State(MacroCatalog catalog,IEnumerable<byte> keys)
    {
        var positions=Positions(catalog);uint state=Neutral(catalog);
        foreach(byte key in keys)
        {
            if(key is 18 or 19||!positions.TryGetValue(key,out int offset))throw new ArgumentException("Botão não suportado na macro digital.");
            state|=1u<<offset;
        }
        return state;
    }
    public static byte[] Encode(MacroCatalog catalog,IReadOnlyList<MacroStep> steps,bool repeat,int interval)
    {
        if(steps.Count==0)return [];
        if(steps.Count>47||interval<0||interval>20475||interval%5!=0)throw new ArgumentException("Até 47 passos; intervalo entre repetições deve ser múltiplo de 5 ms, até 20475 ms.");
        var data=new List<byte>{(byte)((interval/5)&255),(byte)(((interval/5)>>8)|(repeat?128:0))};
        foreach(var step in steps)
        {
            if(step.Milliseconds<5||step.Milliseconds>327675||step.Milliseconds%5!=0||step.State>0xFFFFFF)throw new ArgumentException("Duração dos passos: múltiplos de 5 ms, entre 5 e 327675 ms.");
            int ticks=step.Milliseconds/5;
            data.AddRange([(byte)step.State,(byte)(step.State>>8),(byte)(step.State>>16),(byte)ticks,(byte)(ticks>>8)]);
        }
        return data.ToArray();
    }
    public static bool TryDecode(MacroCatalog catalog,byte[] data,out List<MacroStep> steps,out bool repeat,out int interval)
    {
        steps=[];repeat=false;interval=0;if(data.Length==0)return true;
        if(data.Length<2||(data.Length-2)%5!=0||data.Length>237||(data[1]&0x70)!=0)return false;
        repeat=(data[1]&128)!=0;interval=((data[1]&15)*256+data[0])*5;
        var positions=Positions(catalog);uint allowed=0;
        foreach(var p in positions)allowed|=(p.Key is 18 or 19?15u:1u)<<p.Value;
        for(int offset=2;offset<data.Length;offset+=5)
        {
            uint state=(uint)(data[offset]|data[offset+1]<<8|data[offset+2]<<16);
            if((state&~allowed)!=0)return false;
            foreach(var p in positions)if(p.Key is 18 or 19&&((state>>p.Value)&15)!=8)return false;
            int milliseconds=(data[offset+3]|data[offset+4]<<8)*5;if(milliseconds==0)return false;
            steps.Add(new(state,milliseconds));
        }
        return true;
    }
}
