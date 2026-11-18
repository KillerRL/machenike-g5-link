using System.Text.Json;

namespace G5Link;

internal sealed partial class MainWindow
{
    readonly Button applyTriggers=new(){Text="Aplicar gatilhos",AutoSize=true};
    readonly Button applyMotors=new(){Text="Aplicar vibração",AutoSize=true};
    readonly Button restoreActuators=new(){Text="Restaurar originais",AutoSize=true};
    readonly NumericUpDown[,] triggerValues=new NumericUpDown[2,6];
    readonly NumericUpDown[] motorValues=new NumericUpDown[4];
    readonly Panel[] triggerPreview=[new(){Width=140,Height=65},new(){Width=140,Height=65}];
    ActuatorProfile? actuatorProfile,originalActuators;

    void SetupActuators()
    {
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),AutoScroll=true};actuatorsPage.Controls.Add(panel);
        var actions=new FlowLayoutPanel{Width=960,Height=38};actions.Controls.AddRange([applyTriggers,applyMotors,restoreActuators]);panel.Controls.Add(actions);
        panel.Controls.Add(new Label{Text="Gatilhos: zonas mortas em %; curva de resposta com dois pontos entrada/saída de 0 a 255.",AutoSize=true});
        for(int t=0;t<2;t++)
        {
            int which=t;
            var row=new FlowLayoutPanel{Width=960,Height=80};row.Controls.Add(new Label{Text=t==0?"LT / L2":"RT / R2",Width=75,Height=60,Padding=new Padding(0,20,0,0)});
            string[] labels=["Morta interna","Morta externa","P1 entrada","P1 saída","P2 entrada","P2 saída"];
            for(int i=0;i<6;i++)
            {
                var cell=new FlowLayoutPanel{Width=108,Height=70,FlowDirection=FlowDirection.TopDown};cell.Controls.Add(new Label{Text=labels[i],AutoSize=true});
                var number=Number(i<2?100:255);if(i<2){number.DecimalPlaces=1;number.Increment=.5m;}triggerValues[t,i]=number;number.ValueChanged+=(_,_)=>triggerPreview[which].Invalidate();cell.Controls.Add(number);row.Controls.Add(cell);
            }
            triggerPreview[t].Paint+=(_,e)=>DrawTriggerPreview(e.Graphics,which);row.Controls.Add(triggerPreview[t]);
            panel.Controls.Add(row);
        }
        panel.Controls.Add(new Label{Text="Vibração: intensidade de 0 (desligada) a 255 (máxima). O botão Aplicar salva a intensidade; não aciona os motores.",AutoSize=true});
        var motors=new FlowLayoutPanel{Width=960,Height=80};
        string[] names=["Motor esquerdo","Motor direito","Motor superior esquerdo","Motor superior direito"];
        for(int i=0;i<4;i++)
        {
            var cell=new FlowLayoutPanel{Width=190,Height=65,FlowDirection=FlowDirection.TopDown};cell.Controls.Add(new Label{Text=names[i],AutoSize=true});motorValues[i]=Number(255);cell.Controls.Add(motorValues[i]);motors.Controls.Add(cell);
        }
        panel.Controls.Add(motors);
        applyTriggers.Click+=async(_,_)=>await Run(async()=>
        {
            if(actuatorProfile==null)return;var data=actuatorProfile.Triggers.ToArray();
            for(int t=0;t<2;t++)
            {
                int offset=t*7;
                for(int i=0;i<6;i++)data[offset+i]=(byte)(triggerValues[t,i].Value*(i<2?2:1));
                if(data[offset]+data[offset+1]>=200)throw new ArgumentException("As zonas mortas precisam deixar uma faixa ativa no gatilho.");
                if(data[offset+2]>data[offset+4]||data[offset+3]>data[offset+5])throw new ArgumentException("Os pontos da curva devem estar em ordem crescente.");
            }
            await client!.SaveTriggers(actuatorProfile,data);actuatorProfile=await client.ReadActuators();PopulateActuators(motors:false);Log("Gatilhos aplicados e confirmados por releitura.");
        });
        applyMotors.Click+=async(_,_)=>await Run(async()=>
        {
            if(actuatorProfile==null)return;var data=actuatorProfile.Motors.ToArray();
            for(int i=0;i<4;i++)data[i]=(byte)motorValues[i].Value;
            await client!.SaveMotors(actuatorProfile,data);actuatorProfile=await client.ReadActuators();PopulateActuators(triggers:false);Log("Vibração aplicada e confirmada por releitura.");
        });
        restoreActuators.Click+=async(_,_)=>await Run(async()=>
        {
            if(originalActuators==null)return;
            await client!.SaveTriggers(await client.ReadActuators(),originalActuators.Triggers);
            await client.SaveMotors(await client.ReadActuators(),originalActuators.Motors);
            actuatorProfile=await client.ReadActuators();PopulateActuators();Log("Gatilhos e vibração originais restaurados e confirmados.");
        });
        SetActuatorsBusy(false);
    }
    async Task ReadActuatorProfile()
    {
        actuatorProfile=await client!.ReadActuators();
        if(originalActuators==null)
        {
            originalActuators=new(actuatorProfile.Triggers.ToArray(),actuatorProfile.Motors.ToArray());
            var path=Path.Combine(DataDirectory,$"actuators-backup-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.WriteAllText(path,JsonSerializer.Serialize(originalActuators));Log("Backup: "+Path.GetFileName(path));
        }
        PopulateActuators();Log("Gatilhos e vibração lidos.");
    }
    void PopulateActuators(bool triggers=true,bool motors=true)
    {
        if(actuatorProfile==null)return;
        if(triggers) for(int t=0;t<2;t++)for(int i=0;i<6;i++)triggerValues[t,i].Value=actuatorProfile.Triggers[t*7+i]/(i<2?2m:1m);
        if(motors) for(int i=0;i<4;i++)motorValues[i].Value=actuatorProfile.Motors[i];
    }
    void SetActuatorsBusy(bool value)
    {
        applyTriggers.Enabled=applyMotors.Enabled=restoreActuators.Enabled=!value&&actuatorProfile!=null;
        foreach(var n in triggerValues)if(n!=null)n.Enabled=!value&&actuatorProfile!=null;
        foreach(var n in motorValues)if(n!=null)n.Enabled=!value&&actuatorProfile!=null;
    }
}
