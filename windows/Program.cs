using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace G5Link;

internal static class Program
{
    internal static string DataDirectory
    {
        get
        {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"G5Link");
            Directory.CreateDirectory(path);
            return path;
        }
    }
    static bool controls;
    static bool testControls;
    static bool actuators,testActuators;
    static bool turbo,testTurbo;
    static bool macros;
    static bool testMacros;
    static ulong? knownAddress;
    [STAThread]
    static void Main(string[] args)
    {
        testControls=args.Contains("--test-controls");
        controls=args.Contains("--read-controls") || testControls;
        testActuators=args.Contains("--test-actuators");actuators=args.Contains("--read-actuators")||testActuators;
        testTurbo=args.Contains("--test-turbo");turbo=args.Contains("--read-turbo")||testTurbo;
        testMacros=args.Contains("--test-macros");macros=args.Contains("--read-macros")||testMacros;
        int addressIndex=Array.IndexOf(args,"--address");
        if(addressIndex>=0 && addressIndex+1<args.Length) knownAddress=Convert.ToUInt64(args[addressIndex+1].Replace(":",""),16);
        if (args.Length == 2 && args[0] == "--verify")
        {
            int count = 0;
            foreach (var line in File.ReadLines(args[1]))
            {
                var parts = line.Split('|');
                byte[] clear = Convert.FromHexString(parts[0]), encoded = Convert.FromHexString(parts[1]);
                if (!Protocol.Encode(clear).SequenceEqual(encoded) || !Protocol.Decode(encoded).SequenceEqual(clear)
                    || Protocol.Checksum(clear.AsSpan(0, clear.Length - 1)) != clear[^1])
                    throw new InvalidOperationException("Divergência no vetor " + count);
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Arquivo de vetores vazio.");
            Console.WriteLine($"{count} vetores Java validados: codificação, decodificação e checksum.");
            return;
        }
        if (args.Contains("--scan") || args.Contains("--probe") || args.Contains("--read-lighting") || args.Contains("--test-lighting") || controls || actuators || turbo || macros)
        {
            ScanConsole(args.Contains("--probe"), args.Contains("--read-lighting") || args.Contains("--test-lighting"), args.Contains("--test-lighting")).GetAwaiter().GetResult();
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainWindow());
    }

    static async Task ScanConsole(bool probe, bool lighting, bool test)
    {
        var seen = new ConcurrentDictionary<ulong, string>();
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, e) =>
        {
            var name = e.Advertisement.LocalName;
            if (name.Contains("G5", StringComparison.OrdinalIgnoreCase) && seen.TryAdd(e.BluetoothAddress, name))
                Console.WriteLine($"G5 encontrado: {name} | {e.BluetoothAddress:X12} | RSSI {e.RawSignalStrengthInDBm}");
        };
        watcher.Stopped += (_, e) => Console.WriteLine($"Scanner encerrado: {e.Error}");
        Console.WriteLine("Procurando G5 por 15 segundos...");
        try { watcher.Start(); await Task.Delay(15000); }
        finally { watcher.Stop(); }
        Console.WriteLine($"Resultado: {seen.Count} controle(s) G5 anunciando BLE.");
        if(seen.Count==0 && knownAddress is ulong address) {seen.TryAdd(address,"G5 informado");Console.WriteLine("Tentando endereço previamente identificado.");}
        if(seen.Count==0 && (probe||lighting||controls||actuators||turbo||macros)) Environment.ExitCode=1;
        if(macros && seen.Count==1)
        {
            using var client=new BleClient();client.Log+=Console.WriteLine;
            try
            {
                await client.Connect(seen.Keys.Single());var catalog=await client.ReadMacroCatalog();
                Console.WriteLine("Catálogo: "+System.Text.Json.JsonSerializer.Serialize(catalog));
                if(catalog.Extended)throw new IOException("Formato estendido de macros: somente catálogo identificado nesta etapa.");
                var slots=new List<MacroSlot>();
                for(int slot=0;slot<8;slot++)if((catalog.SlotMask&(1<<slot))!=0)
                {
                    var item=await client.ReadMacro(slot);slots.Add(item);Console.WriteLine($"M{slot+1}: {Convert.ToHexString(item.Data)}");
                }
                File.WriteAllText(Path.Combine(DataDirectory,"macros-backup.json"),System.Text.Json.JsonSerializer.Serialize(new{catalog,slots}));
                if(testMacros)
                {
                    var empty=slots.FirstOrDefault(s=>s.Data.Length==0)??throw new IOException("Não existe slot vazio para o teste.");
                    var macroTestData=MacroCodec.Encode(catalog,[new(MacroCodec.State(catalog,[1]),100),new(MacroCodec.Neutral(catalog),100),new(MacroCodec.State(catalog,[2]),50),new(MacroCodec.Neutral(catalog),50)],false,0);
                    try
                    {
                        await client.SaveMacro(catalog,empty,macroTestData);Console.WriteLine("TESTE: macro digital em slot vazio confirmada por releitura.");
                    }
                    finally
                    {
                        await client.SaveMacro(catalog,await client.ReadMacro(empty.Index),empty.Data,true);Console.WriteLine("RESTAURAÇÃO: slot vazio confirmado.");
                    }
                    foreach(var original in slots)if(!(await client.ReadMacro(original.Index)).Data.SequenceEqual(original.Data))throw new IOException("Diferença inesperada em slot original.");
                    Console.WriteLine("PRESERVAÇÃO: todos os slots originais conferem.");
                }
            }
            catch(Exception e){Console.WriteLine("Leitura não concluída: "+e.Message);Environment.ExitCode=1;}
        }
        if(turbo && seen.Count==1)
        {
            using var client=new BleClient();client.Log+=Console.WriteLine;
            try
            {
                await client.Connect(seen.Keys.Single());var original=await client.ReadTurbo();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(original));
                File.WriteAllText(Path.Combine(DataDirectory,"turbo-backup.json"),System.Text.Json.JsonSerializer.Serialize(original));
                if(testTurbo)
                {
                    var changed=original.Data.ToArray();changed[0]=(byte)(changed[0]==100?50:100);
                    try {await client.SaveTurbo(original,changed);Console.WriteLine("TESTE: intervalo turbo alterado e confirmado.");}
                    finally {await client.SaveTurbo(await client.ReadTurbo(),original.Data);Console.WriteLine("RESTAURAÇÃO: turbo original confirmado.");}
                    if(original.Keys.Length>0)
                    {
                        try
                        {
                            var mode=original.Data.ToArray();mode[3]|=1;mode[6]&=254;
                            await client.SaveTurbo(await client.ReadTurbo(),mode);Console.WriteLine("TESTE: turbo enquanto pressionado confirmado para o primeiro botão.");
                            if(original.AutoKeys.Contains(original.Keys[0]))
                            {
                                mode[3]&=254;mode[6]|=1;
                                await client.SaveTurbo(await client.ReadTurbo(),mode);Console.WriteLine("TESTE: turbo automático confirmado para o primeiro botão.");
                            }
                        }
                        finally {await client.SaveTurbo(await client.ReadTurbo(),original.Data);Console.WriteLine("RESTAURAÇÃO: modos turbo originais confirmados.");}
                    }
                }
            }
            catch(Exception e){Console.WriteLine("Operação não concluída: "+e.Message);Environment.ExitCode=1;}
        }
        if(actuators && seen.Count==1)
        {
            using var client=new BleClient();client.Log+=Console.WriteLine;
            try
            {
                await client.Connect(seen.Keys.Single());var original=await client.ReadActuators();
                File.WriteAllText(Path.Combine(DataDirectory,"actuators-backup.json"),System.Text.Json.JsonSerializer.Serialize(original));
                Console.WriteLine("Gatilhos: "+Convert.ToHexString(original.Triggers));Console.WriteLine("Motores: "+Convert.ToHexString(original.Motors));
                if(testActuators)
                {
                    var changed=original.Triggers.ToArray();changed[0]=(byte)(changed[0]==0?1:changed[0]-1);
                    try {await client.SaveTriggers(original,changed);Console.WriteLine("TESTE: zona morta LT alterada e confirmada.");}
                    finally {await client.SaveTriggers(await client.ReadActuators(),original.Triggers);Console.WriteLine("RESTAURAÇÃO: gatilhos originais confirmados.");}
                    var motors=original.Motors.ToArray();motors[0]=(byte)(motors[0]==0?1:motors[0]-1);
                    try {await client.SaveMotors(await client.ReadActuators(),motors);Console.WriteLine("TESTE: intensidade de motor alterada e confirmada.");}
                    finally {await client.SaveMotors(await client.ReadActuators(),original.Motors);Console.WriteLine("RESTAURAÇÃO: motores originais confirmados.");}
                }
            }
            catch(Exception e){Console.WriteLine("Operação não concluída: "+e.Message);Environment.ExitCode=1;}
        }
        if (probe && seen.Count == 1) await Probe.Run(seen.Keys.Single());
        else if (probe && seen.Count > 1) Console.WriteLine("Mais de um G5 disponível. Selecione o controle pela interface.");
        if(controls && seen.Count==1)
        {
            using var client=new BleClient(); client.Log+=Console.WriteLine;
            try
            {
                await client.Connect(seen.Keys.Single());
                var profile=await client.ReadControls();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(profile));
                File.WriteAllText(Path.Combine(DataDirectory,"controls-backup.json"),System.Text.Json.JsonSerializer.Serialize(profile));
                if(testControls)
                {
                    var mapping=profile.Mapping.ToArray();int a=Array.IndexOf(profile.SupportedKeys,(byte)1), b=Array.IndexOf(profile.SupportedKeys,(byte)2);
                    if(a<0||b<0)throw new IOException("Botões A/B não disponíveis para teste.");
                    (mapping[a],mapping[b])=(mapping[b],mapping[a]);
                    try
                    {
                        await client.SaveMapping(profile,mapping);
                        Console.WriteLine("TESTE: troca A/B confirmada por releitura.");
                    }
                    finally
                    {
                        await client.SaveMapping(await client.ReadControls(),profile.Mapping);
                        Console.WriteLine("RESTAURAÇÃO: botões originais confirmados.");
                    }
                    var sticks=profile.Sticks.ToArray();sticks[0]=(byte)(sticks[0]==0?1:sticks[0]-1);
                    try
                    {
                        await client.SaveSticks(await client.ReadControls(),sticks);
                        Console.WriteLine("TESTE: zona morta alterada em 1 ponto e confirmada por releitura.");
                    }
                    finally
                    {
                        await client.SaveSticks(await client.ReadControls(),profile.Sticks);
                        Console.WriteLine("RESTAURAÇÃO: analógicos originais confirmados.");
                    }
                }
            }
            catch(Exception e) {Console.WriteLine(e.Message);Environment.ExitCode=1;}
        }
        if (lighting && seen.Count == 1)
        {
            using var client = new BleClient(); client.Log += Console.WriteLine;
            try
            {
                await client.Connect(seen.Keys.Single());
                var data = await client.ReadLighting();
                Console.WriteLine("Iluminação: " + Convert.ToHexString(data));
                File.WriteAllText(Path.Combine(DataDirectory,"lighting-backup.hex"),Convert.ToHexString(data));
                if (test)
                {
                    byte[] changed = data.ToArray(); changed[5] = (byte)(data[5] == 0 ? 1 : data[5]-1);
                    try
                    {
                        await client.SaveLighting(changed);
                        Console.WriteLine("TESTE: brilho alterado em 1 unidade e confirmado por releitura.");
                    }
                    finally
                    {
                        await client.SaveLighting(data);
                        Console.WriteLine("RESTAURAÇÃO: configuração original confirmada por releitura.");
                    }
                }
            }
            catch (Exception e) { Console.WriteLine("Leitura não concluída: " + e.Message); Environment.ExitCode=1; }
        }
    }
}

