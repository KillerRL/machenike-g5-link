namespace G5Link;

internal sealed record ActuatorProfile(byte[] Triggers,byte[] Motors);

internal sealed partial class BleClient
{
    public async Task<ActuatorProfile> ReadActuators()
    {
        // e.z/B2 -> HostActivity.z/Ya; e.u/B3 -> HostActivity.N/Wa.
        var triggers=await ReadBlob(0xB2,i=>[i],14);
        var motors=await ReadBlob(0xB3,i=>[i],8);
        if(triggers.Length!=14||motors.Length!=8)throw new IOException("Layout de gatilhos/vibração não suportado.");
        return new(triggers,motors);
    }
    public async Task SaveTriggers(ActuatorProfile expected,byte[] data)
    {
        if(data.Length!=14)throw new ArgumentException("Dados de gatilho inválidos.");
        var current=await ReadActuators();
        if(!current.Triggers.SequenceEqual(expected.Triggers))throw new IOException("Gatilhos mudaram desde a leitura; leia novamente.");
        await WriteBlob(0x32,data); // HostActivity.T9 -> b1.E1 -> e.q0.
        if(!(await ReadActuators()).Triggers.SequenceEqual(data))throw new IOException("Gatilhos não confirmados por releitura.");
    }
    public async Task SaveMotors(ActuatorProfile expected,byte[] data)
    {
        if(data.Length!=8)throw new ArgumentException("Dados de vibração inválidos.");
        var current=await ReadActuators();
        if(!current.Motors.SequenceEqual(expected.Motors))throw new IOException("Vibração mudou desde a leitura; leia novamente.");
        await WriteBlob(0x33,data); // HostActivity.Q9 -> b1.C1 -> e.n0.
        if(!(await ReadActuators()).Motors.SequenceEqual(data))throw new IOException("Vibração não confirmada por releitura.");
    }
}
