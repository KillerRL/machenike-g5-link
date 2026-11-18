using System.Text.Json;

namespace G5Link;

internal sealed record MacroBackup(MacroCatalog Catalog,MacroSlot[] Slots);

internal sealed partial class MainWindow
{
    readonly Button exportMacros=new(){Text="Exportar backup…",AutoSize=true};
    readonly DataGridView macroGrid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
    MacroBackup? macroBackup;
    void SetupMacros()
    {
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=100,Padding=new Padding(10)};
        top.Controls.Add(exportMacros);top.SetFlowBreak(exportMacros,true);
        top.Controls.Add(new Label{Text="Selecione um slot para editar. Sequências com formato não reconhecido são preservadas e ficam bloqueadas para gravação.",AutoSize=true});
        macroGrid.Columns.Add("Slot","Slot");macroGrid.Columns.Add("Size","Dados preservados");macroGrid.Columns.Add("State","Estado");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,90));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,100));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        top.Dock=DockStyle.Fill;layout.Controls.Add(top,0,0);layout.Controls.Add(macroGrid,0,1);layout.Controls.Add(CreateMacroEditor(),0,2);macrosPage.Controls.Add(layout);
        exportMacros.Click+=(_,_)=>
        {
            if(macroBackup==null)return;
            using var dialog=new SaveFileDialog{Filter="Backup de macros (*.json)|*.json",FileName=$"G5ProMax-macros-{DateTime.Now:yyyyMMdd-HHmmss}.json"};
            if(dialog.ShowDialog(this)==DialogResult.OK)
            {
                try{File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(macroBackup,new JsonSerializerOptions{WriteIndented=true}));Log("Backup de macros exportado.");}
                catch(Exception e){Log("Exportação falhou: "+e.Message);}
            }
        };
        SetMacrosBusy(false);
    }
    async Task ReadMacros()
    {
        var catalog=await client!.ReadMacroCatalog();
        if(catalog.Extended)throw new IOException("O formato estendido de macro ainda não é suportado nesta leitura.");
        var slots=new List<MacroSlot>();
        for(int slot=0;slot<8;slot++)if((catalog.SlotMask&(1<<slot))!=0)slots.Add(await client.ReadMacro(slot));
        var backup=new MacroBackup(catalog,slots.ToArray());
        string file=$"macros-backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json";
        File.WriteAllText(Path.Combine(DataDirectory,file),JsonSerializer.Serialize(backup,new JsonSerializerOptions{WriteIndented=true}));
        macroBackup=backup;macroSessionOriginal??=backup;FillMacroSlots();
        Log($"Macros lidas sem alteração: {slots.Count} slots. Backup: {file}");
    }
    void SetMacrosBusy(bool value)
    {
        exportMacros.Enabled=!value&&macroBackup!=null;
        SetMacroEditorBusy(value);
    }
}