internal sealed record Device(ulong Address, string Name)
{
    public override string ToString() => $"{Name} — {Address:X12}";
}

internal sealed partial class MainWindow : Form
{
    static string DataDirectory => Program.DataDirectory;
    readonly ComboBox devices = new() { Width = 430, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button scan = new() { Text = "Buscar controle", AutoSize = true };
    readonly Button connect = new() { Text = "Conectar", AutoSize = true };
    readonly Button info = new() { Text = "Consultar identificação", AutoSize = true, Enabled = false };
    readonly Label connectionStatus = new() { Text="● Desconectado", AutoSize=true, Padding=new Padding(8,8,0,0), ForeColor=Color.Firebrick };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    readonly BluetoothLEAdvertisementWatcher watcher = new() { ScanningMode = BluetoothLEScanningMode.Active };
    readonly System.Windows.Forms.Timer scanTimer = new() { Interval = 15000 };
    BleClient? client;
    readonly TabPage lightingPage=new("Iluminação"), controlsPage=new("Botões e analógicos"), actuatorsPage=new("Gatilhos e vibração"), turboPage=new("Turbo"), macrosPage=new("Macros");
    bool busy, closing, autoConnectAfterScan;

    public MainWindow()
    {
        Text = "G5 Link — Machenike G5Pro Max";
        Width = 1040; Height = 820;
        MinimumSize=new Size(1000,720);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(10) };
        var importBackup = new Button { Text="Importar backup…", AutoSize=true };
        importBackup.Click += async (_,_) => await ImportBackup();
        actions.Controls.AddRange([devices, scan, connect, info, importBackup, connectionStatus]);
        log.Dock=DockStyle.Bottom; log.Height=100; Controls.Add(log);
        var tabs=new TabControl {Dock=DockStyle.Fill}; tabs.TabPages.AddRange([lightingPage,controlsPage,actuatorsPage,turboPage,macrosPage]); Controls.Add(tabs);
        SetupLighting(); SetupControls(); SetupActuators(); SetupTurbo(); SetupMacros(); SetupLiveView(tabs); Controls.Add(actions);
        ApplyVisualStyle(actions, tabs);
        scan.Click += (_, _) => StartScan();
        connect.Click += async (_, _) => await Connect();
        info.Click += async (_, _) => await Identify();
        scanTimer.Tick += async (_, _) =>
        {
            bool automatic=autoConnectAfterScan;
            autoConnectAfterScan=false;
            StopScan();
            if(!automatic || closing || busy) return;
            if(devices.Items.Count==1)
            {
                devices.SelectedIndex=0;
                Log("Controle encontrado. Conectando automaticamente...");
                await Connect();
            }
            else if(devices.Items.Count>1) Log("Mais de um controle encontrado. Selecione um e clique em Conectar.");
            else Log("Nenhum controle encontrado. Ligue o controle e clique em Buscar controle para tentar novamente.");
        };
        watcher.Received += (_, e) =>
        {
            string name = e.Advertisement.LocalName;
            if (!name.Contains("G5", StringComparison.OrdinalIgnoreCase)) return;
            OnUi(() =>
            {
                if (devices.Items.Cast<Device>().Any(d => d.Address == e.BluetoothAddress)) return;
                devices.Items.Add(new Device(e.BluetoothAddress, name));
                if (devices.SelectedIndex < 0) devices.SelectedIndex = 0;
                Log($"Encontrado: {name}, RSSI {e.RawSignalStrengthInDBm}");
            });
        };
        watcher.Stopped += (_, e) => OnUi(() => Log($"Scanner: {e.Error}"));
        FormClosed += (_, _) => { closing = true; scanTimer.Dispose(); liveTimer.Dispose(); lightingSliderTimer.Stop(); watcher.Stop(); Disconnect(); lightingSliderTimer.Dispose(); };
        Shown += (_, _) => StartScan(automatic:true);
        Log("Protótipo independente. Busca G5Pro/G5Pro Max e consulta identificação.\r\nFeche o KeyLinker no celular se o controle não aparecer. Iluminação disponível após leitura. Aplicar envia a configuração ao controle.");
    }

