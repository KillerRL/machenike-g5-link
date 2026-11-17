using System.Text.Json;

namespace G5Link;

internal sealed record TurboInterval(int Milliseconds)
{
    public override string ToString()=>$"{Milliseconds} ms";
}

internal sealed partial class MainWindow
{
    readonly Button applyTurbo=new(){Text="Aplicar turbo",AutoSize=true};
    readonly Button restoreTurbo=new(){Text="Restaurar original",AutoSize=true};
    readonly ComboBox turboInterval=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=100};
    readonly DataGridView turboGrid=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    TurboProfile? turboProfile,originalTurbo;
    const string TurboOff="Desligado",TurboHold="Enquanto pressionado",TurboAuto="Automático";
    void SetupTurbo()
    {
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=105,Padding=new Padding(10)};
        top.Controls.AddRange([new Label{Text="Intervalo:",AutoSize=true,Padding=new Padding(0,6,0,0)},turboInterval,applyTurbo,restoreTurbo]);
        top.SetFlowBreak(restoreTurbo,true);
        top.Controls.Add(new Label{Text="Selecione repetição enquanto mantém o botão pressionado, ou automática quando suportada. Aplicar grava as opções no controle.",AutoSize=true});
        turboPage.Controls.Add(turboGrid);turboPage.Controls.Add(top);
        turboGrid.Columns.Add(new DataGridViewTextBoxColumn{Name="Key",HeaderText="Botão",ReadOnly=true});
        turboGrid.Columns.Add(new DataGridViewComboBoxColumn{Name="Mode",HeaderText="Modo de repetição"});
        turboGrid.DataError+=(_,e)=>{e.ThrowException=false;Log("Seleção de turbo inválida; leia novamente.");};
        applyTurbo.Click+=async(_,_)=>await Run(async()=>
        {
            if(turboProfile==null||turboInterval.SelectedItem is not TurboInterval interval)return;
            turboGrid.EndEdit();var data=turboProfile.Data.ToArray();data[0]=(byte)(interval.Milliseconds/10);
            for(int i=0;i<turboProfile.Keys.Length;i++)
            {
                string mode=Convert.ToString(turboGrid.Rows[i].Cells[1].Value)??TurboOff;
                if(mode==TurboAuto&&!turboProfile.AutoKeys.Contains(turboProfile.Keys[i]))throw new ArgumentException("Esse botão não suporta turbo automático.");
                int manual=3+i/8,auto=6+i/8,mask=1<<(i%8);
                data[manual]=(byte)((data[manual]&~mask)|(mode==TurboHold?mask:0));
                data[auto]=(byte)((data[auto]&~mask)|(mode==TurboAuto?mask:0));
            }
            await client!.SaveTurbo(turboProfile,data);turboProfile=await client.ReadTurbo();PopulateTurbo();Log("Turbo aplicado e confirmado por releitura.");
        });
        restoreTurbo.Click+=async(_,_)=>await Run(async()=>
        {
            if(originalTurbo==null)return;var current=await client!.ReadTurbo();
            if(!current.Keys.SequenceEqual(originalTurbo.Keys)||!current.AutoKeys.SequenceEqual(originalTurbo.AutoKeys))throw new IOException("Lista de suporte turbo mudou.");
            await client.SaveTurbo(current,originalTurbo.Data);turboProfile=await client.ReadTurbo();PopulateTurbo();Log("Turbo original restaurado e confirmado.");
        });
        SetTurboBusy(false);
    }
    async Task ReadTurboProfile()
    {
        turboProfile=await client!.ReadTurbo();
        if(originalTurbo==null)
        {
            originalTurbo=new(turboProfile.Keys.ToArray(),turboProfile.AutoKeys.ToArray(),turboProfile.Data.ToArray());
            var path=Path.Combine(AppContext.BaseDirectory,$"turbo-backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.WriteAllText(path,JsonSerializer.Serialize(originalTurbo));Log("Backup: "+Path.GetFileName(path));
        }
        PopulateTurbo();Log($"Turbo lido: {turboProfile.Keys.Length} botões; automático em {turboProfile.AutoKeys.Length}.");
    }
    void PopulateTurbo()
    {
        if(turboProfile==null)return;
        turboInterval.Items.Clear();
        int[] intervals=[1000,500,330,250,200,160,140,120,110,100,90,80,70,60,50,40,30];
        int current=turboProfile.Data[0]*10;
        foreach(int value in intervals.Append(current).Distinct())if(value>0)turboInterval.Items.Add(new TurboInterval(value));
        turboInterval.SelectedItem=turboInterval.Items.Cast<TurboInterval>().FirstOrDefault(i=>i.Milliseconds==current);
        turboGrid.Rows.Clear();
        for(int i=0;i<turboProfile.Keys.Length;i++)
        {
            bool automatic=turboProfile.AutoKeys.Contains(turboProfile.Keys[i]);
            int row=turboGrid.Rows.Add();turboGrid.Rows[row].Cells[0].Value=KeyName(turboProfile.Keys[i]);
            var cell=(DataGridViewComboBoxCell)turboGrid.Rows[row].Cells[1];cell.Items.AddRange(TurboOff,TurboHold);if(automatic)cell.Items.Add(TurboAuto);
            int mask=1<<(i%8);string mode=(turboProfile.Data[3+i/8]&mask)!=0?TurboHold:TurboOff;
            if((turboProfile.Data[6+i/8]&mask)!=0)mode=TurboAuto;
            if(mode==TurboAuto&&!automatic)cell.Items.Add(TurboAuto); // Preserve an existing value; writes still enforce reported support.
            cell.Value=mode;
        }
    }
    void SetTurboBusy(bool value)
    {
        applyTurbo.Enabled=restoreTurbo.Enabled=turboGrid.Enabled=turboInterval.Enabled=!value&&turboProfile!=null;
    }
}
