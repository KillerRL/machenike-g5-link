using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace G5Link;

internal static class Probe
{
    public static async Task Run(ulong address)
    {
        try
        {
            using var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (device == null) { Console.WriteLine("Windows não abriu o controle."); return; }
            var services = await device.GetGattServicesForUuidAsync(new Guid("d7f010e0-660d-46e9-96c3-19c4148bdab5"), BluetoothCacheMode.Uncached);
            Console.WriteLine("Serviço de configuração: " + services.Status);
            try
            {
                if (services.Status != GattCommunicationStatus.Success || services.Services.Count != 1) return;
                var chars = await services.Services[0].GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                if (chars.Status != GattCommunicationStatus.Success) { Console.WriteLine("Características: " + chars.Status); return; }
                foreach (var c in chars.Characteristics) Console.WriteLine($"{c.Uuid}: {c.CharacteristicProperties}");
                var tx = chars.Characteristics.FirstOrDefault(c => c.Uuid == new Guid("d7f010e1-660d-46e9-96c3-19c4148bdab5"));
                var rx = chars.Characteristics.FirstOrDefault(c => c.Uuid == new Guid("d7f010e2-660d-46e9-96c3-19c4148bdab5"));
                if (tx == null || rx == null) return;
                byte[] clear = Protocol.BuildIdentity();
                var answer = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                void Received(GattCharacteristic _, GattValueChangedEventArgs e)
                {
                    using var reader = DataReader.FromBuffer(e.CharacteristicValue);
                    var bytes = new byte[reader.UnconsumedBufferLength]; reader.ReadBytes(bytes);
                    try
                    {
                        byte[] packet = Protocol.Decode(bytes);
                        if (packet[0] == 25 && packet[2] == clear[2] && Protocol.Checksum(packet.AsSpan(0, packet.Length-1)) == packet[^1])
                            answer.TrySetResult(packet);
                    }
                    catch (ArgumentException) { }
                }
                rx.ValueChanged += Received;
                try
                {
                    var status = await rx.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    Console.WriteLine("Notificações: " + status);
                    if (status != GattCommunicationStatus.Success) return;
                    using var writer = new DataWriter(); writer.WriteBytes(Protocol.Encode(clear));
                    Console.WriteLine("Consulta de identificação: " + Convert.ToHexString(clear));
                    var sent = await tx.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithResponse);
                    Console.WriteLine("Envio: " + sent);
                    if (sent != GattCommunicationStatus.Success) return;
                    var packet = await answer.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    Console.WriteLine("Resposta válida à consulta: " + Convert.ToHexString(packet));
                    // b1.e1 -> j(145): payload[0] is declared data length, then VID/PID/version.
                    if (packet.Length >= 12 && packet[4] >= 6)
                        Console.WriteLine($"VID: {packet[5]:X2}{packet[6]:X2} | PID: {packet[7]:X2}{packet[8]:X2} | versão: {packet[9]:X}.{packet[10]:X2}");
                }
                finally { rx.ValueChanged -= Received; }
            }
            finally { foreach (var service in services.Services) service.Dispose(); }
        }
        catch (Exception e) { Console.WriteLine("Consulta não concluída: " + e.Message); }
    }
}