    void OnUi(Action action)
    {
        if (closing || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }
    void Log(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        log.AppendText(line + Environment.NewLine);
        try { File.AppendAllText(Path.Combine(DataDirectory, "g5link.log"), line + Environment.NewLine); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    void StartScan(bool automatic=false)
    {
        if (busy) return;
        try
        {
            StopScan(); autoConnectAfterScan=automatic; devices.Items.Clear(); watcher.Start(); scanTimer.Start();
            scan.Enabled = false; Log("Buscando anúncios BLE por 15 segundos...");
        }
        catch (Exception e) { autoConnectAfterScan=false; Log("Falha na busca: " + e.Message); scan.Enabled = true; }
    }
    void StopScan() { scanTimer.Stop(); watcher.Stop(); scan.Enabled = !busy; }
    void Disconnect()
    {
        lightingSliderTimer.Stop();
        connectionStatus.Text="● Desconectado";connectionStatus.ForeColor=Color.Firebrick;
        client?.Dispose(); client = null; currentLighting = originalLighting = null;
        profile=originalProfile=null; keyGrid.Rows.Clear();
        actuatorProfile=originalActuators=null;
        turboProfile=originalTurbo=null;turboGrid.Rows.Clear();
        macroBackup=macroSessionOriginal=null;macroGrid.Rows.Clear();
        info.Enabled = applyLight.Enabled = restoreLight.Enabled = false;
    }
    async Task Connect()
    {
        if (busy || devices.SelectedItem is not Device selected) return;
        autoConnectAfterScan=false;
        busy = true; StopScan(); SetBusy(true); Disconnect();
        connectionStatus.Text="● Conectando…";connectionStatus.ForeColor=Color.DarkOrange;
        try
        {
            client = new BleClient(); client.Log += text => OnUi(() => Log(text));
            var connectingClient=client;
            client.ConnectionLost += () => OnUi(() => { if(client!=connectingClient)return; connectionStatus.Text="● Conexão perdida";connectionStatus.ForeColor=Color.Firebrick; if(!busy){Disconnect();SetBusy(false);} Log("Conexão Bluetooth encerrada."); });
            await client.Connect(selected.Address);
            Log("Conectado a " + selected.Name);
            connectionStatus.Text="● Conectado • lendo configurações…";connectionStatus.ForeColor=Color.DarkOrange;
            int failures=0;
            async Task LoadArea(string name,Func<Task> read,Action reset)
            {
                Log("Lendo " + name + "…");
                try { await read(); }
                catch(Exception e) { failures++;reset();Log($"Não foi possível ler {name}: {e.Message}. Reconecte para tentar novamente."); }
            }
            await LoadArea("iluminação",ReadLight,()=>{currentLighting=originalLighting=null;zone.Items.Clear();});
            await LoadArea("botões e analógicos",ReadControlProfile,()=>{profile=originalProfile=null;keyGrid.Rows.Clear();});
            await LoadArea("gatilhos e vibração",ReadActuatorProfile,()=>{actuatorProfile=originalActuators=null;});
            await LoadArea("turbo",ReadTurboProfile,()=>{turboProfile=originalTurbo=null;turboGrid.Rows.Clear();});
            await LoadArea("macros",ReadMacros,()=>{macroBackup=macroSessionOriginal=null;macroGrid.Rows.Clear();});
            if(connectionStatus.Text=="● Conexão perdida") { Disconnect();Log("Leitura interrompida: controle desconectado."); }
            else if(failures>0) { connectionStatus.Text="● Conectado • leitura parcial";connectionStatus.ForeColor=Color.DarkOrange;Log("Algumas configurações não foram carregadas. Reconecte para tentar novamente."); }
            else { connectionStatus.Text="● Conectado • configurações carregadas";connectionStatus.ForeColor=Color.SeaGreen;Log("Todas as configurações foram carregadas."); }
        }
        catch (Exception e) { Log(e.Message); Disconnect(); }
        finally { busy = false; SetBusy(false); }
    }
    async Task Identify()
    {
        await Run(async () =>
        {
            var packet = await client!.Exchange(Protocol.BuildIdentity());
            if (packet.Length >= 12 && packet[4] >= 6)
                Log($"VID {packet[5]:X2}{packet[6]:X2} | PID {packet[7]:X2}{packet[8]:X2} | versão {packet[9]:X}.{packet[10]:X2}");
        });
    }
}
