using Windows.Gaming.Input;

namespace G5Link;

internal sealed class BufferedPanel : Panel { public BufferedPanel() { DoubleBuffered=true; ResizeRedraw=true; } }

internal sealed partial class MainWindow
{
    readonly System.Windows.Forms.Timer liveTimer=new(){Interval=33};
    readonly Panel liveCanvas=new BufferedPanel(){Width=930,Height=350,BackColor=Color.White};
    readonly Panel topCanvas=new BufferedPanel(){Width=930,Height=150,BackColor=Color.White};
    readonly Label liveStatus=new(){AutoSize=true,Text="Aguardando controle de jogo no Windows…",Padding=new Padding(4,8,4,8)};
    Image? controllerImage;
    GamepadReading liveReading;
    GamepadReading lastRenderedReading;
    bool liveAvailable;
    string rawInputs="";

    void SetupLiveView(TabControl tabs)
    {
        var page=new TabPage("Teste ao vivo"){BackColor=Surface,UseVisualStyleBackColor=false};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(12)};
        panel.Controls.Add(new Label{Text="Teste dos botões e analógicos",Font=new Font("Segoe UI",12,FontStyle.Bold),AutoSize=true});
        panel.Controls.Add(liveStatus);
        panel.Controls.Add(new Label{Text="Mostra as entradas de jogo recebidas pelo Windows, após eventual remapeamento.",AutoSize=true});
        panel.Controls.Add(liveCanvas);panel.Controls.Add(topCanvas);page.Controls.Add(panel);tabs.TabPages.Insert(0,page);tabs.SelectedTab=page;
        var imagePath=Path.Combine(AppContext.BaseDirectory,"controller-white.jpg");
        if(File.Exists(imagePath))controllerImage=Image.FromFile(imagePath);
        liveCanvas.Paint+=(_,e)=>DrawLiveController(e.Graphics);
        topCanvas.Paint+=(_,e)=>DrawTopView(e.Graphics);
        liveTimer.Tick+=(_,_)=>UpdateLiveReading();liveTimer.Start();
    }
    void UpdateLiveReading()
    {
        var previous=lastRenderedReading;bool wasAvailable=liveAvailable;string oldRaw=rawInputs;
        try
        {
            var pads=Gamepad.Gamepads;
            if(pads.Count!=1)
            {
                liveAvailable=false;
                liveStatus.Text=pads.Count==0?"Entrada de jogo indisponível no Windows.":"Mais de um controle de jogo detectado; prévia pausada para evitar identificação incorreta.";
            }
            else
            {
                liveReading=pads[0].GetCurrentReading();liveAvailable=true;
                liveStatus.Text="Entrada de jogo detectada • pressione os botões para testar";
                try
                {
                    var raw=RawGameController.FromGameController(pads[0]);
                    if(raw!=null)
                    {
                        var buttons=new bool[raw.ButtonCount];var switches=new GameControllerSwitchPosition[raw.SwitchCount];var axes=new double[raw.AxisCount];
                        raw.GetCurrentReading(buttons,switches,axes);
                        rawInputs=string.Join(", ",buttons.Select((pressed,index)=>(pressed,index)).Where(x=>x.pressed).Select(x=>$"{x.index+1} ({raw.GetButtonLabel(x.index)})"));
                    }
                    else rawInputs="";
                }
                catch(Exception) {rawInputs="Indisponíveis";}
            }
        }
        catch(Exception) {liveAvailable=false;liveStatus.Text="Entrada de jogo indisponível neste perfil do Windows.";}
        bool moved=Math.Abs(previous.LeftThumbstickX-liveReading.LeftThumbstickX)>.02||Math.Abs(previous.LeftThumbstickY-liveReading.LeftThumbstickY)>.02||Math.Abs(previous.RightThumbstickX-liveReading.RightThumbstickX)>.02||Math.Abs(previous.RightThumbstickY-liveReading.RightThumbstickY)>.02;
        bool changed=previous.Buttons!=liveReading.Buttons || Math.Abs(previous.LeftTrigger-liveReading.LeftTrigger)>.01 || Math.Abs(previous.RightTrigger-liveReading.RightTrigger)>.01 || (previous.LeftTrigger>.05)!=(liveReading.LeftTrigger>.05) || (previous.RightTrigger>.05)!=(liveReading.RightTrigger>.05);
        if(liveCanvas.Visible && (wasAvailable!=liveAvailable || moved || changed || oldRaw!=rawInputs)){lastRenderedReading=liveReading;liveCanvas.Invalidate();topCanvas.Invalidate();}
    }
    void DrawTopView(Graphics g)
    {
        g.Clear(Color.White);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var title=new Font("Segoe UI",10,FontStyle.Bold);using var note=new Font("Segoe UI",9);
        using var ink=new SolidBrush(Color.FromArgb(29,39,56));
        g.DrawString("Vista superior • esquema",title,ink,20,5);
        g.DrawString("A posição dos controles é ilustrativa; a resposta vem do controle conectado.",note,ink,20,27);
        using var shell=new SolidBrush(Color.FromArgb(236,240,245));using var border=new Pen(Color.FromArgb(125,140,160),1.5f);
        void Rounded(Brush brush,RectangleF bounds,float radius)
        {
            using var path=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;
            path.AddArc(bounds.X,bounds.Y,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Y,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.X,bounds.Bottom-d,d,d,90,90);path.CloseFigure();
            g.FillPath(brush,path);g.DrawPath(border,path);
        }
        Rounded(shell,new RectangleF(90,77,450,53),20);
        void Control(string name,float x,float y,bool down,double value=0)
        {
            float displacement=down?3:0;var shape=new RectangleF(x,y+displacement,90,30);
            using var fill=new SolidBrush(Color.FromArgb(down?205:248,down?225:250,down?255:253));
            Rounded(fill,shape,9);
            g.DrawString(value>0?$"{name} {(int)(value*100)}%":name,note,ink,x+12,y+7+displacement);
        }
        var b=liveReading.Buttons;
        Control("LT",95,53,liveAvailable&&liveReading.LeftTrigger>.05,liveAvailable?liveReading.LeftTrigger:0);
        Control("LB",190,76,liveAvailable&&(b&GamepadButtons.LeftShoulder)!=0);
        Control("RB",350,76,liveAvailable&&(b&GamepadButtons.RightShoulder)!=0);
        Control("RT",445,53,liveAvailable&&liveReading.RightTrigger>.05,liveAvailable?liveReading.RightTrigger:0);
    }
    void DrawLiveController(Graphics g)
    {
        g.Clear(Color.White);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if(controllerImage==null) {g.DrawString("Imagem do controle indisponível",Font,Brushes.DimGray,15,15);return;}
        var rect=new Rectangle(20,0,510,342);
        g.DrawImage(controllerImage,rect,new Rectangle(100,250,820,550),GraphicsUnit.Pixel);
        using var heading=new Font("Segoe UI",10,FontStyle.Bold);using var note=new Font("Segoe UI",9);
        using var ink=new SolidBrush(Color.FromArgb(29,39,56));using var muted=new SolidBrush(Color.FromArgb(100,110,125));
        g.DrawString("Botões adicionais",heading,ink,560,20);
        g.DrawString("Power / Home: sinal não identificado\nFN: sinal não identificado\nM1 / M2: sinal não identificado\n\nEntradas paddle, quando disponíveis:",note,muted,new RectangleF(560,48,340,100));
        if(!liveAvailable)return;
        g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        void Dot(double x,double y,double radius,bool on)
        {
            if(!on)return;float cx=(float)(rect.X+(x-100)*rect.Width/820),cy=(float)(rect.Y+(y-250)*rect.Height/550),r=(float)(radius*rect.Width/820);
            float inset=r*.08f;
            var depressed=new RectangleF(cx-r+inset,cy-r+inset+1,r*2-inset*2,r*2-inset*2);
            using var clip=new System.Drawing.Drawing2D.GraphicsPath();clip.AddEllipse(depressed);
            var saved=g.Save();g.SetClip(clip,System.Drawing.Drawing2D.CombineMode.Intersect);
            g.DrawImage(controllerImage,depressed,new RectangleF((float)(x-radius),(float)(y-radius),(float)(radius*2),(float)(radius*2)),GraphicsUnit.Pixel);
            g.Restore(saved);
        }
        var b=liveReading.Buttons;
        bool Has(GamepadButtons flag)=>(b&flag)!=0;
        GamepadButtons[] paddles=[GamepadButtons.Paddle1,GamepadButtons.Paddle2,GamepadButtons.Paddle3,GamepadButtons.Paddle4];
        for(int i=0;i<paddles.Length;i++)
        {
            var box=new Rectangle(560+(i%2)*104,150+(i/2)*40,95,30);
            using var fill=new SolidBrush(Has(paddles[i])?Color.FromArgb(180,215,255):Color.FromArgb(238,241,246));
            g.FillRectangle(fill,box);g.DrawString($"Paddle {i+1}",note,ink,box.X+8,box.Y+7);
        }
        g.DrawString("Entradas brutas ativas:",note,ink,560,238);
        g.DrawString(string.IsNullOrEmpty(rawInputs)?"Nenhuma":rawInputs,note,muted,new RectangleF(560,260,340,80));
        Dot(713,445,28,Has(GamepadButtons.A));Dot(759,399,28,Has(GamepadButtons.B));Dot(662,399,28,Has(GamepadButtons.X));Dot(711,351,28,Has(GamepadButtons.Y));
        bool up=Has(GamepadButtons.DPadUp),down=Has(GamepadButtons.DPadDown),left=Has(GamepadButtons.DPadLeft),right=Has(GamepadButtons.DPadRight);
        if(up||down||left||right)
        {
            // Reuse the full photographed cross instead of drawing a marker over one direction.
            const float x=366,y=460,width=114,height=116;
            float scaleX=rect.Width/820f,scaleY=rect.Height/550f;
            var origin=new RectangleF(rect.X+(x-100)*scaleX,rect.Y+(y-250)*scaleY,width*scaleX,height*scaleY);
            float dx=(right?1.5f:0)-(left?1.5f:0),dy=(down?1.5f:0)-(up?1.5f:0);
            var pressed=new RectangleF(origin.X+dx,origin.Y+dy,origin.Width,origin.Height);
            using var cross=new System.Drawing.Drawing2D.GraphicsPath();
            cross.AddRectangle(new RectangleF(origin.X+origin.Width*.32f,origin.Y,origin.Width*.36f,origin.Height));
            cross.AddRectangle(new RectangleF(origin.X,origin.Y+origin.Height*.31f,origin.Width,origin.Height*.38f));
            var crossState=g.Save();g.SetClip(cross,System.Drawing.Drawing2D.CombineMode.Intersect);
            g.DrawImage(controllerImage,pressed,new RectangleF(x,y,width,height),GraphicsUnit.Pixel);
            g.Restore(crossState);
        }
        Dot(430,353,22,Has(GamepadButtons.View));Dot(613,353,22,Has(GamepadButtons.Menu));
        Dot(320,300,28,Has(GamepadButtons.LeftShoulder));Dot(710,300,28,Has(GamepadButtons.RightShoulder));
        Dot(270,290,25,liveReading.LeftTrigger>0.05);Dot(760,290,25,liveReading.RightTrigger>0.05);
        void Stick(double x,double y,double inputX,double inputY,bool pressed)
        {
            double dx=Math.Abs(inputX)<.025?0:inputX*30,dy=Math.Abs(inputY)<.025?0:-inputY*30;
            float cx=(float)(rect.X+(x+dx-100)*rect.Width/820),cy=(float)(rect.Y+(y+dy-250)*rect.Height/550);
            float radius=(float)(27*rect.Width/820);
            using var fill=new SolidBrush(Color.FromArgb(pressed?210:150,35,126,235));
            using var edge=new Pen(Color.FromArgb(230,22,90,190),pressed?3:2);
            g.FillEllipse(fill,cx-radius,cy-radius,radius*2,radius*2);
            g.DrawEllipse(edge,cx-radius,cy-radius,radius*2,radius*2);
        }
        Stick(327,382,liveReading.LeftThumbstickX,liveReading.LeftThumbstickY,Has(GamepadButtons.LeftThumbstick));
        Stick(620,498,liveReading.RightThumbstickX,liveReading.RightThumbstickY,Has(GamepadButtons.RightThumbstick));
    }
}
