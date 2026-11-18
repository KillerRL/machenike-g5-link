using System.Text.Json;

namespace G5Link;

internal sealed record KeyChoice(byte Id,string Name);

internal sealed partial class MainWindow
{
    readonly DataGridView keyGrid=new() {Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    readonly Button saveKeys=new() {Text="Aplicar botões",AutoSize=true};
    readonly Button saveSticks=new() {Text="Aplicar analógicos",AutoSize=true};
    readonly Button restoreControls=new() {Text="Restaurar botões e analógicos",AutoSize=true};
    readonly NumericUpDown[,] stickValues=new NumericUpDown[2,6];
    readonly CheckBox[,] stickFlags=new CheckBox[2,2];
    readonly Panel[] curvePreview=[new(){Width=140,Height=110},new(){Width=140,Height=110}];
    ControlProfile? profile,originalProfile;
    static string KeyName(byte id) => id switch
    {
        1=>"A",2=>"B",3=>"X",4=>"Y",5=>"LB / L1",6=>"RB / R1",7=>"LT / L2",8=>"RT / R2",
        9=>"Back / Select",10=>"Start / Menu",11=>"L3",12=>"R3",13=>"Direcional ↓",14=>"Direcional ↑",15=>"Direcional →",16=>"Direcional ←",17=>"Home",
        18=>"Analógico esquerdo",19=>"Analógico direito",20=>"L4",21=>"R4",22=>"L5",23=>"R5",24=>"ML",25=>"MR",26=>"MC",27=>"M",93=>"Macro − (93)",94=>"Macro + (94)",
        _=>$"Botão {id}"
    };
    static ControlProfile CloneProfile(ControlProfile p) => new(p.SupportedKeys.ToArray(),p.Mapping.ToArray(),p.Sticks.ToArray());
    void SetupControls()
    {
        keyGrid.Columns.Add(new DataGridViewTextBoxColumn {Name="Source",HeaderText="Botão físico",ReadOnly=true});
        keyGrid.Columns.Add(new DataGridViewComboBoxColumn {Name="Target",HeaderText="Enviar como",DisplayMember="Name",ValueMember="Id",ValueType=typeof(byte)});
        keyGrid.DataError+=(_,e)=>{e.ThrowException=false;Log("Seleção de botão inválida. Leia a configuração novamente.");};
        var top=new FlowLayoutPanel {Dock=DockStyle.Top,Height=270,Padding=new Padding(8),AutoScroll=true};
        var actions=new FlowLayoutPanel {Width=970,Height=36};actions.Controls.AddRange([saveKeys,saveSticks,restoreControls]); top.Controls.Add(actions);
        top.Controls.Add(new Label {Text="Analógicos: zonas mortas em % (0–100). Curva de sensibilidade: dois pontos de entrada/saída (0–255).",AutoSize=true});
        for(int stick=0;stick<2;stick++)
        {
            int which=stick;
            var row=new FlowLayoutPanel {Width=970,Height=88};
            row.Controls.Add(new Label {Text=stick==0?"Esquerdo":"Direito",Width=72,Padding=new Padding(0,8,0,0)});
            string[] labels=["Morta interna","Morta externa","P1 entrada","P1 saída","P2 entrada","P2 saída"];
            for(int i=0;i<6;i++)
            {
                var cell=new FlowLayoutPanel {Width=95,Height=66,FlowDirection=FlowDirection.TopDown};
                cell.Controls.Add(new Label {Text=labels[i],AutoSize=true});
                stickValues[stick,i]=Number(i<2?100:255);stickValues[stick,i].ValueChanged+=(_,_)=>curvePreview[which].Invalidate();
                cell.Controls.Add(stickValues[stick,i]);row.Controls.Add(cell);
            }
            var flags=new FlowLayoutPanel {Width=100,Height=66};
            for(int i=0;i<2;i++) {stickFlags[stick,i]=new CheckBox {Text=i==0?"Inverter X":"Inverter Y",AutoSize=true};flags.Controls.Add(stickFlags[stick,i]);}
            row.Controls.Add(flags);row.Controls.Add(curvePreview[stick]);
            curvePreview[stick].Paint+=(_,e)=>DrawCurve(e.Graphics,which);
            top.Controls.Add(row);
        }
        controlsPage.Controls.Add(keyGrid);controlsPage.Controls.Add(top);
        saveKeys.Click+=async(_,_)=>await Run(async()=>
        {
            if(profile==null)return;keyGrid.EndEdit();
            byte[] mapping=keyGrid.Rows.Cast<DataGridViewRow>().Select(r=>Convert.ToByte(r.Cells[1].Value)).ToArray();
            await client!.SaveMapping(profile,mapping);profile=await client.ReadControls();PopulateControls(sticks:false);Log("Botões aplicados e confirmados por releitura.");
        });
        saveSticks.Click+=async(_,_)=>await Run(async()=>
        {
            if(profile==null)return; byte[] data=profile.Sticks.ToArray();
            for(int s=0;s<2;s++)
            {
                int offset=s*7;
                for(int i=0;i<6;i++)data[offset+i]=(byte)stickValues[s,i].Value;
                if(data[offset]+data[offset+1]>=100)throw new ArgumentException("As zonas mortas precisam deixar uma faixa ativa no analógico.");
                if(data[offset+2]>data[offset+4]||data[offset+3]>data[offset+5])throw new ArgumentException("Os pontos da curva devem estar em ordem crescente.");
                data[offset+6]=(byte)((data[offset+6]&~3)|(stickFlags[s,0].Checked?1:0)|(stickFlags[s,1].Checked?2:0));
            }
            await client!.SaveSticks(profile,data);profile=await client.ReadControls();PopulateControls(keys:false);Log("Analógicos aplicados e confirmados por releitura.");
        });
        restoreControls.Click+=async(_,_)=>await Run(async()=>
        {
            if(originalProfile==null)return;
            var now=await client!.ReadControls();
            if(!now.SupportedKeys.SequenceEqual(originalProfile.SupportedKeys))throw new IOException("Lista de botões mudou; restauração cancelada.");
            await client.SaveMapping(now,originalProfile.Mapping);now=await client.ReadControls();
            await client.SaveSticks(now,originalProfile.Sticks);profile=await client.ReadControls();PopulateControls();Log("Botões e analógicos originais restaurados e confirmados.");
        });
        SetControlsBusy(false);
    }
    async Task ReadControlProfile()
    {
        profile=await client!.ReadControls();
        if(originalProfile==null)
        {
            originalProfile=CloneProfile(profile);
            var path=Path.Combine(DataDirectory,$"controls-backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.WriteAllText(path,JsonSerializer.Serialize(originalProfile));Log("Backup de botões e analógicos: "+Path.GetFileName(path));
        }
        PopulateControls();Log($"Configuração lida: {profile.SupportedKeys.Length} botões, dois analógicos.");
    }
    void PopulateControls(bool keys=true,bool sticks=true)
    {
        if(profile==null)return;
        if(keys)
        {
        var choices=profile.SupportedKeys.Concat(profile.Mapping).Distinct().Select(id=>new KeyChoice(id,KeyName(id))).ToList();
        ((DataGridViewComboBoxColumn)keyGrid.Columns[1]).DataSource=choices;
        keyGrid.Rows.Clear();
        for(int i=0;i<profile.SupportedKeys.Length;i++)keyGrid.Rows.Add(KeyName(profile.SupportedKeys[i]),profile.Mapping[i]);
        }
        if(sticks) for(int s=0;s<2;s++)
        {
            for(int i=0;i<6;i++) {stickValues[s,i].Maximum=i<2?100:255;stickValues[s,i].Value=profile.Sticks[s*7+i];}
            stickFlags[s,0].Checked=(profile.Sticks[s*7+6]&1)!=0;stickFlags[s,1].Checked=(profile.Sticks[s*7+6]&2)!=0;
        }
    }
    void SetControlsBusy(bool value)
    {
        saveKeys.Enabled=saveSticks.Enabled=restoreControls.Enabled=keyGrid.Enabled=!value&&profile!=null;
        foreach(var number in stickValues)if(number!=null)number.Enabled=!value&&profile!=null;
        foreach(var flag in stickFlags)if(flag!=null)flag.Enabled=!value&&profile!=null;
    }
    void DrawCurve(Graphics g,int stick)
    {
        g.Clear(Color.White);
        using var gridPen=new Pen(Color.LightGray);using var linePen=new Pen(Color.Teal,2);
        g.DrawRectangle(gridPen,4,4,100,68);
        float X(int index)=>4+(float)stickValues[stick,index].Value*100/255;
        float Y(int index)=>72-(float)stickValues[stick,index].Value*68/255;
        g.DrawLines(linePen,new PointF[]{new(4,72),new(X(2),Y(3)),new(X(4),Y(5)),new(104,4)});
    }
}
