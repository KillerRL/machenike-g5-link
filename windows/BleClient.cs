using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace G5Link;

internal sealed partial class BleClient : IDisposable
{
    public static readonly Guid ServiceId = new("d7f010e0-660d-46e9-96c3-19c4148bdab5");
    BluetoothLEDevice? device;
    GattDeviceService? service;
    GattCharacteristic? tx, rx;
    readonly SemaphoreSlim serial = new(1,1);
    readonly object gate = new();
    (byte Sequence, TaskCompletionSource<byte[]> Completion)? pending;
    public event Action<string>? Log;
    public event Action? ConnectionLost;

    public async Task Connect(ulong address)
    {
        device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        if (device == null) throw new IOException("Windows não abriu o controle.");
        device.ConnectionStatusChanged += DeviceConnectionChanged;
        var result = await device.GetGattServicesForUuidAsync(ServiceId, BluetoothCacheMode.Uncached);
        if (result.Status != GattCommunicationStatus.Success || result.Services.Count == 0)
        {
            foreach (var item in result.Services) item.Dispose();
            throw new IOException("Serviço KeyLinker: " + result.Status);
        }
        service = result.Services[0]; foreach (var extra in result.Services.Skip(1)) extra.Dispose();
        var chars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
        if (chars.Status != GattCommunicationStatus.Success) throw new IOException("Características: " + chars.Status);
        tx = chars.Characteristics.FirstOrDefault(c => c.Uuid == new Guid("d7f010e1-660d-46e9-96c3-19c4148bdab5"));
        rx = chars.Characteristics.FirstOrDefault(c => c.Uuid == new Guid("d7f010e2-660d-46e9-96c3-19c4148bdab5"));
        if (tx == null || rx == null) throw new IOException("Canais KeyLinker ausentes.");
        rx.ValueChanged += Received;
        var status = await rx.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (status != GattCommunicationStatus.Success) throw new IOException("Notificações: " + status);
        Log?.Invoke("Canal Bluetooth conectado.");
    }
    void DeviceConnectionChanged(BluetoothLEDevice sender, object _) { if(sender.ConnectionStatus==BluetoothConnectionStatus.Disconnected) ConnectionLost?.Invoke(); }
    void Received(GattCharacteristic _, GattValueChangedEventArgs e)
    {
        using var reader = DataReader.FromBuffer(e.CharacteristicValue);
        var bytes = new byte[reader.UnconsumedBufferLength]; reader.ReadBytes(bytes);
        try
        {
            byte[] packet = Protocol.Decode(bytes);
            if (Protocol.Checksum(packet.AsSpan(0,packet.Length-1)) != packet[^1]) { Log?.Invoke("Resposta descartada: checksum inválido."); return; }
            Log?.Invoke("RX " + Convert.ToHexString(packet));
            lock (gate)
                if (packet[0] == 0x19 && pending is { } request && request.Sequence == packet[2]) request.Completion.TrySetResult(packet);
        }
        catch (ArgumentException e2) { Log?.Invoke("Resposta descartada: " + e2.Message); }
    }
    public async Task<byte[]> Exchange(byte[] clear)
    {
        await serial.WaitAsync();
        try
        {
            if (tx == null) throw new IOException("Controle desconectado.");
            var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate) pending = (clear[2],completion);
            using var writer = new DataWriter(); writer.WriteBytes(Protocol.Encode(clear));
            Log?.Invoke("TX " + Convert.ToHexString(clear));
            var status = await tx.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
            if (status != GattCommunicationStatus.Success) throw new IOException("Envio: " + status);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
        }
        finally { lock (gate) pending = null; serial.Release(); }
    }
    public async Task<byte[]> ReadLighting()
    {
        // b1.C(index,104) -> e.q(index), HostActivity.l0 assembles first length + 6-byte records.
        var response = await Exchange(Protocol.Build(0xB7,[0]));
        var payload = response.AsSpan(4,response.Length-5).ToArray();
        if (payload.Length == 0) throw new IOException("Resposta de iluminação vazia.");
        int size = payload[0];
        if (size == 0 || size > 24 || size % 6 != 0) throw new IOException("Formato de iluminação não suportado: " + Convert.ToHexString(payload));
        var data = payload.Skip(1).ToList();
        for (int index=1; data.Count < size; index++)
        {
            if (index > 2) throw new IOException("Número inesperado de fragmentos.");
            response = await Exchange(Protocol.Build(0xB7,[(byte)index]));
            data.AddRange(response.AsSpan(4,response.Length-5).ToArray());
        }
        if (data.Count != size) throw new IOException("Comprimento de iluminação inconsistente.");
        return data.ToArray();
    }
    public async Task SaveLighting(byte[] data)
    {
        if (data.Length is < 6 or > 24 || data.Length % 6 != 0) throw new ArgumentException("Dados de iluminação inválidos.");
        byte[] payload = [(byte)data.Length, ..data];
        for (int offset=0,index=0;offset<payload.Length;offset+=15,index++)
        {
            var part = payload.Skip(offset).Take(15).ToArray();
            await Exchange(Protocol.BuildLightingChunk(part,index));
        }
        var actual = await ReadLighting();
        if (!actual.SequenceEqual(data)) throw new IOException("Controle respondeu, mas a releitura difere da configuração enviada.");
    }
    public void Dispose()
    {
        if(device!=null)device.ConnectionStatusChanged-=DeviceConnectionChanged;
        lock (gate) { pending?.Completion.TrySetException(new IOException("Conexão encerrada.")); pending = null; }
        if (rx != null) rx.ValueChanged -= Received;
        tx = rx = null; service?.Dispose(); service = null; device?.Dispose(); device = null;
    }
}
