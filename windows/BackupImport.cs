using System.Text.Json;

namespace G5Link;

internal sealed partial class MainWindow
{
    async Task ImportBackup()
    {
        if(client==null || busy) { Log("Conecte o controle antes de importar um backup."); return; }
        using var dialog=new OpenFileDialog { Filter="Backups do G5 Link (*.json;*.hex)|*.json;*.hex", Title="Selecionar backup" };
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        await Run(async()=>
        {
            string text=File.ReadAllText(dialog.FileName).Trim();
            if(Path.GetExtension(dialog.FileName).Equals(".hex",StringComparison.OrdinalIgnoreCase))
            {
                byte[] data=Convert.FromHexString(text);
                var actual=await client!.ReadLighting();
                if(data.Length!=actual.Length || data.Length==0)throw new IOException("Backup incompatível com as zonas de iluminação deste controle.");
                originalLighting ??= actual.ToArray();
                currentLighting=data;zone.Items.Clear();for(int i=0;i<data.Length/6;i++)zone.Items.Add("Zona "+(i+1));zone.SelectedIndex=0;
                Log("Backup de iluminação carregado. Confira os valores e clique em Aplicar iluminação.");return;
            }
            using var json=JsonDocument.Parse(text);var root=json.RootElement;
            if(root.TryGetProperty("SupportedKeys",out _))
            {
                var saved=JsonSerializer.Deserialize<ControlProfile>(text)??throw new IOException("Backup inválido.");
                var actual=await client!.ReadControls();
                if(saved.SupportedKeys==null||saved.Mapping==null||saved.Sticks==null||!saved.SupportedKeys.SequenceEqual(actual.SupportedKeys)||saved.Mapping.Length!=actual.Mapping.Length||saved.Sticks.Length!=14||saved.Mapping.Any(k=>!actual.SupportedKeys.Contains(k)))throw new IOException("Backup de botões incompatível.");
                for(int s=0;s<2;s++)if(saved.Sticks[s*7]>100||saved.Sticks[s*7+1]>100)throw new IOException("Zonas mortas inválidas no backup.");
                originalProfile ??= CloneProfile(actual);
                profile=actual;PopulateControls();for(int i=0;i<saved.Mapping.Length;i++)keyGrid.Rows[i].Cells[1].Value=saved.Mapping[i];
                for(int s=0;s<2;s++){for(int i=0;i<6;i++)stickValues[s,i].Value=saved.Sticks[s*7+i];stickFlags[s,0].Checked=(saved.Sticks[s*7+6]&1)!=0;stickFlags[s,1].Checked=(saved.Sticks[s*7+6]&2)!=0;}
                Log("Backup carregado. Confira e aplique botões e analógicos separadamente.");return;
            }
            if(root.TryGetProperty("Triggers",out _))
            {
                var saved=JsonSerializer.Deserialize<ActuatorProfile>(text)??throw new IOException("Backup inválido.");
                var actual=await client!.ReadActuators();
                if(saved.Triggers?.Length!=14||saved.Motors?.Length!=8)throw new IOException("Backup de gatilhos incompatível.");
                for(int t=0;t<2;t++)if(saved.Triggers[t*7]>200||saved.Triggers[t*7+1]>200)throw new IOException("Zonas mortas de gatilho inválidas no backup.");
                originalActuators ??= new(actual.Triggers.ToArray(),actual.Motors.ToArray());
                actuatorProfile=actual;
                for(int t=0;t<2;t++)for(int i=0;i<6;i++)triggerValues[t,i].Value=saved.Triggers[t*7+i]/(i<2?2m:1m);
                for(int i=0;i<4;i++)motorValues[i].Value=saved.Motors[i];
                Log("Backup carregado. Confira e aplique gatilhos e vibração separadamente.");return;
            }
            if(root.TryGetProperty("AutoKeys",out _))
            {
                var saved=JsonSerializer.Deserialize<TurboProfile>(text)??throw new IOException("Backup inválido.");var actual=await client!.ReadTurbo();
                if(saved.Keys==null||saved.AutoKeys==null||saved.Data?.Length!=13||!saved.Keys.SequenceEqual(actual.Keys)||!saved.AutoKeys.SequenceEqual(actual.AutoKeys))throw new IOException("Backup de turbo incompatível.");
                if(saved.Data[0]==0)throw new IOException("Intervalo turbo inválido no backup.");
                originalTurbo ??= new(actual.Keys.ToArray(),actual.AutoKeys.ToArray(),actual.Data.ToArray());
                turboProfile=saved;PopulateTurbo();turboProfile=actual;
                Log("Backup de turbo carregado. Confira e clique em Aplicar turbo.");return;
            }
            throw new IOException("Tipo de backup não suportado. Backups de macros continuam preservados para exportação.");
        });
    }
}
