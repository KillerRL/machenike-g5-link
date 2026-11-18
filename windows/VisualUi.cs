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
                if(button.Tag is Color preset)
                {
                    button.BackColor=preset;button.ForeColor=(preset.R*.299+preset.G*.587+preset.B*.114)<145?Color.White:Color.Black;
                    button.FlatAppearance.BorderColor=Color.FromArgb(145,155,170);
                }
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
