namespace G5Link;

internal sealed partial class MainWindow
{
    readonly ComboBox zone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly NumericUpDown red = Number(255), green = Number(255), blue = Number(255);
    readonly TrackBar brightness = new() { Minimum=0, Maximum=255, TickStyle=TickStyle.None, Width=190, Height=30, AccessibleName="Brilho" };
    readonly TrackBar speed = new() { Minimum=0, Maximum=2048, TickStyle=TickStyle.None, Width=190, Height=30, SmallChange=8, LargeChange=64, AccessibleName="Velocidade" };
    readonly Label brightnessValue=new(){AutoSize=true},speedValue=new(){AutoSize=true};
    readonly System.Windows.Forms.Timer lightingSliderTimer=new(){Interval=250};
    readonly CheckBox breath = new() { Text = "Respiração", AutoSize = true };
    readonly Button applyLight = new() { Text = "Aplicar iluminação", AutoSize = true, Enabled = false };
    readonly Button restoreLight = new() { Text = "Restaurar original", AutoSize = true, Enabled = false };
    readonly Button chooseColor = new() { Text = "Escolher cor", AutoSize = true };
    readonly Panel preview = new() { Width=40, Height=25, BorderStyle=BorderStyle.FixedSingle };
    readonly List<Button> colorPresets = [];
    byte[]? currentLighting, originalLighting;
    bool loading;
    static NumericUpDown Number(int max) => new() { Minimum=0, Maximum=max, Width=65 };

