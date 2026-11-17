namespace G5Link;

internal sealed partial class MainWindow
{
    readonly ComboBox zone = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    readonly NumericUpDown red = Number(255), green = Number(255), blue = Number(255), brightness = Number(255), speed = Number(2048);
    readonly CheckBox breath = new() { Text = "Respiração", AutoSize = true };
    readonly Button applyLight = new() { Text = "Aplicar iluminação", AutoSize = true, Enabled = false };
    readonly Button restoreLight = new() { Text = "Restaurar original", AutoSize = true, Enabled = false };
    readonly Button chooseColor = new() { Text = "Escolher cor", AutoSize = true };
    readonly Panel preview = new() { Width=40, Height=25, BorderStyle=BorderStyle.FixedSingle };
    readonly Panel lightingPreview = new() { Width=900, Height=245, Margin=new Padding(4,24,4,4) };
    byte[]? currentLighting, originalLighting;
    bool loading;
    static NumericUpDown Number(int max) => new() { Minimum=0, Maximum=max, Width=65 };

    void SetupLighting()
    {
        var panel = new FlowLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(10), AutoScroll=true };
        void Label(string text) => panel.Controls.Add(new Label { Text=text, AutoSize=true, Padding=new Padding(0,6,0,0) });
        Label("Iluminação"); panel.Controls.Add(zone);
        foreach (var item in new[] { ("R",red),("G",green),("B",blue),("Brilho (0–255)",brightness),("Velocidade (0–2048)",speed) })
        { Label(item.Item1); panel.Controls.Add(item.Item2); item.Item2.ValueChanged += (_,_) => UpdateDraft(); }
        panel.Controls.AddRange([breath,preview,chooseColor,applyLight,restoreLight]);
        panel.SetFlowBreak(restoreLight,true);
        lightingPreview.Paint += (_,e)=>DrawLightingPreview(e.Graphics);
        panel.Controls.Add(lightingPreview);
        lightingPage.Controls.Add(panel);
        zone.SelectedIndexChanged += (_,_) => LoadZone();
        breath.CheckedChanged += (_,_) => UpdateDraft();
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
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory,name),Convert.ToHexString(data));
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
        preview.BackColor=Color.FromArgb((int)red.Value,(int)green.Value,(int)blue.Value);
        lightingPreview.Invalidate();
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
        lightingPreview.Invalidate();
    }
}
