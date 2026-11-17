namespace G5Link;

internal sealed partial class MainWindow
{
    MacroBackup? macroSessionOriginal;
    readonly DataGridView macroSteps=new(){Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    readonly Button addMacroStep=new(){Text="Adicionar passo",AutoSize=true},releaseMacroStep=new(){Text="Soltar todos",AutoSize=true},removeMacroStep=new(){Text="Remover passo",AutoSize=true};
    readonly Button saveMacro=new(){Text="Aplicar macro",AutoSize=true},restoreMacro=new(){Text="Restaurar slot original",AutoSize=true};
    readonly CheckBox macroRepeat=new(){Text="Repetir",AutoSize=true};
    readonly NumericUpDown macroRepeatInterval=new(){Minimum=0,Maximum=20475,Increment=5,Width=80};
    readonly Label macroNotice=new(){AutoSize=true};
    bool macroEditable,loadingMacro;
    static string MacroToken(byte key)=>key switch
    {
        1=>"A",2=>"B",3=>"X",4=>"Y",5=>"LB",6=>"RB",7=>"LT",8=>"RT",9=>"BACK",10=>"START",11=>"L3",12=>"R3",13=>"BAIXO",14=>"CIMA",15=>"DIREITA",16=>"ESQUERDA",17=>"HOME",_=>$"#{key}"
    };
    MacroSlot? SelectedMacro()=>macroBackup!=null&&macroGrid.CurrentRow?.Index is int index&&index>=0&&index<macroBackup.Slots.Length?macroBackup.Slots[index]:null;
    Control CreateMacroEditor()
    {
        var panel=new Panel{Dock=DockStyle.Fill};var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=110,Padding=new Padding(5)};
        top.Controls.AddRange([addMacroStep,releaseMacroStep,removeMacroStep,macroRepeat,new Label{Text="Intervalo de repetição (ms):",AutoSize=true},macroRepeatInterval,saveMacro,restoreMacro]);
        top.SetFlowBreak(restoreMacro,true);top.Controls.Add(macroNotice);
        macroSteps.Columns.Add("Keys","Botões (ex.: A+B; vazio = soltar)");macroSteps.Columns.Add("Duration","Duração (ms, múltiplo de 5)");
        panel.Controls.Add(macroSteps);panel.Controls.Add(top);
        macroGrid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;macroGrid.MultiSelect=false;
        macroGrid.SelectionChanged+=(_,_)=>{if(!loadingMacro)LoadMacroEditor();};
        addMacroStep.Click+=(_,_)=>{if(macroSteps.Rows.Count<47)macroSteps.Rows.Add("A",100);};
        releaseMacroStep.Click+=(_,_)=>{if(macroSteps.Rows.Count<47)macroSteps.Rows.Add("",100);};
        removeMacroStep.Click+=(_,_)=>{if(macroSteps.CurrentRow is {} row)macroSteps.Rows.Remove(row);};
        saveMacro.Click+=async(_,_)=>await Run(async()=>
        {
            var slot=SelectedMacro();if(slot==null||macroBackup==null||!macroEditable)return;macroSteps.EndEdit();var catalog=macroBackup.Catalog;
            var tokens=catalog.Keys.Where(k=>k is not 18 and not 19).ToDictionary(k=>MacroToken(k),k=>k,StringComparer.OrdinalIgnoreCase);
            var steps=new List<MacroStep>();
            foreach(DataGridViewRow row in macroSteps.Rows)
            {
                string text=Convert.ToString(row.Cells[0].Value)??"";var keys=new List<byte>();
                foreach(string token in text.Split(['+',','],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
                {
                    if(!tokens.TryGetValue(token,out byte key))throw new ArgumentException("Botão não suportado: "+token);keys.Add(key);
                }
                if(!int.TryParse(Convert.ToString(row.Cells[1].Value),out int ms))throw new ArgumentException("Informe a duração de cada passo em milissegundos.");
                steps.Add(new(MacroCodec.State(catalog,keys),ms));
            }
            if(steps.Count>0&&steps[^1].State!=MacroCodec.Neutral(catalog))throw new ArgumentException("Adicione Soltar todos ao final da sequência.");
            byte[] data=MacroCodec.Encode(catalog,steps,macroRepeat.Checked,(int)macroRepeatInterval.Value);
            await client!.SaveMacro(catalog,slot,data);await RefreshMacroSlot(slot.Index);Log($"Macro M{slot.Index+1} aplicada e confirmada por releitura.");
        });
        restoreMacro.Click+=async(_,_)=>await Run(async()=>
        {
            var current=SelectedMacro();if(current==null||macroBackup==null||macroSessionOriginal==null)return;
            var original=macroSessionOriginal.Slots.First(s=>s.Index==current.Index);
            await client!.SaveMacro(macroBackup.Catalog,current,original.Data);await RefreshMacroSlot(current.Index);Log($"Macro M{current.Index+1} original restaurada e confirmada.");
        });
        return panel;
    }
    void FillMacroSlots(int? selectedSlot=null)
    {
        if(macroBackup==null)return;loadingMacro=true;macroGrid.Rows.Clear();
        foreach(var slot in macroBackup.Slots)
        {
            bool known=MacroCodec.TryDecode(macroBackup.Catalog,slot.Data,out var steps,out _,out _);
            macroGrid.Rows.Add($"M{slot.Index+1}",$"{slot.Data.Length} bytes",slot.Data.Length==0?"Vazio":known?$"{steps.Count} passos":"Formato não reconhecido — preservado");
        }
        int index=Array.FindIndex(macroBackup.Slots,s=>s.Index==selectedSlot);if(index<0)index=0;
        if(macroGrid.Rows.Count>0)macroGrid.CurrentCell=macroGrid.Rows[index].Cells[0];loadingMacro=false;LoadMacroEditor();
    }
    async Task RefreshMacroSlot(int slot)
    {
        var updated=await client!.ReadMacro(slot);var backup=macroBackup!;
        macroBackup=new(backup.Catalog,backup.Slots.Select(s=>s.Index==slot?updated:s).ToArray());FillMacroSlots(slot);
    }
    void LoadMacroEditor()
    {
        macroSteps.Rows.Clear();var slot=SelectedMacro();macroEditable=false;
        if(slot==null||macroBackup==null){macroNotice.Text="Leia as macros e selecione um slot.";return;}
        var catalog=macroBackup.Catalog;
        macroEditable=MacroCodec.TryDecode(catalog,slot.Data,out var steps,out bool repeat,out int interval);
        if(macroEditable)
        {
            var positions=MacroCodec.Positions(catalog);
            foreach(var step in steps)
            {
                string buttons=string.Join("+",positions.Where(p=>p.Key is not 18 and not 19&&(step.State&(1u<<p.Value))!=0).Select(p=>MacroToken(p.Key)));
                macroSteps.Rows.Add(buttons,step.Milliseconds);
            }
            macroRepeat.Checked=repeat;macroRepeatInterval.Value=interval;
            macroNotice.Text=$"M{slot.Index+1}: até 47 passos digitais. Botões: "+string.Join(", ",catalog.Keys.Where(k=>k is not 18 and not 19).Select(MacroToken));
        }
        else macroNotice.Text=$"M{slot.Index+1}: formato não reconhecido. Sequência preservada; edição e gravação bloqueadas.";
        SetMacroEditorBusy(busy);
    }
    void SetMacroEditorBusy(bool value)
    {
        bool enabled=!value&&macroEditable&&client!=null;
        macroSteps.Enabled=addMacroStep.Enabled=releaseMacroStep.Enabled=removeMacroStep.Enabled=saveMacro.Enabled=macroRepeat.Enabled=macroRepeatInterval.Enabled=enabled;
        var slot=SelectedMacro();var original=macroSessionOriginal?.Slots.FirstOrDefault(s=>s.Index==slot?.Index);
        restoreMacro.Enabled=enabled&&original!=null&&macroBackup!=null&&MacroCodec.TryDecode(macroBackup.Catalog,original.Data,out _,out _,out _);
    }
}