    void SetupLighting()
    {
        var panel = new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(10), AutoScroll=true };
        void Label(string text) => panel.Controls.Add(new Label { Text=text, AutoSize=true, Padding=new Padding(0,6,0,0) });
        Label("Iluminação"); panel.Controls.Add(zone);
        foreach (var item in new[] { ("R",red),("G",green),("B",blue) })
        { Label(item.Item1); panel.Controls.Add(item.Item2); item.Item2.ValueChanged += (_,_) => UpdateDraft(); }
        panel.Controls.AddRange([breath,preview,chooseColor,applyLight,restoreLight]);
        panel.SetFlowBreak(restoreLight,true);
        var sliders=new FlowLayoutPanel { Width=900, Height=65, Margin=new Padding(3,12,3,4), WrapContents=false };
        var brightnessBox=new FlowLayoutPanel { Width=260,Height=60,FlowDirection=FlowDirection.TopDown,WrapContents=false };
        brightnessBox.Controls.Add(brightnessValue);brightnessBox.Controls.Add(brightness);
        var speedBox=new FlowLayoutPanel { Width=300,Height=60,FlowDirection=FlowDirection.TopDown,WrapContents=false };
        speedBox.Controls.Add(speedValue);speedBox.Controls.Add(speed);
        sliders.Controls.AddRange([brightnessBox,speedBox]);panel.Controls.Add(sliders);panel.SetFlowBreak(sliders,true);
        void SliderChanged()
        {
            brightnessValue.Text=$"Brilho: {brightness.Value} / 255";
            speedValue.Text=$"Velocidade: {speed.Value} / 2048";
            if(loading||currentLighting==null||client==null)return;
            UpdateDraft();lightingSliderTimer.Stop();lightingSliderTimer.Start();
        }
        brightness.ValueChanged+=(_,_)=>SliderChanged();speed.ValueChanged+=(_,_)=>SliderChanged();
        brightnessValue.Text="Brilho: 0 / 255";speedValue.Text="Velocidade: 0 / 2048";
        lightingSliderTimer.Tick+=async(_,_)=>
        {
            if(busy)return;
            lightingSliderTimer.Stop();
            if(currentLighting==null||client==null)return;
            await Run(async()=>
            {
                try { await client!.SaveLighting(currentLighting!.ToArray());Log("Brilho e velocidade aplicados e confirmados por releitura."); }
                catch
                {
                    try { currentLighting=await client!.ReadLighting();LoadZone(); }
                    catch { currentLighting=null;zone.Items.Clear();Log("Não foi possível atualizar a iluminação. Reconecte o controle."); }
                    throw;
                }
            });
        };
        panel.Controls.Add(new Label { Text="Cores rápidas — clique para aplicar ao controle", AutoSize=true, Font=new Font("Segoe UI",10,FontStyle.Bold), Margin=new Padding(3,24,3,8) });
        panel.SetFlowBreak(panel.Controls[panel.Controls.Count-1],true);
        (string Name,Color Color)[] colors=[("Vermelho",Color.Red),("Laranja",Color.FromArgb(255,100,0)),("Amarelo",Color.Yellow),("Verde",Color.Lime),("Ciano",Color.Cyan),("Azul",Color.Blue),("Roxo",Color.FromArgb(140,0,255)),("Rosa",Color.FromArgb(255,0,128)),("Branco",Color.White),("Azul-claro",Color.FromArgb(0,128,255))];
        foreach(var item in colors.Append((Name:"Desligar",Color:Color.Black)))
        {
            var color=item.Color;
            var button=new Button { Text=item.Name, Tag=color, Width=112, Height=45, Margin=new Padding(3,3,6,8), AccessibleName=item.Name };
            button.Click+=async(_,_)=>await Run(async()=>
            {
                if(currentLighting==null)return;
                loading=true;red.Value=color.R;green.Value=color.G;blue.Value=color.B;loading=false;UpdateDraft();
                await client!.SaveLighting(currentLighting.ToArray());
                Log(item.Name=="Desligar"?"Iluminação apagada e confirmada por releitura.":$"Cor {item.Name.ToLowerInvariant()} aplicada e confirmada por releitura.");
            });
            colorPresets.Add(button);panel.Controls.Add(button);
        }
        panel.SetFlowBreak(colorPresets[^1],true);
        panel.Controls.Add(new Label { Text="A cor escolhida mantém o modo atual (fixa ou respiração), brilho e velocidade.", AutoSize=true, Margin=new Padding(3,8,3,3) });
        lightingPage.Controls.Add(panel);
        zone.SelectedIndexChanged += (_,_) => LoadZone();
        breath.CheckedChanged += async (_,_) =>
        {
            if(loading || currentLighting==null || client==null || busy)return;
            UpdateDraft();
            await Run(async () =>
            {
                try
                {
                    await client!.SaveLighting(currentLighting!.ToArray());
                    Log(breath.Checked ? "Respiração aplicada e confirmada por releitura." : "Luz fixa aplicada e confirmada por releitura.");
                }
                catch
                {
                    try { currentLighting=await client!.ReadLighting();LoadZone(); }
                    catch { currentLighting=null;zone.Items.Clear();Log("Não foi possível atualizar a iluminação. Reconecte o controle."); }
                    throw;
                }
            });
        };
        applyLight.Click += async (_,_) => await Run(async () =>
        {
            if (currentLighting == null) return;
            await client!.SaveLighting(currentLighting.ToArray());
            Log("Iluminação aplicada e confirmada por releitura.");
        });
        restoreLight.Click += async (_,_) => await Run(async () =>
        {
            if (originalLighting == null) return;
            await client!.SaveLighting(originalLighting.ToArray());
            currentLighting = originalLighting.ToArray(); LoadZone();
            Log("Iluminação original restaurada e confirmada por releitura.");
        });
        chooseColor.Click += (_,_) =>
        {
            using var dialog = new ColorDialog { Color=preview.BackColor, FullOpen=true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            { loading=true; red.Value=dialog.Color.R; green.Value=dialog.Color.G; blue.Value=dialog.Color.B; loading=false; UpdateDraft(); }
        };
        SetBusy(false);
    }
    void SetBusy(bool value)
    {
        scan.Enabled = connect.Enabled = !value;
        info.Enabled = !value && client != null;
        applyLight.Enabled = !value && currentLighting != null;
        restoreLight.Enabled = !value && originalLighting != null;
        zone.Enabled = chooseColor.Enabled = !value && currentLighting != null;
        red.Enabled = green.Enabled = blue.Enabled = brightness.Enabled = breath.Enabled = !value && currentLighting != null;
        speed.Enabled = !value && currentLighting != null && breath.Checked;
        foreach(var button in colorPresets)button.Enabled=!value&&client!=null&&currentLighting!=null;
        SetControlsBusy(value);
        SetActuatorsBusy(value);
        SetTurboBusy(value);
        SetMacrosBusy(value);
    }
    async Task Run(Func<Task> action)
    {
        if (busy || client == null) return;
        busy=true; SetBusy(true);
        try { await action(); }
        catch (Exception e) { Log("Operação não confirmada: " + e.Message); }
        finally { busy=false; SetBusy(false); }
    }
    async Task ReadLight()
    {
        byte[] data = await client!.ReadLighting();
        currentLighting = data;
        if (originalLighting == null)
        {
            originalLighting = data.ToArray();
            string name = $"lighting-backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.hex";
            File.WriteAllText(Path.Combine(DataDirectory,name),Convert.ToHexString(data));
            Log("Backup da iluminação original: " + name);
        }
        zone.Items.Clear(); for (int i=0;i<data.Length/6;i++) zone.Items.Add("Zona " + (i+1));
        zone.SelectedIndex=0;
        Log($"Iluminação lida: {data.Length/6} zona(s).");
    }
    void LoadZone()
    {
        if (currentLighting == null || zone.SelectedIndex < 0) return;
        int offset=zone.SelectedIndex*6; var data=currentLighting;
        loading=true;
        red.Value=data[offset]; green.Value=data[offset+1]; blue.Value=data[offset+2];
        brightness.Value=data[offset+5]; breath.Checked=(data[offset+4]&128)!=0;
        speed.Value=Math.Min(2048,((data[offset+4]&15)<<8)|data[offset+3]);
        brightnessValue.Text=$"Brilho: {brightness.Value} / 255";speedValue.Text=$"Velocidade: {speed.Value} / 2048";
        preview.BackColor=Color.FromArgb((int)red.Value,(int)green.Value,(int)blue.Value);
        loading=false;
    }
    void UpdateDraft()
    {
        if (loading || currentLighting == null || zone.SelectedIndex < 0) return;
        int offset=zone.SelectedIndex*6; var data=currentLighting;
        data[offset]=(byte)red.Value; data[offset+1]=(byte)green.Value; data[offset+2]=(byte)blue.Value;
        data[offset+5]=(byte)brightness.Value;
        // Preserve unrelated flag bits. KeyLinker uses bit 7 for breathing and low 12 bits for speed.
        data[offset+3]=(byte)((int)speed.Value&255);
        data[offset+4]=(byte)((data[offset+4]&0x70)|((int)speed.Value>>8)|(breath.Checked?128:0));
        speed.Enabled=breath.Checked;
        preview.BackColor=Color.FromArgb((int)red.Value,(int)green.Value,(int)blue.Value);
    }
}
