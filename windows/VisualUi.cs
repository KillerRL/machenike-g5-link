using System.Drawing.Drawing2D;

namespace G5Link;

internal sealed partial class MainWindow
{
    static readonly Color Ink=Color.FromArgb(29,39,56), Surface=Color.FromArgb(247,249,252), Accent=Color.FromArgb(50,93,184);

    void ApplyVisualStyle(FlowLayoutPanel actions,TabControl tabs)
    {
        Font=new Font("Segoe UI",9F);
        BackColor=Surface; ForeColor=Ink;
        actions.BackColor=Color.FromArgb(231,237,247);
        tabs.Padding=new Point(14,7);
        foreach(TabPage page in tabs.TabPages) { page.UseVisualStyleBackColor=false; page.BackColor=Surface; }
        StyleChildren(this);
        log.BackColor=Color.FromArgb(30,40,58);log.ForeColor=Color.FromArgb(218,228,242);
        log.Font=new Font("Consolas",8.5F);log.BorderStyle=BorderStyle.FixedSingle;
    }
    static void StyleChildren(Control parent)
    {
        foreach(Control child in parent.Controls)
        {
            if(child is Button button)
            {
                button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Color.FromArgb(198,210,229);
                button.BackColor=Color.White;button.ForeColor=Ink;button.Padding=new Padding(5,2,5,2);
                if(button.Text.StartsWith("Aplicar",StringComparison.Ordinal))
                {
                    void UpdateButton() {button.BackColor=button.Enabled?Accent:Color.FromArgb(231,237,247);button.ForeColor=button.Enabled?Color.White:Color.FromArgb(130,143,161);button.FlatAppearance.BorderColor=button.Enabled?Accent:Color.FromArgb(210,219,231);}
                    button.EnabledChanged+=(_,_)=>UpdateButton();UpdateButton();
                }
            }
            if(child is DataGridView grid)
            {
                grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;
                grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(231,237,247);
                grid.ColumnHeadersDefaultCellStyle.ForeColor=Ink;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(215,228,249);
                grid.DefaultCellStyle.SelectionForeColor=Ink;grid.GridColor=Color.FromArgb(226,232,241);
                grid.RowTemplate.Height=27;
            }
            StyleChildren(child);
        }
    }

    void DrawLightingPreview(Graphics g)
    {
        g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Surface);
        using var title=new Font("Segoe UI",12,FontStyle.Bold);
        using var small=new Font("Segoe UI",9);
        using var dark=new SolidBrush(Ink); using var muted=new SolidBrush(Color.FromArgb(94,110,134));
        g.DrawString("Prévia da iluminação",title,dark,0,0);
        g.DrawString("Representação ilustrativa • a cor é enviada somente ao aplicar",small,muted,0,30);
        Color c=Color.FromArgb((int)(red.Value*brightness.Value/255),(int)(green.Value*brightness.Value/255),(int)(blue.Value*brightness.Value/255));
        if(controllerImage!=null)
        {
            var dest=new Rectangle(60,58,400,180);
            g.DrawImage(controllerImage,dest,new Rectangle(100,260,820,370),GraphicsUnit.Pixel);
            using var light=new Pen(Color.FromArgb(150,c),4);
            g.DrawEllipse(light,132,100,54,49);g.DrawEllipse(light,275,155,54,49);
        }
        g.DrawString($"RGB  {(int)red.Value}  /  {(int)green.Value}  /  {(int)blue.Value}",small,dark,495,104);
        g.DrawString($"Brilho  {(int)brightness.Value} / 255",small,muted,495,130);
        g.DrawString(breath.Checked?"Efeito  Respiração":"Efeito  Cor fixa",small,muted,495,155);
    }
    void DrawTriggerPreview(Graphics g,int t)
    {
        g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Surface);
        using var grid=new Pen(Color.FromArgb(202,213,228));using var line=new Pen(Accent,2);
        g.DrawRectangle(grid,5,5,104,54);
        float X(int i)=>5+(float)triggerValues[t,i].Value*104/255;
        float Y(int i)=>59-(float)triggerValues[t,i].Value*54/255;
        g.DrawLines(line,[new PointF(5,59),new PointF(X(2),Y(3)),new PointF(X(4),Y(5)),new PointF(109,5)]);
    }
}
